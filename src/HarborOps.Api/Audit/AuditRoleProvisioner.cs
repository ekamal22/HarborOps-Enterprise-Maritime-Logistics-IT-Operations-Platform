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
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await EnsureRoleAsync(connection, "harborops_api", apiSecret, ct);
            await EnsureRoleAsync(connection, "harborops_worker", workerSecret, ct);
            await ExecuteAsync(connection, """
                REVOKE CREATE ON SCHEMA public FROM PUBLIC;
                GRANT CONNECT ON DATABASE harborops TO harborops_api, harborops_worker;
                GRANT USAGE ON SCHEMA public TO harborops_api, harborops_worker;

                -- The HTTP runtime requires ordinary app table operations, never DDL.
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO harborops_api;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO harborops_api;
                REVOKE ALL ON TABLE audit_entries FROM harborops_api;
                GRANT SELECT ON TABLE audit_entries TO harborops_api;
                GRANT EXECUTE ON FUNCTION
                    m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar) TO harborops_api;

                -- M9 expands worker grants only through reviewed migration policy.
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM harborops_worker;
                GRANT SELECT ON tracking_inbox_receipts, tracking_accepted_events,
                    tracking_inbox_work_items, integration_sources, integration_credentials,
                    organizations TO harborops_worker;
                GRANT UPDATE ON tracking_inbox_work_items TO harborops_worker;
                REVOKE ALL ON TABLE audit_entries FROM harborops_worker;
                REVOKE ALL ON FUNCTION
                    m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar)
                    FROM harborops_worker;
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
            ? $"ALTER ROLE {role} WITH LOGIN PASSWORD {passwordLiteral}"
            : $"CREATE ROLE {role} LOGIN PASSWORD {passwordLiteral}";
        await ExecuteAsync(connection, command, ct);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection,
        string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
