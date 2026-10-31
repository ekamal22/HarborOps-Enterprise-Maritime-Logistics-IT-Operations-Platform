using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class OrganizationAdversarialTests
{
    private const string Password = "Synthetic-g2-adversarial-passphrase-123";
    private const string EventId = "g2-adversarial";

    private static ServiceProvider Provider() =>
        new ServiceCollection().AddHarborOpsPersistence(TestDatabase.ConnectionString())
            .BuildServiceProvider();

    private static string Email(string prefix, string suffix) =>
        prefix + "-" + suffix + "@example.test";

    [Fact]
    public async Task ConcurrentAdministratorsCannotDemoteEachOtherAndLeaveZeroAdministrators()
    {
        using var provider = Provider();
        using var seed = provider.CreateScope();
        var db = seed.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = seed.ServiceProvider.GetRequiredService<IdentityStore>();
        var orgStore = seed.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var a = Email("g2-admin-a", nonce);
        var b = Email("g2-admin-b", nonce);
        await identity.ProvisionAsync(a, "Administrator A", Password, CancellationToken.None);
        await identity.ProvisionAsync(b, "Administrator B", Password, CancellationToken.None);
        var org = await orgStore.BootstrapAsync("g2-race-" + nonce[..12],
            "G2 Concurrent Administrators", a, CancellationToken.None);
        var sa = (await identity.LoginAsync(a, Password, EventId, CancellationToken.None))!;
        var sb = (await identity.LoginAsync(b, Password, EventId, CancellationToken.None))!;
        Assert.NotNull((await orgStore.SelectAsync(sa.UserId, sa.SessionId,
            org.Id, CancellationToken.None)).Value);
        var added = await orgStore.PutMemberAsync(sa.UserId, sa.SessionId,
            org.Id, sb.UserId, "Administrator", "Active", null, EventId, CancellationToken.None);
        Assert.NotNull(added.Value);
        Assert.NotNull((await orgStore.SelectAsync(sb.UserId, sb.SessionId,
            org.Id, CancellationToken.None)).Value);

        // Separate scoped EF contexts and transactions compete for the same organization lock.
        using var operationA = provider.CreateScope();
        using var operationB = provider.CreateScope();
        var storeA = operationA.ServiceProvider.GetRequiredService<OrganizationStore>();
        var storeB = operationB.ServiceProvider.GetRequiredService<OrganizationStore>();
        var first = storeA.PutMemberAsync(sa.UserId, sa.SessionId, org.Id, sb.UserId,
            "Analyst", "Active", added.Value!.Version, EventId, CancellationToken.None);
        var second = storeB.PutMemberAsync(sb.UserId, sb.SessionId, org.Id, sa.UserId,
            "Analyst", "Active", 1, EventId, CancellationToken.None);
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Single(results, x => x.Error == OrganizationAccessError.None);
        Assert.Single(results, x => x.Error is OrganizationAccessError.Forbidden
            or OrganizationAccessError.Revoked or OrganizationAccessError.Conflict);

        var admins = await (from membership in db.OrganizationMemberships.AsNoTracking()
                            join user in db.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
                            where membership.OrganizationId == org.Id &&
                                membership.Role == "Administrator" && membership.Status == "Active" &&
                                user.Status == "Active"
                            select user.Id).ToListAsync();
        Assert.Single(admins);
        var surviving = admins[0];
        Assert.Equal(OrganizationAccessError.Forbidden,
            (await orgStore.PutMemberAsync(surviving, surviving == sa.UserId ? sa.SessionId : sb.SessionId,
                org.Id, surviving, "Analyst", "Active", null, EventId, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task SuspensionAndLiveRoleRevocationInvalidateCurrentSessions()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminEmail = Email("g2-suspend-admin", nonce);
        var userEmail = Email("g2-suspend-user", nonce);
        await identity.ProvisionAsync(adminEmail, "Administrator", Password, CancellationToken.None);
        await identity.ProvisionAsync(userEmail, "Member", Password, CancellationToken.None);
        var org = await store.BootstrapAsync("g2-suspend-" + nonce[..12], "Suspension Test",
            adminEmail, CancellationToken.None);
        var admin = (await identity.LoginAsync(adminEmail, Password, EventId, CancellationToken.None))!;
        var userA = (await identity.LoginAsync(userEmail, Password, EventId, CancellationToken.None))!;
        var userB = (await identity.LoginAsync(userEmail, Password, EventId, CancellationToken.None))!;
        Assert.NotNull((await store.SelectAsync(admin.UserId, admin.SessionId,
            org.Id, CancellationToken.None)).Value);
        var added = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id,
            userA.UserId, "Analyst", "Active", null, EventId, CancellationToken.None);
        Assert.NotNull(added.Value);
        Assert.NotNull((await store.SelectAsync(userA.UserId, userA.SessionId,
            org.Id, CancellationToken.None)).Value);
        Assert.NotNull((await store.SelectAsync(userB.UserId, userB.SessionId,
            org.Id, CancellationToken.None)).Value);

        // Two sessions for one account remain independent server-side.
        await store.ClearSelectionAsync(userA.UserId, userA.SessionId, CancellationToken.None);
        Assert.Equal(OrganizationAccessError.SelectionRequired,
            (await store.ResolveAsync(userA.UserId, userA.SessionId, null,
                OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);
        Assert.NotNull((await store.ResolveAsync(userB.UserId, userB.SessionId, org.Id,
            OrganizationPermission.ViewOrganization, CancellationToken.None)).Value);
        Assert.NotNull((await store.SelectAsync(userA.UserId, userA.SessionId,
            org.Id, CancellationToken.None)).Value);

        var modified = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id,
            userA.UserId, "Manager", "Active", added.Value!.Version, EventId, CancellationToken.None);
        Assert.NotNull(modified.Value);
        Assert.Equal(OrganizationRole.Manager,
            (await store.ResolveAsync(userB.UserId, userB.SessionId, org.Id,
                OrganizationPermission.ViewOrganization, CancellationToken.None)).Value!.Role);
        Assert.Equal(OrganizationAccessError.Forbidden,
            (await store.ResolveAsync(userA.UserId, userA.SessionId, org.Id,
                OrganizationPermission.ManageMemberships, CancellationToken.None)).Error);

        var suspendMember = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id,
            userA.UserId, "Manager", "Suspended", modified.Value!.Version, EventId, CancellationToken.None);
        Assert.NotNull(suspendMember.Value);
        Assert.Equal(OrganizationAccessError.SelectionRequired,
            (await store.ResolveAsync(userB.UserId, userB.SessionId, null,
                OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.SelectAsync(userB.UserId, userB.SessionId,
                org.Id, CancellationToken.None)).Error);
        Assert.DoesNotContain(await store.ListMineAsync(userA.UserId, CancellationToken.None),
            x => x.Id == org.Id);

        var restored = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id,
            userA.UserId, "Manager", "Active", suspendMember.Value!.Version,
            EventId, CancellationToken.None);
        Assert.NotNull(restored.Value);
        Assert.NotNull((await store.SelectAsync(userA.UserId, userA.SessionId,
            org.Id, CancellationToken.None)).Value);

        await db.Organizations.Where(x => x.Id == org.Id).ExecuteUpdateAsync(
            x => x.SetProperty(o => o.Status, "Suspended"));
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.SelectAsync(userB.UserId, userB.SessionId,
                org.Id, CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.Revoked,
            (await store.ResolveAsync(userA.UserId, userA.SessionId, org.Id,
                OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.Revoked,
            (await store.ResolveAsync(admin.UserId, admin.SessionId, org.Id,
                OrganizationPermission.ManageMemberships, CancellationToken.None)).Error);
        Assert.DoesNotContain(await store.ListMineAsync(admin.UserId, CancellationToken.None),
            x => x.Id == org.Id);
        Assert.Equal(OrganizationAccessError.Revoked,
            (await store.UpdateProfileAsync(admin.UserId, admin.SessionId,
                org.Id, "Wrong Update", org.Version, EventId, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task DatabaseRejectsInvalidRoleStatusVersionAndDuplicateMembership()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var email = Email("g2-constraints", nonce);
        await identity.ProvisionAsync(email, "Administrator", Password, CancellationToken.None);
        var org = await store.BootstrapAsync("g2-constraints-" + nonce[..10],
            "Constraint Test", email, CancellationToken.None);
        var member = await db.OrganizationMemberships.AsNoTracking()
            .SingleAsync(x => x.OrganizationId == org.Id);
        foreach (var (column, value) in new[] {
            ("role", "Superuser"), ("status", "Unknown"), ("version", "0") })
        {
            // Column names are fixed test literals; values are intentionally invalid.
            var sql = column == "role"
                ? "UPDATE organization_memberships SET role = 'Superuser' WHERE id = @id"
                : column == "status"
                    ? "UPDATE organization_memberships SET status = 'Unknown' WHERE id = @id"
                    : "UPDATE organization_memberships SET version = 0 WHERE id = @id";
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("id", member.Id)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO organization_memberships
                    (id, organization_id, user_id, role, status, version,
                     created_at_utc, updated_at_utc)
                VALUES ({Guid.NewGuid()}, {org.Id}, {member.UserId},
                    'Analyst', 'Active', 1, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var invalidSlug = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO organizations
                    (id, slug, display_name, status, version, created_at_utc, updated_at_utc)
                VALUES ({Guid.NewGuid()}, {"Wrong Slug"}, {"Not Blank"}, {"Active"}, {1L},
                    {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidSlug.SqlState);
    }

    [Fact]
    public async Task BootstrapRejectsUnknownAndDuplicateAccountsWithoutPartialWrites()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var slug = "g2-bootstrap-" + nonce[..12];
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.BootstrapAsync(slug, "Missing", Email("nobody", nonce), CancellationToken.None));
        Assert.False(await db.Organizations.AnyAsync(x => x.Slug == slug));
        var email = Email("g2-bootstrap", nonce);
        await identity.ProvisionAsync(email, "Existing", Password, CancellationToken.None);
        var org = await store.BootstrapAsync(slug, "Valid", email, CancellationToken.None);
        Assert.Equal(slug, org.Slug);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.BootstrapAsync(slug, "Duplicate", email, CancellationToken.None));
        Assert.Single(await db.Organizations.Where(x => x.Slug == slug).ToListAsync());
    }
}
