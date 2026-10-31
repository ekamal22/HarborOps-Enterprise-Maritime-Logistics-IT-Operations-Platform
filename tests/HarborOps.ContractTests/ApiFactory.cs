using HarborOps.Application.Baseline;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HarborOps.ContractTests;

/// <summary>
/// Hosts the real API pipeline. The default connection string points at a closed local
/// port so dependency-failure behavior is exercised without any database running.
/// </summary>
public sealed class ApiFactory(IBaselineReadService? baseline = null) : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "http://localhost:5173";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:HarborOps",
            "Host=127.0.0.1;Port=1;Database=harborops_test;Username=contract;Password=contract-only;Timeout=2");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
        builder.UseSetting("Identity:KeyRingPath", Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));

        if (baseline is not null)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBaselineReadService>();
                services.AddSingleton(baseline);
            });
        }
    }
}

public sealed class StubBaseline(BaselineStatus? result) : IBaselineReadService
{
    public Task<BaselineStatus?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(result);
}
