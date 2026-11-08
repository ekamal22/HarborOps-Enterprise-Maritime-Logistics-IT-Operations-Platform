using System.Data;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Api.Audit;

// Executed only by the migration entry point using its privileged connection.
// Runtime connections cannot create roles or grant themselves privileges.
public static class AuditRoleProvisioner
{
    public static async Task ProvisionAsync(HarborOpsDbContext database,
        IConfiguration configuration, CancellationToken ct)
    {
        var apiSecret = configuration["AuditRoles:ApiPassword"];
        var workerSecret = configuration["AuditRoles:WorkerPassword"];
        if (string.IsNullOrEmpty(apiSecret) && string.IsNullOrEmpty(workerSecret))
            return; // Existing offline migration/test environments can provision roles separately.
        if (string.IsNullOrWhiteSpace(apiSecret) || string.IsNullOrWhiteSpace(workerSecret) ||
            string.Equals(apiSecret, workerSecret, StringComparison.Ordinal))
            throw new InvalidOperationException("Separate API and worker database credentials are required");

        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        var migratorSecret = new NpgsqlConnectionStringBuilder(connection.ConnectionString).Password;
        if (string.Equals(apiSecret, migratorSecret, StringComparison.Ordinal) ||
            string.Equals(workerSecret, migratorSecret, StringComparison.Ordinal))
            throw new InvalidOperationException("Database runtime credentials must differ from migration credentials");
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await EnsureRoleAsync(connection, "harborops_api", apiSecret, ct);
            await EnsureRoleAsync(connection, "harborops_worker", workerSecret, ct);
            await ExecuteAsync(connection, """
                REVOKE CREATE ON SCHEMA public FROM PUBLIC;
                DO $connect$
                BEGIN
                    -- The database name is not fixed: the migration target may be a renamed
                    -- or disposable verification database.
                    EXECUTE format('GRANT CONNECT ON DATABASE %I TO harborops_api, harborops_worker',
                        current_database());
                END $connect$;
                GRANT USAGE ON SCHEMA public TO harborops_api, harborops_worker;

                -- The HTTP runtime can read app data and modify only currently
                -- implemented mutable resources. No general DELETE, DDL or role grants.
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM harborops_api;
                GRANT SELECT ON ALL TABLES IN SCHEMA public TO harborops_api;
                GRANT INSERT ON users, user_credentials, auth_sessions, auth_events,
                    organizations, organization_memberships, ports, vessels, voyages,
                    containers, shipments, shipment_status_changes,
                    shipment_container_assignments, shipment_voyage_assignments,
                    integration_sources, integration_credentials,
                    tracking_inbox_receipts, tracking_accepted_events,
                    tracking_inbox_work_items, tracking_receipt_conflicts
                    TO harborops_api;
                GRANT UPDATE ON users, user_credentials, auth_sessions,
                    organizations, organization_memberships, ports, vessels, voyages,
                    containers, shipments, shipment_container_assignments,
                    shipment_voyage_assignments, integration_sources,
                    integration_credentials, tracking_inbox_work_items TO harborops_api;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO harborops_api;
                REVOKE ALL ON TABLE audit_entries FROM harborops_api;
                GRANT SELECT ON TABLE audit_entries TO harborops_api;
                -- M10 is strictly additive to the already accepted M9/M16 role matrix.
                -- The API may write only corrections, replay acknowledgments and
                -- derived projections. Immutable source/index/correction generation
                -- tables remain accessible only through migration-owned triggers.
                DO $m10_roles$
                BEGIN
                    IF to_regclass('public.shipment_tracking_projections') IS NOT NULL
                       AND to_regclass('public.shipment_tracking_corrections') IS NOT NULL
                       AND to_regclass('public.shipment_tracking_operation_replays') IS NOT NULL
                       AND to_regclass('public.shipment_projection_applications') IS NOT NULL THEN
                        GRANT INSERT ON shipment_tracking_operation_replays,
                            shipment_tracking_projections,
                            shipment_projection_applications TO harborops_api;
                        GRANT UPDATE ON shipment_tracking_projections TO harborops_api;
                    END IF;
                END $m10_roles$;
                -- The previous M10 grant is removed on upgraded databases too.
                DO $m10_authority$
                BEGIN
                    IF to_regclass('public.shipment_tracking_corrections') IS NOT NULL THEN
                        REVOKE INSERT ON shipment_tracking_corrections FROM harborops_api;
                    END IF;
                    IF to_regclass('public.m10_correction_proof_keys') IS NOT NULL THEN
                        REVOKE ALL ON m10_correction_proof_keys,
                            m10_correction_proof_nonces FROM harborops_api;
                        GRANT EXECUTE ON FUNCTION m10_apply_signed_correction(
                            uuid,uuid,uuid,uuid,bigint,uuid,uuid,varchar,varchar,
                            varchar,uuid,uuid,bigint,varchar) TO harborops_api;
                    END IF;
                END $m10_authority$;

                -- M11 tables are read-only to the HTTP login. All exception
                -- state changes and central audit writes pass through signed
                -- migration-owned functions. Nonces are never exposed.
                DO $m11_roles$
                BEGIN
                    IF to_regclass('public.shipment_exceptions') IS NOT NULL
                       AND to_regclass('public.shipment_exception_proof_nonces') IS NOT NULL THEN
                        REVOKE ALL ON shipment_exception_proof_nonces,
                            shipment_exception_replays FROM harborops_api;
                        GRANT EXECUTE ON FUNCTION m11_read_allowed(uuid),
                            m11_due_allowed() TO harborops_api;
                        GRANT EXECUTE ON FUNCTION m11_apply_signed_evaluation(
                            uuid,uuid,varchar,varchar,varchar,varchar,varchar,varchar,
                            timestamptz,timestamptz,timestamptz,bigint,bigint,bigint,
                            uuid,bigint,varchar) TO harborops_api;
                        GRANT EXECUTE ON FUNCTION m11_apply_signed_action(
                            uuid,uuid,uuid,uuid,varchar,bigint,varchar,uuid,uuid,bigint,varchar)
                            TO harborops_api;
                    END IF;
                END $m11_roles$;

                GRANT EXECUTE ON FUNCTION
                    m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar) TO harborops_api;
                DO $m12_export$
                BEGIN
                    IF to_regclass('public.m12_export_proof_keys') IS NOT NULL THEN
                        REVOKE ALL ON TABLE public.m12_export_proof_keys
                            FROM harborops_api, harborops_worker;
                        GRANT EXECUTE ON FUNCTION m12_record_shipment_export(
                            uuid,uuid,uuid,integer,bigint,varchar,varchar,bigint,varchar)
                            TO harborops_api;
                        REVOKE ALL ON FUNCTION m12_record_shipment_export(
                            uuid,uuid,uuid,integer,bigint,varchar,varchar,bigint,varchar)
                            FROM harborops_worker;
                    END IF;
                END $m12_export$;

                -- M13 reporting storage is scoped by signed session RLS. Grants
                -- mutate only through the Administrator-authorized definer.
                DO $m13_roles$
                BEGIN
                    IF to_regclass('public.operational_kpi_reports') IS NOT NULL THEN
                        GRANT SELECT, INSERT, DELETE ON operational_kpi_reports,
                            operational_kpi_members TO harborops_api;
                        GRANT SELECT ON operational_kpi_auditor_grants TO harborops_api;
                        GRANT EXECUTE ON FUNCTION m13_scoped_role(uuid),
                            m13_scope_matches(uuid,uuid,uuid),m13_can_view(uuid),
                            m13_can_manage(uuid),m13_member_access(uuid,uuid),
                            m13_set_auditor_grant(uuid,uuid,uuid,uuid,boolean),
                            m13_prune_expired(uuid,integer),
                            m13_lock_auditor_grant(uuid,uuid,uuid),
                            m13_lock_report(uuid,uuid,uuid,uuid,varchar)
                            TO harborops_api;
                    END IF;
                END $m13_roles$;

                -- Worker starts read-only against the accepted M8 schema.
                -- M9-specific column grants are activated only if M9 is present.
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM harborops_worker;
                -- Exactly the accepted M9 worker read set: no source or credential tables.
                GRANT SELECT ON tracking_inbox_receipts, tracking_accepted_events,
                    tracking_inbox_work_items,
                    organizations, containers, shipments,
                    shipment_container_assignments TO harborops_worker;
                REVOKE ALL ON TABLE audit_entries FROM harborops_worker;
                REVOKE ALL ON FUNCTION
                    m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar)
                    FROM harborops_worker;
                DO $worker_grants$
                BEGIN
                    IF to_regclass('public.tracking_work_attempt_events') IS NOT NULL
                       AND to_regclass('public.tracking_processing_effects') IS NOT NULL THEN
                        GRANT SELECT ON tracking_work_attempt_events,
                            tracking_processing_effects TO harborops_worker;
                        -- M9 Manager redrive writes one immutable, attributed attempt
                        -- row in the same transaction as the work-item transition.
                        -- The worker remains unable to insert attempt records.
                        GRANT INSERT ON tracking_work_attempt_events TO harborops_api;
                        -- M9 attempt history is written exclusively by its
                        -- security-definer transition trigger.
                        GRANT INSERT ON tracking_processing_effects TO harborops_worker;
                        GRANT UPDATE (state,attempt_count,lease_token,lease_epoch,
                            lease_expires_at_utc,available_at_utc,redrive_generation,
                            last_error_code,completed_at_utc,resolution_kind,
                            updated_at_utc) ON tracking_inbox_work_items TO harborops_worker;
                        GRANT EXECUTE ON FUNCTION m9_lock_active_organization(uuid)
                            TO harborops_worker;

                        -- The API identity holds table-level UPDATE on work items and INSERT on
                        -- attempt history only so the Manager redrive can run in one transaction.
                        -- These guards confine that identity to exactly that transition and to
                        -- the attributed Redriven record, so a compromised HTTP runtime cannot
                        -- claim leases, finish work or forge worker attempt history. Other roles
                        -- (migrator, worker) are unaffected.
                        -- These functions also run nested inside the SECURITY DEFINER M9 transition
                        -- trigger (search_path=pg_catalog), so every relation is schema-qualified
                        -- through TG_TABLE_SCHEMA and no unqualified name is parsed.
                        CREATE OR REPLACE FUNCTION m16_api_scope_work_item() RETURNS trigger
                            LANGUAGE plpgsql SET search_path = pg_catalog AS $scope$
                        BEGIN
                            IF session_user = 'harborops_api' AND NOT COALESCE(
                                OLD.state IN ('NeedsReconciliation','DeadLetter')
                                AND NEW.state = 'Pending'
                                AND NEW.redrive_generation = OLD.redrive_generation + 1, false) THEN
                                RAISE EXCEPTION 'API role is limited to manual redrive'
                                    USING ERRCODE='23514';
                            END IF;
                            RETURN NEW;
                        END $scope$;
                        CREATE OR REPLACE FUNCTION m16_api_scope_attempt() RETURNS trigger
                            LANGUAGE plpgsql SET search_path = pg_catalog AS $scope$
                        DECLARE valid boolean := true;
                        BEGIN
                            IF session_user = 'harborops_api' THEN
                                -- A new intake is Pending at generation zero. It has never
                                -- been redriven, so allowing a matching Redriven record here
                                -- would let this runtime role forge immutable attempt history.
                                -- The M9 redrive path increments generation and resets the
                                -- attempt counter before inserting its attributed record.
                                valid := COALESCE(
                                    NEW.event_kind = 'Redriven'
                                    AND NEW.actor_user_id IS NOT NULL
                                    AND NEW.generation BETWEEN 1 AND 3
                                    AND NEW.attempt_count = 0
                                    AND NEW.reason_code = 'manual_redrive', false);
                                IF valid THEN
                                    EXECUTE format(
                                        'SELECT EXISTS (
                                            SELECT 1 FROM %1$I.tracking_inbox_work_items w
                                             WHERE w.receipt_id = $1 AND w.organization_id = $2
                                               AND w.source_id = $3 AND w.state = ''Pending''
                                               AND w.redrive_generation = $4 AND w.lease_epoch = $5)
                                          AND EXISTS (
                                            SELECT 1 FROM %1$I.organization_memberships m
                                             WHERE m.organization_id = $2 AND m.user_id = $6
                                               AND m.status = ''Active'' AND m.role = ''Manager'')',
                                        TG_TABLE_SCHEMA)
                                    INTO valid
                                    USING NEW.receipt_id, NEW.organization_id, NEW.source_id,
                                          NEW.generation, NEW.lease_epoch, NEW.actor_user_id;
                                END IF;
                                IF NOT valid THEN
                                    RAISE EXCEPTION 'API role may only record an attributed manual redrive'
                                        USING ERRCODE='23514';
                                END IF;
                            END IF;
                            RETURN NEW;
                        END $scope$;
                        REVOKE ALL ON FUNCTION m16_api_scope_work_item() FROM PUBLIC;
                        REVOKE ALL ON FUNCTION m16_api_scope_attempt() FROM PUBLIC;
                        DROP TRIGGER IF EXISTS m16_api_scope_work_item ON tracking_inbox_work_items;
                        CREATE TRIGGER m16_api_scope_work_item BEFORE UPDATE
                            ON tracking_inbox_work_items FOR EACH ROW
                            EXECUTE FUNCTION m16_api_scope_work_item();
                        DROP TRIGGER IF EXISTS m16_api_scope_attempt ON tracking_work_attempt_events;
                        CREATE TRIGGER m16_api_scope_attempt BEFORE INSERT
                            ON tracking_work_attempt_events FOR EACH ROW
                            EXECUTE FUNCTION m16_api_scope_attempt();
                    ELSE
                        -- M9 was rolled back: drop any scope guard that referenced its columns.
                        DROP TRIGGER IF EXISTS m16_api_scope_work_item ON tracking_inbox_work_items;
                        DROP FUNCTION IF EXISTS m16_api_scope_work_item();
                        DROP FUNCTION IF EXISTS m16_api_scope_attempt();
                    END IF;
                END $worker_grants$;
                """, ct);
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
        await ProvisionCorrectionKeyAsync(database,
            configuration["M10CorrectionProof:Key"], ct);
        await ProvisionExportKeyAsync(database,
            configuration["M12ExportProof:Key"], ct);
    }

