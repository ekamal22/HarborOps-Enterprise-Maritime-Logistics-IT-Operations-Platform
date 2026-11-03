using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingSourceMigrationTests
{
    [Fact]
    public async Task M6DataSurvivesAdditiveM7UpgradeAndDisposableRollback()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "g7_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema + ",public" };
        await using var administrative = new NpgsqlConnection(connection);
        await administrative.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administrative))
            await command.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261102080000_ShipmentAssignments");
            var now = DateTimeOffset.UtcNow;
            var organization = new Organization
            {
                Id = Guid.NewGuid(), Slug = "g7-" + Guid.NewGuid().ToString("N")[..12],
                DisplayName = "Synthetic G7 Existing Tenant", Status = "Active",
                Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
            Assert.Equal(7, (await db.Database.GetAppliedMigrationsAsync()).Count());
            await db.Database.MigrateAsync();
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var store = new IntegrationSourceStore(db, TimeProvider.System);
            var issued = await store.CreateAsync(organization.Id, "synthetic_1", "Synthetic Source",
                CancellationToken.None);
            Assert.NotNull(await store.ValidateAsync(issued.Bearer, CancellationToken.None));
            Assert.All(await db.IntegrationCredentials.AsNoTracking().ToListAsync(),
                x => Assert.Equal(32, x.SecretDigest.Length));
            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync("20261102080000_ShipmentAssignments");
            Assert.Equal(7, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(organization.Id, (await db.Organizations.AsNoTracking()
                .SingleAsync(x => x.Id == organization.Id)).Id);
            await db.Database.MigrateAsync();
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.IntegrationSources.ToListAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administrative);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SimultaneousRotationsNeverIssueThirdLiveCredential()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            await store.CreateAsync(fixture.A, "rotation_1", "Rotation Source", CancellationToken.None);
        }
        async Task<bool> Rotate()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            try
            {
                await store.RotateAsync(fixture.A, "rotation_1", CancellationToken.None);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        var outcomes = await Task.WhenAll(Rotate(), Rotate());
        Assert.Single(outcomes.Where(x => x));
        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var sourceId = await db.IntegrationSources.Where(x => x.OrganizationId == fixture.A &&
            x.SourceCode == "rotation_1").Select(x => x.Id).SingleAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(2, await db.IntegrationCredentials.CountAsync(x => x.SourceId == sourceId &&
            x.RevokedAtUtc == null && x.ExpiresAtUtc > now));
    }
}
