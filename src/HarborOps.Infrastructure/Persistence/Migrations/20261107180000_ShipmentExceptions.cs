using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261107180000_ShipmentExceptions")]
public sealed class ShipmentExceptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE shipment_exceptions (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                rule_code varchar(40) NOT NULL,
                occurrence integer NOT NULL,
                condition_state varchar(12) NOT NULL,
                lifecycle_state varchar(16) NOT NULL,
                severity varchar(16) NOT NULL,
                confidence varchar(32) NOT NULL,
                reason_code varchar(80) NOT NULL,
                source_fingerprint char(64) NOT NULL,
                shipment_version bigint NOT NULL,
                tracking_input_revision bigint NOT NULL,
                correction_generation bigint NOT NULL,
                reference_at_utc timestamptz,
                threshold_at_utc timestamptz,
                next_evaluation_at_utc timestamptz,
                first_detected_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                last_evaluated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                -- Verified clearance survives an intervening Unknown/Stale reading.
                last_confirmed_inactive_at_utc timestamptz,
                resolved_at_utc timestamptz,
                version bigint NOT NULL DEFAULT 1,
                evidence jsonb NOT NULL,
                CONSTRAINT fk_m11_exception_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT uq_m11_exception_occurrence UNIQUE (organization_id,shipment_id,rule_code,occurrence),
                CONSTRAINT uq_m11_exception_scope UNIQUE (id,organization_id),
                CONSTRAINT ck_m11_rule CHECK (rule_code IN ('EquipmentConflict',
                    'ArrivalScheduleOverdue','RecordedArrivalLate','DepartureSlippageRisk')),
                CONSTRAINT ck_m11_condition CHECK (condition_state IN ('Active','Inactive','Unknown','Stale')),
                CONSTRAINT ck_m11_lifecycle CHECK (lifecycle_state IN ('Open','Acknowledged','Resolved')),
                CONSTRAINT ck_m11_severity CHECK (severity IN ('Info','Warning','High')),
                CONSTRAINT ck_m11_confidence CHECK (confidence IN (
                    'RecordedEquipment','RecordedWorkflow','ScheduleOnly','Unverified','NotApplicable')),
                CONSTRAINT ck_m11_reason CHECK (reason_code ~ '^[a-z0-9_]{1,80}$'),
                CONSTRAINT ck_m11_fingerprint CHECK (source_fingerprint ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_m11_versions CHECK (occurrence > 0 AND version > 0
                    AND shipment_version > 0 AND tracking_input_revision >= 0
                    AND correction_generation >= 0),
                CONSTRAINT ck_m11_evidence CHECK (jsonb_typeof(evidence)='object'
                    AND pg_column_size(evidence) <= 2048),
                CONSTRAINT ck_m11_resolved CHECK (
                    (lifecycle_state='Resolved' AND resolved_at_utc IS NOT NULL)
                    OR (lifecycle_state<>'Resolved' AND resolved_at_utc IS NULL))
            );
            CREATE UNIQUE INDEX uq_m11_active_occurrence ON shipment_exceptions
                (organization_id,shipment_id,rule_code) WHERE lifecycle_state <> 'Resolved';
            CREATE INDEX ix_m11_exception_scope ON shipment_exceptions
                (organization_id,lifecycle_state,id);
            CREATE INDEX ix_m11_exception_shipment ON shipment_exceptions
                (organization_id,shipment_id,id);

            CREATE TABLE shipment_exception_history (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                organization_id uuid NOT NULL,
                exception_id uuid NOT NULL,
                action varchar(24) NOT NULL,
                old_state varchar(16),
                new_state varchar(16) NOT NULL,
                actor_kind varchar(16) NOT NULL,
                actor_user_id uuid,
                actor_session_id uuid,
                reason varchar(240),
                source_fingerprint char(64) NOT NULL,
                occurred_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT fk_m11_history_exception FOREIGN KEY (exception_id,organization_id)
                    REFERENCES shipment_exceptions(id,organization_id) ON DELETE RESTRICT,
                CONSTRAINT ck_m11_history_action CHECK (action IN (
                    'Opened','Acknowledged','Resolved','AutoResolved','Reevaluated','InputUnavailable')),
                CONSTRAINT ck_m11_history_actor CHECK (
                    (actor_kind='System' AND actor_user_id IS NULL AND actor_session_id IS NULL)
                    OR (actor_kind='Human' AND actor_user_id IS NOT NULL AND actor_session_id IS NOT NULL)),
                CONSTRAINT ck_m11_history_state CHECK (new_state IN ('Open','Acknowledged','Resolved')
                    AND (old_state IS NULL OR old_state IN ('Open','Acknowledged','Resolved'))),
                CONSTRAINT ck_m11_history_reason CHECK (
                    reason IS NULL OR (length(btrim(reason)) BETWEEN 8 AND 240
                    AND reason !~ '[[:cntrl:]]')),
                CONSTRAINT ck_m11_history_fingerprint CHECK
                    (source_fingerprint ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_m11_history_scope ON shipment_exception_history
                (organization_id,exception_id,occurred_at_utc,id);
            CREATE TRIGGER tr_m11_immutable_history BEFORE UPDATE OR DELETE
                ON shipment_exception_history FOR EACH ROW EXECUTE FUNCTION m10_append_only();

            CREATE TABLE shipment_exception_evaluations (
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                rule_code varchar(40) NOT NULL,
                rule_policy_version varchar(40) NOT NULL,
                condition_state varchar(12) NOT NULL,
                source_fingerprint char(64) NOT NULL,
                shipment_version bigint NOT NULL,
                tracking_input_revision bigint NOT NULL,
                correction_generation bigint NOT NULL,
                next_evaluation_at_utc timestamptz,
                evaluated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                PRIMARY KEY (organization_id,shipment_id,rule_code),
                CONSTRAINT fk_m11_eval_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m11_eval_rule CHECK (rule_code IN ('EquipmentConflict',
                    'ArrivalScheduleOverdue','RecordedArrivalLate','DepartureSlippageRisk')),
                CONSTRAINT ck_m11_eval_policy CHECK (rule_policy_version = 'm11-rules-v1'),
                CONSTRAINT ck_m11_eval_state CHECK (condition_state IN ('Active','Inactive','Unknown','Stale')),
                CONSTRAINT ck_m11_eval_fingerprint CHECK (source_fingerprint ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_m11_eval_due ON shipment_exception_evaluations
                (next_evaluation_at_utc,organization_id,shipment_id)
                WHERE next_evaluation_at_utc IS NOT NULL;

            CREATE TABLE shipment_exception_replays (
                organization_id uuid NOT NULL,
                exception_id uuid NOT NULL,
                actor_user_id uuid NOT NULL,
                idempotency_key uuid NOT NULL,
                request_digest char(64) NOT NULL,
                response_version bigint NOT NULL,
                action varchar(16) NOT NULL,
                created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                -- A key has one meaning per organization across actors and
                -- exceptions. The signed digest includes the selected session.
                PRIMARY KEY (organization_id,idempotency_key),
                CONSTRAINT fk_m11_replay_exception FOREIGN KEY (exception_id,organization_id)
                    REFERENCES shipment_exceptions(id,organization_id) ON DELETE RESTRICT,
                CONSTRAINT ck_m11_replay_digest CHECK (request_digest ~ '^[0-9a-f]{64}$')
            );
            CREATE TRIGGER tr_m11_immutable_replays BEFORE UPDATE OR DELETE
                ON shipment_exception_replays FOR EACH ROW EXECUTE FUNCTION m10_append_only();

            CREATE TABLE shipment_exception_proof_nonces (
                nonce uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                operation varchar(16) NOT NULL,
                expires_at_utc timestamptz NOT NULL,
                CONSTRAINT ck_m11_nonce_operation CHECK (operation IN ('Evaluation','Disposition'))
            );
            -- Ephemeral signing nonces are pruned after proof expiry.
            -- Runtime API credentials cannot delete them directly.
            CREATE INDEX ix_m11_nonce_expiry ON shipment_exception_proof_nonces
                (expires_at_utc,nonce);
            REVOKE ALL ON shipment_exception_proof_nonces FROM PUBLIC;

            CREATE FUNCTION m11_apply_signed_evaluation(
                p_org uuid, p_shipment uuid, p_rule varchar, p_condition varchar,
                p_severity varchar, p_confidence varchar, p_reason varchar, p_fingerprint varchar,
                p_reference timestamptz, p_threshold timestamptz, p_next timestamptz,
                p_ship_version bigint, p_input_revision bigint, p_generation bigint,
                p_nonce uuid, p_expires bigint, p_signature varchar)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER
            SET search_path=pg_catalog,public AS $eval$
            DECLARE
                signing_key bytea;
                expected_sig text;
                message text;
                now_epoch bigint := floor(extract(epoch from clock_timestamp()))::bigint;
                current_version bigint;
                current_record_status text;
                current_workflow_status text;
                current_input bigint;
                current_generation bigint;
                current_state text;
                current_projection_revision bigint;
                current_projection_generation bigint;
                existing_id uuid;
                existing_state text;
                existing_condition text;
                last_confirmed_inactive_utc timestamptz;
                previous_fp text;
                next_occurrence integer;
                stored_condition text;
                exception_id uuid;
                payload jsonb;
                current_org text;
            BEGIN
                IF session_user <> 'harborops_api'
                   OR p_org IS NULL OR p_shipment IS NULL OR p_nonce IS NULL
                   OR p_expires IS NULL OR p_signature IS NULL
                   OR p_rule IS NULL OR p_condition IS NULL
                   OR p_severity IS NULL OR p_confidence IS NULL
                   OR p_reason IS NULL OR p_fingerprint IS NULL
                   OR p_ship_version IS NULL OR p_input_revision IS NULL
                   OR p_generation IS NULL
                   OR p_signature !~ '^[0-9a-f]{64}$'
                   OR p_expires < now_epoch OR p_expires > now_epoch+60
                   OR p_rule NOT IN ('EquipmentConflict','ArrivalScheduleOverdue',
                       'RecordedArrivalLate','DepartureSlippageRisk')
                   OR p_condition NOT IN ('Active','Inactive','Unknown','Stale')
                   OR p_severity NOT IN ('Info','Warning','High')
                   OR p_confidence NOT IN ('RecordedEquipment','RecordedWorkflow',
                       'ScheduleOnly','Unverified','NotApplicable')
                   OR p_reason !~ '^[a-z0-9_]{1,80}$'
                   OR p_fingerprint !~ '^[0-9a-f]{64}$'
                   OR p_ship_version < 1 OR p_input_revision < 0 OR p_generation < 0 THEN
                    RAISE EXCEPTION 'Evaluation authority denied' USING ERRCODE='42501';
                END IF;
                SELECT secret INTO signing_key
                FROM m10_correction_proof_keys WHERE key_id=1 FOR SHARE;
                IF signing_key IS NULL THEN
                    RAISE EXCEPTION 'Evaluation verifier unavailable' USING ERRCODE='PZ002';
                END IF;

                message := 'm11-eval-v2|' || replace(p_nonce::text,'-','') ||
                    '|' || p_expires::text || '|' || replace(p_org::text,'-','') ||
                    '|' || replace(p_shipment::text,'-','') ||
                    '|' || p_rule || '|' || p_condition || '|' || p_severity ||
                    '|' || p_confidence || '|' || p_reason || '|' || p_fingerprint ||
                    '|' || p_ship_version::text || '|' || p_input_revision::text ||
                    '|' || p_generation::text ||
                    '|' || COALESCE(floor(extract(epoch from p_reference)*1000000)::bigint::text,'') ||
                    '|' || COALESCE(floor(extract(epoch from p_threshold)*1000000)::bigint::text,'') ||
                    '|' || COALESCE(floor(extract(epoch from p_next)*1000000)::bigint::text,'');
                expected_sig := encode(hmac(convert_to(message,'UTF8'),signing_key,'sha256'),'hex');
                IF p_signature IS DISTINCT FROM expected_sig THEN
                    RAISE EXCEPTION 'Evaluation authority denied' USING ERRCODE='42501';
                END IF;

                SELECT status INTO current_org FROM organizations
                    WHERE id=p_org FOR SHARE;
                IF current_org IS DISTINCT FROM 'Active' THEN
                    RAISE EXCEPTION 'Inactive evaluation organization' USING ERRCODE='42501';
                END IF;
                SELECT version,record_status,workflow_status
                    INTO current_version,current_record_status,current_workflow_status
                FROM shipments
                    WHERE organization_id=p_org AND id=p_shipment FOR UPDATE;
                IF current_version IS NULL THEN
                    RAISE EXCEPTION 'Shipment not found' USING ERRCODE='PZ003';
                END IF;
                SELECT COALESCE(v.input_revision,0) INTO current_input
                    FROM shipment_tracking_input_versions v
                    WHERE v.organization_id=p_org AND v.shipment_id=p_shipment;
                current_input := COALESCE(current_input,0);
                SELECT COALESCE(h.generation,0) INTO current_generation
                    FROM shipment_tracking_correction_heads h
                    WHERE h.organization_id=p_org AND h.shipment_id=p_shipment;
                current_generation := COALESCE(current_generation,0);
                IF current_version <> p_ship_version
                   OR current_input <> p_input_revision
                   OR current_generation <> p_generation THEN
                    RAISE EXCEPTION 'Evaluation input changed' USING ERRCODE='40001';
                END IF;

                -- Archived, cancelled and Draft shipments have no current
                -- equipment-risk eligibility. Their signed Inactive clearance
                -- must not depend on a projection that may be absent or stale.
                IF current_record_status IS DISTINCT FROM 'Active'
                   OR current_workflow_status IN ('Cancelled','Draft') THEN
                    IF p_condition IS DISTINCT FROM 'Inactive'
                       OR p_reason IS DISTINCT FROM 'shipment_not_eligible' THEN
                        RAISE EXCEPTION 'Ineligible shipment evaluation changed'
                            USING ERRCODE='40001';
                    END IF;
                ELSIF p_rule='EquipmentConflict' AND p_condition IN ('Active','Inactive') THEN
                    SELECT observed_state,input_revision,correction_generation
                    INTO current_state,current_projection_revision,current_projection_generation
                    FROM shipment_tracking_projections
                    WHERE organization_id=p_org AND shipment_id=p_shipment;
                    IF current_projection_revision IS DISTINCT FROM current_input
                       OR current_projection_generation IS DISTINCT FROM current_generation
                       OR current_state IS NULL
                       OR (p_condition='Active') IS DISTINCT FROM (current_state='Conflict') THEN
                        RAISE EXCEPTION 'Equipment projection changed' USING ERRCODE='40001';
                    END IF;
                END IF;

                DELETE FROM shipment_exception_proof_nonces
                WHERE nonce IN (
                    SELECT nonce FROM shipment_exception_proof_nonces
                    WHERE expires_at_utc < clock_timestamp() - interval '1 minute'
                    ORDER BY expires_at_utc,nonce LIMIT 64 FOR UPDATE SKIP LOCKED
                );
                INSERT INTO shipment_exception_proof_nonces
                    (nonce,organization_id,shipment_id,operation,expires_at_utc)
                VALUES (p_nonce,p_org,p_shipment,'Evaluation',to_timestamp(p_expires));
                -- Preserve bounded, typed provenance, not raw tracking payloads.
                -- A projection digest identifies the accepted M10 snapshot,
                -- not an independently confirmed physical arrival.
                payload := jsonb_build_object(
                    'reasonCode',p_reason,'confidence',p_confidence,
                    'referenceAtUtc',p_reference,'thresholdAtUtc',p_threshold,
                    'rulePolicyVersion','m11-rules-v1',
                    'shipmentVersion',p_ship_version,
                    'trackingInputRevision',p_input_revision,
                    'correctionGeneration',p_generation,
                    'trackingPolicyVersion',CASE WHEN p_rule='EquipmentConflict'
                        THEN (SELECT policy_version FROM shipment_tracking_projections
                              WHERE organization_id=p_org AND shipment_id=p_shipment)
                        ELSE NULL END,
                    'trackingInputDigest',CASE WHEN p_rule='EquipmentConflict'
                        THEN (SELECT input_digest::text FROM shipment_tracking_projections
                              WHERE organization_id=p_org AND shipment_id=p_shipment
                                AND input_revision=p_input_revision
                                AND correction_generation=p_generation)
                        ELSE NULL END);
                IF pg_column_size(payload) > 2048 THEN
                    RAISE EXCEPTION 'Evaluation metadata invalid' USING ERRCODE='23514';
                END IF;
                SELECT condition_state,source_fingerprint
                    INTO stored_condition,previous_fp
                FROM shipment_exception_evaluations
                WHERE organization_id=p_org AND shipment_id=p_shipment AND rule_code=p_rule
                FOR UPDATE;
                IF stored_condition=p_condition AND previous_fp=p_fingerprint
                   AND NOT EXISTS (
                       -- A deterministic condition/fingerprint may be unchanged
                       -- across a pre-acceptance severity calibration. Do not
                       -- silently retain a stale visible severity or skip the
                       -- corresponding immutable reevaluation history.
                       SELECT 1 FROM shipment_exceptions
                       WHERE organization_id=p_org AND shipment_id=p_shipment
                         AND rule_code=p_rule AND lifecycle_state<>'Resolved'
                         AND (severity IS DISTINCT FROM p_severity
                              OR confidence IS DISTINCT FROM p_confidence
                              OR reason_code IS DISTINCT FROM p_reason
                              OR reference_at_utc IS DISTINCT FROM p_reference
                              OR threshold_at_utc IS DISTINCT FROM p_threshold
                              OR next_evaluation_at_utc IS DISTINCT FROM p_next)
                   ) THEN
                    -- An unchanged semantic result still represents a new
                    -- source review. Advance the due checkpoint and projection
                    -- refresh watermark without writing duplicate history.
                    UPDATE shipment_exception_evaluations SET
                        rule_policy_version='m11-rules-v1',
                        shipment_version=p_ship_version,
                        tracking_input_revision=p_input_revision,
                        correction_generation=p_generation,
                        next_evaluation_at_utc=p_next,
                        evaluated_at_utc=clock_timestamp()
                    WHERE organization_id=p_org AND shipment_id=p_shipment
                      AND rule_code=p_rule;
                    UPDATE shipment_exceptions SET
                        last_evaluated_at_utc=clock_timestamp()
                    WHERE organization_id=p_org AND shipment_id=p_shipment
                      AND rule_code=p_rule AND lifecycle_state<>'Resolved';
                    RETURN;
                END IF;

                SELECT id,lifecycle_state,condition_state,last_confirmed_inactive_at_utc
                    INTO existing_id,existing_state,existing_condition,last_confirmed_inactive_utc
                FROM shipment_exceptions
                WHERE organization_id=p_org AND shipment_id=p_shipment
                  AND rule_code=p_rule AND lifecycle_state<>'Resolved'
                FOR UPDATE;

                -- Confirmed clearance is durable across later Unknown/Stale
                -- observations. An active recurrence after a verified clearance
                -- closes the acknowledged occurrence and opens a new incident;
                -- uncertainty alone never establishes clearance.
                IF p_condition='Active' AND existing_id IS NOT NULL
                   AND existing_state='Acknowledged'
                   AND last_confirmed_inactive_utc IS NOT NULL THEN
                    UPDATE shipment_exceptions SET
                        lifecycle_state='Resolved',
                        resolved_at_utc=clock_timestamp(),
                        last_evaluated_at_utc=clock_timestamp(),
                        version=version+1
                    WHERE id=existing_id;
                    INSERT INTO shipment_exception_history
                        (organization_id,exception_id,action,old_state,new_state,
                         actor_kind,reason,source_fingerprint)
                    SELECT p_org,existing_id,'AutoResolved','Acknowledged',
                           'Resolved','System',
                           'Prior cleared occurrence superseded by recurrence',
                           source_fingerprint
                    FROM shipment_exceptions WHERE id=existing_id;
                    existing_id := NULL;
                END IF;

                IF p_condition='Active' AND existing_id IS NULL
                   AND NOT (p_rule='RecordedArrivalLate' AND EXISTS (
                       SELECT 1 FROM shipment_exceptions previous
                       JOIN shipment_exception_history disposition
                         ON disposition.exception_id=previous.id
                        AND disposition.organization_id=previous.organization_id
                        AND disposition.action='Resolved'
                        AND disposition.actor_kind='Human'
                       WHERE previous.organization_id=p_org
                         AND previous.shipment_id=p_shipment
                         AND previous.rule_code='RecordedArrivalLate'
                         AND previous.lifecycle_state='Resolved'
                         AND previous.reference_at_utc IS NOT DISTINCT FROM p_reference
                         AND previous.threshold_at_utc IS NOT DISTINCT FROM p_threshold
                   )) THEN
                    SELECT COALESCE(MAX(occurrence),0)+1 INTO next_occurrence
                    FROM shipment_exceptions WHERE organization_id=p_org
                        AND shipment_id=p_shipment AND rule_code=p_rule;
                    INSERT INTO shipment_exceptions
                        (organization_id,shipment_id,rule_code,occurrence,condition_state,
                         lifecycle_state,severity,confidence,reason_code,source_fingerprint,
                         shipment_version,tracking_input_revision,correction_generation,
                         reference_at_utc,threshold_at_utc,next_evaluation_at_utc,evidence)
                    VALUES (p_org,p_shipment,p_rule,next_occurrence,p_condition,'Open',p_severity,
                        p_confidence,p_reason,p_fingerprint,p_ship_version,p_input_revision,
                        p_generation,p_reference,p_threshold,p_next,payload)
                    RETURNING id INTO exception_id;
                    INSERT INTO shipment_exception_history
                        (organization_id,exception_id,action,new_state,actor_kind,source_fingerprint)
                    VALUES (p_org,exception_id,'Opened','Open','System',p_fingerprint);
                ELSIF existing_id IS NOT NULL THEN
                    IF p_condition='Inactive' AND
                       (existing_state='Open' OR p_reason='shipment_not_eligible') THEN
                        -- Cancellation and archive clear even acknowledged
                        -- occurrences because manual actions are disallowed
                        -- on archived shipments. Otherwise, acknowledged
                        -- occurrences remain inactive for Manager review.
                        UPDATE shipment_exceptions SET
                            lifecycle_state='Resolved',condition_state=p_condition,
                            resolved_at_utc=clock_timestamp(),last_evaluated_at_utc=clock_timestamp(),
                            last_confirmed_inactive_at_utc=clock_timestamp(),
                            version=version+1,source_fingerprint=p_fingerprint,
                            confidence=p_confidence,severity=p_severity,
                            reason_code=p_reason,evidence=payload,
                            reference_at_utc=p_reference,threshold_at_utc=p_threshold,
                            next_evaluation_at_utc=p_next,
                            shipment_version=p_ship_version,tracking_input_revision=p_input_revision,
                            correction_generation=p_generation
                        WHERE id=existing_id;
                        INSERT INTO shipment_exception_history
                            (organization_id,exception_id,action,old_state,new_state,actor_kind,
                             source_fingerprint)
                        VALUES (p_org,existing_id,'AutoResolved',existing_state,
                            'Resolved','System',p_fingerprint);
                    ELSE
                        UPDATE shipment_exceptions SET
                            condition_state=p_condition,severity=p_severity,confidence=p_confidence,
                            reason_code=p_reason,source_fingerprint=p_fingerprint,
                            shipment_version=p_ship_version,tracking_input_revision=p_input_revision,
                            correction_generation=p_generation,
                            reference_at_utc=p_reference,threshold_at_utc=p_threshold,
                            next_evaluation_at_utc=p_next,evidence=payload,
                            last_evaluated_at_utc=clock_timestamp(),
                            last_confirmed_inactive_at_utc=CASE WHEN p_condition='Inactive'
                                THEN clock_timestamp() ELSE last_confirmed_inactive_at_utc END,
                            version=version+1
                        WHERE id=existing_id;
                        INSERT INTO shipment_exception_history
                            (organization_id,exception_id,action,old_state,new_state,actor_kind,
                             source_fingerprint)
                        VALUES (p_org,existing_id,
                            CASE WHEN p_condition IN ('Stale','Unknown') THEN 'InputUnavailable'
                                 ELSE 'Reevaluated' END,
                            existing_state,existing_state,'System',p_fingerprint);
                    END IF;
                END IF;

                INSERT INTO shipment_exception_evaluations
                    (organization_id,shipment_id,rule_code,rule_policy_version,
                     condition_state,source_fingerprint,shipment_version,
                     tracking_input_revision,correction_generation,next_evaluation_at_utc)
                VALUES (p_org,p_shipment,p_rule,'m11-rules-v1',p_condition,
                    p_fingerprint,p_ship_version,p_input_revision,p_generation,p_next)
                ON CONFLICT (organization_id,shipment_id,rule_code)
                DO UPDATE SET rule_policy_version=EXCLUDED.rule_policy_version,
                    condition_state=EXCLUDED.condition_state,
                    source_fingerprint=EXCLUDED.source_fingerprint,
                    shipment_version=EXCLUDED.shipment_version,
                    tracking_input_revision=EXCLUDED.tracking_input_revision,
                    correction_generation=EXCLUDED.correction_generation,
                    next_evaluation_at_utc=EXCLUDED.next_evaluation_at_utc,
                    evaluated_at_utc=clock_timestamp();
            END $eval$;

            CREATE FUNCTION m11_apply_signed_action(
                p_org uuid,p_exception uuid,p_actor uuid,p_session uuid,p_action varchar,
                p_expected_version bigint,p_reason varchar,p_idempotency uuid,
                p_nonce uuid,p_expires bigint,p_signature varchar)
            RETURNS TABLE(result_code text,result_version bigint)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $action$
            DECLARE
                signing_key bytea;
                digest_value text;
                message text;
                expected_sig text;
                now_epoch bigint := floor(extract(epoch from clock_timestamp()))::bigint;
                actor_valid boolean;
                row_state text;
                row_condition text;
                row_version bigint;
                row_shipment uuid;
                row_shipment_state text;
                current_org text;
                row_shipment_version bigint;
                row_input_revision bigint;
                row_generation bigint;
                saved_digest text;
                saved_version bigint;
                saved_exception uuid;
                saved_actor uuid;
                row_rule varchar(40);
                row_fingerprint char(64);
            BEGIN
                IF session_user <> 'harborops_api'
                   OR p_org IS NULL OR p_exception IS NULL
                   OR p_actor IS NULL OR p_session IS NULL OR p_idempotency IS NULL
                   OR p_nonce IS NULL OR p_expires IS NULL OR p_signature IS NULL
                   OR p_action IS NULL OR p_expected_version IS NULL
                   OR p_signature !~ '^[0-9a-f]{64}$'
                   OR p_expected_version < 1
                   OR p_action NOT IN ('Acknowledge','Resolve')
                   OR p_reason IS NULL OR length(btrim(p_reason)) NOT BETWEEN 8 AND 240
                   OR p_reason <> btrim(p_reason) OR p_reason ~ '[[:cntrl:]]'
                   OR p_expires < now_epoch OR p_expires > now_epoch+60 THEN
                    RAISE EXCEPTION 'Exception action authority denied' USING ERRCODE='42501';
                END IF;
                SELECT secret INTO signing_key FROM m10_correction_proof_keys
                WHERE key_id=1 FOR SHARE;
                IF signing_key IS NULL THEN
                    RAISE EXCEPTION 'Exception action verifier unavailable' USING ERRCODE='PZ002';
                END IF;
                digest_value := encode(digest(convert_to(
                    'M11Action|' || replace(p_org::text,'-','') || '|' ||
                    replace(p_exception::text,'-','') || '|' ||
                    replace(p_actor::text,'-','') || '|' ||
                    replace(p_session::text,'-','') || '|' || p_action || '|' ||
                    p_expected_version::text || '|' || p_reason,'UTF8'),'sha256'),'hex');
                message := 'm11-action-v1|' || replace(p_nonce::text,'-','') ||
                    '|' || p_expires::text || '|' || replace(p_idempotency::text,'-','') ||
                    '|' || digest_value;
                expected_sig := encode(hmac(convert_to(message,'UTF8'),signing_key,'sha256'),'hex');
                IF p_signature IS DISTINCT FROM expected_sig THEN
                    RAISE EXCEPTION 'Exception action authority denied' USING ERRCODE='42501';
                END IF;

                -- Match the established organization-first lock order before
                -- locking the session or exception row.
                SELECT status INTO current_org FROM organizations
                    WHERE id=p_org FOR SHARE;
                IF current_org IS DISTINCT FROM 'Active' THEN
                    RAISE EXCEPTION 'Exception action authority denied' USING ERRCODE='42501';
                END IF;
                -- A valid UUID is not a grant. Lock the live session and role before mutation.
                SELECT EXISTS (
                    SELECT 1 FROM auth_sessions s
                    JOIN users u ON u.id=s.user_id
                    JOIN organization_memberships m
                      ON m.user_id=s.user_id AND m.organization_id=s.selected_organization_id
                    JOIN organizations o ON o.id=s.selected_organization_id
                    WHERE s.id=p_session AND s.user_id=p_actor
                      AND s.selected_organization_id=p_org
                      AND s.revoked_at_utc IS NULL
                      AND s.idle_expires_at_utc>clock_timestamp()
                      AND s.absolute_expires_at_utc>clock_timestamp()
                      AND u.status='Active' AND u.security_version=s.security_version
                      AND m.status='Active' AND m.role='Manager'
                      AND o.status='Active'
                    FOR SHARE OF s,u,m,o
                ) INTO actor_valid;
                IF NOT actor_valid THEN
                    RAISE EXCEPTION 'Exception action authority denied' USING ERRCODE='42501';
                END IF;

                -- Read the immutable scope first, then lock the shipment before
                -- the exception, matching the evaluation mutation lock order.
                SELECT shipment_id INTO row_shipment FROM shipment_exceptions
                    WHERE id=p_exception AND organization_id=p_org;
                IF row_shipment IS NULL THEN
                    RAISE EXCEPTION 'Exception not found' USING ERRCODE='PZ003';
                END IF;
                SELECT record_status INTO row_shipment_state FROM shipments
                    WHERE organization_id=p_org AND id=row_shipment FOR SHARE;
                IF row_shipment_state IS DISTINCT FROM 'Active' THEN
                    RAISE EXCEPTION 'Exception scope inactive' USING ERRCODE='42501';
                END IF;
                SELECT lifecycle_state,condition_state,version,shipment_version,
                    tracking_input_revision,correction_generation,rule_code,source_fingerprint
                INTO row_state,row_condition,row_version,row_shipment_version,
                    row_input_revision,row_generation,row_rule,row_fingerprint
                FROM shipment_exceptions
                WHERE id=p_exception AND organization_id=p_org FOR UPDATE;

                SELECT request_digest,response_version,exception_id,actor_user_id
                INTO saved_digest,saved_version,saved_exception,saved_actor
                FROM shipment_exception_replays
                WHERE organization_id=p_org AND idempotency_key=p_idempotency;
                IF FOUND THEN
                    IF saved_exception <> p_exception OR saved_actor <> p_actor
                        OR saved_digest <> digest_value THEN
                        RAISE EXCEPTION 'Exception action replay conflict' USING ERRCODE='PZ004';
                    END IF;
                    RETURN QUERY SELECT 'Replayed'::text,saved_version;
                    RETURN;
                END IF;
                IF row_version <> p_expected_version THEN
                    RAISE EXCEPTION 'Exception version changed' USING ERRCODE='PZ004';
                END IF;
                IF p_action='Acknowledge' AND
                    (row_state <> 'Open' OR row_condition <> 'Active') THEN
                    RAISE EXCEPTION 'Exception cannot be acknowledged' USING ERRCODE='PZ005';
                END IF;
                IF p_action='Resolve' AND
                    (row_state <> 'Acknowledged' OR NOT (
                        row_condition='Inactive' OR
                        (row_rule='RecordedArrivalLate' AND row_condition='Active'))) THEN
                    RAISE EXCEPTION 'Exception cannot be resolved' USING ERRCODE='PZ005';
                END IF;
                -- Prevent actions against a rule that has reached an elapsed
                -- time boundary or whose checkpoint no longer matches the
                -- occurrence under review. Require reevaluation first.
                IF NOT EXISTS (
                    SELECT 1 FROM shipment_exception_evaluations e
                    WHERE e.organization_id=p_org AND e.shipment_id=row_shipment
                      AND e.rule_code=row_rule
                      AND e.source_fingerprint=row_fingerprint
                      AND (e.next_evaluation_at_utc IS NULL
                        OR e.next_evaluation_at_utc>clock_timestamp())
                ) THEN
                    RAISE EXCEPTION 'Exception reevaluation required' USING ERRCODE='PZ004';
                END IF;
                -- An equipment condition must not be acted on after a
                -- rebuilt projection, even if M10 input and correction counters
                -- remain unchanged. The current accepted policy and checkpoint
                -- must still describe the projection being reviewed.
                IF row_rule='EquipmentConflict' AND NOT EXISTS (
                    SELECT 1 FROM shipment_tracking_projections p
                    JOIN shipment_exception_evaluations e
                      ON e.organization_id=p.organization_id
                     AND e.shipment_id=p.shipment_id
                     AND e.rule_code=row_rule
                    WHERE p.organization_id=p_org AND p.shipment_id=row_shipment
                      AND p.policy_version='m10-equipment-v2'
                      AND p.input_revision=row_input_revision
                      AND p.correction_generation=row_generation
                      AND p.refreshed_at_utc <= e.evaluated_at_utc
                ) THEN
                    RAISE EXCEPTION 'Equipment reevaluation required' USING ERRCODE='PZ004';
                END IF;
                IF row_shipment_version IS DISTINCT FROM (
                    SELECT version FROM shipments WHERE organization_id=p_org
                        AND id=row_shipment
                ) OR row_input_revision IS DISTINCT FROM COALESCE((
                    SELECT input_revision FROM shipment_tracking_input_versions
                    WHERE organization_id=p_org AND shipment_id=row_shipment
                ),0) OR row_generation IS DISTINCT FROM COALESCE((
                    SELECT generation FROM shipment_tracking_correction_heads
                    WHERE organization_id=p_org AND shipment_id=row_shipment
                ),0) THEN
                    RAISE EXCEPTION 'Exception source changed' USING ERRCODE='PZ004';
                END IF;

                DELETE FROM shipment_exception_proof_nonces
                WHERE nonce IN (
                    SELECT nonce FROM shipment_exception_proof_nonces
                    WHERE expires_at_utc < clock_timestamp() - interval '1 minute'
                    ORDER BY expires_at_utc,nonce LIMIT 64 FOR UPDATE SKIP LOCKED
                );
                INSERT INTO shipment_exception_proof_nonces
                    (nonce,organization_id,shipment_id,operation,expires_at_utc)
                VALUES (p_nonce,p_org,row_shipment,'Disposition',to_timestamp(p_expires));

                UPDATE shipment_exceptions SET
                    lifecycle_state=CASE WHEN p_action='Acknowledge'
                        THEN 'Acknowledged' ELSE 'Resolved' END,
                    resolved_at_utc=CASE WHEN p_action='Resolve'
                        THEN clock_timestamp() ELSE NULL END,
                    version=version+1
                WHERE id=p_exception;
                INSERT INTO shipment_exception_history
                    (organization_id,exception_id,action,old_state,new_state,actor_kind,
                     actor_user_id,actor_session_id,reason,source_fingerprint)
                SELECT p_org,p_exception,CASE WHEN p_action='Acknowledge'
                    THEN 'Acknowledged' ELSE 'Resolved' END,row_state,
                    CASE WHEN p_action='Acknowledge' THEN 'Acknowledged' ELSE 'Resolved' END,
                    'Human',p_actor,p_session,p_reason,source_fingerprint
                FROM shipment_exceptions WHERE id=p_exception;
                BEGIN
                    INSERT INTO audit_entries
                        (organization_id,event_type,event_schema_version,outcome,actor_kind,
                         actor_user_id,actor_session_id,actor_role,
                         resource_type,resource_id,correlation_id,metadata)
                    VALUES (p_org,CASE WHEN p_action='Acknowledge'
                        THEN 'shipment.exception_acknowledged'
                        ELSE 'shipment.exception_resolved' END,1,'Succeeded','Human',
                        p_actor,p_session,'Manager','shipment_exceptions',p_exception,
                        replace(p_nonce::text,'-',''),
                        jsonb_build_object('operation','update','changedFields',
                            jsonb_build_array('lifecycle_state')));
                EXCEPTION WHEN OTHERS THEN
                    RAISE EXCEPTION 'Exception action audit unavailable' USING ERRCODE='PZ001';
                END;
                INSERT INTO shipment_exception_replays
                    (organization_id,exception_id,actor_user_id,idempotency_key,
                     request_digest,response_version,action)
                VALUES (p_org,p_exception,p_actor,p_idempotency,digest_value,
                    row_version+1,p_action);
                RETURN QUERY SELECT 'Applied'::text,row_version+1;
            END $action$;
            REVOKE ALL ON FUNCTION m11_apply_signed_evaluation(
                uuid,uuid,varchar,varchar,varchar,varchar,varchar,varchar,
                timestamptz,timestamptz,timestamptz,bigint,bigint,bigint,
                uuid,bigint,varchar) FROM PUBLIC;
            REVOKE ALL ON FUNCTION m11_apply_signed_action(
                uuid,uuid,uuid,uuid,varchar,bigint,varchar,uuid,uuid,bigint,varchar)
                FROM PUBLIC;
            REVOKE ALL ON shipment_exception_proof_nonces FROM PUBLIC;

            -- Bind SECURITY DEFINER lookups to the schema where the migration
            -- was applied. This also supports isolated populated-upgrade tests
            -- whose accepted M10 objects live outside the public schema.
            DO $m11_schema$
            DECLARE target_schema text := current_schema();
            BEGIN
                EXECUTE format(
                    'ALTER FUNCTION %1$I.m11_apply_signed_evaluation(
                        uuid,uuid,varchar,varchar,varchar,varchar,varchar,varchar,
                        timestamptz,timestamptz,timestamptz,bigint,bigint,bigint,
                        uuid,bigint,varchar)
                     SET search_path TO pg_catalog,%1$I,public', target_schema);
                EXECUTE format(
                    'ALTER FUNCTION %1$I.m11_apply_signed_action(
                        uuid,uuid,uuid,uuid,varchar,bigint,varchar,uuid,uuid,bigint,varchar)
                     SET search_path TO pg_catalog,%1$I,public', target_schema);
            END $m11_schema$;

            -- Direct API reads need a server-signed, transaction-scoped proof.
            -- An arbitrary user-controlled PostgreSQL setting is not authority.
            CREATE FUNCTION m11_read_allowed(p_org uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $read$
            DECLARE
                fields text[];
                secret_key bytea;
                expires bigint;
                epoch bigint;
                actor uuid;
                selected_session uuid;
                expected text;
            BEGIN
                IF session_user<>'harborops_api' OR p_org IS NULL THEN RETURN false; END IF;
                fields := string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                IF COALESCE(array_length(fields,1),0)<>5
                    OR fields[1] IS DISTINCT FROM p_org::text
                    OR fields[4] !~ '^[0-9]{10,11}$'
                    OR fields[5] !~ '^[0-9a-f]{64}$' THEN RETURN false; END IF;
                actor := fields[2]::uuid;
                selected_session := fields[3]::uuid;
                expires := fields[4]::bigint;
                epoch := floor(extract(epoch FROM clock_timestamp()))::bigint;
                IF expires<epoch OR expires>epoch+45 THEN RETURN false; END IF;
                SELECT secret INTO secret_key FROM m10_correction_proof_keys WHERE key_id=1;
                IF secret_key IS NULL THEN RETURN false; END IF;
                expected := encode(public.hmac(convert_to(
                    'm11-read-v1|'||replace(p_org::text,'-','')||
                    '|'||replace(actor::text,'-','')||
                    '|'||replace(selected_session::text,'-','')||
                    '|'||fields[4], 'UTF8'), secret_key,'sha256'),'hex');
                IF expected IS DISTINCT FROM fields[5] THEN RETURN false; END IF;
                PERFORM 1 FROM auth_sessions s
                    JOIN users u ON u.id=s.user_id
                    JOIN organizations o ON o.id=p_org
                    JOIN organization_memberships m
                        ON m.organization_id=p_org AND m.user_id=u.id
                WHERE s.id=selected_session AND s.user_id=actor
                    AND s.selected_organization_id=p_org
                    AND s.revoked_at_utc IS NULL
                    AND s.idle_expires_at_utc>clock_timestamp()
                    AND s.absolute_expires_at_utc>clock_timestamp()
                    AND s.security_version=u.security_version
                    AND u.status='Active' AND o.status='Active'
                    AND m.status='Active';
                RETURN FOUND;
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $read$;

            -- Scheduled evaluation has a separate proof, not an interactive read grant.
            CREATE FUNCTION m11_due_allowed()
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $due$
            DECLARE
                fields text[];
                secret_key bytea;
                expires bigint;
                epoch bigint;
            BEGIN
                IF session_user<>'harborops_api' THEN RETURN false; END IF;
                fields := string_to_array(current_setting('harborops.m11_due_scope',true),'|');
                IF COALESCE(array_length(fields,1),0)<>2
                    OR fields[1] !~ '^[0-9]{10,11}$'
                    OR fields[2] !~ '^[0-9a-f]{64}$' THEN RETURN false; END IF;
                expires := fields[1]::bigint;
                epoch := floor(extract(epoch FROM clock_timestamp()))::bigint;
                IF expires<epoch OR expires>epoch+45 THEN RETURN false; END IF;
                SELECT secret INTO secret_key FROM m10_correction_proof_keys WHERE key_id=1;
                RETURN secret_key IS NOT NULL AND fields[2]=encode(public.hmac(
                    convert_to('m11-due-v1|'||fields[1],'UTF8'),
                    secret_key,'sha256'),'hex');
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $due$;

            REVOKE ALL ON FUNCTION m11_read_allowed(uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION m11_due_allowed() FROM PUBLIC;
            ALTER TABLE shipment_exceptions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE shipment_exception_history ENABLE ROW LEVEL SECURITY;
            ALTER TABLE shipment_exception_evaluations ENABLE ROW LEVEL SECURITY;
            CREATE POLICY m11_exception_read ON shipment_exceptions FOR SELECT
                USING (m11_read_allowed(organization_id));
            CREATE POLICY m11_history_read ON shipment_exception_history FOR SELECT
                USING (m11_read_allowed(organization_id));
            CREATE POLICY m11_evaluation_read ON shipment_exception_evaluations FOR SELECT
                USING (m11_read_allowed(organization_id) OR m11_due_allowed());
            DO $scope_schema$
            DECLARE target_schema text := current_schema();
            BEGIN
                EXECUTE format('ALTER FUNCTION %1$I.m11_read_allowed(uuid)
                    SET search_path TO pg_catalog,%1$I,public',target_schema);
                EXECUTE format('ALTER FUNCTION %1$I.m11_due_allowed()
                    SET search_path TO pg_catalog,%1$I,public',target_schema);
            END $scope_schema$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$
            BEGIN
                IF EXISTS(SELECT 1 FROM shipment_exceptions LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_exception_history LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_exception_replays LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_exception_evaluations LIMIT 1)
                   OR EXISTS(SELECT 1 FROM shipment_exception_proof_nonces LIMIT 1) THEN
                    RAISE EXCEPTION 'Retained M11 evidence prevents downgrade'
                        USING ERRCODE='23514';
                END IF;
            END $guard$;
            DROP FUNCTION m11_apply_signed_action(
                uuid,uuid,uuid,uuid,varchar,bigint,varchar,uuid,uuid,bigint,varchar);
            DROP FUNCTION m11_apply_signed_evaluation(
                uuid,uuid,varchar,varchar,varchar,varchar,varchar,varchar,
                timestamptz,timestamptz,timestamptz,bigint,bigint,bigint,
                uuid,bigint,varchar);
            DROP TABLE shipment_exception_proof_nonces;
            DROP TABLE shipment_exception_replays;
            DROP TABLE shipment_exception_history;
            DROP TABLE shipment_exception_evaluations;
            DROP TABLE shipment_exceptions;
            DROP FUNCTION m11_read_allowed(uuid);
            DROP FUNCTION m11_due_allowed();
            """);
    }
}
