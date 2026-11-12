using System.Diagnostics;
using HarborOps.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17TelemetryContractTests
{
    private static IConfiguration Configuration(bool enabled, string? endpoint = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["M17:Enabled"] = enabled ? "true" : "false",
            ["M17:OtlpEndpoint"] = endpoint
        }).Build();

    [Fact]
    public void ExporterIsDisabledByDefaultAndCannotBeActivatedByEndpointAlone()
    {
        Assert.False(M17TelemetryOptions.FromConfiguration(Configuration(false)).Enabled);
        Assert.False(M17TelemetryOptions.FromConfiguration(
            Configuration(false, "http://untrusted.example.test:4317")).Enabled);
    }

    [Theory]
    [InlineData("http://otelcol:4317")]
    [InlineData("http://127.0.0.1:4317")]
    [InlineData("http://[::1]:4317")]
    public void LocalCollectorEndpointsAreAllowedOnlyWhenEnabled(string endpoint)
    {
        var result = M17TelemetryOptions.FromConfiguration(Configuration(true, endpoint));
        Assert.True(result.Enabled);
        Assert.Equal(4317, result.Endpoint.Port);
    }

    [Theory]
    [InlineData("http://untrusted.example.test:4317")]
    [InlineData("https://otelcol:4317")]
    [InlineData("http://otelcol:4318")]
    [InlineData("http://otelcol:4317/private")]
    [InlineData("http://admin:password@otelcol:4317")]
    [InlineData("http://0.0.0.0:4317")]
    [InlineData("http://192.168.1.50:4317")]
    [InlineData("not-a-uri")]
    public void CloudAndUntrustedCollectorEndpointsAreRejected(string endpoint)
    {
        Assert.Throws<InvalidOperationException>(() =>
            M17TelemetryOptions.FromConfiguration(Configuration(true, endpoint)));
    }

    [Fact]
    public void UnmatchedRequestCannotCreateAnAttackerControlledRouteDimension()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/tenant/private/shipment/secret";
        Assert.Equal("unmatched", M17Telemetry.SafeRoute(context));
    }

    [Fact]
    public void PreExportTraceProcessorStripsSensitiveTagsAndSpanName()
    {
        using var activity = new Activity("POST /secret/private");
        activity.SetTag("db.query.text", "SELECT secret_value");
        activity.SetTag("user.id", "private-user");
        activity.SetTag("http.request.method", "POST");
        activity.Start();
        var sanitizer = new M17TraceSanitizer();
        sanitizer.OnEnd(activity);

        Assert.Null(activity.GetTagItem("db.query.text"));
        Assert.Null(activity.GetTagItem("user.id"));
        Assert.Equal("POST", activity.GetTagItem("http.request.method"));
        Assert.Equal("internal.operation", activity.DisplayName);
    }

    [Fact]
    public void DefaultComposeDoesNotStartACollectorOrExposeLocalDiagnostics()
    {
        var compose = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile("compose.yaml"));
        Assert.DoesNotContain("M17__Enabled", compose);
        Assert.DoesNotContain("otelcol:", compose);
        Assert.DoesNotContain("3301:3000", compose);
        Assert.Contains("127.0.0.1:8080:8080", compose);
        Assert.Contains("127.0.0.1:5173:80", compose);
    }
}
