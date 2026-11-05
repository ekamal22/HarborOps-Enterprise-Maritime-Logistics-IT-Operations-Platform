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
            CREATE TABLE shipment_tracking_projections (
                shipment_id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                observed_state varchar(20) NOT NULL DEFAULT 'Unknown',
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
                    (observed_state IN ('Unknown','EquipmentIn','EquipmentOut','Conflict')),
                CONSTRAINT ck_m10_revision CHECK (materialization_revision > 0),
                CONSTRAINT ck_m10_digest CHECK (input_digest ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_m10_projection_scope
                ON shipment_tracking_projections(organization_id,shipment_id);

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
                CONSTRAINT ck_m10_correction_generation CHECK (generation>0)
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
