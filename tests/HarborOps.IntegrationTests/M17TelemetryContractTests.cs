using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    public void TenThousandAttackerChosenPathsUseOneBoundedOperationSeries()
    {
        var series = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (instrument.Name != "harborops.request.duration") return;
            var dimensions = string.Join("|", tags.ToArray()
                .Select(tag => tag.Key + "=" + tag.Value));
            lock (series) series.Add(dimensions);
        });
        listener.Start();

        for (var i = 0; i < 10_000; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/untrusted/" + i + "/shipments";
            context.Request.Host = new HostString("host" + i + ".example.test");
            context.Response.StatusCode = 200;
            M17Telemetry.ObserveRequest(context, TimeSpan.FromMilliseconds(5));
        }

        Assert.Single(series);
        Assert.Contains("operation=other", Assert.Single(series));
        Assert.Contains("status_class=2xx", Assert.Single(series));
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
