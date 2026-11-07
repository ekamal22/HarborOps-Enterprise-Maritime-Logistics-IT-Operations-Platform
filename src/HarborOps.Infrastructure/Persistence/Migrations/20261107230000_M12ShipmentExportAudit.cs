using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261107230000_M12ShipmentExportAudit")]
public sealed class M12ShipmentExportAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION public.m12_record_shipment_export(
                p_org uuid, p_user uuid, p_session uuid, p_rows integer,
                p_bytes bigint, p_filter_digest varchar, p_correlation varchar)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $m12$
            DECLARE
                v_security_version bigint;
            BEGIN
                IF session_user <> 'harborops_api' THEN
                    RAISE EXCEPTION 'Export audit requires API runtime role';
                END IF;
                IF p_rows < 0 OR p_rows > 5000 OR p_bytes < 1 OR p_bytes > 10485760
                    OR p_filter_digest !~ '^[0-9A-F]{64}$'
                    OR length(p_correlation) NOT BETWEEN 1 AND 64 THEN
                    RAISE EXCEPTION 'Invalid export audit metadata';
                END IF;
                PERFORM 1 FROM public.organizations o WHERE o.id=p_org
                    AND o.status='Active' FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Organization unavailable'; END IF;
                PERFORM 1 FROM public.auth_sessions s WHERE s.id=p_session
                    AND s.user_id=p_user AND s.selected_organization_id=p_org
                    AND s.revoked_at_utc IS NULL AND s.idle_expires_at_utc > now()
                    AND s.absolute_expires_at_utc > now() FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Session unavailable'; END IF;
                SELECT u.security_version INTO v_security_version
                    FROM public.users u WHERE u.id=p_user AND u.status='Active'
                    FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'User unavailable'; END IF;
                PERFORM 1 FROM public.auth_sessions s WHERE s.id=p_session
                    AND s.security_version=v_security_version;
                IF NOT FOUND THEN RAISE EXCEPTION 'Session version unavailable'; END IF;
                PERFORM 1 FROM public.organization_memberships m
                    WHERE m.organization_id=p_org AND m.user_id=p_user
                    AND m.status='Active' AND m.role IN ('Analyst','Manager') FOR SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Export permission unavailable'; END IF;

                INSERT INTO public.audit_entries (
                    organization_id,event_type,event_schema_version,outcome,
                    actor_kind,actor_user_id,actor_session_id,actor_role,
                    resource_type,resource_id,correlation_id,metadata)
                SELECT p_org,'shipment.export_completed',1,'Succeeded',
                    'Human',p_user,p_session,m.role,
                    'shipments',NULL,p_correlation,
                    jsonb_build_object('rowCount',p_rows,'byteCount',p_bytes,
                        'filterDigest',p_filter_digest,'exportSchema','m12-v1')
                FROM public.organization_memberships m
                WHERE m.organization_id=p_org AND m.user_id=p_user
                    AND m.status='Active' AND m.role IN ('Analyst','Manager');
                IF NOT FOUND THEN RAISE EXCEPTION 'Required export audit unavailable'; END IF;
            END $m12$;
            REVOKE ALL ON FUNCTION public.m12_record_shipment_export(
                uuid,uuid,uuid,integer,bigint,varchar,varchar) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS public.m12_record_shipment_export(
                uuid,uuid,uuid,integer,bigint,varchar,varchar);
            """);
    }
}