using HarborOps.Application.Baseline;
using HarborOps.Infrastructure.Baseline;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Shipments;
using HarborOps.Infrastructure.Assignments;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Audit;
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
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!connectionString.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            builder.Timeout = DefaultConnectTimeoutSeconds;

        services.AddDbContext<HarborOpsDbContext>(options => options.UseNpgsql(builder.ConnectionString));
        services.AddScoped<IBaselineReadService, EfBaselineReadService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<PasswordHasher>();
        services.AddScoped<AuditReader>();
        services.AddScoped<AuditAccessRecorder>();
        services.AddScoped<IdentityStore>();
        services.AddScoped<OrganizationStore>();
        services.AddScoped<ReferenceStore>();
        services.AddScoped<MaritimeStore>();
        services.AddScoped<ShipmentStore>();
        services.AddScoped<AssignmentStore>();
        services.AddScoped<IntegrationSourceStore>();
        services.AddScoped<ITrackingReceiptCommitHook, NoopTrackingReceiptCommitHook>();
        services.AddScoped<TrackingInboxStore>();
        return services;
    }
}
