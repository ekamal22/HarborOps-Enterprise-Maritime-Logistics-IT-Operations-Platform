using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentVoyageMatrixTests
{
    private static async Task<Guid> Create(HttpClient client,string path,object body)
    {
        using var response=await ReferenceDataHttpTests.Send(client,HttpMethod.Post,path,body);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task VoyageAllowsManyShipmentsAndSequentialReassignmentWithoutWorkflowChanges()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;using var analyst=f.Analyst;using var admin=f.Admin;
        var seed=await AssignmentHttpTests.Seed(f);
        var basePath=seed.Root+"/";
        var first=basePath+seed.Shipment+"/voyage-assignments";
        var second=basePath+seed.OtherShipment+"/voyage-assignments";
        const string start="2040-01-01T10:00:00Z",mid="2040-01-01T12:00:00Z";
        const string end="2040-01-01T13:00:00Z";
        var firstId=await Create(manager,first,new{
            voyageId=seed.Voyage,startsAtUtc=start,endsAtUtc=mid
        });
        var secondId=await Create(analyst,second,new{
            voyageId=seed.Voyage,startsAtUtc=start,endsAtUtc=mid
        });
        Assert.NotEqual(firstId,secondId);
        using(var forbidden=await ReferenceDataHttpTests.Send(analyst,HttpMethod.Post,first,
            new {voyageId=seed.Voyage,startsAtUtc="2040-01-01T11:00:00Z"}))
            Assert.Equal(HttpStatusCode.Conflict,forbidden.StatusCode);
        var details=await manager.GetFromJsonAsync<JsonElement>(
            "/api/v1/organizations/"+f.A+"/voyages/"+seed.Voyage);
        var nonce=Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var otherVoyage=await Create(manager,"/api/v1/organizations/"+f.A+"/voyages",
            new{referenceCode="G6-SEQ-"+nonce,
                vesselId=details.GetProperty("vesselId").GetGuid(),
                originPortId=details.GetProperty("originPortId").GetGuid(),
                destinationPortId=details.GetProperty("destinationPortId").GetGuid()});
        var next=await Create(manager,first,new{
            voyageId=otherVoyage,startsAtUtc=mid,endsAtUtc=end
        });
        using(var list=await analyst.GetAsync(first))
        {
            Assert.Equal(HttpStatusCode.OK,list.StatusCode);
            var rows=(await list.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("assignments").EnumerateArray().ToList();
            Assert.Equal(2,rows.Count);
            Assert.Contains(rows,x=>x.GetProperty("id").GetGuid()==firstId);
            Assert.Contains(rows,x=>x.GetProperty("id").GetGuid()==next);
        }
        using(var firstShipment=await analyst.GetAsync(seed.Root+"/"+seed.Shipment))
        {
            Assert.Equal(HttpStatusCode.OK,firstShipment.StatusCode);
            var shipment=await firstShipment.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Draft",shipment.GetProperty("workflowStatus").GetString());
        }
        using(var voyageAfter=await manager.GetAsync("/api/v1/organizations/"+f.A+"/voyages/"+seed.Voyage))
        {
            Assert.Equal(HttpStatusCode.OK,voyageAfter.StatusCode);
            Assert.Equal("Planned",(await voyageAfter.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("voyageStatus").GetString());
        }
    }

    [Fact]
    public async Task ReverseRouteAndKnownDisjointSchedulesRejectVoyageAssignment()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;using var analyst=f.Analyst;using var admin=f.Admin;
        var seed=await AssignmentHttpTests.Seed(f);
        var voyageDetails=await manager.GetFromJsonAsync<JsonElement>(
            "/api/v1/organizations/"+f.A+"/voyages/"+seed.Voyage);
        var origin=voyageDetails.GetProperty("originPortId").GetGuid();
        var destination=voyageDetails.GetProperty("destinationPortId").GetGuid();
        var vessel=voyageDetails.GetProperty("vesselId").GetGuid();
        var nonce=Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var reversed=await Create(manager,"/api/v1/organizations/"+f.A+"/voyages",
            new {referenceCode="G6-REV-"+nonce,vesselId=vessel,
                originPortId=destination,destinationPortId=origin});
        using(var rejected=await ReferenceDataHttpTests.Send(analyst,HttpMethod.Post,
            seed.Root+"/"+seed.Shipment+"/voyage-assignments",
            new{voyageId=reversed,startsAtUtc="2042-01-01T10:00:00Z"}))
            Assert.Equal(HttpStatusCode.Conflict,rejected.StatusCode);

        var scheduledVoyage=await Create(manager,"/api/v1/organizations/"+f.A+"/voyages",
            new{referenceCode="G6-TIME-"+nonce,vesselId=vessel,
                originPortId=origin,destinationPortId=destination,
                plannedDepartureAtUtc="2040-01-01T10:00:00Z",
                plannedArrivalAtUtc="2040-01-01T12:00:00Z"});
        var scheduledShipment=await Create(analyst,
            "/api/v1/organizations/"+f.A+"/shipments",
            new{referenceCode="G6-TIME-"+nonce,originPortId=origin,
                destinationPortId=destination,
                plannedDepartureAtUtc="2041-01-01T10:00:00Z",
                plannedArrivalAtUtc="2041-01-01T12:00:00Z"});
        using(var disjoint=await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,
            seed.Root+"/"+scheduledShipment+"/voyage-assignments",
            new{voyageId=scheduledVoyage,startsAtUtc="2041-01-01T10:00:00Z"}))
            Assert.Equal(HttpStatusCode.Conflict,disjoint.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Empty(await db.ShipmentVoyageAssignments.AsNoTracking()
            .Where(x=>x.ShipmentId==scheduledShipment).ToListAsync());
    }

    [Fact]
    public async Task DirectSqlVoyageCancellationAndShipmentCancellationCannotBypassOpenLinks()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;using var analyst=f.Analyst;using var admin=f.Admin;
        var seed=await AssignmentHttpTests.Seed(f);
        await Create(analyst,seed.Root+"/"+seed.Shipment+"/voyage-assignments",
            new{voyageId=seed.Voyage,startsAtUtc="2041-01-01T10:00:00Z"});
        await using var db=new NpgsqlConnection(TestDatabase.ConnectionString());
        await db.OpenAsync();
        await using(var voyage=new NpgsqlCommand("""
            UPDATE voyages SET voyage_status='Cancelled', version=version+1,
                updated_at_utc=NOW()
            WHERE organization_id=@org AND id=@voyage
            """,db))
        {
            voyage.Parameters.AddWithValue("org",f.A);
            voyage.Parameters.AddWithValue("voyage",seed.Voyage);
            var failure=await Assert.ThrowsAsync<PostgresException>(
                async()=>await voyage.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation,failure.SqlState);
        }
        await using(var shipment=new NpgsqlCommand("""
            UPDATE shipments SET workflow_status='Cancelled', version=version+1,
                updated_at_utc=NOW()
            WHERE organization_id=@org AND id=@shipment
            """,db))
        {
            shipment.Parameters.AddWithValue("org",f.A);
            shipment.Parameters.AddWithValue("shipment",seed.Shipment);
            var failure=await Assert.ThrowsAsync<PostgresException>(
                async()=>await shipment.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation,failure.SqlState);
        }
    }
}
