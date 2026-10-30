using HarborOps.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddHostedService<WorkerShell>();
await builder.Build().RunAsync();
