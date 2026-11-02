using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261102080000_ShipmentAssignments")]
public sealed class ShipmentAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE UNIQUE INDEX "AK_containers_OrganizationId_Id" ON containers(organization_id,id);
            CREATE UNIQUE INDEX "AK_voyages_OrganizationId_Id" ON voyages(organization_id,id);

            CREATE TABLE shipment_container_assignments (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                shipment_id uuid NOT NULL,
                container_id uuid NOT NULL,
                starts_at_utc timestamptz NOT NULL,
                ends_at_utc timestamptz NULL,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                created_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                closed_by_user_id uuid NULL REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m6_container_shipment"
                    FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m6_container_target"
                    FOREIGN KEY (organization_id,container_id)
                    REFERENCES containers(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_m6_container_window"
                    CHECK (ends_at_utc IS NULL OR ends_at_utc > starts_at_utc),
                CONSTRAINT "CK_m6_container_version"
                    CHECK (version > 0 AND updated_at_utc >= created_at_utc),
                CONSTRAINT "CK_m6_container_closure"
                    CHECK ((ends_at_utc IS NULL AND closed_by_user_id IS NULL) OR
                           ends_at_utc IS NOT NULL)
            );
            CREATE TABLE shipment_voyage_assignments (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                shipment_id uuid NOT NULL,
                voyage_id uuid NOT NULL,
                starts_at_utc timestamptz NOT NULL,
                ends_at_utc timestamptz NULL,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                created_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                closed_by_user_id uuid NULL REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m6_voyage_shipment"
                    FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "FK_m6_voyage_target"
                    FOREIGN KEY (organization_id,voyage_id)
                    REFERENCES voyages(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_m6_voyage_window"
                    CHECK (ends_at_utc IS NULL OR ends_at_utc > starts_at_utc),
                CONSTRAINT "CK_m6_voyage_version"
                    CHECK (version > 0 AND updated_at_utc >= created_at_utc),
                CONSTRAINT "CK_m6_voyage_closure"
                    CHECK ((ends_at_utc IS NULL AND closed_by_user_id IS NULL) OR
                           ends_at_utc IS NOT NULL)
            );

            CREATE INDEX "IX_m6_container_shipment" ON shipment_container_assignments
                (organization_id,shipment_id,starts_at_utc,id);
            CREATE INDEX "IX_m6_container_target" ON shipment_container_assignments
                (organization_id,container_id,starts_at_utc,id);
            CREATE INDEX "IX_m6_voyage_shipment" ON shipment_voyage_assignments
                (organization_id,shipment_id,starts_at_utc,id);
            CREATE INDEX "IX_m6_voyage_target" ON shipment_voyage_assignments
                (organization_id,voyage_id,starts_at_utc,id);
            CREATE INDEX "IX_m6_container_created_by" ON shipment_container_assignments(created_by_user_id);
            CREATE INDEX "IX_m6_container_closed_by" ON shipment_container_assignments(closed_by_user_id);
            CREATE INDEX "IX_m6_voyage_created_by" ON shipment_voyage_assignments(created_by_user_id);
            CREATE INDEX "IX_m6_voyage_closed_by" ON shipment_voyage_assignments(closed_by_user_id);

            ALTER TABLE shipment_container_assignments
                ADD CONSTRAINT ex_m6_container_overlap EXCLUDE USING gist
                (organization_id WITH =, container_id WITH =,
                 tstzrange(starts_at_utc,ends_at_utc,'[)') WITH &&);
            ALTER TABLE shipment_voyage_assignments
                ADD CONSTRAINT ex_m6_voyage_overlap EXCLUDE USING gist
                (organization_id WITH =, shipment_id WITH =,
                 tstzrange(starts_at_utc,ends_at_utc,'[)') WITH &&);

            CREATE FUNCTION m6_validate_assignment() RETURNS trigger LANGUAGE plpgsql AS $fn$
            DECLARE
                s shipments%ROWTYPE;
                c containers%ROWTYPE;
                v voyages%ROWTYPE;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Assignment history cannot be deleted' USING ERRCODE='23514';
                END IF;
                IF TG_OP = 'UPDATE' THEN
                    IF NEW.id IS DISTINCT FROM OLD.id OR
                       NEW.organization_id IS DISTINCT FROM OLD.organization_id OR
                       NEW.shipment_id IS DISTINCT FROM OLD.shipment_id OR
                       NEW.starts_at_utc IS DISTINCT FROM OLD.starts_at_utc OR
                       NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc OR
                       NEW.created_by_user_id IS DISTINCT FROM OLD.created_by_user_id OR
                       (TG_TABLE_NAME = 'shipment_container_assignments' AND
                          to_jsonb(NEW)->>'container_id' IS DISTINCT FROM
                          to_jsonb(OLD)->>'container_id') OR
                       (TG_TABLE_NAME = 'shipment_voyage_assignments' AND
                          to_jsonb(NEW)->>'voyage_id' IS DISTINCT FROM
                          to_jsonb(OLD)->>'voyage_id') THEN
                        RAISE EXCEPTION 'Immutable assignment identity' USING ERRCODE='23514';
                    END IF;
                    IF OLD.ends_at_utc IS NOT NULL OR NEW.ends_at_utc IS NULL OR
                       NEW.ends_at_utc <= OLD.starts_at_utc OR
                       NEW.version <> OLD.version + 1 OR
                       NEW.closed_by_user_id IS NULL OR
                       NEW.updated_at_utc < OLD.updated_at_utc THEN
                        RAISE EXCEPTION 'Invalid assignment closure' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF NEW.version <> 1 OR NEW.closed_by_user_id IS NOT NULL OR
                   NEW.updated_at_utc < NEW.created_at_utc OR
                   (NEW.ends_at_utc IS NOT NULL AND
                    NEW.ends_at_utc <= NEW.starts_at_utc) THEN
                    RAISE EXCEPTION 'Invalid assignment creation' USING ERRCODE='23514';
                END IF;

                SELECT * INTO s FROM shipments WHERE organization_id=NEW.organization_id
                    AND id=NEW.shipment_id FOR SHARE;
                IF NOT FOUND OR s.record_status <> 'Active' OR
                   s.workflow_status IN ('Cancelled','Completed') THEN
                    RAISE EXCEPTION 'Shipment unavailable for allocation' USING ERRCODE='23514';
                END IF;
                IF TG_TABLE_NAME = 'shipment_container_assignments' THEN
                    SELECT * INTO c FROM containers WHERE organization_id=NEW.organization_id
                        AND id=NEW.container_id FOR SHARE;
                    IF NOT FOUND OR c.record_status <> 'Active' THEN
                        RAISE EXCEPTION 'Container unavailable for allocation' USING ERRCODE='23514';
                    END IF;
                ELSE
                    SELECT * INTO v FROM voyages WHERE organization_id=NEW.organization_id
                        AND id=NEW.voyage_id FOR SHARE;
                    IF NOT FOUND OR v.record_status <> 'Active' OR
                       v.voyage_status NOT IN ('Planned','Underway') OR
                       v.origin_port_id <> s.origin_port_id OR
                       v.destination_port_id <> s.destination_port_id OR
                       (s.planned_departure_at_utc IS NOT NULL AND
                        s.planned_arrival_at_utc IS NOT NULL AND
                        v.planned_departure_at_utc IS NOT NULL AND
                        v.planned_arrival_at_utc IS NOT NULL AND
                        (s.planned_arrival_at_utc <= v.planned_departure_at_utc OR
                         v.planned_arrival_at_utc <= s.planned_departure_at_utc)) THEN
                        RAISE EXCEPTION 'Incompatible voyage allocation' USING ERRCODE='23514';
                    END IF;
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m6_container_validate BEFORE INSERT OR UPDATE OR DELETE
                ON shipment_container_assignments
                FOR EACH ROW EXECUTE FUNCTION m6_validate_assignment();
            CREATE TRIGGER m6_voyage_validate BEFORE INSERT OR UPDATE OR DELETE
                ON shipment_voyage_assignments
                FOR EACH ROW EXECUTE FUNCTION m6_validate_assignment();

            CREATE FUNCTION m6_guard_parent() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Referenced business record cannot be deleted' USING ERRCODE='23514';
                END IF;
                IF TG_TABLE_NAME = 'shipments' THEN
                    IF (NEW.record_status = 'Archived' AND OLD.record_status <> 'Archived') OR
                       (NEW.workflow_status IN ('Cancelled','Completed') AND
                        NEW.workflow_status IS DISTINCT FROM OLD.workflow_status) THEN
                        IF EXISTS (SELECT 1 FROM shipment_container_assignments a
                            WHERE a.organization_id=OLD.organization_id AND
                                  a.shipment_id=OLD.id AND a.ends_at_utc IS NULL) OR
                           EXISTS (SELECT 1 FROM shipment_voyage_assignments a
                            WHERE a.organization_id=OLD.organization_id AND
                                  a.shipment_id=OLD.id AND a.ends_at_utc IS NULL) THEN
                            RAISE EXCEPTION 'Close shipment allocations first' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF (NEW.planned_departure_at_utc IS DISTINCT FROM OLD.planned_departure_at_utc OR
                        NEW.planned_arrival_at_utc IS DISTINCT FROM OLD.planned_arrival_at_utc) AND
                        EXISTS (SELECT 1 FROM shipment_voyage_assignments a JOIN voyages v
                          ON v.organization_id=a.organization_id AND v.id=a.voyage_id
                          WHERE a.organization_id=OLD.organization_id AND a.shipment_id=OLD.id
                            AND a.ends_at_utc IS NULL
                            AND NEW.planned_departure_at_utc IS NOT NULL
                            AND NEW.planned_arrival_at_utc IS NOT NULL
                            AND v.planned_departure_at_utc IS NOT NULL
                            AND v.planned_arrival_at_utc IS NOT NULL
                            AND (NEW.planned_arrival_at_utc <= v.planned_departure_at_utc OR
                                 v.planned_arrival_at_utc <= NEW.planned_departure_at_utc)) THEN
                        RAISE EXCEPTION 'Shipment planning conflicts with allocation' USING ERRCODE='23514';
                    END IF;
                ELSIF TG_TABLE_NAME = 'containers' THEN
                    IF NEW.record_status = 'Archived' AND OLD.record_status <> 'Archived' AND
                        EXISTS (SELECT 1 FROM shipment_container_assignments a
                          WHERE a.organization_id=OLD.organization_id AND
                                a.container_id=OLD.id AND a.ends_at_utc IS NULL) THEN
                        RAISE EXCEPTION 'Close container allocations first' USING ERRCODE='23514';
                    END IF;
                ELSIF TG_TABLE_NAME = 'voyages' THEN
                    IF (NEW.record_status = 'Archived' AND OLD.record_status <> 'Archived') OR
                       (NEW.voyage_status IN ('Arrived','Cancelled') AND
                        NEW.voyage_status IS DISTINCT FROM OLD.voyage_status) THEN
                        IF EXISTS (SELECT 1 FROM shipment_voyage_assignments a
                            WHERE a.organization_id=OLD.organization_id AND
                                  a.voyage_id=OLD.id AND a.ends_at_utc IS NULL) THEN
                            RAISE EXCEPTION 'Close voyage allocations first' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF (NEW.planned_departure_at_utc IS DISTINCT FROM OLD.planned_departure_at_utc OR
                        NEW.planned_arrival_at_utc IS DISTINCT FROM OLD.planned_arrival_at_utc) AND
                        EXISTS (SELECT 1 FROM shipment_voyage_assignments a JOIN shipments s
                          ON s.organization_id=a.organization_id AND s.id=a.shipment_id
                          WHERE a.organization_id=OLD.organization_id AND a.voyage_id=OLD.id
                            AND a.ends_at_utc IS NULL
                            AND s.planned_departure_at_utc IS NOT NULL
                            AND s.planned_arrival_at_utc IS NOT NULL
                            AND NEW.planned_departure_at_utc IS NOT NULL
                            AND NEW.planned_arrival_at_utc IS NOT NULL
                            AND (s.planned_arrival_at_utc <= NEW.planned_departure_at_utc OR
                                 NEW.planned_arrival_at_utc <= s.planned_departure_at_utc)) THEN
                        RAISE EXCEPTION 'Voyage planning conflicts with allocation' USING ERRCODE='23514';
                    END IF;
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m6_guard_shipment BEFORE UPDATE OR DELETE ON shipments
                FOR EACH ROW EXECUTE FUNCTION m6_guard_parent();
            CREATE TRIGGER m6_guard_container BEFORE UPDATE OR DELETE ON containers
                FOR EACH ROW EXECUTE FUNCTION m6_guard_parent();
            CREATE TRIGGER m6_guard_voyage BEFORE UPDATE OR DELETE ON voyages
                FOR EACH ROW EXECUTE FUNCTION m6_guard_parent();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER m6_guard_shipment ON shipments;
            DROP TRIGGER m6_guard_container ON containers;
            DROP TRIGGER m6_guard_voyage ON voyages;
            DROP FUNCTION m6_guard_parent();
            DROP TABLE shipment_voyage_assignments;
            DROP TABLE shipment_container_assignments;
            DROP FUNCTION m6_validate_assignment();
            DROP INDEX "AK_containers_OrganizationId_Id";
            DROP INDEX "AK_voyages_OrganizationId_Id";
            -- Do not drop shared btree_gist; other schemas may depend on it.
            """);
    }
}
