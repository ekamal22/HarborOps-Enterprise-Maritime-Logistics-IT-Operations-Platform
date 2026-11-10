using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HarborOps.Infrastructure.Persistence.Migrations;

// Expired reporting snapshots are disposable, unlike immutable source history.
[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261110010000_M13TenantReportCapacity")]
public sealed class M13TenantReportCapacity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Counts span all signed reporting sessions without exposing their rows
            -- to a regular API SELECT. Caller authorization is mandatory.
            CREATE FUNCTION m13_tenant_quota(p_org uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $quota$
            DECLARE live_reports bigint; live_members bigint;
            BEGIN
                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN
                    RAISE EXCEPTION 'Reporting quota authority denied'
                        USING ERRCODE='42501';
                END IF;
                SELECT count(*) INTO live_reports FROM operational_kpi_reports r
                WHERE r.organization_id=p_org
                  AND r.expires_at_utc>clock_timestamp();
                IF live_reports>4 THEN RETURN false; END IF;
                SELECT count(*) INTO live_members
                FROM operational_kpi_members m
                JOIN operational_kpi_reports r
                  ON r.id=m.report_id AND r.organization_id=m.organization_id
                WHERE r.organization_id=p_org
                  AND r.expires_at_utc>clock_timestamp();
                RETURN live_members<=2000000;
            END $quota$;

            -- Fairness: one user may hold at most two live snapshots across all of
            -- that user's sessions, inside the four-per-tenant limit. The caller
            -- is the signed actor and cannot name another user.
            CREATE FUNCTION m13_user_quota(p_org uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $user_quota$
            DECLARE fields text[]; live_user bigint;
            BEGIN
                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN
                    RAISE EXCEPTION 'Reporting quota authority denied'
                        USING ERRCODE='42501';
                END IF;
                fields:=string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                SELECT count(*) INTO live_user FROM operational_kpi_reports r
                WHERE r.organization_id=p_org AND r.actor_user_id=fields[2]::uuid
                  AND r.expires_at_utc>clock_timestamp();
                RETURN live_user<=2;
            END $user_quota$;

            -- No signed user context is required for maintenance. This function
            -- is only available to the restricted runtime login and can remove
            -- expired M13 snapshots, never M11 histories or M15 notifications.
            -- SKIP LOCKED avoids waiting for any in-flight report reader.
            CREATE FUNCTION m13_prune_expired_idle(p_limit integer)
            RETURNS integer LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $idle$
            DECLARE n integer;
            BEGIN
                IF session_user<>'harborops_api'
                    OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 16 THEN
                    RAISE EXCEPTION 'Idle cleanup authority denied'
                        USING ERRCODE='42501';
                END IF;
                WITH victims AS (
                    SELECT id FROM operational_kpi_reports
                    WHERE expires_at_utc<=clock_timestamp()
                    ORDER BY expires_at_utc,id
                    LIMIT p_limit FOR UPDATE SKIP LOCKED
                )
                DELETE FROM operational_kpi_reports r USING victims v
                WHERE r.id=v.id;
                GET DIAGNOSTICS n=ROW_COUNT;
                RETURN n;
            END $idle$;

            REVOKE ALL ON FUNCTION m13_tenant_quota(uuid),m13_user_quota(uuid),
                m13_prune_expired_idle(integer) FROM PUBLIC;

            DO $schema$
            DECLARE s text:=current_schema();
            BEGIN
                EXECUTE format('ALTER FUNCTION %1$I.m13_tenant_quota(uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_user_quota(uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m13_prune_expired_idle(integer)
                    SET search_path TO pg_catalog,%1$I,public',s);
            END $schema$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION m13_prune_expired_idle(integer);
            DROP FUNCTION m13_user_quota(uuid);
            DROP FUNCTION m13_tenant_quota(uuid);
            """);
    }
}
