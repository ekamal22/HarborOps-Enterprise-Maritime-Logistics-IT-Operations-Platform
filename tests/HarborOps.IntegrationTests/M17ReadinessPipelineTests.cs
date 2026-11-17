using System.Data.Common;
using System.Net;
using HarborOps.Api.Health;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HarborOps.IntegrationTests;

// Found on the real stack: once the readiness check abandoned a provider call at its hard limit, disposing the request
// scope's DbContext threw "Can't close, connection is in state Connecting" and /health/ready answered HTTP 500 instead
// of 503 under ten concurrent probes. These tests run the real health-check service, the real exception handler and the
// real endpoint with a provider call that never finishes, so that the readiness contract (503 and the same JSON body,
// within the bound, never 500) is checked through the pipeline and not only through the check class.
public sealed class M17ReadinessPipelineTests
{
    private sealed class StuckConnectionInterceptor : DbConnectionInterceptor
    {
        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None);
            return result;
        }
    }

    private static async Task<IHost> StartAsync(bool stuck) => await new HostBuilder().ConfigureWebHost(web => web
        .UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddDbContext<HarborOpsDbContext>(options =>
            {
                options.UseNpgsql("Host=127.0.0.1;Port=1;Database=harborops_unreachable;Username=u;Password=p;SSL Mode=Disable;Timeout=3;Command Timeout=3");
                if (stuck) options.AddInterceptors(new StuckConnectionInterceptor());
            });
            services.AddRouting();
            services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
        })
        .Configure(app =>
        {
            app.UseExceptionHandler(handler => handler.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            }));
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("ready"),
                ResponseWriter = (context, report) =>
                    context.Response.WriteAsJsonAsync(new { status = report.Status.ToString().ToLowerInvariant() })
            }));
        })).StartAsync();

    [Theory(Timeout = 60000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwelveConcurrentReadinessProbesAnswerServiceUnavailableWithinTheBoundNeverServerError(bool stuckProvider)
    {
        using var host = await StartAsync(stuckProvider);
        using var client = host.GetTestClient();
        // One warm-up probe pays the one-time cost of building the model and starting the pipeline (JIT, caches), so the
        // measured probes below describe the steady state that a deployed API is in.
        using (var warm = await client.GetAsync("/health/ready")) Assert.Equal(HttpStatusCode.ServiceUnavailable, warm.StatusCode);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            using var response = await client.GetAsync("/health/ready");
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync(), Elapsed: started.Elapsed);
        }));

        Assert.All(responses, r =>
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
            Assert.Equal("{\"status\":\"unhealthy\"}", r.Body);
            Assert.True(r.Elapsed < TimeSpan.FromSeconds(5), $"a probe took {r.Elapsed.TotalSeconds:F2}s");
        });
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), "the probes ran concurrently");

        // Give any abandoned provider call time to end and be disposed, then confirm nothing leaked into the next probe.
        await Task.Delay(TimeSpan.FromSeconds(1));
        using var again = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, again.StatusCode);
    }
}
