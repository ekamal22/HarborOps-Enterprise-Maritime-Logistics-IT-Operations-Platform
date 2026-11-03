using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Shipments;
using HarborOps.Infrastructure.Assignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingSourceMigrationTests
{
    private static readonly string[] PreservedTables =
    [
        "users", "auth_sessions", "auth_events", "organizations", "organization_memberships",
        "ports", "vessels", "voyages", "containers", "shipments",
        "shipment_status_changes", "shipment_container_assignments", "shipment_voyage_assignments"
    ];

    private static async Task<Dictionary<string, string>> SnapshotAsync(string connection)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        foreach (var table in PreservedTables)
        {
            // Table names come only from a fixed local allowlist.
            await using var command = new NpgsqlCommand(
                $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY id), '[]'::jsonb)::text FROM {table} t",
                db);
            result.Add(table, (string)(await command.ExecuteScalarAsync())!);
        }
        return result;
    }

    [Fact]
    public async Task PopulatedM6DataSurvivesM7UpgradeRollbackAndReapply()
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
            var email = "g7-history-" + Guid.NewGuid().ToString("N") + "@example.test";
            const string password = "Synthetic-G7-migration-passphrase-123";
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            await identity.ProvisionAsync(email, "Synthetic Historical User", password, CancellationToken.None);
            var org = await organizations.BootstrapAsync("g7-history-" + Guid.NewGuid().ToString("N")[..10],
                "Historical M6 Organization", email, CancellationToken.None);
            var session = (await identity.LoginAsync(email, password, "g7-history", CancellationToken.None))!;
            Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId,
                org.Id, CancellationToken.None)).Value);
            var now = DateTimeOffset.UtcNow;
            var origin = new PortReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-ORIGIN",
                Name = "Historical Origin", CountryCode = "US", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var destination = new PortReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-DEST",
                Name = "Historical Destination", CountryCode = "US", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var vessel = new VesselReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-VESSEL",
                Name = "Historical Vessel", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var container = new ContainerRecord
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-CONT",
                ContainerIdentifier = "SYN-BOX-G7HISTORY", IdentifierKind = "Synthetic",
                EquipmentType = "Dry20", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var voyage = new VoyageRecord
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-VOYAGE",
                VesselId = vessel.Id, OriginPortId = origin.Id, DestinationPortId = destination.Id,
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var shipment = new ShipmentRecord
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G7-SHIPMENT",
                OriginPortId = origin.Id, DestinationPortId = destination.Id,
                ChangedByUserId = session.UserId, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Ports.AddRange(origin, destination);
            db.Vessels.Add(vessel);
            db.Containers.Add(container);
            db.Voyages.Add(voyage);
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            db.ShipmentContainerAssignments.Add(new ShipmentContainerAssignment
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ShipmentId = shipment.Id,
                ContainerId = container.Id, StartsAtUtc = now.AddMinutes(1),
                CreatedByUserId = session.UserId, CreatedAtUtc = now, UpdatedAtUtc = now
            });
            db.ShipmentVoyageAssignments.Add(new ShipmentVoyageAssignment
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ShipmentId = shipment.Id,
                VoyageId = voyage.Id, StartsAtUtc = now.AddMinutes(1),
                CreatedByUserId = session.UserId, CreatedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
            Assert.Single(await db.ShipmentStatusChanges.AsNoTracking().Where(
                x => x.ShipmentId == shipment.Id).ToListAsync());
            var before = await SnapshotAsync(scoped.ConnectionString);
            Assert.Contains("G7-SHIPMENT", before["shipments"]);
            Assert.Contains("G7-CONT", before["containers"]);
            Assert.NotEqual("[]", before["shipment_voyage_assignments"]);
            Assert.NotEqual("[]", before["shipment_container_assignments"]);
            Assert.NotEqual("[]", before["auth_sessions"]);

            await db.Database.MigrateAsync();
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(before, await SnapshotAsync(scoped.ConnectionString));

            var store = new IntegrationSourceStore(db, TimeProvider.System);
            var issued = await store.CreateAsync(org.Id, "synthetic_1", "Synthetic Source", CancellationToken.None);
            Assert.NotNull(await store.ValidateAsync(issued.Bearer, CancellationToken.None));
            Assert.All(await db.IntegrationCredentials.AsNoTracking().ToListAsync(),
                x => Assert.Equal(32, x.SecretDigest.Length));

            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync("20261102080000_ShipmentAssignments");
            Assert.Equal(7, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(before, await SnapshotAsync(scoped.ConnectionString));
            await db.Database.MigrateAsync();
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(before, await SnapshotAsync(scoped.ConnectionString));
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

    [Fact]
    public async Task DisableAndRotationRaceCannotLeaveUsableCredentials()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        ProvisionedIntegrationSource first;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "race_disable", "Disable Race", CancellationToken.None);
        }

        async Task<ProvisionedIntegrationSource?> Rotate()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            try { return await store.RotateAsync(fixture.A, "race_disable", CancellationToken.None); }
            catch (InvalidOperationException) { return null; }
        }

        async Task Disable()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .DisableAsync(fixture.A, "race_disable", CancellationToken.None);
        }

        var rotated = Rotate();
        var disabled = Disable();
        await Task.WhenAll(rotated, disabled).WaitAsync(TimeSpan.FromSeconds(30));

        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var storeAfter = verification.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        Assert.Null(await storeAfter.ValidateAsync(first.Bearer, CancellationToken.None));
        if (rotated.Result is not null)
            Assert.Null(await storeAfter.ValidateAsync(rotated.Result.Bearer, CancellationToken.None));
        var source = await db.IntegrationSources.AsNoTracking().SingleAsync(
            x => x.OrganizationId == fixture.A && x.SourceCode == "race_disable");
        Assert.Equal("Disabled", source.Status);
        Assert.Equal(0, await db.IntegrationCredentials.AsNoTracking().CountAsync(
            x => x.SourceId == source.Id && x.RevokedAtUtc == null));
    }

    [Fact]
    public async Task RevocationInvalidatesAllSubsequentAuthenticationsAcrossRequests()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        ProvisionedIntegrationSource issued;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            issued = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "race_revoke", "Revoke Race", CancellationToken.None);
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Validate()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            started.TrySetResult();
            await store.ValidateAsync(issued.Bearer, cancellation.Token);
        }
        var validation = Task.Run(Validate, cancellation.Token);
        await started.Task.WaitAsync(cancellation.Token);
        await using (var revokeScope = factory.Services.CreateAsyncScope())
        {
            await revokeScope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .RevokeAsync(fixture.A, "race_revoke", issued.CredentialId, cancellation.Token);
        }
        await validation.WaitAsync(cancellation.Token);
        for (var i = 0; i < 3; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .ValidateAsync(issued.Bearer, cancellation.Token));
        }
    }
}
