using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261103190000_TrackingInboxAndHistory")]
public sealed class TrackingInboxAndHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE integration_credentials
                ADD CONSTRAINT "AK_m8_integration_credential_scope"
                UNIQUE (organization_id, source_id, id);

            CREATE TABLE tracking_inbox_receipts (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                credential_id uuid NOT NULL,
                profile varchar(48) NOT NULL,
                source_event_id varchar(128) NOT NULL,
                canonical_digest bytea NOT NULL,
                canonical_payload_utf8 bytea NOT NULL,
                received_at_utc timestamptz NOT NULL,
                correlation_id varchar(64) NOT NULL,
                CONSTRAINT "AK_m8_receipt_scope" UNIQUE (organization_id, source_id, id),
                CONSTRAINT "FK_m8_receipt_source"
                    FOREIGN KEY (organization_id, source_id)
                    REFERENCES integration_sources(organization_id, id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m8_receipt_credential"
                    FOREIGN KEY (organization_id, source_id, credential_id)
                    REFERENCES integration_credentials(organization_id, source_id, id) ON DELETE RESTRICT,
                CONSTRAINT "CK_m8_receipt_digest" CHECK (octet_length(canonical_digest) = 32),
                CONSTRAINT "CK_m8_receipt_canonical"
                    CHECK (octet_length(canonical_payload_utf8) BETWEEN 1 AND 16384),
                CONSTRAINT "CK_m8_receipt_profile"
                    CHECK (profile = 'tnt-2.2-harborops-v1'),
                CONSTRAINT "CK_m8_receipt_event_id"
                    CHECK (source_event_id ~ '^[A-Za-z0-9._:-]{1,128}$'),
                CONSTRAINT "CK_m8_receipt_correlation"
                    CHECK (length(correlation_id) BETWEEN 1 AND 64)
            );
            CREATE UNIQUE INDEX "UQ_m8_receipt_key"
                ON tracking_inbox_receipts (organization_id, source_id, profile, source_event_id);
            CREATE INDEX "IX_m8_receipt_chronology"
                ON tracking_inbox_receipts(organization_id, source_id, received_at_utc);

            CREATE TABLE tracking_accepted_events (
                id uuid PRIMARY KEY,
                receipt_id uuid NOT NULL,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                event_family varchar(16) NOT NULL,
                event_code varchar(8) NOT NULL,
                classifier varchar(8) NOT NULL,
                external_schema_version varchar(16) NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                equipment_reference varchar(32),
                transport_reference varchar(80),
                raw_payload_utf8 bytea NOT NULL,
                canonical_payload jsonb NOT NULL,
                CONSTRAINT "FK_m8_event_receipt"
                    FOREIGN KEY (organization_id, source_id, receipt_id)
                    REFERENCES tracking_inbox_receipts(organization_id, source_id, id)
                    ON DELETE RESTRICT,
                CONSTRAINT "CK_m8_event_payload"
                    CHECK (octet_length(raw_payload_utf8) BETWEEN 1 AND 16384),
                CONSTRAINT "CK_m8_event_variant"
                    CHECK (classifier = 'ACT' AND external_schema_version = '2.2.0'
                        AND ((event_family = 'Equipment' AND event_code IN ('GTIN', 'GTOT')
                              AND equipment_reference IS NOT NULL AND transport_reference IS NULL)
                          OR (event_family = 'Transport' AND event_code IN ('ARRI', 'DEPA')
                              AND transport_reference IS NOT NULL AND equipment_reference IS NULL))),
                CONSTRAINT "CK_m8_event_canonical"
                    CHECK (jsonb_typeof(canonical_payload) = 'object')
            );
            CREATE UNIQUE INDEX "UQ_m8_event_receipt"
                ON tracking_accepted_events (receipt_id);
            CREATE INDEX "IX_m8_event_scope"
                ON tracking_accepted_events(organization_id, source_id, receipt_id);

            CREATE TABLE tracking_inbox_work_items (
                receipt_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                state varchar(24) NOT NULL DEFAULT 'Pending',
                attempt_count integer NOT NULL DEFAULT 0,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "FK_m8_work_receipt"
                    FOREIGN KEY (organization_id, source_id, receipt_id)
                    REFERENCES tracking_inbox_receipts(organization_id, source_id, id)
                    ON DELETE RESTRICT,
                CONSTRAINT "CK_m8_work_state"
                    CHECK (state = 'Pending' AND attempt_count = 0
                           AND updated_at_utc >= created_at_utc)
            );
            CREATE INDEX "IX_m8_work_pending"
                ON tracking_inbox_work_items(organization_id, state, created_at_utc);
            CREATE INDEX "IX_m8_work_scope"
                ON tracking_inbox_work_items(organization_id, source_id, receipt_id);

            CREATE TABLE tracking_receipt_conflicts (
                id uuid PRIMARY KEY,
                receipt_id uuid NOT NULL,
                organization_id uuid NOT NULL,
                source_id uuid NOT NULL,
                attempted_digest bytea NOT NULL,
                first_seen_at_utc timestamptz NOT NULL,
                correlation_id varchar(64) NOT NULL,
                CONSTRAINT "FK_m8_conflict_receipt"
                    FOREIGN KEY (organization_id, source_id, receipt_id)
                    REFERENCES tracking_inbox_receipts(organization_id, source_id, id)
                    ON DELETE RESTRICT,
                CONSTRAINT "CK_m8_conflict_digest"
                    CHECK (octet_length(attempted_digest) = 32),
                CONSTRAINT "CK_m8_conflict_correlation"
                    CHECK (length(correlation_id) BETWEEN 1 AND 64)
            );
            CREATE UNIQUE INDEX "UQ_m8_conflict_receipt"
                ON tracking_receipt_conflicts (receipt_id);
            CREATE INDEX "IX_m8_conflict_scope"
                ON tracking_receipt_conflicts(organization_id, source_id, receipt_id);

            CREATE FUNCTION m8_guard_append_only() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'Accepted tracking history is immutable' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;

            CREATE TRIGGER m8_receipt_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_inbox_receipts
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();
            CREATE TRIGGER m8_event_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_accepted_events
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();
            CREATE TRIGGER m8_conflict_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_receipt_conflicts
                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();

            CREATE FUNCTION m8_guard_work_item() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP <> 'INSERT' OR NEW.state <> 'Pending' OR NEW.attempt_count <> 0
                   OR NEW.updated_at_utc <> NEW.created_at_utc THEN
                    RAISE EXCEPTION 'Invalid M8 inbox work state' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m8_work_guard
                BEFORE INSERT OR UPDATE OR DELETE ON tracking_inbox_work_items
                FOR EACH ROW EXECUTE FUNCTION m8_guard_work_item();

            CREATE FUNCTION m8_assert_complete_receipt() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM tracking_accepted_events e
                    WHERE e.receipt_id = NEW.id AND e.organization_id = NEW.organization_id
                      AND e.source_id = NEW.source_id
                ) OR NOT EXISTS (
                    SELECT 1 FROM tracking_inbox_work_items w
                    WHERE w.receipt_id = NEW.id AND w.organization_id = NEW.organization_id
                      AND w.source_id = NEW.source_id
                ) THEN
                    RAISE EXCEPTION 'Receipt requires an event and pending work item'
                        USING ERRCODE='23514';
                END IF;
                RETURN NULL;
            END $fn$;
            CREATE CONSTRAINT TRIGGER m8_receipt_complete
                AFTER INSERT ON tracking_inbox_receipts
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION m8_assert_complete_receipt();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Normal downgrade is permitted only before any durable receipt is accepted.
            -- The append-only row triggers do not protect against DROP TABLE.
            DO $m8_downgrade$
            BEGIN
                IF EXISTS (SELECT 1 FROM tracking_inbox_receipts LIMIT 1) THEN
                    RAISE EXCEPTION 'Cannot downgrade M8 with retained tracking receipts'
                        USING ERRCODE = '23514';
                END IF;
            END $m8_downgrade$;
            DROP TABLE tracking_receipt_conflicts;
            DROP TABLE tracking_inbox_work_items;
            DROP TABLE tracking_accepted_events;
            DROP TABLE tracking_inbox_receipts;
            DROP FUNCTION m8_assert_complete_receipt();
            DROP FUNCTION m8_guard_work_item();
            DROP FUNCTION m8_guard_append_only();
            ALTER TABLE integration_credentials
                DROP CONSTRAINT "AK_m8_integration_credential_scope";
            """);
    }
}
