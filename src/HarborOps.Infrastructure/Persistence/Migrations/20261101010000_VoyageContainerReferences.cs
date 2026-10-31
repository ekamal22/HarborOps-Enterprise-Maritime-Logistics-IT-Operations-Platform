using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261101010000_VoyageContainerReferences")]
public sealed class VoyageContainerReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "AK_ports_OrganizationId_Id" ON ports(organization_id,id);
            CREATE UNIQUE INDEX "AK_vessels_OrganizationId_Id" ON vessels(organization_id,id);

            CREATE FUNCTION harborops_iso6346_valid(identifier text) RETURNS boolean
            LANGUAGE plpgsql IMMUTABLE STRICT AS $$
            DECLARE
                total integer := 0;
                i integer;
                symbol text;
                value integer;
            BEGIN
                IF identifier !~ '^[A-Z]{3}U[0-9]{7}$' THEN RETURN false; END IF;
                FOR i IN 1..10 LOOP
                    symbol := substr(identifier,i,1);
                    IF symbol ~ '^[0-9]$' THEN
                        value := symbol::integer;
                    ELSE
                        value := ascii(symbol) - ascii('A') + 10;
                        value := value + (value - 1) / 10;
                    END IF;
                    total := total + value * (1 << (i - 1));
                END LOOP;
                RETURN ((total % 11) % 10) = substr(identifier,11,1)::integer;
            END;
            $$;

            CREATE TABLE voyages (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                reference_code varchar(32) NOT NULL,
                vessel_id uuid NOT NULL,
                origin_port_id uuid NOT NULL,
                destination_port_id uuid NOT NULL,
                planned_departure_at_utc timestamptz NULL,
                planned_arrival_at_utc timestamptz NULL,
                actual_departure_at_utc timestamptz NULL,
                actual_arrival_at_utc timestamptz NULL,
                voyage_status varchar(16) NOT NULL DEFAULT 'Planned',
                record_status varchar(16) NOT NULL DEFAULT 'Active',
                source_kind varchar(16) NOT NULL DEFAULT 'UserProvided',
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "FK_voyages_vessels" FOREIGN KEY (organization_id,vessel_id)
                    REFERENCES vessels(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_voyages_origin" FOREIGN KEY (organization_id,origin_port_id)
                    REFERENCES ports(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_voyages_destination" FOREIGN KEY (organization_id,destination_port_id)
                    REFERENCES ports(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_voyages_route" CHECK (origin_port_id <> destination_port_id),
                CONSTRAINT "CK_voyages_progress" CHECK (voyage_status IN ('Planned','Underway','Arrived','Cancelled')),
                CONSTRAINT "CK_voyages_planned" CHECK (planned_departure_at_utc IS NULL OR planned_arrival_at_utc IS NULL OR planned_departure_at_utc < planned_arrival_at_utc),
                CONSTRAINT "CK_voyages_actual" CHECK (actual_departure_at_utc IS NULL OR actual_arrival_at_utc IS NULL OR actual_departure_at_utc <= actual_arrival_at_utc),
                CONSTRAINT "CK_voyages_progress_time" CHECK (
                    (voyage_status='Planned' AND actual_departure_at_utc IS NULL AND actual_arrival_at_utc IS NULL) OR
                    (voyage_status='Underway' AND actual_departure_at_utc IS NOT NULL AND actual_arrival_at_utc IS NULL) OR
                    (voyage_status='Arrived' AND actual_departure_at_utc IS NOT NULL AND actual_arrival_at_utc IS NOT NULL) OR
                    (voyage_status='Cancelled' AND actual_departure_at_utc IS NULL AND actual_arrival_at_utc IS NULL)),
                CONSTRAINT "CK_voyages_status" CHECK (record_status IN ('Active','Archived')),
                CONSTRAINT "CK_voyages_source" CHECK (source_kind IN ('UserProvided','Synthetic')),
                CONSTRAINT "CK_voyages_version" CHECK (version > 0),
                CONSTRAINT "CK_voyages_code" CHECK (reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32),
                CONSTRAINT "CK_voyages_timestamp" CHECK (updated_at_utc >= created_at_utc)
            );
            CREATE UNIQUE INDEX "IX_voyages_OrganizationId_ReferenceCode" ON voyages(organization_id,reference_code);
            CREATE INDEX "IX_voyages_OrganizationId_Id" ON voyages(organization_id,id);
            CREATE INDEX "IX_voyages_OrganizationId_VoyageStatus_Id" ON voyages(organization_id,voyage_status,id);
            CREATE INDEX "IX_voyages_OrganizationId_VesselId" ON voyages(organization_id,vessel_id);
            CREATE INDEX "IX_voyages_OrganizationId_OriginPortId" ON voyages(organization_id,origin_port_id);
            CREATE INDEX "IX_voyages_OrganizationId_DestinationPortId" ON voyages(organization_id,destination_port_id);

            CREATE TABLE containers (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                reference_code varchar(32) NOT NULL,
                identifier_kind varchar(16) NOT NULL,
                container_identifier varchar(32) NOT NULL,
                equipment_type varchar(20) NOT NULL,
                display_name varchar(160) NULL,
                record_status varchar(16) NOT NULL DEFAULT 'Active',
                source_kind varchar(16) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "CK_containers_kind" CHECK (identifier_kind IN ('ISO6346','Synthetic')),
                CONSTRAINT "CK_containers_type" CHECK (equipment_type IN ('Dry20','Dry40','HighCube40','Reefer20','Reefer40','Other')),
                CONSTRAINT "CK_containers_identifier" CHECK (
                    (identifier_kind='ISO6346' AND harborops_iso6346_valid(container_identifier) AND source_kind='UserProvided') OR
                    (identifier_kind='Synthetic' AND container_identifier ~ '^SYN-BOX-[A-Z0-9]{4,16}$' AND source_kind='Synthetic')),
                CONSTRAINT "CK_containers_display" CHECK (display_name IS NULL OR length(btrim(display_name)) BETWEEN 1 AND 160),
                CONSTRAINT "CK_containers_status" CHECK (record_status IN ('Active','Archived')),
                CONSTRAINT "CK_containers_source" CHECK (source_kind IN ('UserProvided','Synthetic')),
                CONSTRAINT "CK_containers_version" CHECK (version > 0),
                CONSTRAINT "CK_containers_code" CHECK (reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32),
                CONSTRAINT "CK_containers_timestamp" CHECK (updated_at_utc >= created_at_utc)
            );
            CREATE UNIQUE INDEX "IX_containers_OrganizationId_ReferenceCode" ON containers(organization_id,reference_code);
            CREATE UNIQUE INDEX "IX_containers_OrganizationId_ContainerIdentifier" ON containers(organization_id,container_identifier);
            CREATE INDEX "IX_containers_OrganizationId_Id" ON containers(organization_id,id);

            CREATE FUNCTION harborops_maritime_immutable() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organization_id IS DISTINCT FROM OLD.organization_id
                    OR NEW.reference_code IS DISTINCT FROM OLD.reference_code
                    OR NEW.source_kind IS DISTINCT FROM OLD.source_kind
                    OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc THEN
                    RAISE EXCEPTION 'Maritime identity immutable' USING ERRCODE = '23514';
                END IF;
                IF TG_TABLE_NAME = 'voyages' THEN
                    IF NEW.vessel_id IS DISTINCT FROM OLD.vessel_id
                        OR NEW.origin_port_id IS DISTINCT FROM OLD.origin_port_id
                        OR NEW.destination_port_id IS DISTINCT FROM OLD.destination_port_id THEN
                        RAISE EXCEPTION 'Voyage route immutable' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.voyage_status <> OLD.voyage_status AND NOT (
                        (OLD.voyage_status='Planned' AND NEW.voyage_status IN ('Underway','Cancelled')) OR
                        (OLD.voyage_status='Underway' AND NEW.voyage_status='Arrived')) THEN
                        RAISE EXCEPTION 'Illegal voyage transition' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.actual_departure_at_utc IS NOT NULL AND NEW.actual_departure_at_utc IS DISTINCT FROM OLD.actual_departure_at_utc
                        OR OLD.actual_arrival_at_utc IS NOT NULL AND NEW.actual_arrival_at_utc IS DISTINCT FROM OLD.actual_arrival_at_utc THEN
                        RAISE EXCEPTION 'Actual times immutable' USING ERRCODE = '23514';
                    END IF;
                ELSIF TG_TABLE_NAME = 'containers' THEN
                    IF NEW.identifier_kind IS DISTINCT FROM OLD.identifier_kind
                        OR NEW.container_identifier IS DISTINCT FROM OLD.container_identifier
                        OR NEW.equipment_type IS DISTINCT FROM OLD.equipment_type THEN
                        RAISE EXCEPTION 'Container identity immutable' USING ERRCODE = '23514';
                    END IF;
                END IF;
                IF NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'Version must advance exactly once' USING ERRCODE = '23514';
                END IF;
                IF OLD.record_status='Archived' THEN
                    IF NEW.record_status <> 'Active' OR
                        (to_jsonb(NEW) - 'record_status' - 'version' - 'updated_at_utc')
                        IS DISTINCT FROM
                        (to_jsonb(OLD) - 'record_status' - 'version' - 'updated_at_utc') THEN
                        RAISE EXCEPTION 'Archive restoration must not edit fields' USING ERRCODE = '23514';
                    END IF;
                END IF;
                IF TG_TABLE_NAME = 'voyages' THEN
                    IF OLD.voyage_status <> 'Planned' AND
                        (NEW.planned_departure_at_utc IS DISTINCT FROM OLD.planned_departure_at_utc OR
                         NEW.planned_arrival_at_utc IS DISTINCT FROM OLD.planned_arrival_at_utc) THEN
                        RAISE EXCEPTION 'Historical planned schedule immutable' USING ERRCODE = '23514';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER voyages_immutable BEFORE UPDATE ON voyages
                FOR EACH ROW EXECUTE FUNCTION harborops_maritime_immutable();
            CREATE TRIGGER containers_immutable BEFORE UPDATE ON containers
                FOR EACH ROW EXECUTE FUNCTION harborops_maritime_immutable();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE containers;
            DROP TABLE voyages;
            DROP FUNCTION harborops_maritime_immutable();
            DROP FUNCTION harborops_iso6346_valid(text);
            DROP INDEX "AK_ports_OrganizationId_Id";
            DROP INDEX "AK_vessels_OrganizationId_Id";
            """);
    }
}
