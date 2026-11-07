using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261107120000_M10CorrectionAuthority")]
public sealed class M10CorrectionAuthority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;

            -- A dedicated verifier secret is provisioned by the migration identity.
            -- Ordinary runtime identities must not read this table.
            CREATE TABLE m10_correction_proof_keys (
                key_id smallint PRIMARY KEY CHECK (key_id = 1),
                secret bytea NOT NULL CHECK (octet_length(secret) = 32)
            );
            CREATE TABLE m10_correction_proof_nonces (
                nonce uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                actor_user_id uuid NOT NULL,
                idempotency_key uuid NOT NULL,
                request_digest char(64) NOT NULL,
                transaction_id xid8 NOT NULL DEFAULT pg_current_xact_id(),
                expires_at_utc timestamptz NOT NULL
            );
            CREATE TRIGGER tr_m10_immutable_proof_nonces
                BEFORE UPDATE OR DELETE ON m10_correction_proof_nonces
                FOR EACH ROW EXECUTE FUNCTION m10_append_only();

            CREATE FUNCTION m10_apply_signed_correction(
                p_org uuid, p_shipment uuid, p_target uuid, p_replacement uuid,
                p_generation bigint, p_actor uuid, p_session uuid, p_reason varchar,
                p_correlation varchar, p_digest varchar, p_idempotency uuid,
                p_nonce uuid, p_expires_epoch bigint, p_signature varchar)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER
                SET search_path = pg_catalog, public AS $signed$
            DECLARE
                signing_key bytea;
                actual_digest text;
                expected_signature text;
                message text;
                active_manager boolean;
                correction_id uuid := gen_random_uuid();
                now_epoch bigint := floor(extract(epoch FROM clock_timestamp()))::bigint;
            BEGIN
                -- A valid user/session UUID alone is not proof of request authority.
                IF session_user <> 'harborops_api'
                   OR p_actor IS NULL OR p_session IS NULL
                   OR p_idempotency IS NULL OR p_nonce IS NULL
                   OR p_generation < 1
                   OR p_expires_epoch < now_epoch
                   OR p_expires_epoch > now_epoch + 60 THEN
                    RAISE EXCEPTION 'Correction authority denied' USING ERRCODE='42501';
                END IF;

                SELECT secret INTO signing_key FROM public.m10_correction_proof_keys
                WHERE key_id=1 FOR SHARE;
                IF signing_key IS NULL THEN
                    RAISE EXCEPTION 'Correction authority not provisioned' USING ERRCODE='PZ002';
                END IF;

                actual_digest := encode(digest(convert_to(
                    'Correction|' || replace(p_org::text,'-','') || '|' ||
                    replace(p_shipment::text,'-','') || '|' ||
                    replace(p_actor::text,'-','') || '|' ||
                    replace(p_target::text,'-','') || '|' ||
                    COALESCE(replace(p_replacement::text,'-',''),'') || '|' ||
                    (p_generation - 1)::text || '|' || p_reason,
                    'UTF8'), 'sha256'), 'hex');

                message := 'm10-correction-v1|' || replace(p_nonce::text,'-','') ||
                    '|' || p_expires_epoch::text ||
                    '|' || replace(p_idempotency::text,'-','') ||
                    '|' || replace(p_session::text,'-','') ||
                    '|' || actual_digest;
                expected_signature := encode(hmac(convert_to(message,'UTF8'),
                    signing_key,'sha256'), 'hex');

                IF p_digest IS DISTINCT FROM actual_digest
                    OR p_signature IS DISTINCT FROM expected_signature THEN
                    RAISE EXCEPTION 'Correction authority denied' USING ERRCODE='42501';
                END IF;

                -- Lock these rows to serialize with role downgrade, session revocation
                -- and user deactivation. The caller cannot substitute a different actor.
                SELECT EXISTS (
                    SELECT 1 FROM public.auth_sessions s
                    JOIN public.users u ON u.id=s.user_id
                    JOIN public.organization_memberships m
                      ON m.user_id=s.user_id
                     AND m.organization_id=s.selected_organization_id
                    WHERE s.id=p_session AND s.user_id=p_actor
                      AND s.selected_organization_id=p_org
                      AND s.revoked_at_utc IS NULL
                      AND s.idle_expires_at_utc > clock_timestamp()
                      AND s.absolute_expires_at_utc > clock_timestamp()
                      AND u.status='Active' AND u.security_version=s.security_version
                      AND m.status='Active' AND m.role='Manager'
                    FOR SHARE OF s,u,m
                ) INTO active_manager;
                IF NOT active_manager THEN
                    RAISE EXCEPTION 'Correction authority denied' USING ERRCODE='42501';
                END IF;

                INSERT INTO public.m10_correction_proof_nonces
                    (nonce,organization_id,shipment_id,actor_user_id,
                     idempotency_key,request_digest,expires_at_utc)
                VALUES (p_nonce,p_org,p_shipment,p_actor,p_idempotency,
                        actual_digest,to_timestamp(p_expires_epoch));

                -- Existing M10 triggers validate correction scope, chain, generation
                -- and immutable input revisions in this same transaction.
                INSERT INTO public.shipment_tracking_corrections
                    (id,organization_id,shipment_id,target_receipt_id,
                     replacement_receipt_id,generation,actor_user_id,reason,
                     correlation_id,correction_digest)
                VALUES (correction_id,p_org,p_shipment,p_target,p_replacement,
                        p_generation,p_actor,p_reason,p_correlation,actual_digest);

                -- Required central audit evidence is atomic with the correction.
                -- Never expose correction reason, request body or proof in metadata.
                BEGIN
                    INSERT INTO public.audit_entries
                        (organization_id,event_type,event_schema_version,outcome,
                         actor_kind,actor_user_id,actor_session_id,actor_role,
                         resource_type,resource_id,correlation_id,metadata)
                    VALUES (p_org,'shipment.tracking_correction_created',1,'Succeeded',
                            'Human',p_actor,p_session,'Manager',
                            'shipment_tracking_corrections',correction_id,p_correlation,
                            '{"operation":"insert","changedFields":["correction"]}'::jsonb);
                EXCEPTION WHEN OTHERS THEN
                    RAISE EXCEPTION 'Required correction audit is unavailable'
                        USING ERRCODE='PZ001';
                END;
            END $signed$;

            REVOKE ALL ON FUNCTION m10_apply_signed_correction(
                uuid,uuid,uuid,uuid,bigint,uuid,uuid,varchar,varchar,varchar,
                uuid,uuid,bigint,varchar) FROM PUBLIC;

            -- A forged replay acknowledgment must not imply that a correction
            -- committed. Require evidence from the same authorized transaction.
            CREATE FUNCTION m10_guard_correction_replay() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $replay$
            BEGIN
                IF session_user = 'harborops_api'
                   AND NEW.operation_kind = 'Correction'
                   AND NOT EXISTS (
                       SELECT 1 FROM public.m10_correction_proof_nonces n
                       JOIN public.shipment_tracking_corrections c
                         ON c.organization_id=n.organization_id
                        AND c.shipment_id=n.shipment_id
                        AND c.actor_user_id=n.actor_user_id
                        AND c.correction_digest=n.request_digest
                       WHERE n.organization_id=NEW.organization_id
                         AND n.shipment_id=NEW.shipment_id
                         AND n.actor_user_id=NEW.actor_user_id
                         AND n.idempotency_key=NEW.idempotency_key
                         AND n.request_digest=NEW.request_digest
                         AND n.transaction_id=pg_current_xact_id()
                   ) THEN
                    RAISE EXCEPTION 'Correction replay authority denied'
                        USING ERRCODE='42501';
                END IF;
                RETURN NEW;
            END $replay$;
            REVOKE ALL ON FUNCTION m10_guard_correction_replay() FROM PUBLIC;
            CREATE TRIGGER tr_m10_guard_correction_replay
                BEFORE INSERT ON shipment_tracking_operation_replays
                FOR EACH ROW EXECUTE FUNCTION m10_guard_correction_replay();
            REVOKE ALL ON TABLE m10_correction_proof_keys,
                m10_correction_proof_nonces FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
                IF EXISTS(SELECT 1 FROM m10_correction_proof_nonces LIMIT 1) THEN
                    RAISE EXCEPTION 'Retained correction authority evidence prevents downgrade'
                        USING ERRCODE='23514';
                END IF;
            END $guard$;
            DROP TRIGGER tr_m10_guard_correction_replay
                ON shipment_tracking_operation_replays;
            DROP FUNCTION m10_guard_correction_replay();
            DROP FUNCTION m10_apply_signed_correction(
                uuid,uuid,uuid,uuid,bigint,uuid,uuid,varchar,varchar,varchar,
                uuid,uuid,bigint,varchar);
            DROP TABLE m10_correction_proof_nonces;
            DROP TABLE m10_correction_proof_keys;
            """);
    }
}
