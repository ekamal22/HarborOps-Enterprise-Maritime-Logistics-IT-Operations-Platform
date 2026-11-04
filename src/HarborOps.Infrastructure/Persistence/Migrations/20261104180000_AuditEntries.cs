using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261104180000_AuditEntries")]
public sealed class AuditEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE audit_entries (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NULL,
                event_type varchar(96) NOT NULL,
                event_schema_version integer NOT NULL DEFAULT 1,
                outcome varchar(16) NOT NULL,
                actor_kind varchar(24) NOT NULL,
                actor_user_id uuid NULL,
                actor_session_id uuid NULL,
                actor_source_id uuid NULL,
                actor_role varchar(24) NULL,
                resource_type varchar(64) NOT NULL,
                resource_id uuid NULL,
                correlation_id varchar(64) NOT NULL,
                occurred_at_utc timestamptz NOT NULL DEFAULT statement_timestamp(),
                metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
                CONSTRAINT "CK_m16_event_type" CHECK
                    (event_type ~ '^[a-z0-9_.]{1,96}$'),
                CONSTRAINT "CK_m16_event_version" CHECK
                    (event_schema_version = 1),
                CONSTRAINT "CK_m16_event_outcome" CHECK
                    (outcome IN ('Succeeded','Denied','Failed')),
                CONSTRAINT "CK_m16_actor_kind" CHECK
                    (actor_kind IN ('Human','OperatorCli','IntegrationSource','System')),
                CONSTRAINT "CK_m16_resource_type" CHECK
                    (resource_type ~ '^[a-z0-9_]{1,64}$'),
                CONSTRAINT "CK_m16_correlation" CHECK
                    (length(correlation_id) BETWEEN 1 AND 64),
                CONSTRAINT "CK_m16_metadata" CHECK
                    (jsonb_typeof(metadata) = 'object' AND pg_column_size(metadata) <= 1024)
            );
            -- Audit tenant identifiers are immutable historical facts, not a live
            -- row relationship. A foreign key here acquired FOR KEY SHARE on
            -- organizations for every audited mutation and violated the
            -- established organization-first lock order.
            CREATE INDEX "IX_m16_audit_scope_time"
                ON audit_entries(organization_id, occurred_at_utc DESC, id DESC);
            CREATE INDEX "IX_m16_audit_resource"
                ON audit_entries(organization_id, resource_type, resource_id, occurred_at_utc DESC);

            CREATE FUNCTION m16_audit_guard() RETURNS trigger
                LANGUAGE plpgsql AS $audit$
            BEGIN
                RAISE EXCEPTION 'Audit entries are immutable'
                    USING ERRCODE = '23514';
            END $audit$;
            CREATE TRIGGER m16_audit_no_change
                BEFORE UPDATE OR DELETE ON audit_entries
                FOR EACH ROW EXECUTE FUNCTION m16_audit_guard();
            CREATE TRIGGER m16_audit_no_truncate
                BEFORE TRUNCATE ON audit_entries
                FOR EACH STATEMENT EXECUTE FUNCTION m16_audit_guard();

            -- This capture runs inside the business transaction. Any insert
            -- failure aborts that mutation, including ExecuteUpdate and raw SQL.
            -- Only a fixed event code and operation type are stored, never row values.
            CREATE FUNCTION m16_audit_capture() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER
                SET search_path = pg_catalog, public AS $audit$
            DECLARE
                row_data jsonb;
                audit_org uuid;
                audit_resource uuid;
                audit_actor uuid;
                audit_session uuid;
                audit_type varchar(96);
                audit_kind varchar(24);
                audit_corr varchar(64);
                audit_role varchar(24);
                operation_code text;
                safe_fields text[];
                changed_fields text[] := ARRAY[]::text[];
            BEGIN
                IF TG_OP = 'UPDATE' AND to_jsonb(OLD) = to_jsonb(NEW) THEN
                    RETURN NEW;
                END IF;
                row_data := CASE WHEN TG_OP = 'DELETE'
                    THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
                operation_code := lower(TG_OP);
                -- Only names on this fixed allowlist can enter audit metadata.
                -- No column values, credentials or unrestricted payloads are stored.
                safe_fields := CASE TG_TABLE_NAME
                    WHEN 'users' THEN ARRAY['status','security_version']
                    WHEN 'user_credentials' THEN ARRAY['status']
                    WHEN 'organizations' THEN ARRAY['display_name','status']
                    WHEN 'organization_memberships' THEN ARRAY['role','status']
                    WHEN 'ports' THEN ARRAY['name','code','status']
                    WHEN 'vessels' THEN ARRAY['name','status']
                    WHEN 'voyages' THEN ARRAY['status','departure_at_utc','arrival_at_utc']
                    WHEN 'containers' THEN ARRAY['status']
                    WHEN 'shipments' THEN ARRAY['status']
                    WHEN 'shipment_container_assignments' THEN ARRAY['status']
                    WHEN 'shipment_voyage_assignments' THEN ARRAY['status']
                    WHEN 'integration_sources' THEN ARRAY['status','display_name','source_code']
                    WHEN 'integration_credentials' THEN ARRAY['status','revoked_at_utc']
                    ELSE ARRAY[]::text[]
                END;
                IF TG_OP = 'UPDATE' THEN
                    SELECT COALESCE(array_agg(field_name ORDER BY field_name), ARRAY[]::text[])
                      INTO changed_fields
                      FROM unnest(safe_fields) AS allowed(field_name)
                     WHERE to_jsonb(OLD)->field_name IS DISTINCT FROM to_jsonb(NEW)->field_name;
                END IF;
                audit_org := CASE WHEN TG_TABLE_NAME = 'organizations'
                    THEN NULLIF(row_data->>'id', '')::uuid
                    ELSE NULLIF(row_data->>'organization_id', '')::uuid END;
                audit_resource := COALESCE(
                    NULLIF(row_data->>'id', '')::uuid,
                    NULLIF(row_data->>'user_id', '')::uuid);
                audit_actor := NULLIF(current_setting('harborops.audit_actor_user_id', true), '')::uuid;
                audit_session := NULLIF(current_setting('harborops.audit_actor_session_id', true), '')::uuid;
                audit_corr := left(COALESCE(
                    NULLIF(current_setting('harborops.audit_correlation_id', true), ''),
                    CASE WHEN TG_TABLE_NAME = 'auth_events'
                        THEN NULLIF(row_data->>'correlation_id', '') ELSE NULL END,
                    'database'), 64);
                audit_role := NULLIF(left(current_setting('harborops.audit_actor_role', true), 24), '');
                audit_kind := COALESCE(
                    NULLIF(current_setting('harborops.audit_actor_kind', true), ''),
                    CASE WHEN TG_TABLE_NAME = 'tracking_accepted_events'
                        THEN 'IntegrationSource'
                        WHEN TG_TABLE_NAME IN ('integration_sources','integration_credentials')
                        THEN 'OperatorCli'
                        WHEN TG_TABLE_NAME = 'auth_events'
                             AND row_data->>'user_id' IS NOT NULL THEN 'Human'
                        ELSE 'System' END);
                IF TG_TABLE_NAME = 'auth_events' THEN
                    audit_type := left('auth.' || COALESCE(row_data->>'event_type','unknown'), 96);
                    audit_actor := COALESCE(audit_actor, NULLIF(row_data->>'user_id','')::uuid);
                    audit_session := COALESCE(audit_session, NULLIF(row_data->>'session_id','')::uuid);
                ELSIF TG_TABLE_NAME = 'tracking_accepted_events' THEN
                    audit_type := 'integration.tracking_accepted';
                ELSIF TG_TABLE_NAME = 'organization_memberships' AND TG_OP = 'UPDATE' THEN
                    audit_type := CASE
                        WHEN 'role' = ANY(changed_fields) THEN 'membership.role_changed'
                        WHEN 'status' = ANY(changed_fields) THEN 'membership.status_changed'
                        ELSE 'membership.updated' END;
                ELSIF TG_TABLE_NAME = 'integration_sources' AND TG_OP = 'UPDATE'
                    AND 'status' = ANY(changed_fields) THEN
                    audit_type := 'integration.source_status_changed';
                ELSIF TG_TABLE_NAME = 'integration_credentials' AND TG_OP = 'UPDATE'
                    AND 'status' = ANY(changed_fields) THEN
                    audit_type := 'integration.credential_status_changed';
                ELSE
                    audit_type := left(TG_TABLE_NAME || '.' || operation_code, 96);
                END IF;

                -- Explicitly target the audited table's own schema. Isolated
                -- migration rehearsals use disposable schemas before public.
                EXECUTE format(
                    'INSERT INTO %I.audit_entries
                     (id, organization_id, event_type, event_schema_version, outcome,
                      actor_kind, actor_user_id, actor_session_id, actor_source_id,
                      actor_role, resource_type, resource_id, correlation_id,
                      occurred_at_utc, metadata)
                     VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)',
                    TG_TABLE_SCHEMA)
                USING gen_random_uuid(), audit_org, audit_type, 1,
                    CASE WHEN TG_TABLE_NAME = 'auth_events'
                              AND row_data->>'result_code' NOT IN ('success','succeeded','ok')
                         THEN 'Failed' ELSE 'Succeeded' END,
                    audit_kind, audit_actor, audit_session,
                    CASE WHEN TG_TABLE_NAME IN ('integration_sources','integration_credentials',
                                                  'tracking_accepted_events')
                         THEN NULLIF(row_data->>'source_id','')::uuid ELSE NULL END,
                    audit_role, TG_TABLE_NAME, audit_resource, audit_corr,
                    statement_timestamp(), jsonb_build_object(
                        'operation', operation_code, 'changedFields', changed_fields);
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END $audit$;

            -- Only tables whose state changes matter to audit are captured.
            -- Session sliding timestamps and tracking receipts are excluded
            -- because M1/M8 already own those high-frequency histories.
            CREATE TRIGGER m16_users_audit AFTER INSERT OR UPDATE OR DELETE ON users
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_credentials_audit AFTER INSERT OR UPDATE OR DELETE ON user_credentials
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_auth_events_audit AFTER INSERT ON auth_events
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_organizations_audit AFTER INSERT OR UPDATE OR DELETE ON organizations
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_memberships_audit AFTER INSERT OR UPDATE OR DELETE ON organization_memberships
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_ports_audit AFTER INSERT OR UPDATE OR DELETE ON ports
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_vessels_audit AFTER INSERT OR UPDATE OR DELETE ON vessels
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_voyages_audit AFTER INSERT OR UPDATE OR DELETE ON voyages
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_containers_audit AFTER INSERT OR UPDATE OR DELETE ON containers
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_shipments_audit AFTER INSERT OR UPDATE OR DELETE ON shipments
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_status_audit AFTER INSERT ON shipment_status_changes
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_container_assignments_audit AFTER INSERT OR UPDATE OR DELETE ON shipment_container_assignments
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_voyage_assignments_audit AFTER INSERT OR UPDATE OR DELETE ON shipment_voyage_assignments
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_sources_audit AFTER INSERT OR UPDATE OR DELETE ON integration_sources
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m16_integration_credentials_audit AFTER INSERT OR UPDATE OR DELETE ON integration_credentials
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();

            -- Source identity is validated by M8 before an accepted event is
            -- persisted. Capture only the source identifier and a fixed outcome,
            -- never the raw or canonical tracking payload.
            CREATE TRIGGER m16_tracking_accepted_audit AFTER INSERT ON tracking_accepted_events
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();

            CREATE FUNCTION m16_record_audit_access(
                p_org uuid, p_kind varchar, p_actor uuid, p_session uuid,
                p_resource uuid, p_correlation varchar)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER
                SET search_path = pg_catalog, public AS $audit$
            BEGIN
                IF p_kind NOT IN ('audit.read','audit.read_denied')
                   OR p_actor IS NULL OR p_session IS NULL
                   OR p_org IS NULL OR p_correlation IS NULL
                   OR length(p_correlation) NOT BETWEEN 1 AND 64 THEN
                    RAISE EXCEPTION 'Invalid audit access event' USING ERRCODE = '22023';
                END IF;
                -- The write path must not claim a permitted audit read after
                -- membership/session revocation or a cross-tenant scope change.
                IF p_kind = 'audit.read' THEN
                    PERFORM 1
                      FROM public.auth_sessions s
                      JOIN public.users u ON u.id=s.user_id
                      JOIN public.organizations o ON o.id=s.selected_organization_id
                      JOIN public.organization_memberships m
                        ON m.organization_id=o.id AND m.user_id=u.id
                     WHERE s.id=p_session AND s.user_id=p_actor
                       AND s.selected_organization_id=p_org
                       AND s.revoked_at_utc IS NULL
                       AND s.idle_expires_at_utc > now()
                       AND s.absolute_expires_at_utc > now()
                       AND s.security_version=u.security_version
                       AND u.status='Active' AND o.status='Active'
                       AND m.status='Active' AND m.role='Auditor'
                     FOR SHARE OF s,u,o,m;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Audit read authorization revoked'
                            USING ERRCODE='42501';
                    END IF;
                ELSIF NOT EXISTS (
                    SELECT 1 FROM public.auth_sessions s
                     WHERE s.id=p_session AND s.user_id=p_actor
                       AND s.selected_organization_id=p_org
                ) THEN
                    -- A denied request is never made successful by its audit path.
                    RETURN;
                END IF;
                INSERT INTO public.audit_entries (
                    organization_id, event_type, event_schema_version, outcome,
                    actor_kind, actor_user_id, actor_session_id, actor_role,
                    resource_type, resource_id, correlation_id, metadata
                ) VALUES (
                    p_org, p_kind, 1,
                    CASE WHEN p_kind='audit.read' THEN 'Succeeded' ELSE 'Denied' END,
                    'Human', p_actor, p_session,
                    CASE WHEN p_kind='audit.read' THEN 'Auditor' ELSE NULL END,
                    'audit_entries', p_resource, p_correlation, '{}'::jsonb
                );
            END $audit$;
            REVOKE ALL ON FUNCTION m16_audit_capture() FROM PUBLIC;
            REVOKE ALL ON FUNCTION m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar)
                FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $downgrade$
            BEGIN
                IF EXISTS(SELECT 1 FROM audit_entries LIMIT 1) THEN
                    RAISE EXCEPTION 'Cannot downgrade M16 with retained audit evidence'
                        USING ERRCODE='23514';
                END IF;
            END $downgrade$;
            DROP FUNCTION m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar);
            DROP TRIGGER m16_users_audit ON users;
            DROP TRIGGER m16_credentials_audit ON user_credentials;
            DROP TRIGGER m16_auth_events_audit ON auth_events;
            DROP TRIGGER m16_organizations_audit ON organizations;
            DROP TRIGGER m16_memberships_audit ON organization_memberships;
            DROP TRIGGER m16_ports_audit ON ports;
            DROP TRIGGER m16_vessels_audit ON vessels;
            DROP TRIGGER m16_voyages_audit ON voyages;
            DROP TRIGGER m16_containers_audit ON containers;
            DROP TRIGGER m16_shipments_audit ON shipments;
            DROP TRIGGER m16_status_audit ON shipment_status_changes;
            DROP TRIGGER m16_container_assignments_audit ON shipment_container_assignments;
            DROP TRIGGER m16_voyage_assignments_audit ON shipment_voyage_assignments;
            DROP TRIGGER m16_sources_audit ON integration_sources;
            DROP TRIGGER m16_integration_credentials_audit ON integration_credentials;
            DROP TRIGGER m16_tracking_accepted_audit ON tracking_accepted_events;
            DROP FUNCTION m16_audit_capture();
            DROP TABLE audit_entries;
            DROP FUNCTION m16_audit_guard();
            """);
    }
}
