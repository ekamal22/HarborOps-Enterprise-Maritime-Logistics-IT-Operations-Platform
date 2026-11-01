using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261101180000_ShipmentLifecycle")]
public sealed class ShipmentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE shipments (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                reference_code varchar(32) NOT NULL,
                external_reference varchar(80),
                display_name varchar(160),
                origin_port_id uuid NOT NULL,
                destination_port_id uuid NOT NULL,
                planned_departure_at_utc timestamptz,
                planned_arrival_at_utc timestamptz,
                workflow_status varchar(16) NOT NULL DEFAULT 'Draft',
                record_status varchar(16) NOT NULL DEFAULT 'Active',
                source_kind varchar(16) NOT NULL DEFAULT 'UserProvided',
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                archived_at_utc timestamptz,
                changed_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_shipments_origin" FOREIGN KEY (organization_id,origin_port_id)
                    REFERENCES ports(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_shipments_destination" FOREIGN KEY (organization_id,destination_port_id)
                    REFERENCES ports(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_shipments_code" CHECK (reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32),
                CONSTRAINT "CK_shipments_external" CHECK (external_reference IS NULL OR
                    (external_reference ~ '^[A-Z0-9._/-]{1,80}$')),
                CONSTRAINT "CK_shipments_display" CHECK (display_name IS NULL OR
                    (length(btrim(display_name)) BETWEEN 1 AND 160 AND display_name !~ '[[:cntrl:]]')),
                CONSTRAINT "CK_shipments_route" CHECK (origin_port_id <> destination_port_id),
                CONSTRAINT "CK_shipments_times" CHECK (planned_departure_at_utc IS NULL OR
                    planned_arrival_at_utc IS NULL OR planned_departure_at_utc < planned_arrival_at_utc),
                CONSTRAINT "CK_shipments_workflow" CHECK (workflow_status IN
                    ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')),
                CONSTRAINT "CK_shipments_status" CHECK (record_status IN ('Active','Archived')),
                CONSTRAINT "CK_shipments_source" CHECK (source_kind IN ('UserProvided','Synthetic')),
                CONSTRAINT "CK_shipments_version" CHECK (version > 0),
                CONSTRAINT "CK_shipments_archive" CHECK (
                    (record_status='Active' AND archived_at_utc IS NULL) OR
                    (record_status='Archived' AND archived_at_utc IS NOT NULL)),
                CONSTRAINT "CK_shipments_time" CHECK (updated_at_utc >= created_at_utc)
            );
            CREATE UNIQUE INDEX "AK_shipments_OrganizationId_Id" ON shipments(organization_id,id);
            CREATE UNIQUE INDEX "IX_shipments_OrganizationId_ReferenceCode"
                ON shipments(organization_id,reference_code);
            CREATE UNIQUE INDEX "IX_shipments_OrganizationId_ExternalReference"
                ON shipments(organization_id,external_reference) WHERE external_reference IS NOT NULL;
            CREATE INDEX "IX_shipments_OrganizationId_RecordStatus_Id"
                ON shipments(organization_id,record_status,id);
            CREATE INDEX "IX_shipments_OrganizationId_WorkflowStatus_Id"
                ON shipments(organization_id,workflow_status,id);
            CREATE INDEX "IX_shipments_OrganizationId_OriginPortId"
                ON shipments(organization_id,origin_port_id);
            CREATE INDEX "IX_shipments_OrganizationId_DestinationPortId"
                ON shipments(organization_id,destination_port_id);
            CREATE INDEX "IX_shipments_changed_by_user_id" ON shipments(changed_by_user_id);

            CREATE TABLE shipment_status_changes (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                from_status varchar(16),
                to_status varchar(16) NOT NULL,
                version bigint NOT NULL,
                changed_at_utc timestamptz NOT NULL,
                changed_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_shipment_status_changes_shipment"
                    FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_shipment_status_changes_to"
                    CHECK (to_status IN ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')),
                CONSTRAINT "CK_shipment_status_changes_from"
                    CHECK (from_status IS NULL OR from_status IN
                        ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')),
                CONSTRAINT "CK_shipment_status_changes_version" CHECK (version > 0)
            );
            CREATE UNIQUE INDEX "IX_shipment_status_changes_OrganizationId_ShipmentId_Version"
                ON shipment_status_changes(organization_id,shipment_id,version);
            CREATE INDEX "IX_shipment_status_changes_changed_by_user_id"
                ON shipment_status_changes(changed_by_user_id);

            CREATE FUNCTION harborops_shipment_validate() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Shipment deletes forbidden' USING ERRCODE='23514';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    IF NEW.workflow_status <> 'Draft' OR NEW.record_status <> 'Active'
                        OR NEW.version <> 1 OR NEW.archived_at_utc IS NOT NULL THEN
                        RAISE EXCEPTION 'Invalid shipment creation' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF (NEW.id,NEW.organization_id,NEW.reference_code,NEW.external_reference,
                    NEW.origin_port_id,NEW.destination_port_id,NEW.source_kind,NEW.created_at_utc)
                    IS DISTINCT FROM
                    (OLD.id,OLD.organization_id,OLD.reference_code,OLD.external_reference,
                    OLD.origin_port_id,OLD.destination_port_id,OLD.source_kind,OLD.created_at_utc) THEN
                    RAISE EXCEPTION 'Shipment identity immutable' USING ERRCODE='23514';
                END IF;
                IF NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'Invalid shipment version' USING ERRCODE='23514';
                END IF;
                IF OLD.workflow_status <> NEW.workflow_status AND NOT (
                    (OLD.workflow_status='Draft' AND NEW.workflow_status IN ('Booked','Cancelled')) OR
                    (OLD.workflow_status='Booked' AND NEW.workflow_status IN ('InTransit','Cancelled')) OR
                    (OLD.workflow_status='InTransit' AND NEW.workflow_status IN ('Arrived','Cancelled')) OR
                    (OLD.workflow_status='Arrived' AND NEW.workflow_status='Completed')) THEN
                    RAISE EXCEPTION 'Illegal shipment transition' USING ERRCODE='23514';
                END IF;
                IF OLD.workflow_status NOT IN ('Draft','Booked') AND
                    (NEW.planned_departure_at_utc IS DISTINCT FROM OLD.planned_departure_at_utc OR
                     NEW.planned_arrival_at_utc IS DISTINCT FROM OLD.planned_arrival_at_utc) THEN
                    RAISE EXCEPTION 'Historical shipment schedule immutable' USING ERRCODE='23514';
                END IF;
                IF OLD.record_status='Archived' THEN
                    IF NEW.record_status<>'Active' OR NEW.archived_at_utc IS NOT NULL OR
                        (to_jsonb(NEW) - 'record_status' - 'version' - 'updated_at_utc' - 'archived_at_utc' - 'changed_by_user_id')
                        IS DISTINCT FROM
                        (to_jsonb(OLD) - 'record_status' - 'version' - 'updated_at_utc' - 'archived_at_utc' - 'changed_by_user_id') THEN
                        RAISE EXCEPTION 'Invalid shipment restoration' USING ERRCODE='23514';
                    END IF;
                ELSIF OLD.record_status='Active' AND NEW.record_status='Archived' THEN
                    IF NEW.archived_at_utc IS NULL OR
                        (to_jsonb(NEW) - 'record_status' - 'version' - 'updated_at_utc' - 'archived_at_utc' - 'changed_by_user_id')
                        IS DISTINCT FROM
                        (to_jsonb(OLD) - 'record_status' - 'version' - 'updated_at_utc' - 'archived_at_utc' - 'changed_by_user_id') THEN
                        RAISE EXCEPTION 'Archive cannot edit shipment' USING ERRCODE='23514';
                    END IF;
                ELSE
                    IF NEW.record_status<>OLD.record_status OR NEW.archived_at_utc IS DISTINCT FROM OLD.archived_at_utc THEN
                        RAISE EXCEPTION 'Invalid archive change' USING ERRCODE='23514';
                    END IF;
                END IF;
                RETURN NEW;
            END $$;

            CREATE FUNCTION harborops_shipment_history_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP<>'INSERT' OR pg_trigger_depth()<2 THEN
                    RAISE EXCEPTION 'Shipment status history immutable' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $$;

            CREATE FUNCTION harborops_shipment_record_transition() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP='INSERT' OR NEW.workflow_status IS DISTINCT FROM OLD.workflow_status THEN
                    INSERT INTO shipment_status_changes
                        (id,organization_id,shipment_id,from_status,to_status,version,changed_at_utc,changed_by_user_id)
                    VALUES (gen_random_uuid(), NEW.organization_id, NEW.id,
                        CASE WHEN TG_OP='INSERT' THEN NULL ELSE OLD.workflow_status END,
                        NEW.workflow_status, NEW.version, NEW.updated_at_utc, NEW.changed_by_user_id);
                END IF;
                RETURN NULL;
            END $$;

            CREATE TRIGGER shipments_validate BEFORE INSERT OR UPDATE OR DELETE ON shipments
                FOR EACH ROW EXECUTE FUNCTION harborops_shipment_validate();
            CREATE TRIGGER shipments_status_history AFTER INSERT OR UPDATE ON shipments
                FOR EACH ROW EXECUTE FUNCTION harborops_shipment_record_transition();
            CREATE TRIGGER status_history_immutable BEFORE INSERT OR UPDATE OR DELETE ON shipment_status_changes
                FOR EACH ROW EXECUTE FUNCTION harborops_shipment_history_guard();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE shipment_status_changes;
            DROP TABLE shipments;
            DROP FUNCTION harborops_shipment_record_transition();
            DROP FUNCTION harborops_shipment_history_guard();
            DROP FUNCTION harborops_shipment_validate();
            """);
    }
}
