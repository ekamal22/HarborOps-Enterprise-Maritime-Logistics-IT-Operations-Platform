using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261104190000_TrackingWorker")]
public sealed class TrackingWorker : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tracking_inbox_work_items
              ADD COLUMN lease_token uuid NULL,
              ADD COLUMN lease_epoch bigint NOT NULL DEFAULT 0,
              ADD COLUMN lease_expires_at_utc timestamptz NULL,
              ADD COLUMN available_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
              ADD COLUMN redrive_generation integer NOT NULL DEFAULT 0,
              ADD COLUMN last_error_code varchar(40) NULL,
              ADD COLUMN completed_at_utc timestamptz NULL,
              ADD COLUMN resolution_kind varchar(32) NULL;
            ALTER TABLE tracking_inbox_work_items DROP CONSTRAINT "CK_m8_work_state";
            ALTER TABLE tracking_inbox_work_items ADD CONSTRAINT "CK_m9_work_state"
              CHECK (state IN ('Pending','Leased','RetryWaiting','NeedsReconciliation','Processed','DeadLetter')
                AND attempt_count BETWEEN 0 AND 5 AND lease_epoch >= 0
                AND redrive_generation BETWEEN 0 AND 3
                AND updated_at_utc >= created_at_utc
                AND (last_error_code IS NULL OR length(last_error_code) BETWEEN 1 AND 40)
                AND ((state = 'Leased' AND lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL)
                     OR (state <> 'Leased' AND lease_token IS NULL AND lease_expires_at_utc IS NULL))
                AND (state <> 'Processed' OR (completed_at_utc IS NOT NULL AND resolution_kind = 'ReferenceResolved'))
                AND (state <> 'Pending' OR (attempt_count = 0 AND last_error_code IS NULL))
                AND (state <> 'RetryWaiting' OR attempt_count BETWEEN 1 AND 4)
                AND (state <> 'Leased' OR attempt_count BETWEEN 1 AND 5));

            CREATE INDEX "IX_m9_work_due" ON tracking_inbox_work_items
                (organization_id,available_at_utc,created_at_utc,receipt_id)
                WHERE state IN ('Pending','RetryWaiting');
            CREATE INDEX "IX_m9_work_expired" ON tracking_inbox_work_items
                (organization_id,lease_expires_at_utc,receipt_id) WHERE state = 'Leased';
            CREATE INDEX "IX_m9_work_queue" ON tracking_inbox_work_items
                (organization_id,state,receipt_id);

            CREATE TABLE tracking_work_attempt_events (
                id uuid PRIMARY KEY,
                receipt_id uuid NOT NULL,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                generation integer NOT NULL,
                attempt_count integer NOT NULL,
                lease_epoch bigint NOT NULL,
                event_kind varchar(32) NOT NULL,
                reason_code varchar(40) NULL,
                actor_user_id uuid NULL REFERENCES users(id) ON DELETE RESTRICT,
                redrive_reason varchar(240) NULL,
                occurred_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT "FK_m9_attempt_receipt" FOREIGN KEY
                    (organization_id,source_id,receipt_id)
                    REFERENCES tracking_inbox_receipts(organization_id,source_id,id)
                    ON DELETE RESTRICT,
                CONSTRAINT "CK_m9_attempt_kind"
                    CHECK (event_kind IN ('Claimed','LeaseRecovered','Retried','ReconciliationRequired',
                         'DeadLettered','Processed','Redriven','LeaseBudgetExhausted')),
                CONSTRAINT "CK_m9_attempt_counter" CHECK
                    (generation BETWEEN 0 AND 3 AND attempt_count BETWEEN 0 AND 5 AND lease_epoch >= 0),
                CONSTRAINT "CK_m9_attempt_reason" CHECK
                    ((reason_code IS NULL OR length(reason_code) BETWEEN 1 AND 40)
                     AND ((event_kind='Redriven' AND actor_user_id IS NOT NULL
                           AND redrive_reason IS NOT NULL
                           AND length(btrim(redrive_reason)) BETWEEN 8 AND 240)
                       OR (event_kind<>'Redriven' AND actor_user_id IS NULL
                           AND redrive_reason IS NULL)))
            );
            CREATE INDEX "IX_m9_attempt_receipt"
                ON tracking_work_attempt_events(organization_id,receipt_id,occurred_at_utc,id);
            CREATE UNIQUE INDEX "UQ_m9_redrive_generation"
                ON tracking_work_attempt_events(receipt_id,generation)
                WHERE event_kind='Redriven';

            CREATE TABLE tracking_processing_effects (
                receipt_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                resolution_kind varchar(32) NOT NULL,
                resolution_policy varchar(48) NOT NULL,
                container_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                resolved_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT "FK_m9_effect_receipt" FOREIGN KEY
                    (organization_id,source_id,receipt_id)
                    REFERENCES tracking_inbox_receipts(organization_id,source_id,id)
                    ON DELETE RESTRICT,
                CONSTRAINT "FK_m9_effect_container" FOREIGN KEY
                    (organization_id,container_id)
                    REFERENCES containers(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m9_effect_shipment" FOREIGN KEY
                    (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_m9_effect_kind"
                    CHECK (resolution_kind = 'ReferenceResolved'),
                CONSTRAINT "CK_m9_effect_policy"
                    CHECK (resolution_policy = 'm9-reference-v1')
            );
            CREATE INDEX "IX_m9_effect_tenant"
                ON tracking_processing_effects(organization_id,source_id,receipt_id);

            CREATE TRIGGER m9_attempt_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_work_attempt_events
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();
            CREATE TRIGGER m9_effect_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_processing_effects
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();

            DROP TRIGGER m8_work_guard ON tracking_inbox_work_items;
            -- Bind the organization table to the schema at migration time,
            -- not to a schema selected later by the caller.
            DO $create_lock$
            DECLARE schema_name text := current_schema();
            BEGIN
                EXECUTE format($sql$
                    CREATE FUNCTION %1$I.m9_lock_active_organization(p_org uuid)
                    RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                    SET search_path=pg_catalog AS $fn$
                    DECLARE current_status text;
                    BEGIN
                        SELECT status INTO current_status FROM %1$I.organizations
                        WHERE id=p_org FOR UPDATE;
                        RETURN current_status = 'Active';
                    END $fn$;
                $sql$, schema_name);
            END $create_lock$;
            REVOKE ALL ON FUNCTION m9_lock_active_organization(uuid) FROM PUBLIC;

            CREATE FUNCTION m9_validate_effect_reference() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$
            DECLARE
                family text;
                equipment text;
                occurred timestamptz;
                matches integer;
            BEGIN
                EXECUTE format(
                    'SELECT event_family,equipment_reference,occurred_at_utc FROM %I.tracking_accepted_events
                     WHERE organization_id=$1 AND source_id=$2 AND receipt_id=$3',
                    TG_TABLE_SCHEMA)
                    INTO family,equipment,occurred
                    USING NEW.organization_id,NEW.source_id,NEW.receipt_id;
                IF family IS DISTINCT FROM 'Equipment' OR equipment IS NULL THEN
                    RAISE EXCEPTION 'Resolution effect requires equipment event'
                        USING ERRCODE='23514', CONSTRAINT='CK_m9_effect_reference';
                END IF;
                EXECUTE format(
                    'SELECT count(*) FROM (
                       SELECT 1 FROM %1$I.containers c
                       JOIN %1$I.shipment_container_assignments a
                         ON a.organization_id=c.organization_id AND a.container_id=c.id
                       JOIN %1$I.shipments sh
                         ON sh.organization_id=a.organization_id AND sh.id=a.shipment_id
                       WHERE c.organization_id=$1 AND c.id=$2
                         AND c.container_identifier=upper(btrim($3))
                         AND c.record_status=''Active'' AND sh.record_status=''Active''
                         AND a.shipment_id=$4
                         AND a.starts_at_utc<=$5
                         AND (a.ends_at_utc IS NULL OR $5<a.ends_at_utc)
                       FOR SHARE OF c,a,sh
                     ) qualified',
                    TG_TABLE_SCHEMA)
                    INTO matches
                    USING NEW.organization_id,NEW.container_id,equipment,NEW.shipment_id,occurred;
                IF matches <> 1 THEN
                    RAISE EXCEPTION 'Resolution effect is not tenant-time valid'
                        USING ERRCODE='23514', CONSTRAINT='CK_m9_effect_reference';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m9_effect_reference
                BEFORE INSERT ON tracking_processing_effects
                FOR EACH ROW EXECUTE FUNCTION m9_validate_effect_reference();

            CREATE FUNCTION m9_guard_work_item() RETURNS trigger LANGUAGE plpgsql AS $fn$
            DECLARE
                at_utc timestamptz := clock_timestamp();
                allowed boolean := false;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Tracking work cannot be deleted' USING ERRCODE='23514';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    IF NEW.state <> 'Pending' OR NEW.attempt_count <> 0 OR NEW.lease_epoch <> 0 OR
                       NEW.redrive_generation <> 0 OR NEW.lease_token IS NOT NULL OR
                       NEW.completed_at_utc IS NOT NULL OR NEW.last_error_code IS NOT NULL OR
                       NEW.updated_at_utc <> NEW.created_at_utc THEN
                        RAISE EXCEPTION 'Invalid initial tracking work' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF NEW.receipt_id IS DISTINCT FROM OLD.receipt_id OR
                   NEW.organization_id IS DISTINCT FROM OLD.organization_id OR
                   NEW.source_id IS DISTINCT FROM OLD.source_id OR
                   NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc OR
                   NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'Immutable tracking work identity' USING ERRCODE='23514';
                END IF;
                IF OLD.state = 'Processed' THEN
                    RAISE EXCEPTION 'Processed work is terminal' USING ERRCODE='23514';
                END IF;
                -- The scoped service role cannot bypass the Manager-only redrive path.
                IF session_user = 'harborops_worker' AND
                   OLD.state IN ('DeadLetter','NeedsReconciliation') AND
                   NEW.state = 'Pending' THEN
                    RAISE EXCEPTION 'Worker role cannot perform manual redrive'
                        USING ERRCODE='23514';
                END IF;

                IF OLD.state IN ('Pending','RetryWaiting') AND NEW.state = 'Leased' THEN
                    allowed := OLD.available_at_utc <= at_utc AND
                        NEW.attempt_count = OLD.attempt_count + 1 AND NEW.attempt_count <= 5 AND
                        NEW.redrive_generation = OLD.redrive_generation AND
                        NEW.lease_epoch = OLD.lease_epoch + 1 AND NEW.lease_token IS NOT NULL AND
                        NEW.lease_expires_at_utc > at_utc AND
                        NEW.lease_expires_at_utc <= at_utc + interval '10 minutes' AND
                        NEW.completed_at_utc IS NULL;
                ELSIF OLD.state = 'Leased' AND NEW.state = 'Leased' THEN
                    allowed := NEW.redrive_generation = OLD.redrive_generation AND
                        ((OLD.lease_expires_at_utc > at_utc AND
                          NEW.lease_token = OLD.lease_token AND NEW.lease_epoch = OLD.lease_epoch AND
                          NEW.attempt_count = OLD.attempt_count AND
                          NEW.lease_expires_at_utc > OLD.lease_expires_at_utc AND
                          NEW.lease_expires_at_utc <= at_utc + interval '10 minutes')
                         OR (OLD.lease_expires_at_utc <= at_utc AND
                          NEW.lease_token IS DISTINCT FROM OLD.lease_token AND
                          NEW.lease_epoch = OLD.lease_epoch + 1 AND
                          NEW.attempt_count = OLD.attempt_count + 1 AND
                          NEW.attempt_count <= 5 AND NEW.lease_expires_at_utc > at_utc AND
                          NEW.lease_expires_at_utc <= at_utc + interval '10 minutes'));
                ELSIF OLD.state = 'Leased' AND NEW.state = 'DeadLetter' AND
                    OLD.lease_expires_at_utc <= at_utc AND OLD.attempt_count >= 5 THEN
                    allowed := NEW.attempt_count = OLD.attempt_count AND
                        NEW.lease_epoch = OLD.lease_epoch AND
                        NEW.redrive_generation = OLD.redrive_generation AND
                        NEW.lease_token IS NULL AND NEW.lease_expires_at_utc IS NULL;
                ELSIF OLD.state = 'Leased' AND
                    NEW.state IN ('Processed','RetryWaiting','NeedsReconciliation','DeadLetter') THEN
                    allowed := OLD.lease_expires_at_utc > at_utc AND
                        NEW.attempt_count = OLD.attempt_count AND
                        NEW.lease_epoch = OLD.lease_epoch AND
                        NEW.redrive_generation = OLD.redrive_generation AND
                        NEW.lease_token IS NULL AND NEW.lease_expires_at_utc IS NULL;
                ELSIF OLD.state IN ('NeedsReconciliation','DeadLetter') AND
                    NEW.state = 'Pending' THEN
                    allowed := NEW.redrive_generation = OLD.redrive_generation + 1 AND
                        NEW.redrive_generation <= 3 AND NEW.attempt_count = 0 AND
                        NEW.lease_epoch = OLD.lease_epoch + 1 AND
                        NEW.completed_at_utc IS NULL AND NEW.resolution_kind IS NULL AND
                        NEW.last_error_code IS NULL;
                END IF;
                IF OLD.state = 'Leased' AND NEW.state = 'Leased' AND
                   (NEW.available_at_utc IS DISTINCT FROM OLD.available_at_utc OR
                    NEW.last_error_code IS DISTINCT FROM OLD.last_error_code OR
                    NEW.completed_at_utc IS DISTINCT FROM OLD.completed_at_utc OR
                    NEW.resolution_kind IS DISTINCT FROM OLD.resolution_kind) THEN
                    RAISE EXCEPTION 'Lease renewal cannot mutate processing outcome'
                        USING ERRCODE='23514';
                END IF;
                IF NOT allowed THEN
                    RAISE EXCEPTION 'Invalid tracking work transition' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m9_work_guard BEFORE INSERT OR UPDATE OR DELETE
                ON tracking_inbox_work_items FOR EACH ROW
                EXECUTE FUNCTION m9_guard_work_item();

            -- A valid state transition creates its own immutable attempt record.
            -- No direct INSERT grant on this table is given to the worker role.
            CREATE FUNCTION m9_log_work_transition() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $fn$
            DECLARE kind text;
            BEGIN
                IF NEW.state='Pending' THEN
                    -- Manual redrives are attributed separately by the authenticated API.
                    RETURN NULL;
                ELSIF NEW.state='Leased' THEN
                    IF OLD.state='Leased' AND NEW.lease_epoch=OLD.lease_epoch THEN
                        RETURN NULL;
                    END IF;
                    kind := CASE WHEN OLD.state='Leased' THEN 'LeaseRecovered' ELSE 'Claimed' END;
                ELSIF NEW.state='Processed' THEN
                    kind := 'Processed';
                ELSIF NEW.state='NeedsReconciliation' THEN
                    kind := 'ReconciliationRequired';
                ELSIF NEW.state='RetryWaiting' THEN
                    kind := 'Retried';
                ELSIF NEW.state='DeadLetter' THEN
                    kind := CASE WHEN OLD.state='Leased' AND
                        OLD.lease_expires_at_utc<=clock_timestamp() AND OLD.attempt_count>=5
                        THEN 'LeaseBudgetExhausted' ELSE 'DeadLettered' END;
                ELSE
                    RETURN NULL;
                END IF;
                EXECUTE format(
                    'INSERT INTO %I.tracking_work_attempt_events
                       (id,receipt_id,organization_id,source_id,generation,attempt_count,
                        lease_epoch,event_kind,reason_code,occurred_at_utc)
                     VALUES (gen_random_uuid(),$1,$2,$3,$4,$5,$6,$7,$8,clock_timestamp())',
                    TG_TABLE_SCHEMA)
                USING NEW.receipt_id,NEW.organization_id,NEW.source_id,
                      NEW.redrive_generation,NEW.attempt_count,NEW.lease_epoch,
                      kind,NEW.last_error_code;
                RETURN NULL;
            END $fn$;
            REVOKE ALL ON FUNCTION m9_log_work_transition() FROM PUBLIC;
            CREATE TRIGGER m9_work_transition_audit AFTER UPDATE
                ON tracking_inbox_work_items FOR EACH ROW
                EXECUTE FUNCTION m9_log_work_transition();

            CREATE FUNCTION m9_assert_processing_effect() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF NEW.redrive_generation > OLD.redrive_generation AND NOT EXISTS (
                    SELECT 1 FROM tracking_work_attempt_events a
                    WHERE a.receipt_id = NEW.receipt_id
                      AND a.organization_id = NEW.organization_id
                      AND a.source_id = NEW.source_id
                      AND a.generation = NEW.redrive_generation
                      AND a.event_kind = 'Redriven' AND a.actor_user_id IS NOT NULL
                ) THEN
                    RAISE EXCEPTION 'Manual redrive requires immutable attributed evidence'
                        USING ERRCODE='23514';
                END IF;
                IF NEW.state = 'Processed' AND NOT EXISTS (
                    SELECT 1 FROM tracking_processing_effects e
                    WHERE e.receipt_id = NEW.receipt_id
                      AND e.organization_id = NEW.organization_id
                      AND e.source_id = NEW.source_id
                ) THEN
                    RAISE EXCEPTION 'Processed work requires immutable resolution effect'
                        USING ERRCODE='23514';
                END IF;
                RETURN NULL;
            END $fn$;
            CREATE CONSTRAINT TRIGGER m9_work_effect_required AFTER UPDATE
                ON tracking_inbox_work_items
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION m9_assert_processing_effect();

            CREATE FUNCTION m9_assert_effect_terminal() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM tracking_inbox_work_items w
                    WHERE w.receipt_id = NEW.receipt_id AND w.organization_id = NEW.organization_id
                      AND w.source_id = NEW.source_id AND w.state = 'Processed'
                ) THEN
                    RAISE EXCEPTION 'Resolution effect requires Processed work'
                        USING ERRCODE='23514';
                END IF;
                RETURN NULL;
            END $fn$;
            CREATE CONSTRAINT TRIGGER m9_effect_terminal AFTER INSERT
                ON tracking_processing_effects
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION m9_assert_effect_terminal();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM tracking_work_attempt_events LIMIT 1) OR
                   EXISTS (SELECT 1 FROM tracking_processing_effects LIMIT 1) OR
                   EXISTS (SELECT 1 FROM tracking_inbox_work_items
                       WHERE state <> 'Pending' OR attempt_count <> 0 OR lease_epoch <> 0 OR
                             redrive_generation <> 0 LIMIT 1) THEN
                    RAISE EXCEPTION 'M9 work evidence prevents downgrade'
                        USING ERRCODE='23514';
                END IF;
            END $guard$;
            DROP TRIGGER m9_effect_reference ON tracking_processing_effects;
            DROP FUNCTION m9_validate_effect_reference();
            DROP FUNCTION m9_lock_active_organization(uuid);
            DROP TRIGGER m9_effect_terminal ON tracking_processing_effects;
            DROP FUNCTION m9_assert_effect_terminal();
            DROP TRIGGER m9_work_effect_required ON tracking_inbox_work_items;
            DROP FUNCTION m9_assert_processing_effect();
            DROP TRIGGER m9_work_transition_audit ON tracking_inbox_work_items;
            DROP FUNCTION m9_log_work_transition();
            DROP TRIGGER m9_work_guard ON tracking_inbox_work_items;
            DROP FUNCTION m9_guard_work_item();
            DROP TABLE tracking_processing_effects;
            DROP TABLE tracking_work_attempt_events;
            DROP INDEX "IX_m9_work_queue";
            DROP INDEX "IX_m9_work_expired";
            DROP INDEX "IX_m9_work_due";
            ALTER TABLE tracking_inbox_work_items DROP CONSTRAINT "CK_m9_work_state";
            ALTER TABLE tracking_inbox_work_items
              DROP COLUMN lease_token,
              DROP COLUMN lease_epoch,
              DROP COLUMN lease_expires_at_utc,
              DROP COLUMN available_at_utc,
              DROP COLUMN redrive_generation,
              DROP COLUMN last_error_code,
              DROP COLUMN completed_at_utc,
              DROP COLUMN resolution_kind;
            ALTER TABLE tracking_inbox_work_items
              ADD CONSTRAINT "CK_m8_work_state"
              CHECK (state = 'Pending' AND attempt_count = 0
                     AND updated_at_utc >= created_at_utc);
            CREATE TRIGGER m8_work_guard
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_inbox_work_items
                FOR EACH ROW EXECUTE FUNCTION m8_guard_work_item();
            """);
    }
}
