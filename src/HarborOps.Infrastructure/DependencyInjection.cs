using HarborOps.Application.Baseline;
using HarborOps.Infrastructure.Baseline;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HarborOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHarborOpsPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HarborOpsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBaselineReadService, EfBaselineReadService>();
        return services;
    }
}
