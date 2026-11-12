using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HarborOps.IntegrationTests;

// G15-S01-A05 and the G13 cleanup decision, proven on a started host: with the
// packaged defaults neither background service exists, and each exists only
// when its own explicit setting is on.
[Collection("M13ExclusiveServer")]
public sealed class G13G15RuntimeOptInAcceptanceTests
{
    private static string[] HostedServices(ReferenceDataHttpTests.ApiFactory factory)
    {
        using var client = factory.CreateClient();
        return factory.Services.GetServices<IHostedService>()
            .Select(x => x.GetType().Name).ToArray();
    }

    [ExclusiveM13PostgresFact]
    public async Task DefaultHostRegistersNeitherBackgroundServiceAndExplicitSettingsRegisterEach()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using (var plain = new ReferenceDataHttpTests.ApiFactory(roles.Api))
        {
            var names = HostedServices(plain);
            Assert.DoesNotContain("NotificationDeliveryService", names);
            Assert.DoesNotContain("OperationalKpiCleanupService", names);
        }
        using (var delivery = new ReferenceDataHttpTests.ApiFactory(roles.Api, null, null,
            new Dictionary<string, string> { ["M15:EnableDelivery"] = "true" }))
        {
            var names = HostedServices(delivery);
            Assert.Contains("NotificationDeliveryService", names);
            Assert.DoesNotContain("OperationalKpiCleanupService", names);
        }
        using (var cleanup = new ReferenceDataHttpTests.ApiFactory(roles.Api, null, null,
            new Dictionary<string, string> { ["M13:EnableIdleCleanup"] = "true" }))
        {
            var names = HostedServices(cleanup);
            Assert.Contains("OperationalKpiCleanupService", names);
            Assert.DoesNotContain("NotificationDeliveryService", names);
        }
    }
}
