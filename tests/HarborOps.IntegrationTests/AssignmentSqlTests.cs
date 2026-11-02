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
            UPDATE containers SET record_status={"Archived"},version=version+1,
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
                AND contype='x' AND connamespace=current_schema()::regnamespace
            """).SingleAsync();
        Assert.Equal(2, sql);
    }

    [Fact]
    public async Task CompositeForeignKeysRejectCrossTenantParentsWithoutValidationTrigger()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // Transactional trigger suppression isolates the FK itself; ROLLBACK restores
        // both validation triggers. System-generated FK triggers remain enabled.
        await using (var disable = new NpgsqlCommand("""
            ALTER TABLE shipment_container_assignments DISABLE TRIGGER m6_container_validate;
            ALTER TABLE shipment_voyage_assignments DISABLE TRIGGER m6_voyage_validate;
            """, connection, transaction))
            await disable.ExecuteNonQueryAsync();

        Guid actor;
        await using (var user = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE organization_id=@org AND id=@shipment",
            connection, transaction))
        {
            user.Parameters.AddWithValue("org", f.A);
            user.Parameters.AddWithValue("shipment", seed.Shipment);
            actor = (Guid)(await user.ExecuteScalarAsync())!;
        }
        async Task Probe(string table, Guid org, Guid shipment, Guid target, string expected)
        {
            await using (var save = new NpgsqlCommand("SAVEPOINT fk_probe", connection, transaction))
                await save.ExecuteNonQueryAsync();
            var targetColumn = table == "shipment_container_assignments" ? "container_id" : "voyage_id";
            try
            {
                await using var cmd = new NpgsqlCommand(
                    $"INSERT INTO {table} (id,organization_id,shipment_id,{targetColumn}," +
                    "starts_at_utc,created_at_utc,updated_at_utc,created_by_user_id) " +
                    "VALUES (@id,@org,@shipment,@target,@utc,@utc,@utc,@actor)",
                    connection, transaction);
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("org", org);
                cmd.Parameters.AddWithValue("shipment", shipment);
                cmd.Parameters.AddWithValue("target", target);
                cmd.Parameters.AddWithValue("utc", DateTimeOffset.UtcNow);
                cmd.Parameters.AddWithValue("actor", actor);
                var error = await Assert.ThrowsAsync<PostgresException>(
                    () => cmd.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
                Assert.Equal(expected, error.ConstraintName);
            }
            finally
            {
                await using var rollback = new NpgsqlCommand(
                    "ROLLBACK TO SAVEPOINT fk_probe", connection, transaction);
                await rollback.ExecuteNonQueryAsync();
            }
        }
        // Foreign tenant shipment and valid own target.
        await Probe("shipment_container_assignments", f.B, seed.Shipment,
            seed.Container, "FK_m6_container_shipment");
        await Probe("shipment_container_assignments", f.A, seed.Shipment,
            Guid.NewGuid(), "FK_m6_container_target");
        await Probe("shipment_voyage_assignments", f.B, seed.Shipment,
            seed.Voyage, "FK_m6_voyage_shipment");
        await Probe("shipment_voyage_assignments", f.A, seed.Shipment,
            Guid.NewGuid(), "FK_m6_voyage_target");
        await transaction.RollbackAsync();
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
