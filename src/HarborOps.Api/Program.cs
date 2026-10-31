using System.Data.Common;
using HarborOps.Api.Authentication;
using HarborOps.Api.Organizations;
using Microsoft.AspNetCore.Diagnostics;
using System.Net.Sockets;
using HarborOps.Api.Health;
using HarborOps.Application.Baseline;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

var connectionString = builder.Configuration.GetConnectionString("HarborOps");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:HarborOps is required");

builder.Services.AddHarborOpsPersistence(connectionString);
builder.Services.AddHarborOpsAuthentication(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
{
    if (origins.Length > 0)
        policy.WithOrigins(origins).WithMethods("GET").WithHeaders("Content-Type");
}));

var app = builder.Build();

if (args.Contains("--account-cli", StringComparer.Ordinal))
{
    await AccountCli.RunAsync(args, app.Services);
    return;
}

if (args.Contains("--organization-cli", StringComparer.Ordinal))
{
    await OrganizationCli.RunAsync(args, app.Services);
    return;
}

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
    await database.Database.MigrateAsync();
    await BootstrapSeeder.SeedAsync(database, CancellationToken.None);
    return;
}

app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using var scope = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("HarborOps.Request").BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
    await next();
});

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
    var dependency = error is IdentityDependencyException || (error is not null && IsDependencyFailure(error));
    context.Response.StatusCode = dependency
        ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new
    {
        code = dependency ? "dependency_unavailable" : "internal_error",
        message = dependency ? "Dependency temporarily unavailable" : "An unexpected error occurred",
        correlationId = context.Items["CorrelationId"]
    });
}));

app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapAuthenticationEndpoints();
app.MapOrganizationEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = (context, report) =>
        context.Response.WriteAsJsonAsync(new { status = report.Status.ToString().ToLowerInvariant() })
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = (context, report) =>
        context.Response.WriteAsJsonAsync(new { status = report.Status.ToString().ToLowerInvariant() })
});

app.MapGet("/api/v1/system/baseline", async (IBaselineReadService service,
    HttpContext context, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.ReadAsync(cancellationToken);
        return result is null
            ? Results.Json(new { code = "baseline_unavailable",
                message = "Baseline data is unavailable", correlationId = context.Items["CorrelationId"] },
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(result);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (Exception exception) when (IsDependencyFailure(exception))
    {
        // Type only: provider messages can carry host details that do not belong in logs.
        logger.LogWarning("Baseline request failed because a dependency is unavailable ({ExceptionType})",
            exception.GetType().Name);
        return Results.Json(new { code = "dependency_unavailable",
            message = "Database temporarily unavailable", correlationId = context.Items["CorrelationId"] },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

// EF Core wraps provider connection failures in InvalidOperationException ("likely due
// to a transient failure"), so the provider exception sits in the inner chain.
static bool IsDependencyFailure(Exception exception)
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (current is DbException or TimeoutException or SocketException)
            return true;
    }
    return false;
}

public partial class Program { }
