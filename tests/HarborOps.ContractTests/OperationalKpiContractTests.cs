using System.Net;
using HarborOps.Domain.Reporting;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class OperationalKpiContractTests
{
    [Theory]
    [InlineData("catalog")]
    [InlineData("overview")]
    [InlineData("reports/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/members?metricCode=K01")]
    [InlineData("grants")]
    public async Task AllAnalyticsReadsRequireAuthenticatedSession(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/v1/organizations/bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/analytics/" + path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("no-store", string.Join(",", response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task GrantMutationRequiresAuthenticatedSession()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put,
            "/api/v1/organizations/bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/analytics/" +
            "grants/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa") {
            Content = System.Net.Http.Json.JsonContent.Create(new { enabled = true })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ReportingCatalogNeverLabelsScheduleRiskAsPhysicalArrival()
    {
        var catalog = OperationalKpiPolicy.Definitions;
        var strict = catalog.Single(x => x.Code == "K05");
        var grace = catalog.Single(x => x.Code == "K06");
        Assert.Equal("D05", strict.DenominatorCode);
        Assert.Equal(strict.DenominatorCode, grace.DenominatorCode);
        Assert.Contains("recorded", strict.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unconfirmed", catalog.Single(x => x.Code == "K08").Label,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("risk", catalog.Single(x => x.Code == "K09").Label,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fresh", catalog.Single(x => x.Code == "K10").Description,
            StringComparison.OrdinalIgnoreCase);
    }
}