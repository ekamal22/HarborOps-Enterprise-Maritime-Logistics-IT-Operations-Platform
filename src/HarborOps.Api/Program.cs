using System.Data.Common;
using System.Diagnostics;
using HarborOps.Api.Authentication;
using HarborOps.Api.Organizations;
using HarborOps.Api.References;
using HarborOps.Api.Maritime;
using HarborOps.Api.Shipments;
using HarborOps.Api.Search;
using HarborOps.Api.Assignments;
using HarborOps.Api.Integrations;
using HarborOps.Api.Audit;
using HarborOps.Api.Exceptions;
using HarborOps.Api.Notifications;
using HarborOps.Api.Reporting;
using HarborOps.Infrastructure.Notifications;
using HarborOps.Infrastructure.Audit;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using HarborOps.Api.Health;
using HarborOps.Api.Observability;
using HarborOps.Application.Baseline;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using HarborOps.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
var m17Enabled = builder.AddM17Telemetry();
M17Telemetry.SetNotificationWorkerEnabled(builder.Configuration.GetValue<bool>("M15:EnableDelivery"));

var connectionString = builder.Configuration.GetConnectionString("HarborOps");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:HarborOps is required");

// Reject invalid intake quotas before the API can accept any request.
_ = TrackingInboxStore.ReadPendingLimit(builder.Configuration);
builder.Services.AddHarborOpsPersistence(connectionString);
// The narrow idle sweeper is opt-in (default off) and, when enabled, runs only
// under the dedicated restricted API login. It removes expired reporting
// snapshots, never notification or M11 history. Request-scoped bounded expiry
// handling during report builds stays active regardless of this switch.
if (builder.Configuration.GetValue<bool>("M13:EnableIdleCleanup") &&
    string.Equals(new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Username,
    "harborops_api", StringComparison.Ordinal))
    builder.Services.AddHostedService<OperationalKpiCleanupService>();
builder.Services.AddHarborOpsAuthentication(builder.Configuration);
builder.Services.AddSingleton<IntegrationRequestLimiter>();
builder.Services.AddScoped<TrackingWorkAdminStore>();
builder.Services.AddScoped<ShipmentTimelineStore>();
builder.Services.AddScoped<ShipmentExceptionStore>();
builder.Services.AddScoped<NotificationStore>();
if (builder.Configuration.GetValue<bool>("M15:EnableDelivery"))
    builder.Services.AddHostedService<NotificationDeliveryService>();
if (builder.Configuration.GetValue<bool>("M11:EnableEvaluator"))
    builder.Services.AddHostedService<ShipmentExceptionEvaluationService>();
builder.Services.AddSingleton<ShipmentCursorCodec>();
builder.Services.AddSingleton<ShipmentExportGate>();
if (builder.Configuration.GetValue<bool>("M10:EnableProjector"))
{
    var projectorConnection = builder.Configuration.GetConnectionString("HarborOpsProjector");
    if (string.IsNullOrWhiteSpace(projectorConnection))
        throw new InvalidOperationException("M10 projector requires separate database credentials");
    var projectorAccount = new Npgsql.NpgsqlConnectionStringBuilder(projectorConnection);
    var apiAccount = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(projectorAccount.Username) ||
        !string.Equals(projectorAccount.Username, "harborops_projector", StringComparison.Ordinal) ||
        string.Equals(projectorAccount.Username, apiAccount.Username, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("M10 projector must use a distinct restricted database role");
    builder.Services.AddHostedService<TrackingProjectionHostedService>();
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    var known = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var address in known)
    {
        if (!IPAddress.TryParse(address, out var parsed))
            throw new InvalidOperationException("Invalid trusted reverse-proxy address");
        options.KnownProxies.Add(parsed);
    }
});
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
    Environment.ExitCode = await RunCommandAsync(() => AccountCli.RunAsync(args, app.Services));
    return;
}

if (args.Contains("--organization-cli", StringComparer.Ordinal))
{
    Environment.ExitCode = await RunCommandAsync(() => OrganizationCli.RunAsync(args, app.Services));
    return;
}

if (args.Contains("--source-cli", StringComparer.Ordinal))
{
    Environment.ExitCode = await RunCommandAsync(() => SourceCli.RunAsync(args, app.Services));
    return;
}

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
    await database.Database.MigrateAsync();
    await BootstrapSeeder.SeedAsync(database, CancellationToken.None);
    await AuditRoleProvisioner.ProvisionAsync(database, builder.Configuration, CancellationToken.None);
    return;
}

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    using var auditCorrelationScope = AuditCorrelation.Begin(correlationId);
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    // Handlers set no-store themselves; this also covers responses produced by the framework
    // (authentication challenges, rate limiting, the exception handler) on every API route.
    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/integrations"))
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Cache-Control"))
                context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    var diagnosticScope = new Dictionary<string, object> { ["CorrelationId"] = correlationId };
    if (m17Enabled && Activity.Current is { } span)
        diagnosticScope["TraceId"] = span.TraceId.ToString();
    using var scope = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("HarborOps.Request").BeginScope(diagnosticScope);
    await next();
});

if (m17Enabled)
    app.UseMiddleware<M17TelemetryMiddleware>();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
    var invalidRequest = error is BadHttpRequestException;
    var dependency = error is IdentityDependencyException || (error is not null && IsDependencyFailure(error));
    context.Response.StatusCode = invalidRequest ? StatusCodes.Status400BadRequest
        : dependency ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status500InternalServerError;
    if (dependency && context.Request.Path.StartsWithSegments("/integrations/v1/tracking-events"))
        context.Response.Headers.RetryAfter = "60";
    await context.Response.WriteAsJsonAsync(new
    {
        code = invalidRequest ? "invalid_request"
            : dependency ? "dependency_unavailable" : "internal_error",
        message = invalidRequest ? "Invalid request body"
            : dependency ? "Dependency temporarily unavailable" : "An unexpected error occurred",
        correlationId = context.Items["CorrelationId"]
    });
}));

app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapAuthenticationEndpoints();
app.MapOrganizationEndpoints();
app.MapReferenceEndpoints();
app.MapMaritimeEndpoints();
app.MapShipmentEndpoints();
app.MapShipmentSearchEndpoints();
app.MapAssignmentEndpoints();
app.MapTrackingEndpoints();
app.MapTrackingWorkEndpoints();
app.MapShipmentTimelineEndpoints();
app.MapShipmentExceptionEndpoints();
app.MapNotificationEndpoints();
app.MapOperationalKpiEndpoints();
app.MapAuditEndpoints();

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

// Restricted operator commands fail with a sanitized message and exit code 1. An unhandled exception
// would abort the process and let the host write a memory dump, which could contain secrets that
// were just entered. Only messages raised by the commands themselves are shown; wrapped provider
// failures report their type alone.
static async Task<int> RunCommandAsync(Func<Task> operation)
{
    try
    {
        await operation();
        return 0;
    }
    catch (Exception exception)
    {
        var message = exception is InvalidOperationException or ArgumentException &&
            exception.InnerException is null
            ? exception.Message
            : "The operation could not be completed (" + exception.GetType().Name + ")";
        Console.Error.WriteLine(message);
        return 1;
    }
}

public partial class Program { }
