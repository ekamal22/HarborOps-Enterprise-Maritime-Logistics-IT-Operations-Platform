using System.Net;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentConcurrencyTests
{
    [Fact]
    public async Task QueuedAllocationAndParentArchiveCannotCommitInconsistentState()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        await using var blocker = new Npgsql.NpgsqlConnection(TestDatabase.ConnectionString());
        await using var probe = new Npgsql.NpgsqlConnection(TestDatabase.ConnectionString());
        await blocker.OpenAsync();
        await probe.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var lockRow = new Npgsql.NpgsqlCommand(
            "SELECT id FROM containers WHERE organization_id=@org AND id=@id FOR UPDATE",
            blocker, transaction))
        {
            lockRow.Parameters.AddWithValue("org", f.A);
            lockRow.Parameters.AddWithValue("id", seed.Container);
            Assert.Equal(seed.Container,(Guid)(await lockRow.ExecuteScalarAsync())!);
        }

        var route = seed.Root + "/" + seed.Shipment + "/container-assignments";
        var incoming = ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2036-01-02T10:00:00Z" });
        var observed = false;
        var committed = false;
        var deadline = DateTime.UtcNow.AddSeconds(35);
        while (DateTime.UtcNow < deadline)
        {
            await using var observation = new Npgsql.NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE pid<>pg_backend_pid() AND wait_event_type='Lock'
                  AND state='active' AND query ILIKE '%containers%'
                  AND query ILIKE '%FOR SHARE%'
                """,probe);
            if ((long)(await observation.ExecuteScalarAsync())! > 0)
            {
                observed=true;break;
            }
            await Task.Delay(50);
        }
        try
        {
            Assert.True(observed,
                "Assignment insert did not reach the observed parent row lock");
            var archive = ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
                "/api/v1/organizations/" + f.A + "/containers/" + seed.Container,
                new { expectedVersion = 1L });
            await transaction.CommitAsync();
            committed = true;
            using var created = await incoming;
            using var denied = await archive;
            Assert.Equal(HttpStatusCode.Created,created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,denied.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            Assert.Equal("Active",(await db.Containers.AsNoTracking()
                .SingleAsync(x => x.Id==seed.Container)).RecordStatus);
            Assert.Equal(1,await db.ShipmentContainerAssignments.AsNoTracking()
                .CountAsync(x=>x.OrganizationId==f.A&&x.ContainerId==seed.Container&&x.EndsAtUtc==null));
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync();
        }
    }


    [Fact]
    public async Task ContainerArchiveAndAllocationCannotBothCommit()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var allocation = seed.Root + "/" + seed.Shipment + "/container-assignments";
        var parent = "/api/v1/organizations/" + f.A + "/containers/" + seed.Container;
        var create = ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, allocation,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" });
        var archive = ReferenceDataHttpTests.Send(manager, HttpMethod.Delete, parent,
            new { expectedVersion = 1L });
        using var a = await create;
        using var b = await archive;
        Assert.Contains(HttpStatusCode.Conflict,new[] { a.StatusCode,b.StatusCode });
        Assert.Equal(1,new[] { a.StatusCode,b.StatusCode }.Count(x =>
            x == HttpStatusCode.Created || x == HttpStatusCode.NoContent));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var record = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == seed.Container);
        var open = await db.ShipmentContainerAssignments.AsNoTracking()
            .CountAsync(x => x.ContainerId == seed.Container && x.EndsAtUtc == null);
        Assert.False(record.RecordStatus == "Archived" && open > 0);
    }

    [Fact]
    public async Task VoyageCancellationAndAllocationCannotBothCommit()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var allocation = seed.Root + "/" + seed.Shipment + "/voyage-assignments";
        var parent = "/api/v1/organizations/" + f.A + "/voyages/" + seed.Voyage;
        var create = ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, allocation,
            new { voyageId = seed.Voyage, startsAtUtc = "2026-11-10T10:00:00Z" });
        var cancel = ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, parent,
            new { voyageStatus = "Cancelled", expectedVersion = 1L });
        using var a = await create;
        using var b = await cancel;
        Assert.Contains(HttpStatusCode.Conflict,new[] { a.StatusCode,b.StatusCode });
        Assert.Equal(1,new[] { a.StatusCode,b.StatusCode }.Count(x =>
            x == HttpStatusCode.Created || x == HttpStatusCode.OK));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var record = await db.Voyages.AsNoTracking().SingleAsync(x => x.Id == seed.Voyage);
        var open = await db.ShipmentVoyageAssignments.AsNoTracking()
            .CountAsync(x => x.VoyageId == seed.Voyage && x.EndsAtUtc == null);
        Assert.False(record.VoyageStatus == "Cancelled" && open > 0);
    }
}
