using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentSqlMatrixTests
{
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string url, object body) => ReferenceDataHttpTests.Send(client, method, url, body);
    private static async Task<Guid> Create(HttpClient client, string route, object body)
    {
        using var r = await Send(client,HttpMethod.Post,route,body);
        Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task CompositePortsAndImmutableFieldsRejectIndependentSqlMutations()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;
        using var admin=f.Admin;
        using var analyst=f.Analyst;
        var ports="/api/v1/organizations/"+f.A+"/ports";
        var origin=await Create(manager,ports,new { referenceCode="G5-SQL-A",name="Origin",countryCode="US" });
        var destination=await Create(manager,ports,new { referenceCode="G5-SQL-B",name="Destination",countryCode="US" });
        var api="/api/v1/organizations/"+f.A+"/shipments";
        var id=await Create(manager,api,new { referenceCode="G5-SQL-SHIP",
            originPortId=origin,destinationPortId=destination });
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var foreign=new PortReference { Id=Guid.NewGuid(),OrganizationId=f.B,
            ReferenceCode="G5-SQL-FOREIGN",Name="Foreign",CountryCode="US",
            CreatedAtUtc=DateTimeOffset.UtcNow,UpdatedAtUtc=DateTimeOffset.UtcNow };
        db.Ports.Add(foreign);
        await db.SaveChangesAsync();
        string[] invalid = {
            "UPDATE shipments SET origin_port_id = '"+foreign.Id+"',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET destination_port_id = '"+foreign.Id+"',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET organization_id = '"+f.B+"',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET reference_code='REWRITTEN',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET external_reference='BAD',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET workflow_status='Completed',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET workflow_status='Unknown',version=version+1 WHERE id='"+id+"'",
            "UPDATE shipments SET version=version+2 WHERE id='"+id+"'",
            "UPDATE shipments SET planned_departure_at_utc='2026-11-04',planned_arrival_at_utc='2026-11-03',version=version+1 WHERE id='"+id+"'",
            "DELETE FROM shipments WHERE id='"+id+"'"
        };
        foreach(var statement in invalid)
            await Assert.ThrowsAnyAsync<PostgresException>(async ()=>
                await db.Database.ExecuteSqlRawAsync(statement));
        await Assert.ThrowsAnyAsync<PostgresException>(async ()=>await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM shipment_status_changes WHERE shipment_id='"+id+"'"));
        await Assert.ThrowsAnyAsync<PostgresException>(async ()=>await db.Database.ExecuteSqlRawAsync(
            "UPDATE shipment_status_changes SET to_status='Completed' WHERE shipment_id='"+id+"'"));
        await Assert.ThrowsAnyAsync<PostgresException>(async ()=>await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO shipment_status_changes(id,organization_id,shipment_id,to_status,version,changed_at_utc,changed_by_user_id) "+
            "SELECT gen_random_uuid(),organization_id,id,'Booked',2,now(),changed_by_user_id FROM shipments WHERE id='"+id+"'"));
        Assert.Equal(1L,(await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id)).Version);
        Assert.Single(await db.ShipmentStatusChanges.AsNoTracking().Where(x=>x.ShipmentId==id).ToListAsync());
        using var valid=await Send(manager,HttpMethod.Patch,api+"/"+id,
            new { workflowStatus="Booked",expectedVersion=1L });
        Assert.Equal(HttpStatusCode.OK,valid.StatusCode);
        var changes=await db.ShipmentStatusChanges.AsNoTracking().Where(x=>x.ShipmentId==id)
            .OrderBy(x=>x.Version).ToListAsync();
        Assert.Equal(2,changes.Count);
        Assert.Null(changes[0].FromStatus);
        Assert.Equal("Booked",changes[1].ToStatus);
    }

    [Fact]
    public async Task ExternalIdentifiersReserveArchivedValuesButPermitCrossTenantReuse()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;
        using var analyst=f.Analyst;
        using var admin=f.Admin;
        var ports="/api/v1/organizations/"+f.A+"/ports";
        var origin=await Create(manager,ports,new {referenceCode="G5-EXT-PORTA",name="Origin",countryCode="US"});
        var dest=await Create(manager,ports,new {referenceCode="G5-EXT-PORTB",name="Destination",countryCode="US"});
        var api="/api/v1/organizations/"+f.A+"/shipments";
        var id=await Create(manager,api,new {referenceCode="G5-EXT-ONE",
            externalReference="external/1",originPortId=origin,destinationPortId=dest});
        using(var duplicate=await Send(manager,HttpMethod.Post,api,new {
            referenceCode="G5-EXT-TWO",externalReference="EXTERNAL/1",
            originPortId=origin,destinationPortId=dest}))
            Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        using(var archive=await Send(manager,HttpMethod.Delete,api+"/"+id,new{expectedVersion=1}))
            Assert.Equal(HttpStatusCode.NoContent,archive.StatusCode);
        using(var retained=await Send(manager,HttpMethod.Post,api,new{
            referenceCode="G5-EXT-THREE",externalReference="EXTERNAL/1",
            originPortId=origin,destinationPortId=dest}))
            Assert.Equal(HttpStatusCode.Conflict,retained.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var foreignA=new PortReference{Id=Guid.NewGuid(),OrganizationId=f.B,
            ReferenceCode="G5-EXT-FORA",Name="Foreign A",CountryCode="US",
            CreatedAtUtc=DateTimeOffset.UtcNow,UpdatedAtUtc=DateTimeOffset.UtcNow};
        var foreignB=new PortReference{Id=Guid.NewGuid(),OrganizationId=f.B,
            ReferenceCode="G5-EXT-FORB",Name="Foreign B",CountryCode="US",
            CreatedAtUtc=DateTimeOffset.UtcNow,UpdatedAtUtc=DateTimeOffset.UtcNow};
        db.Ports.AddRange(foreignA,foreignB);
        await db.SaveChangesAsync();
        var user=await db.Shipments.AsNoTracking().Where(x=>x.Id==id)
            .Select(x=>x.ChangedByUserId).SingleAsync();
        var foreignId=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
          INSERT INTO shipments(id,organization_id,reference_code,external_reference,
            origin_port_id,destination_port_id,created_at_utc,updated_at_utc,changed_by_user_id)
          VALUES ({foreignId},{f.B},{"G5-EXT-ONE"},{"EXTERNAL/1"},
            {foreignA.Id},{foreignB.Id},{DateTimeOffset.UtcNow},{DateTimeOffset.UtcNow},{user})");
        Assert.Equal(2,await db.Shipments.AsNoTracking()
            .CountAsync(x=>x.ExternalReference=="EXTERNAL/1"));
        Assert.Equal(1,await db.ShipmentStatusChanges.AsNoTracking()
            .CountAsync(x=>x.OrganizationId==f.B && x.ShipmentId==foreignId));
    }

    [Fact]
    public async Task StatusNeutralEditAndArchiveNeverFabricateHistory()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;using var analyst=f.Analyst;using var admin=f.Admin;
        var portApi="/api/v1/organizations/"+f.A+"/ports";
        var a=await Create(manager,portApi,new {referenceCode="G5-NEUTRAL-A",name="A",countryCode="US"});
        var b=await Create(manager,portApi,new {referenceCode="G5-NEUTRAL-B",name="B",countryCode="US"});
        var url="/api/v1/organizations/"+f.A+"/shipments";
        var id=await Create(analyst,url,new{referenceCode="G5-NEUTRAL",
            originPortId=a,destinationPortId=b});
        using(var edit=await Send(analyst,HttpMethod.Patch,url+"/"+id,
            new {displayName="Revised label",expectedVersion=1L}))
            Assert.Equal(HttpStatusCode.OK,edit.StatusCode);
        using(var noOp=await Send(analyst,HttpMethod.Patch,url+"/"+id,
            new {displayName="Revised label",expectedVersion=2L}))
            Assert.Equal(HttpStatusCode.BadRequest,noOp.StatusCode);
        using(var archive=await Send(analyst,HttpMethod.Delete,url+"/"+id,
            new{expectedVersion=2L}))
            Assert.Equal(HttpStatusCode.NoContent,archive.StatusCode);
        using(var restore=await Send(manager,HttpMethod.Patch,url+"/"+id,
            new {recordStatus="Active",expectedVersion=3L}))
            Assert.Equal(HttpStatusCode.OK,restore.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row=await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(4L,row.Version);
        Assert.Equal("Revised label",row.DisplayName);
        var history=await db.ShipmentStatusChanges.AsNoTracking()
            .Where(x=>x.ShipmentId==id).ToListAsync();
        Assert.Single(history);
        Assert.Equal("Draft",history[0].ToStatus);
    }
}
