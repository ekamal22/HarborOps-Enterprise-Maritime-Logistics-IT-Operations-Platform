using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var readiness = args.Contains("--readiness", StringComparer.Ordinal);
var hostArgs = readiness ? args.Where(arg => arg != "--readiness").ToArray() : args;
var builder = Host.CreateApplicationBuilder(hostArgs);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

var connectionString = builder.Configuration.GetConnectionString("HarborOps");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:HarborOps is required");
var settings = builder.Configuration.GetSection("TrackingWorker").Get<TrackingWorkerSettings>()
    ?? new TrackingWorkerSettings();
settings.Validate();

builder.Services.AddHarborOpsPersistence(connectionString);
builder.Services.AddSingleton(settings);
builder.Services.AddScoped<TrackingWorkStore>();
builder.Services.AddHostedService<WorkerShell>();

using var host = builder.Build();
if (readiness)
{
    try
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 5;
            // Check the real worker identity, schema and its minimum operational rights.
            // A database connection alone is not sufficient for worker readiness.
            command.CommandText = """
                SELECT
                    has_table_privilege(current_user, 'tracking_inbox_work_items', 'SELECT')
                    AND has_column_privilege(current_user, 'tracking_inbox_work_items', 'state', 'UPDATE')
                    AND has_table_privilege(current_user, 'tracking_work_attempt_events', 'SELECT')
                    AND NOT has_table_privilege(current_user, 'tracking_work_attempt_events', 'INSERT')
                    AND has_table_privilege(current_user, 'tracking_processing_effects', 'INSERT')
                    AND has_function_privilege(current_user, 'm9_lock_active_organization(uuid)', 'EXECUTE')
                """;
            Environment.ExitCode = await command.ExecuteScalarAsync() is bool healthy && healthy ? 0 : 1;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    catch (Exception)
    {
        // Do not print credentials, connection strings or provider messages.
        Environment.ExitCode = 1;
    }
    return;
}
await host.RunAsync();
