using System.Net;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ApiAgainstPostgresTests
{
    private sealed class PostgresApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:HarborOps", connectionString);
        }
    }

    [Fact]
    public async Task ReadinessAndBaseline_AreServedFromTheSeededDatabase()
    {
        var connectionString = TestDatabase.ConnectionString();
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new HarborOpsDbContext(options))
        {
            await db.Database.MigrateAsync();
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        }

        using var factory = new PostgresApiFactory(connectionString);
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var baseline = await client.GetAsync("/api/v1/system/baseline");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);

        using var body = JsonDocument.Parse(await baseline.Content.ReadAsStringAsync());
        Assert.Equal("HarborOps", body.RootElement.GetProperty("service").GetString());
        Assert.Equal("m0-synthetic-v1", body.RootElement.GetProperty("datasetVersion").GetString());
        Assert.Equal("synthetic", body.RootElement.GetProperty("source").GetString());
        Assert.Equal("2026-10-30T00:00:00+00:00", body.RootElement.GetProperty("seededAtUtc").GetString());
    }

    [Fact]
    public async Task ReadinessAndBaseline_Return503WhenBaselineRowIsMissingThenRecover()
    {
        var connectionString = TestDatabase.ConnectionString();
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new HarborOpsDbContext(options);
        await db.Database.MigrateAsync();
        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);

        using var factory = new PostgresApiFactory(connectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        await db.Database.ExecuteSqlRawAsync("DELETE FROM bootstrap_records");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await client.GetAsync("/api/v1/system/baseline")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        }
        finally
        {
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/baseline")).StatusCode);
    }
}
