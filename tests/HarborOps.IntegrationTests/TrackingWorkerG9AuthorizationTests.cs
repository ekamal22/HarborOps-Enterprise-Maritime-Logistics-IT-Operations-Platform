using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// G9-S19 negatives: operator queue access and redrive stay denied after every kind of revocation,
/// and foreign organization identifiers never cross a tenant boundary.
/// </summary>
public sealed class TrackingWorkerG9AuthorizationTests
{
    private static string Key(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

    internal static async Task<Guid> ParkedReceipt(ReferenceDataHttpTests.ApiFactory factory, Guid organization,
        ProvisionedIntegrationSource source, string label)
    {
        var receipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key(label), "SYN-BOX-UNKNOWN"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await store.ClaimOneAsync(organization, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(receipt, claim!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, await store.FinalizeAsync(claim, CancellationToken.None));
        return receipt;
    }

    [Theory]
    [InlineData("session_revoked", HttpStatusCode.Unauthorized)]
    [InlineData("account_disabled", HttpStatusCode.Unauthorized)]
    [InlineData("security_version", HttpStatusCode.Unauthorized)]
    [InlineData("membership_removed", HttpStatusCode.Forbidden)]
    [InlineData("organization_suspended", HttpStatusCode.Forbidden)]
    public async Task ManagerQueueAccessAndRedriveStayDeniedAfterRevocation(string invalidation, HttpStatusCode expected)
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var source = await G9Support.NewSource(factory, fixture.A, "g9_auth");
        var receipt = await ParkedReceipt(factory, fixture.A, source, "g9-auth-" + invalidation);
        var url = "/api/v1/organizations/" + fixture.A + "/tracking-work";

        // Control: before the change the same Manager can read and the request is well formed.
        using (var allowed = await manager.GetAsync(url + "/" + receipt))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        Guid userId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            userId = (await db.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(fixture.ManagerEmail))).Id;
        }
        await using (var direct = new NpgsqlConnection(connection))
        {
            await direct.OpenAsync();
            var sql = invalidation switch
            {
                "session_revoked" => "UPDATE auth_sessions SET revoked_at_utc = now() WHERE user_id = @user",
                "account_disabled" => "UPDATE users SET status = 'Disabled', disabled_at_utc = now() WHERE id = @user",
                "security_version" => "UPDATE users SET security_version = security_version + 1 WHERE id = @user",
                "membership_removed" => "UPDATE organization_memberships SET status = 'Removed', version = version + 1, updated_at_utc = now() WHERE organization_id = @org AND user_id = @user",
                _ => "UPDATE organizations SET status = 'Suspended' WHERE id = @org"
            };
            await using var command = new NpgsqlCommand(sql, direct);
            command.Parameters.AddWithValue("user", userId);
            command.Parameters.AddWithValue("org", fixture.A);
            Assert.True(await command.ExecuteNonQueryAsync() >= 1);
        }

        try
        {
            using var list = await manager.GetAsync(url);
            Assert.Equal(expected, list.StatusCode);
            using var one = await manager.GetAsync(url + "/" + receipt);
            Assert.Equal(expected, one.StatusCode);
            using var redrive = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
                url + "/" + receipt + "/redrive", new { reason = "denied after " + invalidation });
            Assert.Equal(expected, redrive.StatusCode);

