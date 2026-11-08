using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HarborOps.Infrastructure.Persistence.Migrations;

// SQL-owned reporting tables are intentionally not added to the EF model snapshot.
// API reporting uses parameterized SQL against tenant-scoped, row-secured storage.
[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261109020000_M13OperationalKpis")]
public sealed class M13OperationalKpis : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE operational_kpi_auditor_grants (
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                active boolean NOT NULL,
                version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
                changed_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                changed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                PRIMARY KEY (organization_id,user_id)
            );
            CREATE TABLE operational_kpi_reports (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                actor_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                actor_session_id uuid NOT NULL REFERENCES auth_sessions(id) ON DELETE RESTRICT,
                policy_version varchar(32) NOT NULL CHECK (policy_version='m13-kpi-v1'),
                filters_sha256 char(64) NOT NULL CHECK (filters_sha256 ~ '^[0-9a-f]{64}$'),
                from_utc timestamptz NOT NULL,
                before_utc timestamptz NOT NULL,
                evaluated_at_utc timestamptz NOT NULL,
                created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                expires_at_utc timestamptz NOT NULL,
                member_count integer NOT NULL DEFAULT 0 CHECK (member_count >= 0),
                CONSTRAINT ck_m13_report_window CHECK (before_utc>from_utc AND
                    before_utc<=from_utc+interval '366 days'),
                CONSTRAINT ck_m13_report_expiry CHECK (expires_at_utc>created_at_utc
                    AND expires_at_utc<=created_at_utc+interval '15 minutes'),
                CONSTRAINT uq_m13_report_org UNIQUE (id,organization_id)
            );
            CREATE INDEX ix_m13_report_expiry ON operational_kpi_reports
                (organization_id,expires_at_utc,id);
            CREATE INDEX ix_m13_report_owner ON operational_kpi_reports
                (organization_id,actor_user_id,actor_session_id,expires_at_utc);
            CREATE TABLE operational_kpi_members (
                organization_id uuid NOT NULL,
                report_id uuid NOT NULL,
                metric_code varchar(4) NOT NULL,
                dimension varchar(40) NOT NULL DEFAULT '',
                member_key uuid NOT NULL,
                shipment_id uuid NOT NULL,
                exception_id uuid,
                bucket_start_utc timestamptz,
                PRIMARY KEY (report_id,metric_code,dimension,member_key),
                CONSTRAINT fk_m13_member_report FOREIGN KEY (report_id,organization_id)
                    REFERENCES operational_kpi_reports(id,organization_id) ON DELETE CASCADE,
                CONSTRAINT fk_m13_member_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT fk_m13_member_exception FOREIGN KEY (exception_id,organization_id)
                    REFERENCES shipment_exceptions(id,organization_id) ON DELETE RESTRICT,
                CONSTRAINT ck_m13_metric CHECK (metric_code IN (
                    'K01','K02','K03','K04','K05','K06','K07','K08','K09','K10',
                    'K11','K12','K13','K14','D01','D05','D13')),
                CONSTRAINT ck_m13_member_dimension CHECK (
                    (metric_code='K02' AND dimension IN
                        ('Draft','Booked','InTransit','Arrived','Completed','Cancelled'))
                    OR (metric_code='K12' AND dimension IN
                        ('EquipmentConflict','RecordedArrivalLate','ArrivalScheduleOverdue','DepartureSlippageRisk'))
                    OR (metric_code NOT IN ('K02','K12') AND dimension='')),
                CONSTRAINT ck_m13_bucket CHECK ((metric_code='K12' AND bucket_start_utc IS NOT NULL)
                    OR (metric_code<>'K12' AND bucket_start_utc IS NULL)),
                CONSTRAINT ck_m13_exception CHECK (
                    (metric_code IN ('K11','K12') AND exception_id=member_key)
                    OR (metric_code NOT IN ('K11','K12') AND exception_id IS NULL
                        AND member_key=shipment_id))
            );
            CREATE INDEX ix_m13_member_page ON operational_kpi_members
                (organization_id,report_id,metric_code,dimension,bucket_start_utc,member_key);

            -- The M11 signed scope is bound to one live, selected session and tenant.
            -- The reporting layer checks that scope again and imposes a stricter
            -- business permission, never broadening M11 exception SELECT rights.
            CREATE FUNCTION m13_scoped_role(p_org uuid)
            RETURNS varchar LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $role$
            DECLARE fields text[]; role_name varchar;
            BEGIN
                IF session_user<>'harborops_api' OR NOT m11_read_allowed(p_org) THEN
                    RETURN NULL;
                END IF;
                fields:=string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                SELECT m.role INTO role_name FROM organization_memberships m
                    WHERE m.organization_id=p_org AND m.user_id=fields[2]::uuid
                    AND m.status='Active';
                RETURN role_name;
            EXCEPTION WHEN OTHERS THEN RETURN NULL;
            END $role$;
            CREATE FUNCTION m13_scope_matches(p_org uuid,p_actor uuid,p_session uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $scope$
            DECLARE fields text[];
            BEGIN
                IF NOT m11_read_allowed(p_org) THEN RETURN false; END IF;
                fields:=string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                RETURN fields[1] IS NOT DISTINCT FROM p_org::text
                    AND fields[2] IS NOT DISTINCT FROM p_actor::text
                    AND fields[3] IS NOT DISTINCT FROM p_session::text;
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $scope$;
            CREATE FUNCTION m13_can_view(p_org uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $view$
            DECLARE role_name varchar; fields text[];
            BEGIN
                role_name:=m13_scoped_role(p_org);
                IF role_name IN ('Analyst','Manager') THEN RETURN true; END IF;
                IF role_name<>'Auditor' THEN RETURN false; END IF;
                fields:=string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                RETURN EXISTS (SELECT 1 FROM operational_kpi_auditor_grants g
                    WHERE g.organization_id=p_org AND g.user_id=fields[2]::uuid
                    AND g.active);
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $view$;
            CREATE FUNCTION m13_can_manage(p_org uuid)
            RETURNS boolean LANGUAGE sql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $manage$
                SELECT m13_scoped_role(p_org)='Administrator';
            $manage$;
            CREATE FUNCTION m13_member_access(p_org uuid,p_report uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $member$
            DECLARE fields text[];
            BEGIN
                IF NOT m13_can_view(p_org) THEN RETURN false; END IF;
                fields:=string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                RETURN EXISTS (SELECT 1 FROM operational_kpi_reports r
                    WHERE r.id=p_report AND r.organization_id=p_org
                        AND r.actor_user_id=fields[2]::uuid
                        AND r.actor_session_id=fields[3]::uuid
                        AND r.expires_at_utc>clock_timestamp());
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $member$;

            CREATE FUNCTION m13_set_auditor_grant(
                p_org uuid,p_actor uuid,p_session uuid,p_target uuid,p_enabled boolean)
            RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $grant$
            DECLARE new_version bigint;
            BEGIN
                IF session_user<>'harborops_api' OR p_enabled IS NULL
                    OR NOT m13_scope_matches(p_org,p_actor,p_session) THEN
                    RAISE EXCEPTION 'Invalid grant authority' USING ERRCODE='42501';
                END IF;
                -- Keep the same org, session, user, membership lock order as
                -- accepted M12 transactional checks.
                PERFORM 1 FROM organizations WHERE id=p_org AND status='Active' FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Organization unavailable' USING ERRCODE='42501'; END IF;
                PERFORM 1 FROM auth_sessions WHERE id=p_session AND user_id=p_actor
                    AND selected_organization_id=p_org AND revoked_at_utc IS NULL
                    AND idle_expires_at_utc>clock_timestamp()
                    AND absolute_expires_at_utc>clock_timestamp() FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Session unavailable' USING ERRCODE='42501'; END IF;
                PERFORM 1 FROM users WHERE id=p_actor AND status='Active' FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Actor unavailable' USING ERRCODE='42501'; END IF;
                PERFORM 1 FROM organization_memberships WHERE organization_id=p_org
                    AND user_id=p_actor AND status='Active' AND role='Administrator' FOR SHARE;
                IF NOT FOUND OR NOT m13_can_manage(p_org) THEN
                    RAISE EXCEPTION 'Administrator required' USING ERRCODE='42501';
                END IF;
                PERFORM 1 FROM organization_memberships WHERE organization_id=p_org
                    AND user_id=p_target AND (NOT p_enabled OR
                        (status='Active' AND role='Auditor')) FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Target not eligible' USING ERRCODE='42501'; END IF;
                INSERT INTO operational_kpi_auditor_grants
                    (organization_id,user_id,active,changed_by_user_id)
                VALUES (p_org,p_target,p_enabled,p_actor)
                ON CONFLICT (organization_id,user_id) DO UPDATE
                SET active=EXCLUDED.active,version=operational_kpi_auditor_grants.version+1,
                    changed_by_user_id=p_actor,changed_at_utc=clock_timestamp()
                RETURNING version INTO new_version;

                INSERT INTO audit_entries
                    (organization_id,event_type,event_schema_version,outcome,
                    actor_kind,actor_user_id,actor_session_id,actor_role,
                    resource_type,resource_id,correlation_id,metadata)
                VALUES (p_org,'analytics.grant_updated',1,'Succeeded','Human',
                    p_actor,p_session,'Administrator','operational_kpi_grants',p_target,
                    replace(gen_random_uuid()::text,'-',''),
                    jsonb_build_object('enabled',p_enabled));
                RETURN new_version;
            END $grant$;

            REVOKE ALL ON FUNCTION m13_scoped_role(uuid),
                m13_scope_matches(uuid,uuid,uuid),m13_can_view(uuid),
                m13_can_manage(uuid),m13_member_access(uuid,uuid),
                m13_set_auditor_grant(uuid,uuid,uuid,uuid,boolean) FROM PUBLIC;

            ALTER TABLE operational_kpi_auditor_grants ENABLE ROW LEVEL SECURITY;
            ALTER TABLE operational_kpi_reports ENABLE ROW LEVEL SECURITY;
            ALTER TABLE operational_kpi_members ENABLE ROW LEVEL SECURITY;
            CREATE POLICY m13_grants_select ON operational_kpi_auditor_grants
                FOR SELECT USING (m13_can_manage(organization_id));
            CREATE POLICY m13_report_select ON operational_kpi_reports
                FOR SELECT USING (m13_can_view(organization_id)
                    AND m13_scope_matches(organization_id,actor_user_id,actor_session_id));
            CREATE POLICY m13_report_insert ON operational_kpi_reports
                FOR INSERT WITH CHECK (m13_can_view(organization_id)
                    AND m13_scope_matches(organization_id,actor_user_id,actor_session_id));
            CREATE POLICY m13_report_delete ON operational_kpi_reports
                FOR DELETE USING (m13_can_view(organization_id)
                    AND m13_scope_matches(organization_id,actor_user_id,actor_session_id));
            CREATE POLICY m13_member_select ON operational_kpi_members
                FOR SELECT USING (m13_member_access(organization_id,report_id));
            CREATE POLICY m13_member_insert ON operational_kpi_members
                FOR INSERT WITH CHECK (m13_member_access(organization_id,report_id));
            CREATE POLICY m13_member_delete ON operational_kpi_members
                FOR DELETE USING (m13_member_access(organization_id,report_id));

            -- A populated-upgrade test can apply the migration under a dedicated
            -- schema. Definer lookup must use that exact schema, not request scope.
            DO $schema$
            DECLARE s text:=current_schema();
            BEGIN
                EXECUTE format('ALTER FUNCTION %1$I.m13_scoped_role(uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_scope_matches(uuid,uuid,uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_can_view(uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_can_manage(uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_member_access(uuid,uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_set_auditor_grant(
                    uuid,uuid,uuid,uuid,boolean)
                    SET search_path TO pg_catalog,%1$I,public',s);
            END $schema$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM operational_kpi_auditor_grants LIMIT 1)
                    OR EXISTS (SELECT 1 FROM operational_kpi_reports LIMIT 1) THEN
                    RAISE EXCEPTION 'M13 reporting or grant history exists; migration rollback refused'
                        USING ERRCODE='23514';
                END IF;
            END $guard$;
            DROP TABLE operational_kpi_members;
            DROP TABLE operational_kpi_reports;
            DROP TABLE operational_kpi_auditor_grants;
            DROP FUNCTION m13_set_auditor_grant(uuid,uuid,uuid,uuid,boolean);
            DROP FUNCTION m13_member_access(uuid,uuid);
            DROP FUNCTION m13_can_manage(uuid);
            DROP FUNCTION m13_can_view(uuid);
            DROP FUNCTION m13_scope_matches(uuid,uuid,uuid);
            DROP FUNCTION m13_scoped_role(uuid);
            """);
    }
}