using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13G15FeatureDefaultAcceptanceTests
{
    [Fact]
    public void M10ProjectorAndM11EvaluatorRequireExplicitOptIn()
    {
        // Read the actual packaged default configuration, not an override
        // supplied by a disposable verification host.
        var path = M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Api/appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        Assert.False(config.GetValue<bool>("M10:EnableProjector"));
        Assert.False(config.GetValue<bool>("M11:EnableEvaluator"));
        var program = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Api/Program.cs"));
        Assert.Contains("builder.Configuration.GetValue<bool>(\"M10:EnableProjector\")", program);
        Assert.Contains("builder.Configuration.GetValue<bool>(\"M11:EnableEvaluator\")", program);
    }

    // G15-S23-A05: the KPI aggregation reads only reporting and source tables. It
    // never reads or writes a notification receipt, outbox or replay table, so a
    // receipt can neither add to nor remove from a KPI.
    [Fact]
    public void KpiAggregationNeverReadsOrWritesNotificationTables()
    {
        var store = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Infrastructure/Reporting/OperationalKpiStore.cs"));
        Assert.DoesNotContain("m15_", store, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotificationStore", store);
        var migration = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Infrastructure/Persistence/Migrations/20261109020000_M13OperationalKpis.cs"));
        Assert.DoesNotContain("m15_", migration, StringComparison.OrdinalIgnoreCase);
        var notifications = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Infrastructure/Notifications/NotificationStore.cs"));
        Assert.DoesNotContain("operational_kpi_", notifications, StringComparison.OrdinalIgnoreCase);
    }

    // G15-S01-A05 and the G13 cleanup decision: both background services are
    // off unless explicitly enabled, in the packaged settings, the code and the
    // default Compose file.
    [Fact]
    public void M15DeliveryAndM13IdleCleanupRequireExplicitOptIn()
    {
        var path = M10RuntimeIdentityTests.RepositoryFile("src/HarborOps.Api/appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        Assert.False(config.GetValue<bool>("M15:EnableDelivery"));
        Assert.False(config.GetValue<bool>("M13:EnableIdleCleanup"));
        var program = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Api/Program.cs"));
        Assert.Contains("if (builder.Configuration.GetValue<bool>(\"M15:EnableDelivery\"))\r\n    builder.Services.AddHostedService<NotificationDeliveryService>();"
            .Replace("\r\n", "\n"), program.Replace("\r\n", "\n"));
        Assert.Contains("builder.Configuration.GetValue<bool>(\"M13:EnableIdleCleanup\")", program);
        var compose = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile("compose.yaml"));
        Assert.Contains("M15__EnableDelivery: \"${HARBOROPS_M15_ENABLE_DELIVERY:-false}\"", compose);
        Assert.Contains("M13__EnableIdleCleanup: \"${HARBOROPS_M13_ENABLE_IDLE_CLEANUP:-false}\"", compose);
    }
}
