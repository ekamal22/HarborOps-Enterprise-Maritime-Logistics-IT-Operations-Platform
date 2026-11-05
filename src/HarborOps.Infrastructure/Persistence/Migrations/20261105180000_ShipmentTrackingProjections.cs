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
        // M10 tables are explicitly managed by SQL. No EF entity mappings change
        // and the accepted M9 model snapshot remains authoritative for its entities.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX uq_m10_effect_scope
                ON tracking_processing_effects(receipt_id,organization_id,shipment_id);

            CREATE TABLE shipment_tracking_projections (
                shipment_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                observed_state varchar(20) NOT NULL DEFAULT 'Unknown',
                correction_generation bigint NOT NULL DEFAULT 0,
                verified_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                last_event_id uuid NULL,
                last_occurred_at_utc timestamptz NULL,
                input_digest char(64) NOT NULL,
                policy_version varchar(48) NOT NULL,
                materialization_revision bigint NOT NULL DEFAULT 1,
                refreshed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m10_projection_shipment
                    FOREIGN KEY (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT,
                CONSTRAINT ck_m10_observed_state CHECK
                    (observed_state IN ('Unknown','EquipmentIn','EquipmentOut','Conflict','Mixed')),
                CONSTRAINT ck_m10_revision CHECK (materialization_revision > 0),
                CONSTRAINT ck_m10_correction_revision CHECK (correction_generation >= 0),
                CONSTRAINT ck_m10_digest CHECK (input_digest ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_m10_projection_scope
                ON shipment_tracking_projections(organization_id,shipment_id);

            CREATE TABLE shipment_tracking_correction_heads (
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                generation bigint NOT NULL DEFAULT 0,
                CONSTRAINT pk_m10_correction_head PRIMARY KEY (organization_id,shipment_id),
                CONSTRAINT fk_m10_correction_head_shipment FOREIGN KEY
                    (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT,
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
                CONSTRAINT fk_m10_correction_shipment
                    FOREIGN KEY (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_target
                    FOREIGN KEY (target_receipt_id) REFERENCES tracking_processing_effects(receipt_id)
                    ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_replacement
                    FOREIGN KEY (replacement_receipt_id) REFERENCES tracking_processing_effects(receipt_id)
                    ON DELETE RESTRICT,
                CONSTRAINT fk_m10_correction_actor
                    FOREIGN KEY (actor_user_id) REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT uq_m10_correction_target
                    UNIQUE (organization_id,shipment_id,target_receipt_id),
                CONSTRAINT uq_m10_correction_generation
                    UNIQUE (organization_id,shipment_id,generation),
                CONSTRAINT ck_m10_correction_distinct
                    CHECK (replacement_receipt_id IS NULL OR replacement_receipt_id<>target_receipt_id),
                CONSTRAINT ck_m10_correction_reason
                    CHECK (length(btrim(reason)) BETWEEN 8 AND 240
                        AND reason=btrim(reason) AND reason !~ '[[:cntrl:]]'),
                CONSTRAINT ck_m10_correction_generation CHECK (generation>0),
                CONSTRAINT ck_m10_correction_digest CHECK (correction_digest ~ '^[0-9a-f]{64}
            );
            CREATE UNIQUE INDEX uq_m10_replacement_once
                ON shipment_tracking_corrections(organization_id,shipment_id,replacement_receipt_id)
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
                CONSTRAINT fk_m10_application_shipment
                    FOREIGN KEY (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT
            );
            CREATE INDEX ix_m10_unapplied_scope ON shipment_projection_applications
                (organization_id,shipment_id);

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
                CONSTRAINT ck_m10_replay_digest CHECK (request_digest ~ '^[0-9a-f]{64} RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              -- Serialize every direct writer using the same organization-first,
              -- shipment-second lock hierarchy as the API.
              PERFORM 1 FROM organizations WHERE id=NEW.organization_id
                AND status='Active' FOR UPDATE;
              IF NOT FOUND THEN
                RAISE EXCEPTION 'inactive correction organization' USING ERRCODE='23514';
              END IF;
              PERFORM 1 FROM shipments WHERE organization_id=NEW.organization_id
                AND id=NEW.shipment_id AND record_status='Active' FOR UPDATE;
              IF NOT FOUND THEN
                RAISE EXCEPTION 'inactive correction shipment' USING ERRCODE='23514';
              END IF;
              IF NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.target_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction target' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.replacement_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction replacement' USING ERRCODE='23514';
              END IF;
              IF EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND c.replacement_receipt_id=NEW.target_receipt_id) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND (c.target_receipt_id=NEW.replacement_receipt_id
                       OR c.replacement_receipt_id=NEW.target_receipt_id)) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              -- Every insert writes a durable per-shipment generation counter.
              -- Competing REPEATABLE READ/SERIALIZABLE writers conflict rather than
              -- using a stale pre-lock snapshot to accept a forbidden chain.
              INSERT INTO shipment_tracking_correction_heads
                  (organization_id,shipment_id,generation)
              VALUES (NEW.organization_id,NEW.shipment_id,0)
              ON CONFLICT (organization_id,shipment_id) DO NOTHING;
              UPDATE shipment_tracking_correction_heads
              SET generation=generation+1
              WHERE organization_id=NEW.organization_id AND shipment_id=NEW.shipment_id
              RETURNING generation INTO new_generation;
              IF NEW.generation IS DISTINCT FROM new_generation THEN
                RAISE EXCEPTION 'correction generation conflict' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END
            $fn$;
            CREATE TRIGGER tr_m10_validate_correction BEFORE INSERT
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_validate_correction();

            CREATE OR REPLACE FUNCTION m10_block_evidence_change() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              RAISE EXCEPTION 'derived correction history is append-only'
                USING ERRCODE='23514';
            END
            $fn$;
            CREATE TRIGGER tr_m10_immutable_corrections BEFORE UPDATE OR DELETE
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_block_evidence_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
              IF EXISTS (SELECT 1 FROM shipment_tracking_corrections LIMIT 1)
                 OR EXISTS (SELECT 1 FROM shipment_projection_applications LIMIT 1)
                 OR EXISTS (SELECT 1 FROM shipment_tracking_operation_replays LIMIT 1)
                 OR EXISTS (SELECT 1 FROM shipment_tracking_correction_heads LIMIT 1)
                 OR EXISTS (SELECT 1 FROM shipment_tracking_projections LIMIT 1) THEN
                RAISE EXCEPTION 'M10 retained projection or correction history prevents downgrade'
                    USING ERRCODE='23514';
              END IF;
            END $guard$;
            DROP TABLE IF EXISTS shipment_tracking_operation_replays;
            DROP TABLE IF EXISTS shipment_projection_applications;
            DROP TABLE IF EXISTS shipment_tracking_corrections;
            DROP TABLE IF EXISTS shipment_tracking_correction_heads;
            DROP TABLE IF EXISTS shipment_tracking_projections;
            DROP FUNCTION IF EXISTS m10_validate_correction();
            DROP FUNCTION IF EXISTS m10_block_evidence_change();
            DROP INDEX IF EXISTS uq_m10_effect_scope;
            """);
    }
}
)
            );
            CREATE UNIQUE INDEX uq_m10_replacement_once
                ON shipment_tracking_corrections(organization_id,shipment_id,replacement_receipt_id)
                WHERE replacement_receipt_id IS NOT NULL;

            CREATE TABLE shipment_projection_applications (
                receipt_id uuid PRIMARY KEY REFERENCES tracking_processing_effects(receipt_id)
                    ON DELETE RESTRICT,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                policy_version varchar(48) NOT NULL,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m10_application_shipment
                    FOREIGN KEY (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT
            );
            CREATE INDEX ix_m10_unapplied_scope ON shipment_projection_applications
                (organization_id,shipment_id);

            CREATE OR REPLACE FUNCTION m10_validate_correction() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            DECLARE new_generation bigint;
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.target_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction target' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.replacement_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction replacement' USING ERRCODE='23514';
              END IF;
              IF EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND c.replacement_receipt_id=NEW.target_receipt_id) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND (c.target_receipt_id=NEW.replacement_receipt_id
                       OR c.replacement_receipt_id=NEW.target_receipt_id)) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END
            $fn$;
            CREATE TRIGGER tr_m10_validate_correction BEFORE INSERT
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_validate_correction();

            CREATE OR REPLACE FUNCTION m10_block_evidence_change() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              RAISE EXCEPTION 'derived correction history is append-only'
                USING ERRCODE='23514';
            END
            $fn$;
            CREATE TRIGGER tr_m10_immutable_corrections BEFORE UPDATE OR DELETE
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_block_evidence_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS shipment_projection_applications;
            DROP TABLE IF EXISTS shipment_tracking_corrections;
            DROP TABLE IF EXISTS shipment_tracking_projections;
            DROP FUNCTION IF EXISTS m10_validate_correction();
            DROP FUNCTION IF EXISTS m10_block_evidence_change();
            """);
    }
}
)
            );

            CREATE OR REPLACE FUNCTION m10_validate_correction() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.target_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction target' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.replacement_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction replacement' USING ERRCODE='23514';
              END IF;
              IF EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND c.replacement_receipt_id=NEW.target_receipt_id) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND (c.target_receipt_id=NEW.replacement_receipt_id
                       OR c.replacement_receipt_id=NEW.target_receipt_id)) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END
            $fn$;
            CREATE TRIGGER tr_m10_validate_correction BEFORE INSERT
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_validate_correction();

            CREATE OR REPLACE FUNCTION m10_block_evidence_change() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              RAISE EXCEPTION 'derived correction history is append-only'
                USING ERRCODE='23514';
            END
            $fn$;
            CREATE TRIGGER tr_m10_immutable_corrections BEFORE UPDATE OR DELETE
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_block_evidence_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS shipment_projection_applications;
            DROP TABLE IF EXISTS shipment_tracking_corrections;
            DROP TABLE IF EXISTS shipment_tracking_projections;
            DROP FUNCTION IF EXISTS m10_validate_correction();
            DROP FUNCTION IF EXISTS m10_block_evidence_change();
            """);
    }
}
)
            );
            CREATE UNIQUE INDEX uq_m10_replacement_once
                ON shipment_tracking_corrections(organization_id,shipment_id,replacement_receipt_id)
                WHERE replacement_receipt_id IS NOT NULL;

            CREATE TABLE shipment_projection_applications (
                receipt_id uuid PRIMARY KEY REFERENCES tracking_processing_effects(receipt_id)
                    ON DELETE RESTRICT,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                policy_version varchar(48) NOT NULL,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m10_application_shipment
                    FOREIGN KEY (organization_id,shipment_id) REFERENCES shipments(organization_id,id)
                    ON DELETE RESTRICT
            );
            CREATE INDEX ix_m10_unapplied_scope ON shipment_projection_applications
                (organization_id,shipment_id);

            CREATE OR REPLACE FUNCTION m10_validate_correction() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.target_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction target' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM tracking_processing_effects t
                WHERE t.receipt_id=NEW.replacement_receipt_id
                  AND t.organization_id=NEW.organization_id
                  AND t.shipment_id=NEW.shipment_id) THEN
                RAISE EXCEPTION 'invalid correction replacement' USING ERRCODE='23514';
              END IF;
              IF EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND c.replacement_receipt_id=NEW.target_receipt_id) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              IF NEW.replacement_receipt_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM shipment_tracking_corrections c
                WHERE c.organization_id=NEW.organization_id
                  AND c.shipment_id=NEW.shipment_id
                  AND (c.target_receipt_id=NEW.replacement_receipt_id
                       OR c.replacement_receipt_id=NEW.target_receipt_id)) THEN
                RAISE EXCEPTION 'correction chain not supported' USING ERRCODE='23514';
              END IF;
              RETURN NEW;
            END
            $fn$;
            CREATE TRIGGER tr_m10_validate_correction BEFORE INSERT
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_validate_correction();

            CREATE OR REPLACE FUNCTION m10_block_evidence_change() RETURNS trigger
            LANGUAGE plpgsql AS $fn$
            BEGIN
              RAISE EXCEPTION 'derived correction history is append-only'
                USING ERRCODE='23514';
            END
            $fn$;
            CREATE TRIGGER tr_m10_immutable_corrections BEFORE UPDATE OR DELETE
                ON shipment_tracking_corrections FOR EACH ROW
                EXECUTE FUNCTION m10_block_evidence_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS shipment_projection_applications;
            DROP TABLE IF EXISTS shipment_tracking_corrections;
            DROP TABLE IF EXISTS shipment_tracking_projections;
            DROP FUNCTION IF EXISTS m10_validate_correction();
            DROP FUNCTION IF EXISTS m10_block_evidence_change();
            """);
    }
}
