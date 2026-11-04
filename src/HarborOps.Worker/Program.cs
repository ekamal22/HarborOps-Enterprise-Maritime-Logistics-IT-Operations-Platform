using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Worker;

var builder = Host.CreateApplicationBuilder(args);
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
await builder.Build().RunAsync();
