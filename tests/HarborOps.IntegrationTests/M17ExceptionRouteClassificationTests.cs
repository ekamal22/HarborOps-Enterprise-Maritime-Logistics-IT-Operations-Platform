using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HarborOps.IntegrationTests;

// Found by the first-failure rehearsal on a real stack: a dependency failure on an authenticated route (session
// validation, tracking credential lookup) raises an exception, and the exception handler clears the endpoint of the
// request it re-executes. The telemetry middleware used to read the endpoint afterwards, so those 503 responses were
// exported as operation="other" instead of "auth", "organization" or "tracking". The pipeline below has the same order
// as Program.cs: routing, telemetry middleware, exception handler, endpoints.
public sealed class M17ExceptionRouteClassificationTests
{
    private sealed record Failure(string Operation, string StatusClass);

    private static async Task<IHost> StartAsync() => await new HostBuilder().ConfigureWebHost(web => web
        .UseTestServer()
        .ConfigureServices(services => services.AddRouting())
        .Configure(app =>
        {
            app.UseRouting();
            app.UseMiddleware<M17TelemetryMiddleware>();
            app.UseExceptionHandler(handler => handler.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }));
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/api/v1/auth/session", (RequestDelegate)(_ => throw new InvalidOperationException("dependency")));
                endpoints.MapGet("/api/v1/organizations/{organizationId}", (RequestDelegate)(_ => throw new InvalidOperationException("dependency")));
                endpoints.MapPost("/integrations/v1/tracking-events", (RequestDelegate)(_ => throw new InvalidOperationException("dependency")));
                endpoints.MapGet("/api/v1/system/baseline", (RequestDelegate)(_ => throw new InvalidOperationException("dependency")));
                endpoints.MapGet("/health/ready", (RequestDelegate)(_ => throw new InvalidOperationException("dependency")));
                endpoints.MapGet("/ok/{value}", (RequestDelegate)(context => { context.Response.StatusCode = 200; return Task.CompletedTask; }));
            });
        })).StartAsync();

    private static ConcurrentQueue<Failure> Listen(MeterListener listener)
    {
        var failures = new ConcurrentQueue<Failure>();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName) observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name != "harborops.request.failures" || value == 0) return;
            var copy = tags.ToArray();
            string Tag(string key) => copy.FirstOrDefault(t => t.Key == key).Value?.ToString() ?? "";
            failures.Enqueue(new Failure(Tag("operation"), Tag("status_class")));
        });
        listener.Start();
        return failures;
    }

    [Theory]
    [InlineData("GET", "/api/v1/auth/session", "auth")]
    [InlineData("GET", "/api/v1/organizations/11111111111111111111111111111111", "organization")]
    [InlineData("POST", "/integrations/v1/tracking-events", "tracking")]
    [InlineData("GET", "/api/v1/system/baseline", "business")]
    [InlineData("GET", "/health/ready", "health")]
    public async Task ADependencyFailureHandledByTheExceptionHandlerKeepsItsOperationFamily(string method, string path, string expected)
    {
        using var host = await StartAsync();
        using var listener = new MeterListener();
        var failures = Listen(listener);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var failure = Assert.Single(failures);
        Assert.Equal(expected, failure.Operation);
        Assert.Equal("5xx", failure.StatusClass);
    }

    [Fact]
    public async Task ARequestThatMatchedNoEndpointStaysOtherAndNeverBecomesAFailure()
    {
        using var host = await StartAsync();
        using var listener = new MeterListener();
        var failures = Listen(listener);
        using var client = host.GetTestClient();

        var missing = await client.GetAsync("/api/v1/organizations/x/y/z/does/not/exist");
        var ok = await client.GetAsync("/ok/1");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, ok.StatusCode);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task TenThousandAttackerChosenPathsStillUseOneOperationSeriesWhenTheHandlerRuns()
    {
        using var host = await StartAsync();
        using var listener = new MeterListener();
        var failures = Listen(listener);
        using var client = host.GetTestClient();
        for (var i = 0; i < 200; i++)
            await client.GetAsync($"/api/v1/organizations/{Guid.NewGuid():N}");
        var distinct = failures.Select(f => (f.Operation, f.StatusClass)).Distinct().ToArray();
        Assert.Equal([("organization", "5xx")], distinct);
        Assert.Equal(200, failures.Count);
    }
}
