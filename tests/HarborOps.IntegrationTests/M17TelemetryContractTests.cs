using System.Diagnostics;
using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
using HarborOps.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17TelemetryContractTests
{
    private static IConfiguration Configuration(bool enabled, string? endpoint = null,
        bool includeLinkKey = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["M17:Enabled"] = enabled ? "true" : "false",
            ["M17:OtlpEndpoint"] = endpoint,
            ["M17:LinkKey"] = includeLinkKey
                ? Convert.ToBase64String(Enumerable.Range(1, 32).Select(n => (byte)n).ToArray())
                : null
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
    public void MissingLocalLinkKeyFailsClosedOnlyWhenTelemetryIsEnabled()
    {
        Assert.Throws<InvalidOperationException>(() => M17TelemetryOptions.FromConfiguration(
            Configuration(true, "http://otelcol:4317", includeLinkKey: false)));
        Assert.False(M17TelemetryOptions.FromConfiguration(
            Configuration(false, includeLinkKey: false)).Enabled);
    }

    [Fact]
    public void ServerCorrelationDigestIsKeyedAndNeverExportsRawIdentifier()
    {
        var key = M17CorrelationDigest.RequireKey(Convert.ToBase64String(
            Enumerable.Range(1, 32).Select(n => (byte)n).ToArray()));
        const string correlation = "00112233445566778899aabbccddeeff";
        var digest = M17CorrelationDigest.FromServerCorrelation(correlation, key);
        Assert.NotNull(digest);
        Assert.Equal(64, digest.Length);
        Assert.NotEqual(correlation, digest);
        Assert.Equal(digest, M17CorrelationDigest.FromServerCorrelation(correlation, key));
        Assert.Null(M17CorrelationDigest.FromServerCorrelation("client-forged", key));

        using var activity = new Activity("POST /private");
        activity.SetTag("harborops.link_digest", digest);
        activity.SetTag("CorrelationId", correlation);
        activity.SetTag("user.id", "private-user");
        activity.Start();
        new M17TraceSanitizer().OnEnd(activity);
        Assert.Equal(digest, activity.GetTagItem("harborops.link_digest"));
        Assert.Null(activity.GetTagItem("CorrelationId"));
        Assert.Null(activity.GetTagItem("user.id"));
    }

    [Fact]
    public void ForgedDiagnosticDigestIsRemovedAtExportBoundary()
    {
        using var activity = new Activity("unknown");
        activity.SetTag("harborops.link_digest", "client-payload");
        activity.SetTag("harborops.outcome", "my-secret-value");
        activity.Start();
        new M17TraceSanitizer().OnEnd(activity);
        Assert.Null(activity.GetTagItem("harborops.link_digest"));
        Assert.Null(activity.GetTagItem("harborops.outcome"));
    }

    [Fact]
    public void NotificationGaugeDifferentiatesDisabledActiveAndExpectedButStopped()
    {
        var notificationStates = new List<int>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "harborops.service.running" &&
                tags.ToArray().Any(tag => tag.Key == "service" &&
                    Equals(tag.Value, "notifications")))
                notificationStates.Add(value);
        });
        listener.Start();
        try
        {
            M17Telemetry.SetNotificationWorkerEnabled(false);
            M17Telemetry.SetNotificationWorkerRunning(false);
            listener.RecordObservableInstruments();
            Assert.Equal(0, notificationStates.Last());

            M17Telemetry.SetNotificationWorkerEnabled(true);
            listener.RecordObservableInstruments();
            Assert.Equal(-1, notificationStates.Last());

            M17Telemetry.SetNotificationWorkerRunning(true);
            listener.RecordObservableInstruments();
            Assert.Equal(1, notificationStates.Last());
        }
        finally
        {
            M17Telemetry.SetNotificationWorkerRunning(false);
            M17Telemetry.SetNotificationWorkerEnabled(false);
        }
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
