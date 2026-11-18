using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261118190000_M18IncidentLifecycle")]
public sealed class M18IncidentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE it_incidents (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL REFERENCES organizations(id),
                title varchar(160) NOT NULL,
                service_code varchar(64) NOT NULL,
                severity varchar(5) NOT NULL,
                state varchar(16) NOT NULL DEFAULT 'New',
                impact_code varchar(80) NOT NULL,
                detected_at_utc timestamptz NOT NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NOT NULL DEFAULT now(),
                created_by uuid NOT NULL REFERENCES users(id),
                assigned_to uuid NULL REFERENCES users(id),
                root_cause_code varchar(80) NULL,
                resolved_by uuid NULL REFERENCES users(id),
                closed_by uuid NULL REFERENCES users(id),
                resolved_at_utc timestamptz NULL,
                closed_at_utc timestamptz NULL,
                version bigint NOT NULL DEFAULT 1,
                recovery_verified boolean NOT NULL DEFAULT false,
                integrity_verified boolean NOT NULL DEFAULT false,
                actions_dispositioned boolean NOT NULL DEFAULT false,
                CONSTRAINT "CK_m18_state" CHECK (state IN
                    ('New','Triage','Investigating','Mitigated','Monitoring','Resolved','Closed')),
                CONSTRAINT "CK_m18_severity" CHECK (severity IN ('SEV-1','SEV-2','SEV-3','SEV-4')),
                CONSTRAINT "CK_m18_version" CHECK (version > 0),
                CONSTRAINT "CK_m18_service" CHECK (service_code ~ '^[a-z0-9_-]{1,64}$'),
                CONSTRAINT "CK_m18_impact" CHECK (impact_code ~ '^[a-z0-9_-]{1,80}
            );
            CREATE UNIQUE INDEX "UX_m18_incidents_org_id" ON it_incidents(organization_id,id);
            CREATE INDEX "IX_m18_incidents_list" ON it_incidents
                (organization_id,updated_at_utc DESC,id DESC);

            CREATE TABLE it_incident_events (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                sequence bigint NOT NULL,
                kind varchar(32) NOT NULL,
                from_state varchar(16) NULL,
                to_state varchar(16) NULL,
                reason_code varchar(80) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                related_id uuid NULL,
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                incident_version bigint NOT NULL,
                CONSTRAINT "FK_m18_event_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "UX_m18_event_sequence"
                    UNIQUE(organization_id,incident_id,sequence),
                CONSTRAINT "CK_m18_event_version" CHECK (sequence > 0 AND incident_version > 0)
            );
            CREATE INDEX "IX_m18_events_timeline" ON it_incident_events
                (organization_id,incident_id,sequence);

            CREATE TABLE it_incident_notes (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                body varchar(4096) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_note_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_note_body" CHECK(length(body)>0)
            );

            CREATE TABLE it_incident_actions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                title varchar(160) NOT NULL,
                state varchar(16) NOT NULL DEFAULT 'Proposed',
                owner_user_id uuid NOT NULL REFERENCES users(id),
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_action_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_action_state" CHECK(state IN
                    ('Proposed','InProgress','Verified','Cancelled')),
                CONSTRAINT "CK_m18_action_version" CHECK(version > 0)
            );

            CREATE TABLE it_incident_evidence (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                kind varchar(24) NOT NULL,
                service_code varchar(64) NOT NULL,
                observation_code varchar(80) NOT NULL,
                source_status varchar(16) NOT NULL,
                observed_at_utc timestamptz NOT NULL,
                captured_at_utc timestamptz NOT NULL DEFAULT now(),
                correlation_digest varchar(64) NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                CONSTRAINT "FK_m18_evidence_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_evidence_kind" CHECK(kind IN ('ManualObservation','M17Snapshot','RecoveryProbe')),
                CONSTRAINT "CK_m18_evidence_status" CHECK(source_status IN
                    ('healthy','degraded','unknown','unavailable','disabled','held')),
                CONSTRAINT "CK_m18_evidence_code" CHECK(observation_code ~ '^[a-z0-9_-]{1,80}$')
            );

            CREATE TABLE it_incident_operation_ids (
                organization_id uuid NOT NULL REFERENCES organizations(id),
                actor_user_id uuid NOT NULL REFERENCES users(id),
                operation_key uuid NOT NULL,
                operation_kind varchar(32) NOT NULL,
                payload_digest varchar(64) NOT NULL,
                incident_id uuid NULL,
                result_version bigint NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY(organization_id,actor_user_id,operation_key),
                CONSTRAINT "FK_m18_operation_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id)
            );

            CREATE TABLE it_incident_read_grants (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL REFERENCES organizations(id),
                user_id uuid NOT NULL REFERENCES users(id),
                granted_by uuid NOT NULL REFERENCES users(id),
                status varchar(8) NOT NULL DEFAULT 'Active',
                expires_at_utc timestamptz NOT NULL,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                revoked_at_utc timestamptz NULL,
                CONSTRAINT "UX_m18_read_grant" UNIQUE(organization_id,user_id),
                CONSTRAINT "CK_m18_grant_status" CHECK(status IN ('Active','Revoked')),
                CONSTRAINT "CK_m18_grant_version" CHECK(version > 0)
            );

            CREATE FUNCTION m18_history_immutable() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $immutable$
            BEGIN
                RAISE EXCEPTION 'Incident history is immutable';
            END $immutable$;
            CREATE TRIGGER m18_events_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_events
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_notes_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_notes
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_evidence_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_evidence
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();

            CREATE TRIGGER m18_incidents_audit AFTER INSERT OR UPDATE ON it_incidents
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_events_audit AFTER INSERT ON it_incident_events
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_notes_audit AFTER INSERT ON it_incident_notes
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_actions_audit AFTER INSERT OR UPDATE ON it_incident_actions
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_evidence_audit AFTER INSERT ON it_incident_evidence
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_grants_audit AFTER INSERT OR UPDATE ON it_incident_read_grants
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();

            -- M18 does not trust a caller-controlled identity GUC. Reuse the
            -- existing, keyed M11 proof and current selected-session checks.
            -- A fabricated set_config cannot produce a valid signed authority.
            CREATE FUNCTION m18_scoped_session(org uuid) RETURNS boolean
                LANGUAGE plpgsql STABLE SECURITY DEFINER
                SET search_path = pg_catalog, public AS $scope$
            BEGIN
                RETURN session_user='harborops_api' AND m11_read_allowed(org);
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $scope$;
            REVOKE ALL ON FUNCTION m18_scoped_session(uuid) FROM PUBLIC;

            DO $m18_rls$
            DECLARE target_name text;
            BEGIN
                FOREACH target_name IN ARRAY ARRAY[
                    'it_incidents','it_incident_events','it_incident_notes',
                    'it_incident_actions','it_incident_evidence',
                    'it_incident_operation_ids','it_incident_read_grants']
                LOOP
                    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', target_name);
                    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', target_name);
                    EXECUTE format(
                        'CREATE POLICY m18_tenant_scope ON %I USING (m18_scoped_session(organization_id)) WITH CHECK (m18_scoped_session(organization_id))',
                        target_name);
                END LOOP;
            END $m18_rls$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS it_incident_read_grants;
            DROP TABLE IF EXISTS it_incident_operation_ids;
            DROP TABLE IF EXISTS it_incident_evidence;
            DROP TABLE IF EXISTS it_incident_actions;
            DROP TABLE IF EXISTS it_incident_notes;
            DROP TABLE IF EXISTS it_incident_events;
            DROP TABLE IF EXISTS it_incidents;
            DROP FUNCTION IF EXISTS m18_scoped_session(uuid);
            DROP FUNCTION IF EXISTS m18_history_immutable();
            """);
    }
}
),
                CONSTRAINT "CK_m18_root_cause" CHECK (root_cause_code IS NULL OR root_cause_code ~ '^[a-z0-9_-]{1,80}
            );
            CREATE UNIQUE INDEX "UX_m18_incidents_org_id" ON it_incidents(organization_id,id);
            CREATE INDEX "IX_m18_incidents_list" ON it_incidents
                (organization_id,updated_at_utc DESC,id DESC);

            CREATE TABLE it_incident_events (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                sequence bigint NOT NULL,
                kind varchar(32) NOT NULL,
                from_state varchar(16) NULL,
                to_state varchar(16) NULL,
                reason_code varchar(80) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                incident_version bigint NOT NULL,
                CONSTRAINT "FK_m18_event_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "UX_m18_event_sequence"
                    UNIQUE(organization_id,incident_id,sequence),
                CONSTRAINT "CK_m18_event_version" CHECK (sequence > 0 AND incident_version > 0)
            );
            CREATE INDEX "IX_m18_events_timeline" ON it_incident_events
                (organization_id,incident_id,sequence);

            CREATE TABLE it_incident_notes (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                body varchar(4096) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_note_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_note_body" CHECK(length(body)>0)
            );

            CREATE TABLE it_incident_actions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                title varchar(160) NOT NULL,
                state varchar(16) NOT NULL DEFAULT 'Proposed',
                owner_user_id uuid NOT NULL REFERENCES users(id),
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_action_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_action_state" CHECK(state IN
                    ('Proposed','InProgress','Verified','Cancelled')),
                CONSTRAINT "CK_m18_action_version" CHECK(version > 0)
            );

            CREATE TABLE it_incident_evidence (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                kind varchar(24) NOT NULL,
                service_code varchar(64) NOT NULL,
                observation_code varchar(80) NOT NULL,
                source_status varchar(16) NOT NULL,
                observed_at_utc timestamptz NOT NULL,
                captured_at_utc timestamptz NOT NULL DEFAULT now(),
                correlation_digest varchar(64) NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                CONSTRAINT "FK_m18_evidence_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_evidence_kind" CHECK(kind IN ('ManualObservation','M17Snapshot','RecoveryProbe')),
                CONSTRAINT "CK_m18_evidence_status" CHECK(source_status IN
                    ('healthy','degraded','unknown','unavailable','disabled','held')),
                CONSTRAINT "CK_m18_evidence_code" CHECK(observation_code ~ '^[a-z0-9_-]{1,80}$')
            );

            CREATE TABLE it_incident_operation_ids (
                organization_id uuid NOT NULL REFERENCES organizations(id),
                actor_user_id uuid NOT NULL REFERENCES users(id),
                operation_key uuid NOT NULL,
                operation_kind varchar(32) NOT NULL,
                payload_digest varchar(64) NOT NULL,
                incident_id uuid NULL,
                result_version bigint NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY(organization_id,actor_user_id,operation_key),
                CONSTRAINT "FK_m18_operation_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id)
            );

            CREATE TABLE it_incident_read_grants (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL REFERENCES organizations(id),
                user_id uuid NOT NULL REFERENCES users(id),
                granted_by uuid NOT NULL REFERENCES users(id),
                status varchar(8) NOT NULL DEFAULT 'Active',
                expires_at_utc timestamptz NOT NULL,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                revoked_at_utc timestamptz NULL,
                CONSTRAINT "UX_m18_read_grant" UNIQUE(organization_id,user_id),
                CONSTRAINT "CK_m18_grant_status" CHECK(status IN ('Active','Revoked')),
                CONSTRAINT "CK_m18_grant_version" CHECK(version > 0)
            );

            CREATE FUNCTION m18_history_immutable() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $immutable$
            BEGIN
                RAISE EXCEPTION 'Incident history is immutable';
            END $immutable$;
            CREATE TRIGGER m18_events_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_events
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_notes_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_notes
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_evidence_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_evidence
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();

            CREATE TRIGGER m18_incidents_audit AFTER INSERT OR UPDATE ON it_incidents
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_events_audit AFTER INSERT ON it_incident_events
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_notes_audit AFTER INSERT ON it_incident_notes
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_actions_audit AFTER INSERT OR UPDATE ON it_incident_actions
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_evidence_audit AFTER INSERT ON it_incident_evidence
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_grants_audit AFTER INSERT OR UPDATE ON it_incident_read_grants
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();

            CREATE FUNCTION m18_scoped_session(org uuid) RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER
                SET search_path = pg_catalog, public AS $scope$
                SELECT EXISTS (
                    SELECT 1
                    FROM auth_sessions s
                    JOIN users u ON u.id = s.user_id
                    JOIN organization_memberships m ON
                        m.user_id = s.user_id AND m.organization_id = org
                    JOIN organizations o ON o.id = org
                    WHERE s.id = NULLIF(current_setting('harborops.m18_session',true),'')::uuid
                      AND s.user_id = NULLIF(current_setting('harborops.m18_actor',true),'')::uuid
                      AND s.selected_organization_id = org
                      AND s.revoked_at_utc IS NULL
                      AND s.absolute_expires_at_utc > now()
                      AND s.idle_expires_at_utc > now()
                      AND u.status = 'Active'
                      AND m.status = 'Active'
                      AND o.status = 'Active'
                )
            $scope$;
            REVOKE ALL ON FUNCTION m18_scoped_session(uuid) FROM PUBLIC;

            DO $m18_rls$
            DECLARE target_name text;
            BEGIN
                FOREACH target_name IN ARRAY ARRAY[
                    'it_incidents','it_incident_events','it_incident_notes',
                    'it_incident_actions','it_incident_evidence',
                    'it_incident_operation_ids','it_incident_read_grants']
                LOOP
                    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', target_name);
                    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', target_name);
                    EXECUTE format(
                        'CREATE POLICY m18_tenant_scope ON %I USING (m18_scoped_session(organization_id)) WITH CHECK (m18_scoped_session(organization_id))',
                        target_name);
                END LOOP;
            END $m18_rls$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS it_incident_read_grants;
            DROP TABLE IF EXISTS it_incident_operation_ids;
            DROP TABLE IF EXISTS it_incident_evidence;
            DROP TABLE IF EXISTS it_incident_actions;
            DROP TABLE IF EXISTS it_incident_notes;
            DROP TABLE IF EXISTS it_incident_events;
            DROP TABLE IF EXISTS it_incidents;
            DROP FUNCTION IF EXISTS m18_scoped_session(uuid);
            DROP FUNCTION IF EXISTS m18_history_immutable();
            """);
    }
}
)
            );
            CREATE UNIQUE INDEX "UX_m18_incidents_org_id" ON it_incidents(organization_id,id);
            CREATE INDEX "IX_m18_incidents_list" ON it_incidents
                (organization_id,updated_at_utc DESC,id DESC);

            CREATE TABLE it_incident_events (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                sequence bigint NOT NULL,
                kind varchar(32) NOT NULL,
                from_state varchar(16) NULL,
                to_state varchar(16) NULL,
                reason_code varchar(80) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                incident_version bigint NOT NULL,
                CONSTRAINT "FK_m18_event_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "UX_m18_event_sequence"
                    UNIQUE(organization_id,incident_id,sequence),
                CONSTRAINT "CK_m18_event_version" CHECK (sequence > 0 AND incident_version > 0)
            );
            CREATE INDEX "IX_m18_events_timeline" ON it_incident_events
                (organization_id,incident_id,sequence);

            CREATE TABLE it_incident_notes (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                body varchar(4096) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_note_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_note_body" CHECK(length(body)>0)
            );

            CREATE TABLE it_incident_actions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                title varchar(160) NOT NULL,
                state varchar(16) NOT NULL DEFAULT 'Proposed',
                owner_user_id uuid NOT NULL REFERENCES users(id),
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                updated_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_action_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_action_state" CHECK(state IN
                    ('Proposed','InProgress','Verified','Cancelled')),
                CONSTRAINT "CK_m18_action_version" CHECK(version > 0)
            );

            CREATE TABLE it_incident_evidence (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                kind varchar(24) NOT NULL,
                service_code varchar(64) NOT NULL,
                observation_code varchar(80) NOT NULL,
                source_status varchar(16) NOT NULL,
                observed_at_utc timestamptz NOT NULL,
                captured_at_utc timestamptz NOT NULL DEFAULT now(),
                correlation_digest varchar(64) NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                CONSTRAINT "FK_m18_evidence_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "CK_m18_evidence_kind" CHECK(kind IN ('ManualObservation','M17Snapshot','RecoveryProbe')),
                CONSTRAINT "CK_m18_evidence_status" CHECK(source_status IN
                    ('healthy','degraded','unknown','unavailable','disabled','held')),
                CONSTRAINT "CK_m18_evidence_code" CHECK(observation_code ~ '^[a-z0-9_-]{1,80}$')
            );

            CREATE TABLE it_incident_operation_ids (
                organization_id uuid NOT NULL REFERENCES organizations(id),
                actor_user_id uuid NOT NULL REFERENCES users(id),
                operation_key uuid NOT NULL,
                operation_kind varchar(32) NOT NULL,
                payload_digest varchar(64) NOT NULL,
                incident_id uuid NULL,
                result_version bigint NULL,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY(organization_id,actor_user_id,operation_key),
                CONSTRAINT "FK_m18_operation_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id)
            );

            CREATE TABLE it_incident_read_grants (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL REFERENCES organizations(id),
                user_id uuid NOT NULL REFERENCES users(id),
                granted_by uuid NOT NULL REFERENCES users(id),
                status varchar(8) NOT NULL DEFAULT 'Active',
                expires_at_utc timestamptz NOT NULL,
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                revoked_at_utc timestamptz NULL,
                CONSTRAINT "UX_m18_read_grant" UNIQUE(organization_id,user_id),
                CONSTRAINT "CK_m18_grant_status" CHECK(status IN ('Active','Revoked')),
                CONSTRAINT "CK_m18_grant_version" CHECK(version > 0)
            );

            CREATE FUNCTION m18_history_immutable() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $immutable$
            BEGIN
                RAISE EXCEPTION 'Incident history is immutable';
            END $immutable$;
            CREATE TRIGGER m18_events_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_events
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_notes_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_notes
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_evidence_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_evidence
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();

            CREATE TRIGGER m18_incidents_audit AFTER INSERT OR UPDATE ON it_incidents
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_events_audit AFTER INSERT ON it_incident_events
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_notes_audit AFTER INSERT ON it_incident_notes
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_actions_audit AFTER INSERT OR UPDATE ON it_incident_actions
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_evidence_audit AFTER INSERT ON it_incident_evidence
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();
            CREATE TRIGGER m18_grants_audit AFTER INSERT OR UPDATE ON it_incident_read_grants
                FOR EACH ROW EXECUTE FUNCTION m16_audit_capture();

            CREATE FUNCTION m18_scoped_session(org uuid) RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER
                SET search_path = pg_catalog, public AS $scope$
                SELECT EXISTS (
                    SELECT 1
                    FROM auth_sessions s
                    JOIN users u ON u.id = s.user_id
                    JOIN organization_memberships m ON
                        m.user_id = s.user_id AND m.organization_id = org
                    JOIN organizations o ON o.id = org
                    WHERE s.id = NULLIF(current_setting('harborops.m18_session',true),'')::uuid
                      AND s.user_id = NULLIF(current_setting('harborops.m18_actor',true),'')::uuid
                      AND s.selected_organization_id = org
                      AND s.revoked_at_utc IS NULL
                      AND s.absolute_expires_at_utc > now()
                      AND s.idle_expires_at_utc > now()
                      AND u.status = 'Active'
                      AND m.status = 'Active'
                      AND o.status = 'Active'
                )
            $scope$;
            REVOKE ALL ON FUNCTION m18_scoped_session(uuid) FROM PUBLIC;

            DO $m18_rls$
            DECLARE target_name text;
            BEGIN
                FOREACH target_name IN ARRAY ARRAY[
                    'it_incidents','it_incident_events','it_incident_notes',
                    'it_incident_actions','it_incident_evidence',
                    'it_incident_operation_ids','it_incident_read_grants']
                LOOP
                    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', target_name);
                    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', target_name);
                    EXECUTE format(
                        'CREATE POLICY m18_tenant_scope ON %I USING (m18_scoped_session(organization_id)) WITH CHECK (m18_scoped_session(organization_id))',
                        target_name);
                END LOOP;
            END $m18_rls$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS it_incident_read_grants;
            DROP TABLE IF EXISTS it_incident_operation_ids;
            DROP TABLE IF EXISTS it_incident_evidence;
            DROP TABLE IF EXISTS it_incident_actions;
            DROP TABLE IF EXISTS it_incident_notes;
            DROP TABLE IF EXISTS it_incident_events;
            DROP TABLE IF EXISTS it_incidents;
            DROP FUNCTION IF EXISTS m18_scoped_session(uuid);
            DROP FUNCTION IF EXISTS m18_history_immutable();
            """);
    }
}
