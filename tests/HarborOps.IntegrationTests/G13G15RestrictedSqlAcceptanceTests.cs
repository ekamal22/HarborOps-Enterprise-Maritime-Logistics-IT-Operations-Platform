using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13G15RestrictedSqlAcceptanceTests
{
    [Fact]
    public async Task ApiAndWorkerCannotEnumeratePersonalReplayAndDispatchInternals()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        foreach (var restricted in new[] { roles.Api, roles.Worker })
        {
            await using var connection = new NpgsqlConnection(restricted);
            await connection.OpenAsync();
            foreach (var name in new[]
            {
                "m15_notification_replays",
                "m15_notification_outbox"
            })
            {
                await using var query = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM " + name, connection);
                var rejected = await Assert.ThrowsAsync<PostgresException>(
                    () => query.ExecuteScalarAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                    rejected.SqlState);
            }
            foreach (var statement in new[]
            {
                "UPDATE m15_notification_replays SET result_version=result_version+1",
                "DELETE FROM m15_notification_replays",
                "UPDATE m15_notification_outbox SET completed_at_utc=clock_timestamp()"
            })
            {
                await using var command = new NpgsqlCommand(statement, connection);
                var denied = await Assert.ThrowsAsync<PostgresException>(
                    () => command.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
            }
        }
    }

    [Fact]
    public async Task RestrictedRolesCannotModifyMaterializedReportingMembership()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        foreach (var restricted in new[] { roles.Api, roles.Worker })
        {
            await using var connection = new NpgsqlConnection(restricted);
            await connection.OpenAsync();
            foreach (var statement in new[]
            {
                "UPDATE operational_kpi_members SET metric_code='K01'",
                "UPDATE operational_kpi_reports SET policy_version='m13-kpi-v1'",
                "UPDATE operational_kpi_auditor_grants SET active=true"
            })
            {
                await using var command = new NpgsqlCommand(statement, connection);
                var rejected = await Assert.ThrowsAsync<PostgresException>(
                    () => command.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                    rejected.SqlState);
            }
        }
    }
}
