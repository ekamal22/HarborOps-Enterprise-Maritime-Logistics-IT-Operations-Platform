using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class OrganizationFinalInvariantsTests
{
    private const string Password = "Synthetic-G2-final-guards-passphrase-123";

    private static ServiceProvider Services() =>
        new ServiceCollection().AddHarborOpsPersistence(TestDatabase.ConnectionString())
            .BuildServiceProvider();

    [Fact]
    public async Task ConcurrentAccountDisablementCannotEraseBothAdministrators()
    {
        using var services = Services();
        using var setup = services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identities = setup.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = setup.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var firstEmail = "g2-disable-a-" + nonce + "@example.test";
        var secondEmail = "g2-disable-b-" + nonce + "@example.test";
        await identities.ProvisionAsync(firstEmail, "First", Password, CancellationToken.None);
        await identities.ProvisionAsync(secondEmail, "Second", Password, CancellationToken.None);
        var org = await organizations.BootstrapAsync("g2-disabling-" + nonce[..12],
            "Concurrent Disable", firstEmail, CancellationToken.None);
        var a = (await identities.LoginAsync(firstEmail, Password, "g2", CancellationToken.None))!;
        var b = (await identities.LoginAsync(secondEmail, Password, "g2", CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(a.UserId, a.SessionId,
            org.Id, CancellationToken.None)).Value);
        Assert.NotNull((await organizations.PutMemberAsync(a.UserId, a.SessionId,
            org.Id, b.UserId, "Administrator", "Active", null, "g2", CancellationToken.None)).Value);
        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var storeA = firstScope.ServiceProvider.GetRequiredService<IdentityStore>();
        var storeB = secondScope.ServiceProvider.GetRequiredService<IdentityStore>();
        async Task<bool> Attempt(IdentityStore store, Guid userId) {
            try { return await store.DisableSelfAsync(userId, Password, "g2", CancellationToken.None); }
            catch (LastActiveAdministratorException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(storeA, a.UserId),
            Attempt(storeB, b.UserId)).WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Single(results, x => x);
        Assert.Single(results, x => !x);
        var remaining = await (from membership in db.OrganizationMemberships.AsNoTracking()
                               join user in db.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
                               where membership.OrganizationId == org.Id &&
                                  membership.Status == "Active" && membership.Role == "Administrator" &&
                                  user.Status == "Active"
                               select membership.Id).ToListAsync();
        Assert.Single(remaining);
    }

    [Fact]
    public async Task StaleWritesAndRepeatedGrantsCannotSilentlyEscalate()
    {
        using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminEmail = "g2-versions-admin-" + nonce + "@example.test";
        var memberEmail = "g2-versions-member-" + nonce + "@example.test";
        await identity.ProvisionAsync(adminEmail, "Admin", Password, CancellationToken.None);
        await identity.ProvisionAsync(memberEmail, "Member", Password, CancellationToken.None);
        var org = await store.BootstrapAsync("g2-version-" + nonce[..12],
            "Versioning", adminEmail, CancellationToken.None);
        var admin = (await identity.LoginAsync(adminEmail, Password, "g2", CancellationToken.None))!;
        var memberId = (await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(memberEmail))).Id;
        Assert.NotNull((await store.SelectAsync(admin.UserId, admin.SessionId,
            org.Id, CancellationToken.None)).Value);
        var created = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id, memberId,
            "Analyst", "Active", null, "g2", CancellationToken.None);
        Assert.NotNull(created.Value);
        var duplicated = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id, memberId,
            "Administrator", "Active", null, "g2", CancellationToken.None);
        Assert.Equal(OrganizationAccessError.Conflict, duplicated.Error);
        var changed = await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id, memberId,
            "Manager", "Active", 1, "g2", CancellationToken.None);
        Assert.NotNull(changed.Value);
        Assert.Equal(OrganizationAccessError.Conflict,
            (await store.PutMemberAsync(admin.UserId, admin.SessionId, org.Id, memberId,
                "Administrator", "Active", 1, "g2", CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.Conflict,
            (await store.RemoveMemberAsync(admin.UserId, admin.SessionId, org.Id, memberId,
                1, "g2", CancellationToken.None)));
        Assert.Equal(OrganizationAccessError.Conflict,
            (await store.UpdateProfileAsync(admin.UserId, admin.SessionId, org.Id,
                "Stale", 9, "g2", CancellationToken.None)).Error);
        var actual = await store.GetMemberAsync(
            (await store.ResolveAsync(admin.UserId, admin.SessionId, org.Id,
                OrganizationPermission.ViewMemberships, CancellationToken.None)).Value!,
            memberId, CancellationToken.None);
        Assert.NotNull(actual);
        Assert.Equal("Manager", actual!.Role);
        Assert.Equal("Active", actual.Status);
        Assert.Equal(2, actual.Version);
        Assert.Single(await db.OrganizationMemberships.Where(x =>
            x.OrganizationId == org.Id && x.UserId == memberId).ToListAsync());
    }
}