            await using var verify = factory.Services.CreateAsyncScope();
            var db = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var work = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
            Assert.Equal("NeedsReconciliation", work.State);
            Assert.Equal(0, work.RedriveGeneration);
            Assert.Equal(0, await db.TrackingWorkAttemptEvents.CountAsync(x =>
                x.ReceiptId == receipt && x.EventKind == "Redriven"));
        }
        finally
        {
            if (invalidation == "organization_suspended")
            {
                await using var direct = new NpgsqlConnection(connection);
                await direct.OpenAsync();
                await using var restore = new NpgsqlCommand(
                    "UPDATE organizations SET status = 'Active' WHERE id = @org", direct);
                restore.Parameters.AddWithValue("org", fixture.A);
                await restore.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task ForeignOrganizationIdentifiersNeverCrossTheTenantBoundary()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var source = await G9Support.NewSource(factory, fixture.A, "g9_foreign");
        var receipt = await ParkedReceipt(factory, fixture.A, source, "g9-foreign");
        var own = "/api/v1/organizations/" + fixture.A + "/tracking-work";
        var foreign = "/api/v1/organizations/" + fixture.B + "/tracking-work";

        // The Manager belongs to organization A only: B's queue and A's receipt through B's path are unavailable.
        using (var list = await manager.GetAsync(foreign))
            Assert.NotEqual(HttpStatusCode.OK, list.StatusCode);
        using (var item = await manager.GetAsync(foreign + "/" + receipt))
            Assert.NotEqual(HttpStatusCode.OK, item.StatusCode);
        using (var redrive = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            foreign + "/" + receipt + "/redrive", new { reason = "foreign substitution attempt" }))
            Assert.NotEqual(HttpStatusCode.OK, redrive.StatusCode);
        // A receipt that does not exist in the caller's organization is not found, and malformed ids are rejected.
        using (var missing = await manager.GetAsync(own + "/" + Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var malformed = await manager.GetAsync(own + "/not-a-guid"))
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using (var missingRedrive = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            own + "/" + Guid.NewGuid() + "/redrive", new { reason = "unknown receipt redrive" }))
            Assert.Equal(HttpStatusCode.NotFound, missingRedrive.StatusCode);

        // The own-tenant list never contains another tenant's receipts and exposes no raw payload.
        using var ownList = await manager.GetAsync(own);
        Assert.Equal(HttpStatusCode.OK, ownList.StatusCode);
        var text = await ownList.Content.ReadAsStringAsync();
        Assert.Contains(receipt.ToString(), text);
        Assert.DoesNotContain("rawPayload", text);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var work = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
        Assert.Equal("NeedsReconciliation", work.State);
        Assert.Equal(0, work.RedriveGeneration);
        Assert.Equal(fixture.A, work.OrganizationId);
    }

    [Fact]
    public async Task ArchivedContainerCannotCarryAnActiveAssignmentSoItIsNeverResolved()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_archived");

        // The reference model forbids archiving a container that holds an active shipment assignment.
        await using (var direct = new NpgsqlConnection(connection))
        {
            await direct.OpenAsync();
            await using var archive = new NpgsqlCommand("""
                UPDATE containers SET record_status = 'Archived', version = version + 1, updated_at_utc = now()
                WHERE id = @id
                """, direct);
            archive.Parameters.AddWithValue("id", target.Container);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => archive.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
        }

        // A second, unassigned container with the same shape is archived legitimately and never resolves.
        var maritime = await AssignmentHttpTests.Seed(fixture);
        string archivedReference;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            archivedReference = (await db.Containers.AsNoTracking().SingleAsync(c => c.Id == maritime.Container)).ContainerIdentifier;
        }
        await using (var direct = new NpgsqlConnection(connection))
        {
            await direct.OpenAsync();
            await using var archive = new NpgsqlCommand("""
                UPDATE containers SET record_status = 'Archived', version = version + 1, updated_at_utc = now()
                WHERE id = @id
                """, direct);
            archive.Parameters.AddWithValue("id", maritime.Container);
            Assert.Equal(1, await archive.ExecuteNonQueryAsync());
        }
        var archivedReceipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-archived"), archivedReference));
        var activeReceipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-active"), target.Reference));

        await using var work = factory.Services.CreateAsyncScope();
        var database = work.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(database, new TrackingWorkerSettings());
        var outcomes = new Dictionary<Guid, TrackingWorkDisposition>();
        while (await store.ClaimOneAsync(fixture.A, CancellationToken.None) is { } claim)
            outcomes[claim.ReceiptId] = await store.FinalizeAsync(claim, CancellationToken.None);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, outcomes[archivedReceipt]);
        Assert.Equal(TrackingWorkDisposition.Processed, outcomes[activeReceipt]);
        Assert.Equal(0, await database.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == archivedReceipt));
        Assert.Equal(1, await database.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == activeReceipt));
    }
}
