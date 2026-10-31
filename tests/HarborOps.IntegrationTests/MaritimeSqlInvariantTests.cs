using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeSqlInvariantTests
{
    private static async Task<Guid> Create(HttpClient client, string route, object body)
    {
        using var response = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, route, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<PostgresException> CheckFailure(Func<Task<int>> operation) =>
        await Assert.ThrowsAsync<PostgresException>(operation);

    [Fact]
    public async Task DirectSqlCannotBypassVoyageTransitionRouteVersionOrScheduleRules()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var root = $"/api/v1/organizations/{f.A}";
        var first = await Create(manager, root + "/ports", new {
            referenceCode = "SQLV-PORT1", name = "SQL First", countryCode = "US"
        });
        var second = await Create(manager, root + "/ports", new {
            referenceCode = "SQLV-PORT2", name = "SQL Second", countryCode = "US"
        });
        var vessel = await Create(manager, root + "/vessels", new {
            referenceCode = "SQLV-VESSEL", name = "SQL Vessel"
        });
        var voyage = await Create(manager, root + "/voyages", new {
            referenceCode = "SQLV-VOYAGE", vesselId = vessel,
            originPortId = first, destinationPortId = second,
            plannedDepartureAtUtc = "2026-11-01T22:00:00Z",
            plannedArrivalAtUtc = "2026-11-02T06:00:00Z"
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var updated = DateTimeOffset.UtcNow.AddMinutes(10);
        var illegalVersion = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE voyages SET version = {4L}, updated_at_utc = {updated} WHERE id = {voyage}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, illegalVersion.SqlState);

        var illegalRoute = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE voyages SET destination_port_id = {first}, version = {2L}, updated_at_utc = {updated} WHERE id = {voyage}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, illegalRoute.SqlState);

        var illegalTransition = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE voyages SET voyage_status = {"Arrived"}, actual_departure_at_utc = {DateTimeOffset.Parse("2026-11-01T23:00:00Z")}, actual_arrival_at_utc = {DateTimeOffset.Parse("2026-11-02T03:00:00Z")}, version = {2L}, updated_at_utc = {updated} WHERE id = {voyage}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, illegalTransition.SqlState);

        var badWindow = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE voyages SET planned_arrival_at_utc = {DateTimeOffset.Parse("2026-11-01T21:00:00Z")}, version = {2L}, updated_at_utc = {updated} WHERE id = {voyage}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badWindow.SqlState);

        var prematureActual = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE voyages SET actual_departure_at_utc = {DateTimeOffset.Parse("2026-11-01T23:00:00Z")}, version = {2L}, updated_at_utc = {updated} WHERE id = {voyage}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, prematureActual.SqlState);

        var row = await db.Voyages.AsNoTracking().SingleAsync(x => x.Id == voyage);
        Assert.Equal(1L, row.Version);
        Assert.Equal("Planned", row.VoyageStatus);
        Assert.Equal(second, row.DestinationPortId);
        Assert.Null(row.ActualDepartureAtUtc);
    }

    [Fact]
    public async Task DirectSqlCannotModifyArchivedContainerOrImmutableIdentifiers()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var root = $"/api/v1/organizations/{f.A}/containers";
        var id = await Create(manager, root, new {
            referenceCode = "SQL-CHECK-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-SQLCHECK", equipmentType = "Dry40"
        });
        var updated = DateTimeOffset.UtcNow.AddMinutes(10);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var changedKind = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE containers SET identifier_kind = {"ISO6346"}, container_identifier = {"CSQU3054383"}, source_kind = {"UserProvided"}, version = {2L}, updated_at_utc = {updated} WHERE id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, changedKind.SqlState);

        var changedCode = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE containers SET reference_code = {"SQL-CHANGED"}, version = {2L}, updated_at_utc = {updated} WHERE id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, changedCode.SqlState);

        using var archived = await ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
            root + "/" + id, new { expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        var archiveChange = await CheckFailure(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE containers SET display_name = {"Direct write"}, version = {3L}, updated_at_utc = {updated} WHERE id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, archiveChange.SqlState);
        var row = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Archived", row.RecordStatus);
        Assert.Null(row.DisplayName);
        Assert.Equal("SYN-BOX-SQLCHECK", row.ContainerIdentifier);
        Assert.Equal(2L, row.Version);
    }
}