    // The migration identity alone provisions the verifier key. Production API
    // requests fail closed when the key has not been provisioned.
    public static async Task ProvisionCorrectionKeyAsync(HarborOpsDbContext database,
        string? encodedKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(encodedKey)) return;
        byte[] secret;
        try { secret = Convert.FromBase64String(encodedKey); }
        catch (FormatException error)
        {
            throw new InvalidOperationException("Invalid correction proof key encoding", error);
        }
        if (secret.Length != 32)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
            throw new InvalidOperationException("Correction proof key must contain 32 bytes");
        }
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await using var check = new NpgsqlCommand(
                "SELECT to_regclass('public.m10_correction_proof_keys') IS NOT NULL", connection);
            if (await check.ExecuteScalarAsync(ct) is not true) return;
            await using var put = new NpgsqlCommand("""
                INSERT INTO public.m10_correction_proof_keys (key_id, secret)
                VALUES (1,@key)
                ON CONFLICT (key_id) DO UPDATE SET secret=EXCLUDED.secret
                """, connection);
            put.Parameters.AddWithValue("key", secret);
            await put.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
            if (close) await connection.CloseAsync();
        }
    }

    // Only the privileged migration identity provisions or rotates this key.
    // It must never be granted directly to the API/worker runtime identities.
    public static async Task ProvisionExportKeyAsync(HarborOpsDbContext database,
        string? encodedKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(encodedKey)) return;
        byte[] secret;
        try { secret = Convert.FromBase64String(encodedKey); }
        catch (FormatException error)
        {
            throw new InvalidOperationException("Invalid export proof key encoding", error);
        }
        if (secret.Length != 32)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
            throw new InvalidOperationException("Export proof key must contain 32 bytes");
        }
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        try
        {
            if (close) await connection.OpenAsync(ct);
            await using var check = new NpgsqlCommand(
                "SELECT to_regclass('public.m12_export_proof_keys') IS NOT NULL", connection);
            if (await check.ExecuteScalarAsync(ct) is not true) return;
            await using var put = new NpgsqlCommand("""
                INSERT INTO public.m12_export_proof_keys (key_id, secret)
                VALUES (1,@key)
                ON CONFLICT (key_id) DO UPDATE SET secret=EXCLUDED.secret
                """, connection);
            put.Parameters.AddWithValue("key", secret);
            await put.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
            if (close && connection.State == System.Data.ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private static async Task EnsureRoleAsync(NpgsqlConnection connection,
        string role, string secret, CancellationToken ct)
    {
        await using var exists = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @name)", connection);
        exists.Parameters.AddWithValue("name", role);
        var found = (bool)(await exists.ExecuteScalarAsync(ct) ?? false);

        // PostgreSQL utility statements cannot accept a password parameter.
        // Quote it server-side using quote_literal, never string escaping in code.
        await using var quote = new NpgsqlCommand("SELECT quote_literal(@secret)", connection);
        quote.Parameters.AddWithValue("secret", secret);
        var passwordLiteral = (string)(await quote.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("Database credential quoting failed"));
        var command = found
            ? $"ALTER ROLE {role} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD {passwordLiteral}"
            : $"CREATE ROLE {role} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD {passwordLiteral}";
        await ExecuteAsync(connection, command, ct);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection,
        string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
