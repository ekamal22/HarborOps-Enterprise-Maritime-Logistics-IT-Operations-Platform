using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// The report build disables nested loops for one statement with SET LOCAL. Every
// pooled connection of the API must come back with default planner and timeout
// settings after a success, a refusal, a timeout, a cancellation and a rollback.
//
// Note: the shared restricted test logins disable connection pooling, so on those hosts
// each probe below is a fresh session and can only confirm the server defaults. The
// discriminating witness for a leaked setting is the pooled, no-reset host in
// M13AdmissionControlAcceptanceTests.PlannerAndTimeoutSettingsNeverOutliveABuild...,
// and its mutant planner-setting-made-session-wide.
internal static class PlannerGuard
{
    internal static async Task AssertDefaultAsync(IServiceProvider services, int connectionsToProbe = 8)
    {
        var scopes = new List<AsyncServiceScope>();
        try
        {
            var connections = new List<NpgsqlConnection>();
            for (var i = 0; i < connectionsToProbe; i++)
            {
                var scope = services.CreateAsyncScope();
                scopes.Add(scope);
                var context = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var connection = (NpgsqlConnection)context.Database.GetDbConnection();
                await connection.OpenAsync();
                connections.Add(connection);
            }
            foreach (var connection in connections)
            {
                await using var probe = new NpgsqlCommand(
                    "SELECT current_setting('enable_nestloop'),current_setting('statement_timeout')",
                    connection);
                await using var reader = await probe.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("on", reader.GetString(0));
                Assert.Equal("0", reader.GetString(1));
            }
        }
        finally
        {
            foreach (var scope in scopes) await scope.DisposeAsync();
        }
    }
}
