using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace HarborOps.IntegrationTests;

// S25 finding: the zero baseline of the 5xx counter was seeded only for operation="business", which is the baseline
// endpoint alone. Real traffic is overwhelmingly "organization" and "auth", readiness probes are "health", and
// integration intake is "tracking". Prometheus rate() cannot count the first sample of a series, so the first 5xx
// burst in any unseeded family would have been invisible. These tests pin the closed family set to its baseline.
public sealed class M17FirstFailureBaselineTests
{
    private sealed record Point(string Operation, string StatusClass, long Value, int LabelCount);

    private static readonly string[] Families = ["auth", "organization", "tracking", "health", "business", "other"];

    private static ConcurrentQueue<Point> Listen(MeterListener listener)
    {
        var points = new ConcurrentQueue<Point>();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName) observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name != "harborops.request.failures") return;
            var copy = tags.ToArray();
            string Tag(string key) => copy.FirstOrDefault(t => t.Key == key).Value?.ToString() ?? "";
            points.Enqueue(new Point(Tag("operation"), Tag("status_class"), value, copy.Length));
        });
        listener.Start();
        return points;
    }

    private static HttpContext Request(string? routePattern, int status)
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = status;
        if (routePattern is not null)
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(routePattern),
                0, EndpointMetadataCollection.Empty, "m17-test"));
        return context;
    }

    [Fact]
    public void HeartbeatSeedsAZeroFiveXxBaselineForEveryOperationFamilyAndNothingElse()
    {
        using var listener = new MeterListener();
        var points = Listen(listener);
        M17Telemetry.Heartbeat();
        var failures = points.ToArray();
        Assert.Equal(Families.Order().ToArray(), failures.Select(p => p.Operation).Order().ToArray());
        Assert.All(failures, p =>
        {
            Assert.Equal(0, p.Value);
            Assert.Equal("5xx", p.StatusClass);
            Assert.Equal(2, p.LabelCount);
        });
    }

    [Fact]
    public void RepeatedHeartbeatsNeverRaiseTheBaselineAboveZero()
    {
        using var listener = new MeterListener();
        var points = Listen(listener);
        for (var beat = 0; beat < 5; beat++) M17Telemetry.Heartbeat();
        Assert.Equal(5 * Families.Length, points.Count);
        Assert.Equal(0, points.Sum(p => p.Value));
    }

    // Representative real routes. The first column is the route pattern the endpoint registers.
    public static TheoryData<string?, string> RoutesAndFamilies => new()
    {
        {"/api/v1/auth/login", "auth"},
        {"/api/v1/auth/session/organization", "auth"},
        {"/api/v1/organizations/{organizationId}/shipments", "organization"},
        {"/api/v1/organizations/{organizationId}/audit-events", "organization"},
        {"/api/v1/organizations", "organization"},
        {"/integrations/v1/tracking-events", "tracking"},
        {"/health/ready", "health"},
        {"/health/live", "health"},
        {"/api/v1/system/baseline", "business"},
        {"/api/v1/anything-else", "business"},
        {"/robots.txt", "other"},
        {null, "other"},
        {"/api/v1/organizations-lookalike-that-is-too-long-" +
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "other"}
    };

    [Theory]
    [MemberData(nameof(RoutesAndFamilies))]
    public void EveryRealFiveXxFamilyAlreadyHasItsSeededBaselineSeries(string? route, string expectedFamily)
    {
        using var listener = new MeterListener();
        var points = Listen(listener);
        M17Telemetry.Heartbeat();
        var seeded = points.ToArray().Select(p => (p.Operation, p.StatusClass)).ToHashSet();
        while (points.TryDequeue(out _)) { }

        foreach (var status in new[] { 500, 502, 503, 504, 599 })
        {
            M17Telemetry.ObserveRequest(Request(route, status), TimeSpan.FromMilliseconds(5));
        }

        var real = points.ToArray();
        Assert.Equal(5, real.Length);
        Assert.All(real, p =>
        {
            Assert.Equal(expectedFamily, p.Operation);
            Assert.Equal("5xx", p.StatusClass);
            Assert.Equal(1, p.Value);
            Assert.Contains((p.Operation, p.StatusClass), seeded);
        });
    }

    [Fact]
    public void OnlyServerErrorsAreFailuresAndCodesOutsideTheHttpRangeAreNot()
    {
        using var listener = new MeterListener();
        var points = Listen(listener);
        foreach (var status in new[] { 100, 200, 204, 301, 304, 400, 401, 403, 404, 429, 499, 600, 999, 0 })
            M17Telemetry.ObserveRequest(Request("/api/v1/system/baseline", status), TimeSpan.FromMilliseconds(1));
        Assert.Empty(points);
    }

    [Fact]
    public void NoRouteValueOrIdentifierEverReachesAFailureLabel()
    {
        using var listener = new MeterListener();
        var points = Listen(listener);
        M17Telemetry.ObserveRequest(Request(
            "/api/v1/organizations/{organizationId}/shipments/{shipmentId}/exceptions/{token}", 503),
            TimeSpan.FromMilliseconds(1));
        var observed = Assert.Single(points);
        Assert.Equal("organization", observed.Operation);
        Assert.Equal(2, observed.LabelCount);
    }
}
