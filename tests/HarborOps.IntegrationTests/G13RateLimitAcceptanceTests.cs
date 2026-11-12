using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class G13RateLimitAcceptanceTests
{
    [ExclusiveM13PostgresFact]
    public async Task ReportingBuildLimiterRejectsSeventhRequestWithoutChangingTheCommittedSnapshot()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var administrator = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var client = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(client, f.A);
        var root = $"/api/v1/organizations/{f.A}/analytics/overview";

        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var response = await client.GetAsync(root);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        async Task<long> Reports()
        {
            await using var sql = new NpgsqlCommand("""
                SELECT COUNT(*) FROM operational_kpi_reports WHERE organization_id=@org
                """, db);
            sql.Parameters.AddWithValue("org", f.A);
            return Convert.ToInt64(await sql.ExecuteScalarAsync());
        }
        var before = await Reports();
        using var denied = await client.GetAsync(root);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.Equal(before, await Reports());
    }
}
