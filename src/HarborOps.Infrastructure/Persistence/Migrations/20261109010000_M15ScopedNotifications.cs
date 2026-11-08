using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261109010000_M15ScopedNotifications")]
public sealed class M15ScopedNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE m15_notification_outbox (
                source_history_id uuid PRIMARY KEY REFERENCES shipment_exception_history(id) ON DELETE RESTRICT,
                organization_id uuid NOT NULL,
                shipment_id uuid NOT NULL,
                exception_id uuid NOT NULL,
                event_type varchar(32) NOT NULL,
                rule_code varchar(40) NOT NULL,
                created_at_utc timestamptz NOT NULL,
                completed_at_utc timestamptz,
                CONSTRAINT fk_m15_outbox_exception FOREIGN KEY (exception_id,organization_id)
                    REFERENCES shipment_exceptions(id,organization_id) ON DELETE RESTRICT,
                CONSTRAINT fk_m15_outbox_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT ck_m15_outbox_type CHECK (event_type IN
                    ('ExceptionOpened','ExceptionResolved','ExceptionAutoResolved')),
                CONSTRAINT ck_m15_outbox_rule CHECK (rule_code IN
                    ('EquipmentConflict','ArrivalScheduleOverdue','RecordedArrivalLate','DepartureSlippageRisk'))
            );
            CREATE INDEX ix_m15_outbox_pending ON m15_notification_outbox
                (created_at_utc,source_history_id) WHERE completed_at_utc IS NULL;

            CREATE TABLE m15_user_notifications (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                source_history_id uuid NOT NULL
                    REFERENCES m15_notification_outbox(source_history_id) ON DELETE RESTRICT,
                organization_id uuid NOT NULL,
                recipient_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                membership_id uuid NOT NULL REFERENCES organization_memberships(id) ON DELETE RESTRICT,
                membership_version bigint NOT NULL CHECK (membership_version > 0),
                shipment_id uuid NOT NULL,
                exception_id uuid NOT NULL,
                event_type varchar(32) NOT NULL,
                rule_code varchar(40) NOT NULL,
                created_at_utc timestamptz NOT NULL,
                read_at_utc timestamptz,
                acknowledged_at_utc timestamptz,
                version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
                CONSTRAINT fk_m15_notification_exception FOREIGN KEY (exception_id,organization_id)
                    REFERENCES shipment_exceptions(id,organization_id) ON DELETE RESTRICT,
                CONSTRAINT fk_m15_notification_shipment FOREIGN KEY (organization_id,shipment_id)
                    REFERENCES shipments(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT uq_m15_event_recipient UNIQUE(source_history_id,recipient_user_id),
                CONSTRAINT ck_m15_ack_read CHECK
                    (acknowledged_at_utc IS NULL OR read_at_utc IS NOT NULL),
                CONSTRAINT ck_m15_receipt_order CHECK
                    (acknowledged_at_utc IS NULL OR acknowledged_at_utc >= read_at_utc)
            );
            CREATE INDEX ix_m15_recipient_feed ON m15_user_notifications
                (organization_id,recipient_user_id,created_at_utc DESC,id DESC);
            CREATE INDEX ix_m15_recipient_unread ON m15_user_notifications
                (organization_id,recipient_user_id) WHERE read_at_utc IS NULL;

            CREATE TABLE m15_notification_replays (
                organization_id uuid NOT NULL,
                recipient_user_id uuid NOT NULL,
                idempotency_key uuid NOT NULL,
                notification_id uuid NOT NULL REFERENCES m15_user_notifications(id) ON DELETE RESTRICT,
                actor_session_id uuid NOT NULL,
                operation varchar(16) NOT NULL CHECK(operation IN ('MarkRead','Acknowledge')),
                expected_version bigint NOT NULL CHECK(expected_version > 0),
                result_version bigint NOT NULL CHECK(result_version > 0),
                occurred_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                PRIMARY KEY(organization_id,recipient_user_id,idempotency_key)
            );

            CREATE FUNCTION m15_capture_exception_history() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $m15$
            BEGIN
                IF NEW.action IN ('Opened','Resolved','AutoResolved') THEN
                    INSERT INTO m15_notification_outbox
                        (source_history_id,organization_id,shipment_id,exception_id,
                         event_type,rule_code,created_at_utc)
                    SELECT NEW.id,NEW.organization_id,e.shipment_id,e.id,
                        CASE NEW.action
                            WHEN 'Opened' THEN 'ExceptionOpened'
                            WHEN 'Resolved' THEN 'ExceptionResolved'
                            ELSE 'ExceptionAutoResolved'
                        END,e.rule_code,NEW.occurred_at_utc
                    FROM shipment_exceptions e
                    WHERE e.id=NEW.exception_id AND e.organization_id=NEW.organization_id
                    ON CONFLICT (source_history_id) DO NOTHING;
                END IF;
                RETURN NEW;
            END $m15$;
            CREATE TRIGGER tr_m15_capture_m11_history
                AFTER INSERT ON shipment_exception_history FOR EACH ROW
                EXECUTE FUNCTION m15_capture_exception_history();

            -- The API-hosted dispatcher calls this narrow, bounded, atomic function.
            -- SKIP LOCKED permits multiple dispatchers without recipient duplicates.
            CREATE FUNCTION m15_deliver_pending(p_limit integer)
            RETURNS integer LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $m15$
            DECLARE
                evt record;
                delivered integer := 0;
            BEGIN
                IF session_user <> 'harborops_api' OR p_limit IS NULL
                    OR p_limit NOT BETWEEN 1 AND 20 THEN
                    RAISE EXCEPTION 'Notification dispatcher unavailable'
                        USING ERRCODE='42501';
                END IF;
                FOR evt IN
                    SELECT * FROM m15_notification_outbox
                    WHERE completed_at_utc IS NULL
                    ORDER BY created_at_utc,source_history_id
                    FOR UPDATE SKIP LOCKED LIMIT p_limit
                LOOP
                    INSERT INTO m15_user_notifications
                        (source_history_id,organization_id,recipient_user_id,
                         membership_id,membership_version,shipment_id,exception_id,
                         event_type,rule_code,created_at_utc)
                    SELECT evt.source_history_id,evt.organization_id,m.user_id,
                        m.id,m.version,evt.shipment_id,evt.exception_id,
                        evt.event_type,evt.rule_code,evt.created_at_utc
                    FROM organizations o
                    JOIN shipments s ON s.organization_id=o.id
                        AND s.id=evt.shipment_id
                    JOIN organization_memberships m ON m.organization_id=o.id
                    JOIN users u ON u.id=m.user_id
                    WHERE o.id=evt.organization_id AND o.status='Active'
                        AND m.status='Active' AND u.status='Active'
                        AND m.role IN ('Analyst','Manager','Auditor')
                    ON CONFLICT (source_history_id,recipient_user_id) DO NOTHING;
                    UPDATE m15_notification_outbox SET completed_at_utc=clock_timestamp()
                        WHERE source_history_id=evt.source_history_id;
                    delivered := delivered+1;
                END LOOP;
                RETURN delivered;
            END $m15$;

            -- Reuses M11's accepted session-bound signed read scope. A plain
            -- SQL session without a valid HTTP-issued scope cannot enumerate rows.
            CREATE FUNCTION m15_can_read(p_org uuid,p_recipient uuid,p_membership uuid,
                p_membership_version bigint,p_shipment uuid)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $m15$
            DECLARE
                fields text[];
            BEGIN
                IF session_user <> 'harborops_api'
                    OR NOT m11_read_allowed(p_org) THEN RETURN false; END IF;
                fields := string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                IF fields[2]::uuid IS DISTINCT FROM p_recipient THEN RETURN false; END IF;
                RETURN EXISTS (
                    SELECT 1 FROM organization_memberships m
                    JOIN users u ON u.id=m.user_id
                    JOIN organizations o ON o.id=m.organization_id
                    JOIN shipments s ON s.organization_id=o.id AND s.id=p_shipment
                    WHERE m.id=p_membership AND m.organization_id=p_org
                        AND m.user_id=p_recipient AND m.version=p_membership_version
                        AND m.status='Active' AND o.status='Active' AND u.status='Active'
                        AND m.role IN ('Analyst','Manager','Auditor')
                );
            EXCEPTION WHEN OTHERS THEN RETURN false;
            END $m15$;

            ALTER TABLE m15_notification_outbox ENABLE ROW LEVEL SECURITY;
            ALTER TABLE m15_user_notifications ENABLE ROW LEVEL SECURITY;
            ALTER TABLE m15_notification_replays ENABLE ROW LEVEL SECURITY;
            CREATE POLICY m15_personal_read ON m15_user_notifications FOR SELECT
                USING (m15_can_read(organization_id,recipient_user_id,
                    membership_id,membership_version,shipment_id));

            -- All state changes and acknowledgment audit are one SQL transaction.
            -- The effective SQL login never has direct notification UPDATE rights.
            CREATE FUNCTION m15_apply_personal_action(
                p_org uuid,p_user uuid,p_session uuid,p_notification uuid,
                p_action varchar,p_expected_version bigint,p_idempotency uuid,p_correlation varchar)
            RETURNS TABLE(result_code text,result_version bigint)
            LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $m15$
            DECLARE
                fields text[];
                item record;
                old_replay record;
                next_version bigint;
                current_role text;
                now_utc timestamptz;
                changing boolean;
            BEGIN
                IF session_user <> 'harborops_api'
                    OR p_action NOT IN ('MarkRead','Acknowledge')
                    OR p_expected_version IS NULL OR p_expected_version < 1
                    OR p_idempotency IS NULL
                    OR p_idempotency='00000000-0000-0000-0000-000000000000'::uuid
                    OR p_correlation IS NULL OR length(p_correlation) NOT BETWEEN 1 AND 64
                    OR NOT m11_read_allowed(p_org) THEN
                    RETURN QUERY SELECT 'Forbidden'::text,0::bigint; RETURN;
                END IF;
                fields := string_to_array(current_setting('harborops.m11_read_scope',true),'|');
                IF fields[2]::uuid <> p_user OR fields[3]::uuid <> p_session THEN
                    RETURN QUERY SELECT 'Forbidden'::text,0::bigint; RETURN;
                END IF;

                -- Match accepted M2 organization-first lock discipline before a write.
                PERFORM 1 FROM organizations o
                    WHERE o.id=p_org AND o.status='Active' FOR SHARE;
                IF NOT FOUND THEN
                    RETURN QUERY SELECT 'Forbidden'::text,0::bigint; RETURN;
                END IF;
                -- Hold the live identity and session against concurrent revocation
                -- through commit, after acquiring the organization row lock.
                PERFORM 1 FROM auth_sessions a JOIN users u ON u.id=a.user_id
                    WHERE a.id=p_session AND a.user_id=p_user
                        AND a.selected_organization_id=p_org
                        AND a.revoked_at_utc IS NULL
                        AND a.idle_expires_at_utc>clock_timestamp()
                        AND a.absolute_expires_at_utc>clock_timestamp()
                        AND a.security_version=u.security_version
                        AND u.status='Active'
                    FOR SHARE OF a,u;
                IF NOT FOUND THEN
                    RETURN QUERY SELECT 'Forbidden'::text,0::bigint; RETURN;
                END IF;
                SELECT n.* INTO item FROM m15_user_notifications n
                    WHERE n.id=p_notification AND n.organization_id=p_org
                        AND n.recipient_user_id=p_user FOR UPDATE;
                IF NOT FOUND THEN
                    RETURN QUERY SELECT 'NotFound'::text,0::bigint; RETURN;
                END IF;
                -- Hold the exact membership version against role changes or removal.
                PERFORM 1 FROM organization_memberships member
                    WHERE member.id=item.membership_id
                        AND member.user_id=p_user
                        AND member.organization_id=p_org
                        AND member.status='Active'
                        AND member.version=item.membership_version
                        AND member.role IN ('Analyst','Manager','Auditor')
                    FOR SHARE;
                IF NOT FOUND OR NOT m15_can_read(p_org,p_user,item.membership_id,
                    item.membership_version,item.shipment_id) THEN
                    RETURN QUERY SELECT 'NotFound'::text,0::bigint; RETURN;
                END IF;

                SELECT r.* INTO old_replay FROM m15_notification_replays r
                    WHERE r.organization_id=p_org AND r.recipient_user_id=p_user
                        AND r.idempotency_key=p_idempotency;
                IF FOUND THEN
                    IF old_replay.notification_id=p_notification
                        AND old_replay.actor_session_id=p_session
                        AND old_replay.operation=p_action
                        AND old_replay.expected_version=p_expected_version THEN
                        RETURN QUERY SELECT 'Replayed'::text,old_replay.result_version;
                    ELSE
                        RETURN QUERY SELECT 'Conflict'::text,0::bigint;
                    END IF;
                    RETURN;
                END IF;
                IF item.version <> p_expected_version THEN
                    RETURN QUERY SELECT 'Conflict'::text,0::bigint; RETURN;
                END IF;
                now_utc := clock_timestamp();
                changing := CASE WHEN p_action='MarkRead'
                    THEN item.read_at_utc IS NULL ELSE item.acknowledged_at_utc IS NULL END;
                next_version := item.version;
                IF changing THEN
                    next_version := item.version+1;
                    UPDATE m15_user_notifications SET
                        read_at_utc=COALESCE(read_at_utc,now_utc),
                        acknowledged_at_utc=CASE WHEN p_action='Acknowledge'
                            THEN now_utc ELSE acknowledged_at_utc END,
                        version=next_version
                    WHERE id=p_notification;
                    IF p_action='Acknowledge' THEN
                        SELECT m.role INTO current_role FROM organization_memberships m
                            WHERE m.id=item.membership_id;
                        INSERT INTO audit_entries
                            (organization_id,event_type,event_schema_version,outcome,
                             actor_kind,actor_user_id,actor_session_id,actor_role,
                             resource_type,resource_id,correlation_id,metadata)
                        VALUES (p_org,'notification.receipt_acknowledged',1,'Succeeded',
                            'Human',p_user,p_session,current_role,
                            'notification',p_notification,p_correlation,
                            '{"action":"Acknowledge"}'::jsonb);
                    END IF;
                END IF;
                INSERT INTO m15_notification_replays
                    (organization_id,recipient_user_id,idempotency_key,
                     notification_id,actor_session_id,operation,expected_version,result_version)
                VALUES (p_org,p_user,p_idempotency,p_notification,p_session,p_action,
                    p_expected_version,next_version);
                RETURN QUERY SELECT 'Applied'::text,next_version;
            END $m15$;

            REVOKE ALL ON m15_notification_outbox,m15_user_notifications,
                m15_notification_replays FROM PUBLIC;
            REVOKE ALL ON FUNCTION m15_capture_exception_history() FROM PUBLIC;
            REVOKE ALL ON FUNCTION m15_deliver_pending(integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION m15_can_read(uuid,uuid,uuid,bigint,uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION m15_apply_personal_action(
                uuid,uuid,uuid,uuid,varchar,bigint,uuid,varchar) FROM PUBLIC;

            -- Historical M11 functions use explicit schema-qualified key access.
            -- Bind new functions to the installation schema for non-public test schemas.
            DO $m15_schema$
            DECLARE s text := current_schema();
            BEGIN
                EXECUTE format('ALTER FUNCTION %1$I.m15_capture_exception_history()
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m15_deliver_pending(integer)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m15_can_read(uuid,uuid,uuid,bigint,uuid)
                    SET search_path TO pg_catalog,%1$I,public',s);
                EXECUTE format('ALTER FUNCTION %1$I.m15_apply_personal_action(
                    uuid,uuid,uuid,uuid,varchar,bigint,uuid,varchar)
                    SET search_path TO pg_catalog,%1$I,public',s);
            END $m15_schema$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $m15_guard$
            BEGIN
                IF EXISTS(SELECT 1 FROM m15_notification_outbox)
                    OR EXISTS(SELECT 1 FROM m15_user_notifications)
                    OR EXISTS(SELECT 1 FROM m15_notification_replays) THEN
                    RAISE EXCEPTION 'M15 downgrade would destroy delivery evidence';
                END IF;
            END $m15_guard$;
            DROP TRIGGER tr_m15_capture_m11_history ON shipment_exception_history;
            DROP FUNCTION m15_capture_exception_history();
            DROP FUNCTION m15_apply_personal_action(
                uuid,uuid,uuid,uuid,varchar,bigint,uuid,varchar);
            DROP FUNCTION m15_can_read(uuid,uuid,uuid,bigint,uuid);
            DROP FUNCTION m15_deliver_pending(integer);
            DROP TABLE m15_notification_replays;
            DROP TABLE m15_user_notifications;
            DROP TABLE m15_notification_outbox;
            """);
    }
}
