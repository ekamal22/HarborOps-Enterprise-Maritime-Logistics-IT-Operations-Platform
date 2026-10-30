using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Application.Baseline;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class ApiContractTests
{
    private static readonly BaselineStatus Seeded = new(
        "HarborOps", "m0-synthetic-v1",
        new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), "synthetic");

    [Fact]
    public async Task Liveness_IsHealthyWithoutDatabase()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("healthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_Returns503WhenDatabaseUnreachable()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unhealthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Baseline_Returns503ProblemWhenDatabaseUnreachable()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/baseline");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(raw);
        Assert.Equal("dependency_unavailable", body.RootElement.GetProperty("code").GetString());
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Equal(correlation, body.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("contract-only", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Baseline_Returns503WhenSeedIsMissing()
    {
        using var factory = new ApiFactory(new StubBaseline(null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/baseline");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("baseline_unavailable", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Baseline_ReturnsExactSyntheticContract()
    {
        using var factory = new ApiFactory(new StubBaseline(Seeded));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/baseline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = body.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "datasetVersion", "seededAtUtc", "service", "source" }, names);
        Assert.Equal("HarborOps", body.RootElement.GetProperty("service").GetString());
        Assert.Equal("m0-synthetic-v1", body.RootElement.GetProperty("datasetVersion").GetString());
        Assert.Equal("synthetic", body.RootElement.GetProperty("source").GetString());
        Assert.Equal("2026-10-30T00:00:00+00:00", body.RootElement.GetProperty("seededAtUtc").GetString());
    }

    [Fact]
    public async Task EveryResponseCarriesAUniqueCorrelationId()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var first = await client.GetAsync("/health/live");
        var second = await client.GetAsync("/health/live");
        var missing = await client.GetAsync("/does-not-exist");

        var ids = new[] { first, second, missing }
            .Select(r => Assert.Single(r.Headers.GetValues("X-Correlation-ID"))).ToArray();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(3, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^[0-9a-f]{32}$", id));
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginOnly()
    {
        using var factory = new ApiFactory(new StubBaseline(Seeded));
        using var client = factory.CreateClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/baseline");
        allowed.Headers.Add("Origin", ApiFactory.AllowedOrigin);
        using var allowedResponse = await client.SendAsync(allowed);

        using var denied = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/baseline");
        denied.Headers.Add("Origin", "http://evil.example");
        using var deniedResponse = await client.SendAsync(denied);

        Assert.Equal(ApiFactory.AllowedOrigin,
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Baseline_TimestampMatchesFrontendParsingRules()
    {
        using var factory = new ApiFactory(new StubBaseline(Seeded));
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/baseline");

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|\+00:00)$",
            payload.GetProperty("seededAtUtc").GetString()!);
    }
}
