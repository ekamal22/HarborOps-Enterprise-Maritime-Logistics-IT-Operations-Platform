using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261031110000_PortVesselReferences")]
public sealed class PortVesselReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ports (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                reference_code varchar(32) NOT NULL,
                name varchar(160) NOT NULL,
                country_code varchar(2) NOT NULL,
                un_locode varchar(5) NULL,
                status varchar(16) NOT NULL,
                source_kind varchar(16) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "CK_ports_status" CHECK (status IN ('Active','Archived')),
                CONSTRAINT "CK_ports_source_kind" CHECK (source_kind IN ('UserProvided','Synthetic')),
                CONSTRAINT "CK_ports_version" CHECK (version > 0),
                CONSTRAINT "CK_ports_code" CHECK (reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32),
                CONSTRAINT "CK_ports_name" CHECK (length(btrim(name)) BETWEEN 1 AND 160),
                CONSTRAINT "CK_ports_time" CHECK (updated_at_utc >= created_at_utc),
                CONSTRAINT "CK_ports_country" CHECK (country_code ~ '^[A-Z]{2}$'),
                CONSTRAINT "CK_ports_locode" CHECK (un_locode IS NULL OR (un_locode ~ '^[A-Z]{2}[A-Z2-9]{3}$' AND substring(un_locode,1,2) = country_code))
            );
            CREATE UNIQUE INDEX "IX_ports_OrganizationId_ReferenceCode" ON ports(organization_id, reference_code);
            CREATE INDEX "IX_ports_OrganizationId_Id" ON ports(organization_id, id);
            CREATE UNIQUE INDEX "IX_ports_OrganizationId_UnLocode" ON ports(organization_id, un_locode) WHERE un_locode IS NOT NULL;

            CREATE TABLE vessels (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                reference_code varchar(32) NOT NULL,
                name varchar(160) NOT NULL,
                imo_number varchar(7) NULL,
                mmsi varchar(9) NULL,
                flag_country_code varchar(2) NULL,
                status varchar(16) NOT NULL,
                source_kind varchar(16) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "CK_vessels_status" CHECK (status IN ('Active','Archived')),
                CONSTRAINT "CK_vessels_source_kind" CHECK (source_kind IN ('UserProvided','Synthetic')),
                CONSTRAINT "CK_vessels_version" CHECK (version > 0),
                CONSTRAINT "CK_vessels_code" CHECK (reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32),
                CONSTRAINT "CK_vessels_name" CHECK (length(btrim(name)) BETWEEN 1 AND 160),
                CONSTRAINT "CK_vessels_time" CHECK (updated_at_utc >= created_at_utc),
                CONSTRAINT "CK_vessels_mmsi" CHECK (mmsi IS NULL OR (mmsi ~ '^[0-9]{9}$' AND mmsi <> '000000000')),
                CONSTRAINT "CK_vessels_flag" CHECK (flag_country_code IS NULL OR flag_country_code ~ '^[A-Z]{2}$'),
                CONSTRAINT "CK_vessels_imo" CHECK (imo_number IS NULL OR (imo_number ~ '^[0-9]{7}$' AND (substring(imo_number,1,1)::int * 7 + substring(imo_number,2,1)::int * 6 + substring(imo_number,3,1)::int * 5 + substring(imo_number,4,1)::int * 4 + substring(imo_number,5,1)::int * 3 + substring(imo_number,6,1)::int * 2) % 10 = substring(imo_number,7,1)::int))
            );
            CREATE UNIQUE INDEX "IX_vessels_OrganizationId_ReferenceCode" ON vessels(organization_id, reference_code);
            CREATE INDEX "IX_vessels_OrganizationId_Id" ON vessels(organization_id, id);
            CREATE UNIQUE INDEX "IX_vessels_OrganizationId_ImoNumber" ON vessels(organization_id, imo_number) WHERE imo_number IS NOT NULL;
            CREATE UNIQUE INDEX "IX_vessels_OrganizationId_Mmsi" ON vessels(organization_id, mmsi) WHERE mmsi IS NOT NULL;

            CREATE FUNCTION harborops_reference_immutable_fields() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id IS DISTINCT FROM OLD.id
                    OR NEW.organization_id IS DISTINCT FROM OLD.organization_id
                    OR NEW.reference_code IS DISTINCT FROM OLD.reference_code
                    OR NEW.source_kind IS DISTINCT FROM OLD.source_kind
                    OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc THEN
                    RAISE EXCEPTION 'Reference identity and provenance are immutable'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER ports_immutable_fields BEFORE UPDATE ON ports
            FOR EACH ROW EXECUTE FUNCTION harborops_reference_immutable_fields();
            CREATE TRIGGER vessels_immutable_fields BEFORE UPDATE ON vessels
            FOR EACH ROW EXECUTE FUNCTION harborops_reference_immutable_fields();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE vessels;
            DROP TABLE ports;
            DROP FUNCTION harborops_reference_immutable_fields();
            """);
    }
}
