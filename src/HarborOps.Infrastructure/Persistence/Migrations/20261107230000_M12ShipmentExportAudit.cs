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
            CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;

            -- Only the migration account can provision this key. The API login
            -- never receives direct SELECT on the verifier material.
            CREATE TABLE public.m12_export_proof_keys (
                key_id smallint PRIMARY KEY CHECK (key_id = 1),
                secret bytea NOT NULL CHECK (octet_length(secret) = 32)
            );
            REVOKE ALL ON TABLE public.m12_export_proof_keys FROM PUBLIC;

            -- A proof is issued once per HTTP request. A repeated signed SQL
            -- call with the same correlation cannot forge a second export audit.
            CREATE UNIQUE INDEX uq_m12_export_prepared_correlation
                ON public.audit_entries (correlation_id)
                WHERE event_type = 'shipment.export_prepared';

            CREATE FUNCTION public.m12_record_shipment_export(
                p_org uuid, p_user uuid, p_session uuid, p_rows integer,
                p_bytes bigint, p_filter_digest varchar, p_correlation varchar,
                p_expires_epoch bigint, p_signature varchar)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $m12$
            DECLARE
                v_security_version bigint;
                signing_key bytea;
                signed_message text;
                expected_signature text;
                current_epoch bigint;
            BEGIN
                IF session_user <> 'harborops_api' THEN
                    RAISE EXCEPTION 'Export audit requires API runtime role'
                        USING ERRCODE = '42501';
                END IF;
                IF p_org IS NULL OR p_user IS NULL OR p_session IS NULL
                    OR p_rows IS NULL OR p_bytes IS NULL
                    OR p_filter_digest IS NULL OR p_correlation IS NULL
                    OR p_expires_epoch IS NULL OR p_signature IS NULL
                    OR p_org = '00000000-0000-0000-0000-000000000000'::uuid
                    OR p_user = '00000000-0000-0000-0000-000000000000'::uuid
                    OR p_session = '00000000-0000-0000-0000-000000000000'::uuid
                    OR p_rows < 0 OR p_rows > 5000 OR p_bytes < 1 OR p_bytes > 10485760
                    OR p_filter_digest !~ '^[0-9A-F]{64}$'
                    OR p_correlation !~ '^[0-9a-f]{32}$'
                    OR p_signature !~ '^[0-9a-f]{64}$' THEN
                    RAISE EXCEPTION 'Invalid export audit metadata'
                        USING ERRCODE = '22023';
                END IF;
                current_epoch := floor(extract(epoch FROM clock_timestamp()))::bigint;
                IF p_expires_epoch < current_epoch
                    OR p_expires_epoch > current_epoch + 45 THEN
                    RAISE EXCEPTION 'Export authorization expired'
                        USING ERRCODE = '42501';
                END IF;

                SELECT secret INTO signing_key
                    FROM public.m12_export_proof_keys WHERE key_id=1 FOR SHARE;
                IF signing_key IS NULL THEN
                    RAISE EXCEPTION 'Export authority unavailable'
                        USING ERRCODE = '42501';
                END IF;
                signed_message := 'm12-export-v1|' || replace(p_org::text,'-','') ||
                    '|' || replace(p_user::text,'-','') ||
                    '|' || replace(p_session::text,'-','') ||
                    '|' || p_rows::text ||
                    '|' || p_bytes::text ||
                    '|' || p_filter_digest ||
                    '|' || p_correlation ||
                    '|' || p_expires_epoch::text;
                expected_signature := encode(public.hmac(
                    convert_to(signed_message,'UTF8'), signing_key, 'sha256'), 'hex');
                IF p_signature IS DISTINCT FROM expected_signature THEN
                    RAISE EXCEPTION 'Invalid export authorization'
                        USING ERRCODE = '42501';
                END IF;

                PERFORM 1 FROM public.organizations o WHERE o.id=p_org
                    AND o.status='Active' FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Organization unavailable' USING ERRCODE='42501';
                END IF;
                PERFORM 1 FROM public.auth_sessions s WHERE s.id=p_session
                    AND s.user_id=p_user AND s.selected_organization_id=p_org
                    AND s.revoked_at_utc IS NULL
                    AND s.idle_expires_at_utc > clock_timestamp()
                    AND s.absolute_expires_at_utc > clock_timestamp() FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Session unavailable' USING ERRCODE='42501';
                END IF;
                SELECT u.security_version INTO v_security_version
                    FROM public.users u WHERE u.id=p_user AND u.status='Active'
                    FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'User unavailable' USING ERRCODE='42501';
                END IF;
                PERFORM 1 FROM public.auth_sessions s WHERE s.id=p_session
                    AND s.security_version=v_security_version;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Session version unavailable' USING ERRCODE='42501';
                END IF;
                PERFORM 1 FROM public.organization_memberships m
                    WHERE m.organization_id=p_org AND m.user_id=p_user
                    AND m.status='Active' AND m.role IN ('Analyst','Manager') FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Export permission unavailable' USING ERRCODE='42501';
                END IF;

                -- This transaction commits before HTTP transfer. The audit records
                -- successful preparation and authorization, not a client receipt.
                INSERT INTO public.audit_entries (
                    organization_id,event_type,event_schema_version,outcome,
                    actor_kind,actor_user_id,actor_session_id,actor_role,
                    resource_type,resource_id,correlation_id,metadata)
                SELECT p_org,'shipment.export_prepared',1,'Succeeded',
                    'Human',p_user,p_session,m.role,
                    'shipments',NULL,p_correlation,
                    jsonb_build_object('rowCount',p_rows,'byteCount',p_bytes,
                        'filterDigest',p_filter_digest,'exportSchema','m12-v1')
                FROM public.organization_memberships m
                WHERE m.organization_id=p_org AND m.user_id=p_user
                    AND m.status='Active' AND m.role IN ('Analyst','Manager');
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Required export audit unavailable' USING ERRCODE='42501';
                END IF;
            END $m12$;
            REVOKE ALL ON FUNCTION public.m12_record_shipment_export(
                uuid,uuid,uuid,integer,bigint,varchar,varchar,bigint,varchar)
                FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS public.m12_record_shipment_export(
                uuid,uuid,uuid,integer,bigint,varchar,varchar,bigint,varchar);
            DROP INDEX IF EXISTS public.uq_m12_export_prepared_correlation;
            DROP TABLE IF EXISTS public.m12_export_proof_keys;
            """);
    }
}
