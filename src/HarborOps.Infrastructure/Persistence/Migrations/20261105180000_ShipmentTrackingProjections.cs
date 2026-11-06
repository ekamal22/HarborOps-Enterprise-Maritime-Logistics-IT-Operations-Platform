using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261105180000_ShipmentTrackingProjections")]
public sealed class ShipmentTrackingProjections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Additive M10 storage; M8/M9 entities and the accepted EF snapshot are unchanged.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX uq_m10_effect_scope
                ON tracking_processing_effects(receipt_id,organization_id,shipment_id);

            CREATE TABLE shipment_tracking_input_versions (
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                input_revision bigint NOT NULL DEFAULT 0,
                event_count bigint NOT NULL DEFAULT 0,
                touched_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT pk_m10_input_version PRIMARY KEY (organization_id,shipment_id),
                CONSTRAINT fk_m10_input_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m10_input_revision CHECK (input_revision >= 0 AND event_count >= 0)
            );

            CREATE TABLE shipment_tracking_timeline_entries (
                receipt_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                event_id uuid NOT NULL,
                source_id uuid NOT NULL,
                source_event_id varchar(128) NOT NULL,
                event_code varchar(32) NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                received_at_utc timestamptz NOT NULL,
                container_id uuid NOT NULL,
                CONSTRAINT fk_m10_timeline_effect FOREIGN KEY
                    (receipt_id,organization_id,shipment_id)
                    REFERENCES tracking_processing_effects(receipt_id,organization_id,shipment_id)
                    ON DELETE RESTRICT
            );
            CREATE INDEX ix_m10_timeline_keyset
                ON shipment_tracking_timeline_entries
                (organization_id,shipment_id,occurred_at_utc,received_at_utc,source_id,
                 source_event_id COLLATE "C",receipt_id,event_id);
            CREATE TRIGGER tr_m10_immutable_timeline
                BEFORE UPDATE OR DELETE ON shipment_tracking_timeline_entries
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();

            CREATE FUNCTION m10_validate_timeline_entry() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $fn$
            DECLARE matched bigint;
            BEGIN
                EXECUTE format(
                    'SELECT COUNT(*) FROM %1$I.tracking_processing_effects fx
                     JOIN %1$I.tracking_accepted_events e ON e.receipt_id=fx.receipt_id
                         AND e.organization_id=fx.organization_id AND e.source_id=fx.source_id
                     JOIN %1$I.tracking_inbox_receipts r ON r.id=e.receipt_id
                         AND r.organization_id=e.organization_id AND r.source_id=e.source_id
                     WHERE fx.receipt_id=$1 AND fx.organization_id=$2
                       AND fx.shipment_id=$3 AND e.id=$4 AND e.source_id=$5
                       AND r.source_event_id=$6 AND e.event_code=$7
                       AND e.occurred_at_utc=$8 AND r.received_at_utc=$9
                       AND fx.container_id=$10', TG_TABLE_SCHEMA)
                INTO matched USING NEW.receipt_id,NEW.organization_id,
                    NEW.shipment_id,NEW.event_id,NEW.source_id,NEW.source_event_id,
                    NEW.event_code,NEW.occurred_at_utc,NEW.received_at_utc,NEW.container_id;
                IF matched<>1 THEN
                    RAISE EXCEPTION 'M10 timeline entry must match immutable M9 evidence'
                        USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER tr_m10_validate_timeline_entry
                BEFORE INSERT ON shipment_tracking_timeline_entries
                FOR EACH ROW EXECUTE FUNCTION m10_validate_timeline_entry();

            CREATE TABLE shipment_tracking_projections (
                shipment_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                observed_state varchar(20) NOT NULL,
                input_revision bigint NOT NULL,
                correction_generation bigint NOT NULL DEFAULT 0,
                event_count bigint NOT NULL DEFAULT 0,
                container_count integer NOT NULL DEFAULT 0,
                container_observations_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                last_event_id uuid NULL,
                last_occurred_at_utc timestamptz NULL,
                input_digest char(64) NOT NULL,
                policy_version varchar(48) NOT NULL,
                materialization_revision bigint NOT NULL DEFAULT 1,
                refreshed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                verified_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m10_projection_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m10_projection_state CHECK
                    (observed_state IN ('Unknown','EquipmentIn','EquipmentOut','Conflict','Mixed')),
                CONSTRAINT ck_m10_projection_revision CHECK (input_revision >= 0
                    AND correction_generation >= 0 AND materialization_revision > 0
                    AND event_count >= 0 AND container_count >= 0),
                CONSTRAINT ck_m10_projection_digest CHECK (input_digest ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_m10_projection_container_json CHECK
                    (jsonb_typeof(container_observations_json)='array')
            );
            CREATE INDEX ix_m10_projection_scope
                ON shipment_tracking_projections(organization_id,shipment_id);

            CREATE TABLE shipment_tracking_correction_heads (
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                generation bigint NOT NULL DEFAULT 0,
                CONSTRAINT pk_m10_correction_head PRIMARY KEY (organization_id,shipment_id),
                CONSTRAINT fk_m10_correction_head_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m10_head_generation CHECK (generation >= 0)
            );

            CREATE TABLE shipment_tracking_corrections (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                target_receipt_id uuid NOT NULL,
                replacement_receipt_id uuid NULL,
                generation bigint NOT NULL,
                actor_user_id uuid NOT NULL,
                reason varchar(240) NOT NULL,
                correlation_id varchar(64) NOT NULL,
                correction_digest char(64) NOT NULL,
                occurred_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m10_correction_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_target FOREIGN KEY (target_receipt_id)
                    REFERENCES tracking_processing_effects(receipt_id) ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_replacement FOREIGN KEY (replacement_receipt_id)
                    REFERENCES tracking_processing_effects(receipt_id) ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_actor FOREIGN KEY (actor_user_id)
                    REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT uq_m10_correction_target UNIQUE
                    (organization_id,shipment_id,target_receipt_id),
                CONSTRAINT uq_m10_correction_generation UNIQUE
                    (organization_id,shipment_id,generation),
                CONSTRAINT ck_m10_correction_distinct CHECK
                    (replacement_receipt_id IS NULL OR replacement_receipt_id<>target_receipt_id),
                CONSTRAINT ck_m10_correction_reason CHECK
                    (length(btrim(reason)) BETWEEN 8 AND 240 AND reason=btrim(reason)
                     AND reason !~ '[[:cntrl:]]'),
                CONSTRAINT ck_m10_correction_generation CHECK (generation > 0),
                CONSTRAINT ck_m10_correction_digest CHECK
                    (correction_digest ~ '^[0-9a-f]{64}$')
            );
            CREATE UNIQUE INDEX uq_m10_replacement_once ON shipment_tracking_corrections
                (organization_id,shipment_id,replacement_receipt_id)
                WHERE replacement_receipt_id IS NOT NULL;

            CREATE TABLE shipment_projection_applications (
                receipt_id uuid NOT NULL,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                policy_version varchar(48) NOT NULL,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT pk_m10_application PRIMARY KEY (receipt_id,policy_version),
                CONSTRAINT fk_m10_application_effect FOREIGN KEY
                    (receipt_id,organization_id,shipment_id)
                    REFERENCES tracking_processing_effects(receipt_id,organization_id,shipment_id)
                    ON DELETE RESTRICT,
                CONSTRAINT fk_m10_application_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_m10_application_scope
                ON shipment_projection_applications(organization_id,shipment_id);

            CREATE TABLE shipment_tracking_operation_replays (
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                idempotency_key uuid NOT NULL,
                operation_kind varchar(24) NOT NULL,
                request_digest char(64) NOT NULL,
                response_revision bigint NOT NULL,
                response_generation bigint NOT NULL,
                created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT pk_m10_operation_replay PRIMARY KEY
                    (organization_id,shipment_id,actor_user_id,idempotency_key),
                CONSTRAINT fk_m10_replay_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m10_replay_kind CHECK
                    (operation_kind IN ('Correction','Rebuild')),
                CONSTRAINT ck_m10_replay_digest CHECK (request_digest ~ '^[0-9a-f]{64}$')
            );

            CREATE FUNCTION m10_validate_correction() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$
            DECLARE
                actual_generation bigint;
                org_status text;
                shipment_status text;
                scope_valid boolean;
                chain_present boolean;
            BEGIN
                -- All table references are pinned to the triggering schema.
                EXECUTE format('SELECT status FROM %I.organizations WHERE id=$1 FOR UPDATE',
                    TG_TABLE_SCHEMA) INTO org_status USING NEW.organization_id;
                EXECUTE format(
                    'SELECT record_status FROM %I.shipments
                     WHERE organization_id=$1 AND id=$2 FOR UPDATE',
                    TG_TABLE_SCHEMA) INTO shipment_status
                    USING NEW.organization_id,NEW.shipment_id;
                IF org_status IS DISTINCT FROM 'Active'
                   OR shipment_status IS DISTINCT FROM 'Active' THEN
                    RAISE EXCEPTION 'inactive correction scope' USING ERRCODE='23514';
                END IF;
                EXECUTE format(
                    'SELECT EXISTS(SELECT 1 FROM %I.tracking_processing_effects
                     WHERE receipt_id=$1 AND organization_id=$2 AND shipment_id=$3)',
                    TG_TABLE_SCHEMA) INTO scope_valid
                    USING NEW.target_receipt_id,NEW.organization_id,NEW.shipment_id;
                IF NOT scope_valid THEN
                    RAISE EXCEPTION 'invalid correction target' USING ERRCODE='23514';
                END IF;
                IF NEW.replacement_receipt_id IS NOT NULL THEN
                    EXECUTE format(
                        'SELECT EXISTS(SELECT 1 FROM %I.tracking_processing_effects
                         WHERE receipt_id=$1 AND organization_id=$2 AND shipment_id=$3)',
                        TG_TABLE_SCHEMA) INTO scope_valid
                        USING NEW.replacement_receipt_id,NEW.organization_id,NEW.shipment_id;
                    IF NOT scope_valid THEN
                        RAISE EXCEPTION 'invalid correction replacement' USING ERRCODE='23514';
                    END IF;
                END IF;
                EXECUTE format(
                    'SELECT EXISTS(SELECT 1 FROM %I.shipment_tracking_corrections
                     WHERE organization_id=$1 AND shipment_id=$2
                       AND replacement_receipt_id=$3)',
                    TG_TABLE_SCHEMA) INTO chain_present
                    USING NEW.organization_id,NEW.shipment_id,NEW.target_receipt_id;
                IF chain_present THEN
                    RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
                END IF;
                IF NEW.replacement_receipt_id IS NOT NULL THEN
                    EXECUTE format(
                        'SELECT EXISTS(SELECT 1 FROM %I.shipment_tracking_corrections
                         WHERE organization_id=$1 AND shipment_id=$2
                           AND (target_receipt_id=$3 OR replacement_receipt_id=$4))',
                        TG_TABLE_SCHEMA) INTO chain_present
                        USING NEW.organization_id,NEW.shipment_id,
                              NEW.replacement_receipt_id,NEW.target_receipt_id;
                    IF chain_present THEN
                        RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
                    END IF;
                END IF;
                EXECUTE format(
                    'INSERT INTO %I.shipment_tracking_correction_heads
                         (organization_id,shipment_id,generation)
                     VALUES ($1,$2,0)
                     ON CONFLICT (organization_id,shipment_id) DO NOTHING',
                    TG_TABLE_SCHEMA) USING NEW.organization_id,NEW.shipment_id;
                EXECUTE format(
                    'UPDATE %I.shipment_tracking_correction_heads
                     SET generation=generation+1
                     WHERE organization_id=$1 AND shipment_id=$2
                     RETURNING generation',
                    TG_TABLE_SCHEMA) INTO actual_generation
                    USING NEW.organization_id,NEW.shipment_id;
                IF NEW.generation IS DISTINCT FROM actual_generation THEN
                    RAISE EXCEPTION 'correction generation conflict' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;

            CREATE FUNCTION m10_append_only() RETURNS trigger
            LANGUAGE plpgsql SET search_path=pg_catalog AS $fn$
            BEGIN
                RAISE EXCEPTION 'M10 retained history is immutable' USING ERRCODE='23514';
            END $fn$;

            CREATE TRIGGER tr_m10_validate_correction
                BEFORE INSERT ON shipment_tracking_corrections
                FOR EACH ROW EXECUTE FUNCTION m10_validate_correction();
            CREATE TRIGGER tr_m10_immutable_corrections
                BEFORE UPDATE OR DELETE ON shipment_tracking_corrections
                FOR EACH ROW EXECUTE FUNCTION m10_append_only();
            CREATE TRIGGER tr_m10_immutable_replays
                BEFORE UPDATE OR DELETE ON shipment_tracking_operation_replays
                FOR EACH ROW EXECUTE FUNCTION m10_append_only();

            -- Trigger functions run with the migration role's rights, not the M9
            -- worker or the restricted projector's table privileges.
            CREATE FUNCTION m10_track_input_change() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$
            DECLARE
                delta integer;
                inserted_rows bigint;
            BEGIN
                delta := CASE WHEN TG_TABLE_NAME='tracking_processing_effects' THEN 1 ELSE 0 END;
                IF delta=1 THEN
                    EXECUTE format(
                        'INSERT INTO %1$I.shipment_tracking_timeline_entries
                            (receipt_id,organization_id,shipment_id,event_id,source_id,
                             source_event_id,event_code,occurred_at_utc,received_at_utc,container_id)
                         SELECT fx.receipt_id,fx.organization_id,fx.shipment_id,
                                e.id,e.source_id,r.source_event_id,e.event_code,
                                e.occurred_at_utc,r.received_at_utc,fx.container_id
                         FROM %1$I.tracking_processing_effects fx
                         JOIN %1$I.tracking_accepted_events e ON e.receipt_id=fx.receipt_id
                             AND e.organization_id=fx.organization_id AND e.source_id=fx.source_id
                         JOIN %1$I.tracking_inbox_receipts r ON r.id=e.receipt_id
                             AND r.organization_id=e.organization_id AND r.source_id=e.source_id
                         WHERE fx.receipt_id=$1', TG_TABLE_SCHEMA)
                    USING NEW.receipt_id;
                    GET DIAGNOSTICS inserted_rows = ROW_COUNT;
                    IF inserted_rows<>1 THEN
                        RAISE EXCEPTION 'M10 effect provenance is incomplete' USING ERRCODE='23514';
                    END IF;
                END IF;
                EXECUTE format(
                    'INSERT INTO %I.shipment_tracking_input_versions AS current_input
                        (organization_id,shipment_id,input_revision,event_count)
                     VALUES ($1,$2,1,$3)
                     ON CONFLICT (organization_id,shipment_id) DO UPDATE
                     SET input_revision=current_input.input_revision+1,
                         event_count=current_input.event_count+EXCLUDED.event_count,
                         touched_at_utc=clock_timestamp()',
                    TG_TABLE_SCHEMA)
                USING NEW.organization_id,NEW.shipment_id,delta;
                RETURN NEW;
            END $fn$;
            REVOKE ALL ON FUNCTION m10_track_input_change() FROM PUBLIC;
            CREATE TRIGGER tr_m10_effect_input_revision
                AFTER INSERT ON tracking_processing_effects FOR EACH ROW
                EXECUTE FUNCTION m10_track_input_change();
            CREATE TRIGGER tr_m10_correction_input_revision
                AFTER INSERT ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_track_input_change();

            INSERT INTO shipment_tracking_timeline_entries
                (receipt_id,organization_id,shipment_id,event_id,source_id,
                 source_event_id,event_code,occurred_at_utc,received_at_utc,container_id)
            SELECT fx.receipt_id,fx.organization_id,fx.shipment_id,
                   e.id,e.source_id,r.source_event_id,e.event_code,
                   e.occurred_at_utc,r.received_at_utc,fx.container_id
            FROM tracking_processing_effects fx
            JOIN tracking_accepted_events e ON e.receipt_id=fx.receipt_id
                AND e.organization_id=fx.organization_id AND e.source_id=fx.source_id
            JOIN tracking_inbox_receipts r ON r.id=e.receipt_id
                AND r.organization_id=e.organization_id AND r.source_id=e.source_id;

            INSERT INTO shipment_tracking_input_versions
                (organization_id,shipment_id,input_revision,event_count)
            SELECT organization_id,shipment_id,COUNT(*),COUNT(*)
            FROM tracking_processing_effects GROUP BY organization_id,shipment_id;

            -- The fixed schema and lock order are bound at migration time.
            DO $create$
            DECLARE schema_name text := current_schema();
            BEGIN
                EXECUTE format($sql$
                    CREATE FUNCTION %1$I.m10_lock_projector_scope(p_org uuid,p_shipment uuid)
                    RETURNS boolean
                    LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$
                    DECLARE organization_state text;
                    DECLARE shipment_state text;
                    BEGIN
                        IF session_user <> 'harborops_projector' THEN RETURN false; END IF;
                        SELECT status INTO organization_state FROM %1$I.organizations
                            WHERE id=p_org FOR UPDATE;
                        SELECT record_status INTO shipment_state FROM %1$I.shipments
                            WHERE organization_id=p_org AND id=p_shipment FOR UPDATE;
                        RETURN organization_state='Active' AND shipment_state='Active';
                    END $fn$;
                $sql$,schema_name);
            END $create$;
            REVOKE ALL ON FUNCTION m10_lock_projector_scope(uuid,uuid) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
                IF EXISTS(SELECT 1 FROM shipment_tracking_timeline_entries LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_tracking_input_versions LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_tracking_projections LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_tracking_corrections LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_tracking_correction_heads LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_tracking_operation_replays LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_projection_applications LIMIT 1) THEN
                    RAISE EXCEPTION 'M10 retained history prevents downgrade'
                        USING ERRCODE='23514';
                END IF;
            END $guard$;
            DROP TRIGGER tr_m10_effect_input_revision ON tracking_processing_effects;
            DROP TRIGGER tr_m10_correction_input_revision ON shipment_tracking_corrections;
            DROP FUNCTION m10_track_input_change();
            DROP FUNCTION m10_lock_projector_scope(uuid,uuid);
            DROP TABLE shipment_tracking_operation_replays;
            DROP TABLE shipment_tracking_timeline_entries;
            DROP TABLE shipment_projection_applications;
            DROP TABLE shipment_tracking_corrections;
            DROP TABLE shipment_tracking_correction_heads;
            DROP TABLE shipment_tracking_projections;
            DROP TABLE shipment_tracking_input_versions;
            DROP FUNCTION m10_validate_timeline_entry();
            DROP FUNCTION m10_validate_correction();
            DROP FUNCTION m10_append_only();
            DROP INDEX uq_m10_effect_scope;
            """);
    }
}
