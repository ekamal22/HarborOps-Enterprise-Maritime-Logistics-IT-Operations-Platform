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
                GRANT EXECUTE ON FUNCTION
                    m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar) TO harborops_api;

                -- Worker starts read-only against the accepted M8 schema.
                -- M9-specific column grants are activated only if M9 is present.
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM harborops_worker;
                GRANT SELECT ON tracking_inbox_receipts, tracking_accepted_events,
                    tracking_inbox_work_items, integration_sources, integration_credentials,
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
                    END IF;
                END $worker_grants$;
                """, ct);
        }
        finally
        {
            if (close) await connection.CloseAsync();
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
