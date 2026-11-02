using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace HarborOps.IntegrationTests;

// In-memory test requests have no socket peer. Set a loopback peer only for TestServer.
internal static class LocalPeerTestServer
{
    public static void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new LoopbackPeerStartupFilter()));
    }

    private sealed class LoopbackPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, proceed) =>
                {
                    context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                    await proceed();
                });
                next(app);
            };
    }
}
