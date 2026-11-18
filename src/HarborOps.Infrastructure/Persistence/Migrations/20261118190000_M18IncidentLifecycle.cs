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
                CONSTRAINT "CK_m18_impact" CHECK (impact_code ~ '^[a-z0-9_-]{1,80}$'),
                CONSTRAINT "CK_m18_root_cause" CHECK (root_cause_code IS NULL OR root_cause_code ~ '^[a-z0-9_-]{1,80}$')
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
                CONSTRAINT "CK_m18_action_version" CHECK(version > 0),
                CONSTRAINT "UX_m18_action_scoped" UNIQUE(organization_id,id)
            );

            CREATE TABLE it_incident_action_events (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                incident_id uuid NOT NULL,
                action_id uuid NOT NULL,
                action_version bigint NOT NULL CHECK(action_version>0),
                previous_state varchar(16) NULL,
                new_state varchar(16) NOT NULL CHECK(new_state IN
                    ('Proposed','InProgress','Verified','Cancelled')),
                reason_code varchar(80) NOT NULL,
                actor_user_id uuid NOT NULL REFERENCES users(id),
                occurred_at_utc timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "FK_m18_action_history_incident"
                    FOREIGN KEY(organization_id,incident_id)
                    REFERENCES it_incidents(organization_id,id),
                CONSTRAINT "FK_m18_action_history_action"
                    FOREIGN KEY(organization_id,action_id)
                    REFERENCES it_incident_actions(organization_id,id),
                CONSTRAINT "UX_m18_action_history_version"
                    UNIQUE(organization_id,action_id,action_version)
            );
            CREATE INDEX "IX_m18_action_history_incident"
                ON it_incident_action_events(organization_id,incident_id,occurred_at_utc,id);

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
            CREATE TRIGGER m18_action_events_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_action_events
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();
            CREATE TRIGGER m18_evidence_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON it_incident_evidence
                FOR EACH STATEMENT EXECUTE FUNCTION m18_history_immutable();

            -- The current-state projection is mutable but only along the approved
            -- paths. This guard repeats the transition and closure rules inside the
            -- database so a direct statement from the API role cannot skip a state,
            -- rewrite identity or ownership, or close without a distinct confirmer.
            CREATE FUNCTION m18_incident_guard() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $guard$
            BEGIN
                IF NEW.id IS DISTINCT FROM OLD.id
                   OR NEW.organization_id IS DISTINCT FROM OLD.organization_id
                   OR NEW.created_by IS DISTINCT FROM OLD.created_by
                   OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
                   OR NEW.detected_at_utc IS DISTINCT FROM OLD.detected_at_utc
                   OR NEW.title IS DISTINCT FROM OLD.title
                   OR NEW.service_code IS DISTINCT FROM OLD.service_code THEN
                    RAISE EXCEPTION 'Incident identity is immutable' USING ERRCODE = '23514';
                END IF;
                IF NEW.version IS DISTINCT FROM OLD.version + 1 THEN
                    RAISE EXCEPTION 'Incident version must advance by exactly one'
                        USING ERRCODE = '23514';
                END IF;
                IF NEW.state IS DISTINCT FROM OLD.state AND NOT (
                    (OLD.state = 'New' AND NEW.state = 'Triage') OR
                    (OLD.state = 'Triage' AND NEW.state = 'Investigating') OR
                    (OLD.state = 'Investigating' AND NEW.state = 'Mitigated') OR
                    (OLD.state = 'Mitigated' AND NEW.state = 'Monitoring') OR
                    (OLD.state = 'Monitoring' AND NEW.state IN ('Investigating','Resolved')) OR
                    (OLD.state = 'Resolved' AND NEW.state IN ('Closed','Investigating')) OR
                    (OLD.state = 'Closed' AND NEW.state = 'Investigating')) THEN
                    RAISE EXCEPTION 'Incident state transition is not allowed' USING ERRCODE = '23514';
                END IF;
                IF NEW.state = 'Resolved' AND OLD.state IS DISTINCT FROM 'Resolved'
                   AND (NEW.resolved_by IS NULL OR NEW.resolved_at_utc IS NULL
                        OR NEW.root_cause_code IS NULL
                        OR NOT (NEW.recovery_verified AND NEW.integrity_verified
                                AND NEW.actions_dispositioned)) THEN
                    RAISE EXCEPTION 'Resolution requires verified recovery' USING ERRCODE = '23514';
                END IF;
                IF NEW.state = 'Closed' AND OLD.state IS DISTINCT FROM 'Closed'
                   AND (NEW.closed_by IS NULL OR NEW.closed_at_utc IS NULL
                        OR NOT NEW.actions_dispositioned
                        OR (NEW.severity IN ('SEV-1','SEV-2')
                            AND NEW.closed_by IS NOT DISTINCT FROM NEW.resolved_by)) THEN
                    RAISE EXCEPTION 'Closure requires an eligible confirmer' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $guard$;
            CREATE TRIGGER m18_incidents_guard BEFORE UPDATE ON it_incidents
                FOR EACH ROW EXECUTE FUNCTION m18_incident_guard();
            ALTER TABLE it_incident_events ADD CONSTRAINT "CK_m18_event_sequence"
                CHECK (sequence = incident_version);
            ALTER TABLE it_incident_events ADD CONSTRAINT "CK_m18_event_kind" CHECK (kind IN
                ('Created','Transitioned','Reopened','NoteAdded','Assigned','ActionCreated',
                 'ActionChanged','EvidenceAttached','M17SnapshotAdded','RecoveryVerified',
                 'ImpactUpdated','SeverityChanged'));

            -- Migration-owned M18 audit taxonomy. Each logical sensitive change appends
            -- exactly one fixed M16 event in the same transaction; the immutable event
            -- and action-event histories are not audited a second time. Only event
            -- names, operation and an allowlist of changed column NAMES are stored,
            -- never a value, note body, title or evidence content.
            CREATE FUNCTION m18_audit_capture() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER
                SET search_path = pg_catalog, public AS $audit$
            DECLARE
                audit_type text;
                changed text[] := ARRAY[]::text[];
                audit_actor uuid;
                audit_session uuid;
                audit_corr varchar(64);
                audit_role varchar(24);
                audit_kind varchar(24);
                row_json jsonb := to_jsonb(NEW);
                old_json jsonb;
                field text;
            BEGIN
                IF TG_TABLE_NAME = 'it_incidents' THEN
                    IF TG_OP = 'INSERT' THEN
                        audit_type := 'incident.created';
                    ELSE
                        old_json := to_jsonb(OLD);
                        FOREACH field IN ARRAY ARRAY['state','severity','assigned_to','impact_code',
                            'root_cause_code','recovery_verified','integrity_verified',
                            'actions_dispositioned'] LOOP
                            IF old_json->field IS DISTINCT FROM row_json->field THEN
                                changed := changed || field;
                            END IF;
                        END LOOP;
                        IF 'state' = ANY(changed) AND OLD.state IN ('Resolved','Closed')
                           AND NEW.state = 'Investigating' THEN
                            audit_type := 'incident.reopened';
                        ELSIF 'state' = ANY(changed) THEN audit_type := 'incident.state_changed';
                        ELSIF 'severity' = ANY(changed) THEN audit_type := 'incident.severity_changed';
                        ELSIF 'assigned_to' = ANY(changed) THEN audit_type := 'incident.assigned';
                        ELSIF 'impact_code' = ANY(changed) THEN audit_type := 'incident.impact_changed';
                        ELSIF 'recovery_verified' = ANY(changed) OR 'integrity_verified' = ANY(changed)
                            OR 'root_cause_code' = ANY(changed) THEN
                            audit_type := 'incident.recovery_verified';
                        ELSE
                            -- A version-only change is the parent bump of a child record that
                            -- carries its own audit event.
                            RETURN NEW;
                        END IF;
                    END IF;
                ELSIF TG_TABLE_NAME = 'it_incident_notes' THEN audit_type := 'incident.note_added';
                ELSIF TG_TABLE_NAME = 'it_incident_actions' THEN
                    IF TG_OP = 'INSERT' THEN audit_type := 'incident.action_created';
                    ELSE
                        audit_type := 'incident.action_changed';
                        IF OLD.state IS DISTINCT FROM NEW.state THEN changed := ARRAY['state']; END IF;
                    END IF;
                ELSIF TG_TABLE_NAME = 'it_incident_evidence' THEN audit_type := 'incident.evidence_attached';
                ELSIF TG_TABLE_NAME = 'it_incident_read_grants' THEN
                    IF TG_OP = 'UPDATE' AND NEW.status = 'Revoked' THEN
                        audit_type := 'incident.read_grant_revoked';
                    ELSE audit_type := 'incident.read_grant_granted'; END IF;
                    IF TG_OP = 'UPDATE' THEN
                        old_json := to_jsonb(OLD);
                        FOREACH field IN ARRAY ARRAY['status','expires_at_utc'] LOOP
                            IF old_json->field IS DISTINCT FROM row_json->field THEN
                                changed := changed || field;
                            END IF;
                        END LOOP;
                    END IF;
                ELSE
                    RAISE EXCEPTION 'Unaudited incident table' USING ERRCODE = '23514';
                END IF;

                audit_actor := NULLIF(current_setting('harborops.audit_actor_user_id', true), '')::uuid;
                audit_session := NULLIF(current_setting('harborops.audit_actor_session_id', true), '')::uuid;
                audit_corr := left(COALESCE(
                    NULLIF(current_setting('harborops.audit_correlation_id', true), ''), 'database'), 64);
                audit_role := NULLIF(left(current_setting('harborops.audit_actor_role', true), 24), '');
                audit_kind := COALESCE(
                    NULLIF(current_setting('harborops.audit_actor_kind', true), ''), 'System');
                IF audit_kind = 'Human' AND audit_actor IS NULL THEN audit_kind := 'System'; END IF;

                EXECUTE format(
                    'INSERT INTO %I.audit_entries
                     (id, organization_id, event_type, event_schema_version, outcome,
                      actor_kind, actor_user_id, actor_session_id, actor_source_id,
                      actor_role, resource_type, resource_id, correlation_id,
                      occurred_at_utc, metadata)
                     VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)',
                    TG_TABLE_SCHEMA)
                USING gen_random_uuid(), NEW.organization_id, audit_type, 1, 'Succeeded',
                    audit_kind, audit_actor, audit_session, NULL::uuid, audit_role,
                    TG_TABLE_NAME, NEW.id, audit_corr, statement_timestamp(),
                    jsonb_build_object('operation', lower(TG_OP), 'changedFields', changed);
                RETURN NEW;
            END $audit$;
            REVOKE ALL ON FUNCTION m18_audit_capture() FROM PUBLIC;
            REVOKE ALL ON FUNCTION m18_incident_guard() FROM PUBLIC;
            CREATE TRIGGER m18_incidents_audit AFTER INSERT OR UPDATE ON it_incidents
                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();
            CREATE TRIGGER m18_notes_audit AFTER INSERT ON it_incident_notes
                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();
            CREATE TRIGGER m18_actions_audit AFTER INSERT OR UPDATE ON it_incident_actions
                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();
            CREATE TRIGGER m18_evidence_audit AFTER INSERT ON it_incident_evidence
                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();
            CREATE TRIGGER m18_grants_audit AFTER INSERT OR UPDATE ON it_incident_read_grants
                FOR EACH ROW EXECUTE FUNCTION m18_audit_capture();

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
                    'it_incident_actions','it_incident_action_events','it_incident_evidence',
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
            DO $m18_retention$
            BEGIN
                IF EXISTS (SELECT 1 FROM it_incidents LIMIT 1)
                    OR EXISTS (SELECT 1 FROM it_incident_read_grants LIMIT 1)
                    OR EXISTS (SELECT 1 FROM it_incident_operation_ids LIMIT 1) THEN
                    RAISE EXCEPTION 'Retained M18 incident evidence prevents downgrade'
                        USING ERRCODE = '23514';
                END IF;
            END $m18_retention$;
            DROP TABLE IF EXISTS it_incident_read_grants;
            DROP TABLE IF EXISTS it_incident_operation_ids;
            DROP TABLE IF EXISTS it_incident_evidence;
            DROP TABLE IF EXISTS it_incident_action_events;
            DROP TABLE IF EXISTS it_incident_actions;
            DROP TABLE IF EXISTS it_incident_notes;
            DROP TABLE IF EXISTS it_incident_events;
            DROP TABLE IF EXISTS it_incidents;
            DROP FUNCTION IF EXISTS m18_scoped_session(uuid);
            DROP FUNCTION IF EXISTS m18_history_immutable();
            DROP FUNCTION IF EXISTS m18_audit_capture();
            DROP FUNCTION IF EXISTS m18_incident_guard();
            """);
    }
}
