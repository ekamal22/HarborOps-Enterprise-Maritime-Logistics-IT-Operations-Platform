using HarborOps.Application.Baseline;
using HarborOps.Infrastructure.Baseline;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HarborOps.Infrastructure;

public static class DependencyInjection
{
    private const int DefaultConnectTimeoutSeconds = 5;

    public static IServiceCollection AddHarborOpsPersistence(this IServiceCollection services, string connectionString)
    {
        // Bound connection attempts so an unreachable database fails fast instead of
        // holding a request for the Npgsql default of 15 seconds.
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!connectionString.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            builder.Timeout = DefaultConnectTimeoutSeconds;

        var bounded = builder.ConnectionString;
        services.AddDbContext<HarborOpsDbContext>(options => options.UseNpgsql(bounded));
        services.AddScoped<IBaselineReadService, EfBaselineReadService>();
        return services;
    }
}
