using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class M13FaultRecoveryTests
{
    [ExclusiveM13PostgresFact]
    public async Task BlockedMemberMaterializationRollsBackAndRecovers()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var manager = fixture.Manager;
        using var analystSeed = fixture.Analyst;
        var seeded = await AssignmentHttpTests.Seed(fixture);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        analyst.Timeout = TimeSpan.FromMinutes(2);
        await ReferenceDataHttpTests.Login(analyst, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, fixture.A);
        var url = $"/api/v1/organizations/{fixture.A}/analytics/overview";
        await using var privileged = new NpgsqlConnection(roles.Admin);
        await privileged.OpenAsync();
        await using (var lockTx = await privileged.BeginTransactionAsync())
        {
            await using (var hold = new NpgsqlCommand(
                "LOCK TABLE shipments IN ACCESS EXCLUSIVE MODE", privileged, lockTx))
                await hold.ExecuteNonQueryAsync();
            var blocked = analyst.GetAsync(url);
            // The report source statement must time out while an exclusive
            // disposable test lock is held, never returning partial metrics.
            await Task.Delay(TimeSpan.FromSeconds(17));
            await lockTx.RollbackAsync();
            using var failed = await blocked;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            var error = await failed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(error.GetProperty("code").GetString(),
                new[] { "report_timeout", "report_dependency_unavailable" });
        }
        await using (var empty = new NpgsqlCommand("""
            SELECT COUNT(*) FROM operational_kpi_reports WHERE organization_id=@org
            """, privileged))
        {
            empty.Parameters.AddWithValue("org", fixture.A);
            Assert.Equal(0L, Convert.ToInt64(await empty.ExecuteScalarAsync()));
        }
        using (var recovered = await analyst.GetAsync(url))
        {
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            var json = await recovered.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual(Guid.Empty, json.GetProperty("reportId").GetGuid());
        }
        Console.WriteLine("G13_FAULT sql_timeout=503 rollback_empty=true recovery=200");

        // Reproducible client cancellation under the same disposable lock.
        await using (var secondLock = await privileged.BeginTransactionAsync())
        {
            await using (var hold = new NpgsqlCommand(
                "LOCK TABLE shipments IN ACCESS EXCLUSIVE MODE", privileged, secondLock))
                await hold.ExecuteNonQueryAsync();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => analyst.GetAsync(url, stop.Token));
            await secondLock.RollbackAsync();
        }
        using (var afterCancellation = await analyst.GetAsync(url))
        {
            Assert.Equal(HttpStatusCode.OK, afterCancellation.StatusCode);
        }
        Console.WriteLine("G13_FAULT client_cancel=aborted recovery=200");
    }
}
