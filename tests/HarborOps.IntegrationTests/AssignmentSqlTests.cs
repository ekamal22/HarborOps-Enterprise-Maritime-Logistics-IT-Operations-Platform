using System.Net;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentSqlTests
{
    [Fact]
    public async Task ExclusionConstraintsAndDirectSqlGuardsProtectAllocationHistory()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var route = seed.Root + "/" + seed.Shipment + "/container-assignments";
        using (var response = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var one = await db.ShipmentContainerAssignments.AsNoTracking()
            .SingleAsync(x => x.OrganizationId == f.A && x.ShipmentId == seed.Shipment);
        var now = DateTimeOffset.UtcNow;
        var overlap = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shipment_container_assignments
            (id,organization_id,shipment_id,container_id,starts_at_utc,created_at_utc,
             updated_at_utc,created_by_user_id)
            VALUES ({Guid.NewGuid()},{f.A},{seed.OtherShipment},{seed.Container},
                    {one.StartsAtUtc.AddMinutes(30)},{now},{now},{one.CreatedByUserId})
            """));
        Assert.Equal("23P01", overlap.SqlState);

        var immutable = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shipment_container_assignments SET starts_at_utc={one.StartsAtUtc.AddMinutes(5)},
            version={2L} WHERE id={one.Id}
            """));
        Assert.Equal("23514", immutable.SqlState);
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM shipment_container_assignments WHERE id={one.Id}"));
        Assert.Equal("23514", delete.SqlState);
        var guard = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE containers SET record_status={'A' + "rchived"},version=version+1,
              updated_at_utc={now.AddMinutes(1)}
            WHERE organization_id={f.A} AND id={seed.Container}
            """));
        Assert.Equal("23514", guard.SqlState);
        var retained = await db.ShipmentContainerAssignments.AsNoTracking().SingleAsync(x => x.Id == one.Id);
        Assert.Equal(one.StartsAtUtc, retained.StartsAtUtc);
        Assert.Null(retained.EndsAtUtc);
        Assert.Equal(1L, retained.Version);

        var sql = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_constraint
            WHERE conname IN ('ex_m6_container_overlap','ex_m6_voyage_overlap')
                AND contype='x'
            """).SingleAsync();
        Assert.Equal(2, sql);
    }

    [Fact]
    public async Task RealForeignTenantParentFailsCompositeFk()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var foreignId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Containers.Add(new HarborOps.Infrastructure.Maritime.ContainerRecord {
            Id = foreignId, OrganizationId = f.B,
            ReferenceCode = "G6-FOREIGN", ContainerIdentifier = "SYN-BOX-G6FOREIGN",
            IdentifierKind = "Synthetic", EquipmentType = "Dry20", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        var ownActor = (await db.Shipments.AsNoTracking()
            .SingleAsync(x => x.Id == seed.Shipment)).ChangedByUserId;
        var wrong = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO shipment_container_assignments
                (id,organization_id,shipment_id,container_id,starts_at_utc,
                 created_at_utc,updated_at_utc,created_by_user_id)
                VALUES ({Guid.NewGuid()},{f.A},{seed.Shipment},{foreignId},
                        {now},{now},{now},{ownActor})
                """));
        Assert.True(wrong.SqlState is "23514" or "23503");
        Assert.Empty(await db.ShipmentContainerAssignments.AsNoTracking()
            .Where(x => x.OrganizationId == f.A && x.ContainerId == foreignId).ToListAsync());
    }
}
