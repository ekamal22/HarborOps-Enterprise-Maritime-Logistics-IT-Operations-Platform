using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class OrganizationIsolationTests
{
    private const string Password = "Synthetic-organization-test-passphrase-123";

    [Fact]
    public async Task TwoOrganizationsHaveIndependentSelectionsAndLiveRoleChanges()
    {
        var connection = TestDatabase.ConnectionString();
        var services = new ServiceCollection()
            .AddHarborOpsPersistence(connection)
            .BuildServiceProvider();
        using var serviceScope = services.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = serviceScope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = serviceScope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var suffix = Guid.NewGuid().ToString("N");
        var adminEmail = "admin-" + suffix + "@example.test";
        var otherEmail = "other-" + suffix + "@example.test";
        var dualEmail = "dual-" + suffix + "@example.test";
        foreach (var email in new[] { adminEmail, otherEmail, dualEmail })
            await identity.ProvisionAsync(email, "Synthetic Member", Password, CancellationToken.None);
        var a = await store.BootstrapAsync("north-" + suffix[..10], "North Harbor",
            adminEmail, CancellationToken.None);
        var b = await store.BootstrapAsync("south-" + suffix[..10], "South Harbor",
            otherEmail, CancellationToken.None);
        var adminSession = (await identity.LoginAsync(adminEmail, Password, "org-test", CancellationToken.None))!;
        var otherSession = (await identity.LoginAsync(otherEmail, Password, "org-test", CancellationToken.None))!;
        var dualSession = (await identity.LoginAsync(dualEmail, Password, "org-test", CancellationToken.None))!;

        Assert.NotNull((await store.SelectAsync(adminSession.UserId, adminSession.SessionId,
            a.Id, CancellationToken.None)).Value);
        Assert.NotNull((await store.SelectAsync(otherSession.UserId, otherSession.SessionId,
            b.Id, CancellationToken.None)).Value);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.ResolveAsync(adminSession.UserId, adminSession.SessionId,
                b.Id, OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.SelectAsync(dualSession.UserId, dualSession.SessionId,
                b.Id, CancellationToken.None)).Error);
        var grantA = await store.PutMemberAsync(adminSession.UserId, adminSession.SessionId,
            a.Id, dualSession.UserId, "Analyst", "Active", null,
            "org-test", CancellationToken.None);
        Assert.NotNull(grantA.Value);
        var grantB = await store.PutMemberAsync(otherSession.UserId, otherSession.SessionId,
            b.Id, dualSession.UserId, "Manager", "Active", null,
            "org-test", CancellationToken.None);
        Assert.NotNull(grantB.Value);

        Assert.Equal(OrganizationRole.Analyst,
            (await store.SelectAsync(dualSession.UserId, dualSession.SessionId,
                a.Id, CancellationToken.None)).Value!.Role);
        Assert.Equal(OrganizationAccessError.Forbidden,
            (await store.ResolveAsync(dualSession.UserId, dualSession.SessionId,
                a.Id, OrganizationPermission.ManageMemberships, CancellationToken.None)).Error);
        Assert.Equal(OrganizationRole.Manager,
            (await store.SelectAsync(dualSession.UserId, dualSession.SessionId,
                b.Id, CancellationToken.None)).Value!.Role);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.ResolveAsync(dualSession.UserId, dualSession.SessionId,
                a.Id, OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);

        var removed = await store.RemoveMemberAsync(otherSession.UserId,
            otherSession.SessionId, b.Id, dualSession.UserId, grantB.Value!.Version,
            "org-test", CancellationToken.None);
        Assert.Equal(OrganizationAccessError.None, removed);
        Assert.Equal(OrganizationAccessError.SelectionRequired,
            (await store.ResolveAsync(dualSession.UserId, dualSession.SessionId,
                null, OrganizationPermission.ViewOrganization, CancellationToken.None)).Error);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.SelectAsync(dualSession.UserId, dualSession.SessionId,
                b.Id, CancellationToken.None)).Error);
        Assert.Equal(OrganizationRole.Analyst,
            (await store.SelectAsync(dualSession.UserId, dualSession.SessionId,
                a.Id, CancellationToken.None)).Value!.Role);
    }

    [Fact]
    public async Task MissingAndStaleMembershipCannotAlterAdministrator()
    {
        var services = new ServiceCollection()
            .AddHarborOpsPersistence(TestDatabase.ConnectionString()).BuildServiceProvider();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var suffix = Guid.NewGuid().ToString("N");
        var adminEmail = "root-" + suffix + "@example.test";
        var memberEmail = "member-" + suffix + "@example.test";
        await identity.ProvisionAsync(adminEmail, "Root", Password, CancellationToken.None);
        await identity.ProvisionAsync(memberEmail, "Member", Password, CancellationToken.None);
        var org = await store.BootstrapAsync("group-" + suffix[..10],
            "Test Group", adminEmail, CancellationToken.None);
        var admin = (await identity.LoginAsync(adminEmail, Password, "org-test", CancellationToken.None))!;
        var member = (await identity.LoginAsync(memberEmail, Password, "org-test", CancellationToken.None))!;
        await store.SelectAsync(admin.UserId, admin.SessionId, org.Id, CancellationToken.None);

        Assert.Equal(OrganizationAccessError.Forbidden,
            (await store.RemoveMemberAsync(admin.UserId, admin.SessionId, org.Id,
                admin.UserId, 1, "org-test", CancellationToken.None)));
        var created = await store.PutMemberAsync(admin.UserId, admin.SessionId,
            org.Id, member.UserId, "Auditor", "Active", null,
            "org-test", CancellationToken.None);
        Assert.NotNull(created.Value);
        var stale = await store.PutMemberAsync(admin.UserId, admin.SessionId,
            org.Id, member.UserId, "Analyst", "Active", 42,
            "org-test", CancellationToken.None);
        Assert.Equal(OrganizationAccessError.Conflict, stale.Error);
        Assert.Equal(OrganizationAccessError.NotFound,
            (await store.SelectAsync(member.UserId, member.SessionId,
                Guid.NewGuid(), CancellationToken.None)).Error);
        Assert.Equal(OrganizationRole.Auditor,
            (await store.SelectAsync(member.UserId, member.SessionId,
                org.Id, CancellationToken.None)).Value!.Role);
    }
}
