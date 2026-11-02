using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentExtendedGateTests
{
    private static string Root(Guid org)=>"/api/v1/organizations/"+org+"/shipments";
    private static Task<HttpResponseMessage> Send(HttpClient client,HttpMethod method,string url,object body)=>
        ReferenceDataHttpTests.Send(client,method,url,body);
    private static async Task<Guid> Create(HttpClient client,string root,object payload)
    {
        using var response=await Send(client,HttpMethod.Post,root,payload);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<(Guid,Guid)> Ports(HttpClient manager,Guid org)
    {
        var root="/api/v1/organizations/"+org+"/ports";
        var first=await Create(manager,root,new{referenceCode="G5-GATE-PORTA",name="Gate Origin",countryCode="US"});
        var second=await Create(manager,root,new{referenceCode="G5-GATE-PORTB",name="Gate Destination",countryCode="US"});
        return (first,second);
    }
    private static async Task<Guid> Shipment(HttpClient client,Guid org,Guid from,Guid to,string code)=>
        await Create(client,Root(org),new{referenceCode=code,originPortId=from,destinationPortId=to});
    private static async Task SetRole(ReferenceDataHttpTests.ApiFactory factory,Guid org,string email,string role)
    {
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user=await db.AuthUsers.AsNoTracking().SingleAsync(x=>
            x.NormalizedEmail==IdentityStore.NormalizeEmail(email));
        await db.OrganizationMemberships.Where(x=>x.UserId==user.Id && x.OrganizationId==org)
            .ExecuteUpdateAsync(x=>x.SetProperty(m=>m.Role,role)
                .SetProperty(m=>m.Version,m=>m.Version+1));
    }

    [Fact]
    public async Task FullSixRouteFiveRolePermissions()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (from,to)=await Ports(manager,f.A);
        async Task Check(HttpClient client,string role,bool read,bool write)
        {
            var root=Root(f.A);
            var id=await Shipment(analyst,f.A,from,to,"G5-ROLE-"+role.ToUpperInvariant());
            var readRoutes=new[]{root,root+"/"+id,root+"/"+id+"/status-changes"};
            foreach(var url in readRoutes)
            {
                using var response=await client.GetAsync(url);
                Assert.Equal(read?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
            }
            using(var post=await Send(client,HttpMethod.Post,root,new{
                referenceCode="G5-NEW-"+role.ToUpperInvariant(),originPortId=from,destinationPortId=to}))
                Assert.Equal(write?HttpStatusCode.Created:HttpStatusCode.Forbidden,post.StatusCode);
            using(var patch=await Send(client,HttpMethod.Patch,root+"/"+id,new{
                displayName="Scoped "+role,expectedVersion=1L}))
                Assert.Equal(write?HttpStatusCode.OK:HttpStatusCode.Forbidden,patch.StatusCode);
            using(var deletion=await Send(client,HttpMethod.Delete,root+"/"+id,
                new{expectedVersion=write?2L:1L}))
                Assert.Equal(write?HttpStatusCode.NoContent:HttpStatusCode.Forbidden,deletion.StatusCode);
            using var detail=await analyst.GetAsync(root+"/"+id);
            Assert.Equal(HttpStatusCode.OK,detail.StatusCode);
            var data=await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains("Gate Origin",data.GetProperty("originPortLabel").GetString());
            Assert.Contains("Gate Destination",data.GetProperty("destinationPortLabel").GetString());
        }
        await Check(analyst,"Analyst",true,true);
        await Check(manager,"Manager",true,true);
        await Check(admin,"Administrator",false,false);
        await SetRole(factory,f.A,f.ManagerEmail,"Auditor");
        await Check(manager,"Auditor",true,false);
        using(var maritime=await manager.GetAsync("/api/v1/organizations/"+f.A+"/ports"))
            Assert.Equal(HttpStatusCode.Forbidden,maritime.StatusCode);
        await SetRole(factory,f.A,f.ManagerEmail,"Support");
        await Check(manager,"Support",false,false);
    }

    [Fact]
    public async Task MissingSelectionAndForeignActualShipmentNeverDiscloseData()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-FOREIGN-REAL");
        var foreignRoot=Root(f.B);var localRoot=Root(f.A);
        foreach(var url in new[]{foreignRoot,foreignRoot+"/"+id,
            foreignRoot+"/"+id+"/status-changes"})
        {
            using var r=await manager.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);
        }
        foreach(var method in new[]{HttpMethod.Patch,HttpMethod.Delete})
        {
            using var r=await Send(manager,method,foreignRoot+"/"+id,
                method==HttpMethod.Patch?(object)new{workflowStatus="Booked",expectedVersion=1L}:
                new{expectedVersion=1L});
            Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);
        }
        using(var post=await Send(manager,HttpMethod.Post,foreignRoot,new{
            referenceCode="G5-FOREIGN-DENIED",originPortId=a,destinationPortId=b}))
            Assert.Equal(HttpStatusCode.NotFound,post.StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var user=await db.AuthUsers.AsNoTracking().SingleAsync(x=>
                x.NormalizedEmail==IdentityStore.NormalizeEmail(f.ManagerEmail));
            await db.AuthSessions.Where(s=>s.UserId==user.Id).ExecuteUpdateAsync(x=>
                x.SetProperty(s=>s.SelectedOrganizationId,(Guid?)null));
        }
        using(var list=await manager.GetAsync(localRoot))
            Assert.Equal(HttpStatusCode.Conflict,list.StatusCode);
        using(var patch=await Send(manager,HttpMethod.Patch,localRoot+"/"+id,new{
            workflowStatus="Booked",expectedVersion=1L}))
            Assert.Equal(HttpStatusCode.Conflict,patch.StatusCode);
    }

    [Fact]
    public async Task InvalidJsonContentAndVersionMatrix()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var url=Root(f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-RAW-INPUT");
        using var csrf=await manager.GetAsync("/api/v1/auth/csrf");
        var token=(await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var cases=new[]{
            ("{", "application/json"),
            ("{\"referenceCode\":\"ABC\",\"referenceCode\":\"DEF\"}", "application/json"),
            ("{\"referenceCode\":\""+new string('X',9000)+"\"}", "application/json"),
            ("{\"referenceCode\":\"G5-WRONG-MIME\"}", "text/plain"),
            ("{\"referenceCode\":[\"bad\"]}", "application/json"),
            ("{\"containerIds\":[\"not-allowed\"]}", "application/json")
        };
        foreach(var (data,mime) in cases)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,url);
            request.Headers.TryAddWithoutValidation("Origin","http://localhost");
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",token);
            request.Content=new StringContent(data,Encoding.UTF8,mime);
            using var response=await manager.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        }
        foreach(var ver in new[]{0L,-1L})
        {
            using var response=await Send(manager,HttpMethod.Patch,url+"/"+id,
                new{workflowStatus="Booked",expectedVersion=ver});
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        }
        using(var noVersion=await Send(manager,HttpMethod.Delete,url+"/"+id,new{}))
            Assert.Equal(HttpStatusCode.BadRequest,noVersion.StatusCode);
        using var detail=await manager.GetAsync(url+"/"+id);
        Assert.Equal(HttpStatusCode.OK,detail.StatusCode);
        Assert.Equal(1L,(await detail.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("none",true)]
    [InlineData("session",false)]
    [InlineData("account",false)]
    [InlineData("security",false)]
    [InlineData("membership",false)]
    [InlineData("downgrade",false)]
    [InlineData("suspension",false)]
    [InlineData("selection",false)]
    public async Task QueuedShipmentWriteRechecksEveryAuthorizationDependency(
        string change,bool expectedSuccess)
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-QUEUE-"+change.ToUpperInvariant());
        await using var dbLock=new NpgsqlConnection(TestDatabase.ConnectionString());
        await dbLock.OpenAsync();
        await using var hold=await dbLock.BeginTransactionAsync();
        await using(var cmd=new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE",dbLock,hold))
        {
            cmd.Parameters.AddWithValue("id",f.A);
            Assert.Equal(f.A,(Guid)(await cmd.ExecuteScalarAsync())!);
        }
        var queued=Send(manager,HttpMethod.Patch,Root(f.A)+"/"+id,
            new{workflowStatus="Booked",expectedVersion=1L});
        bool observed=false;
        await using(var observer=new NpgsqlConnection(TestDatabase.ConnectionString()))
        {
            await observer.OpenAsync();
            for(int i=0;i<100;i++)
            {
                await using var cmd=new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() "+
                    "AND wait_event_type='Lock' AND query ILIKE '%organizations%'",observer);
                if(Convert.ToInt64(await cmd.ExecuteScalarAsync())>0){observed=true;break;}
                if(queued.IsCompleted) break;
                await Task.Delay(100);
            }
        }
        try
        {
            Assert.True(observed,"G5 shipment request must wait on the held organization lock");
            await using var scope=factory.Services.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var user=await db.AuthUsers.AsNoTracking().SingleAsync(x=>
                x.NormalizedEmail==IdentityStore.NormalizeEmail(f.ManagerEmail));
            if(change=="selection")await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(s=>s.SelectedOrganizationId,(Guid?)f.B));
            if(change=="session")await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(s=>s.RevokedAtUtc,DateTimeOffset.UtcNow));
            if(change=="account")await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Disabled"));
            if(change=="security")await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.SecurityVersion,x=>x.SecurityVersion+1));
            if(change=="membership")await db.OrganizationMemberships
                .Where(x=>x.OrganizationId==f.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Removed"));
            if(change=="downgrade")await db.OrganizationMemberships
                .Where(x=>x.OrganizationId==f.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Role,"Auditor"));
            if(change=="suspension")
            {
                await using var update=new NpgsqlCommand(
                    "UPDATE organizations SET status='Suspended' WHERE id=@id",dbLock,hold);
                update.Parameters.AddWithValue("id",f.A);
                Assert.Equal(1,await update.ExecuteNonQueryAsync());
            }
        }
        finally { await hold.CommitAsync(); }
        using var result=await queued;
        if(expectedSuccess) Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        else Assert.True(result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        await using var verify=factory.Services.CreateAsyncScope();
        var store=verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row=await store.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(expectedSuccess?2L:1L,row.Version);
        Assert.Equal(expectedSuccess?2:1,await store.ShipmentStatusChanges
            .CountAsync(x=>x.ShipmentId==id));
    }


    [Theory]
    [InlineData("none",true)]
    [InlineData("session",false)]
    [InlineData("account",false)]
    [InlineData("security",false)]
    [InlineData("membership",false)]
    [InlineData("downgrade",false)]
    [InlineData("auditor",true)]
    [InlineData("suspension",false)]
    [InlineData("selection",false)]
    public async Task QueuedShipmentReadRechecksEveryAuthorizationDependency(
        string change,bool expectedSuccess)
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-QUEUE-"+change.ToUpperInvariant());
        await using var dbLock=new NpgsqlConnection(TestDatabase.ConnectionString());
        await dbLock.OpenAsync();
        await using var hold=await dbLock.BeginTransactionAsync();
        await using(var cmd=new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE",dbLock,hold))
        {
            cmd.Parameters.AddWithValue("id",f.A);
            Assert.Equal(f.A,(Guid)(await cmd.ExecuteScalarAsync())!);
        }
        var queued=manager.GetAsync(Root(f.A)+"/"+id);
        bool observed=false;
        await using(var observer=new NpgsqlConnection(TestDatabase.ConnectionString()))
        {
            await observer.OpenAsync();
            for(int i=0;i<100;i++)
            {
                await using var cmd=new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() "+
                    "AND wait_event_type='Lock' AND query ILIKE '%organizations%'",observer);
                if(Convert.ToInt64(await cmd.ExecuteScalarAsync())>0){observed=true;break;}
                if(queued.IsCompleted) break;
                await Task.Delay(100);
            }
        }
        try
        {
            Assert.True(observed,"G5 shipment request must wait on the held organization lock");
            await using var scope=factory.Services.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var user=await db.AuthUsers.AsNoTracking().SingleAsync(x=>
                x.NormalizedEmail==IdentityStore.NormalizeEmail(f.ManagerEmail));
            if(change=="selection")await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(s=>s.SelectedOrganizationId,(Guid?)f.B));
            if(change=="session")await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(s=>s.RevokedAtUtc,DateTimeOffset.UtcNow));
            if(change=="account")await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Disabled"));
            if(change=="security")await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.SecurityVersion,x=>x.SecurityVersion+1));
            if(change=="membership")await db.OrganizationMemberships
                .Where(x=>x.OrganizationId==f.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Removed"));
            // Support has no shipment read permission; Auditor keeps it (positive control).
            if(change=="downgrade")await db.OrganizationMemberships
                .Where(x=>x.OrganizationId==f.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Role,"Support"));
            if(change=="auditor")await db.OrganizationMemberships
                .Where(x=>x.OrganizationId==f.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Role,"Auditor"));
            if(change=="suspension")
            {
                await using var update=new NpgsqlCommand(
                    "UPDATE organizations SET status='Suspended' WHERE id=@id",dbLock,hold);
                update.Parameters.AddWithValue("id",f.A);
                Assert.Equal(1,await update.ExecuteNonQueryAsync());
            }
        }
        finally { await hold.CommitAsync(); }
        using var result=await queued;
        if(expectedSuccess) Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        else Assert.True(result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        await using var verify=factory.Services.CreateAsyncScope();
        var store=verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row=await store.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(1L,row.Version);
        Assert.Equal(1,await store.ShipmentStatusChanges
            .CountAsync(x=>x.ShipmentId==id));
    }

    [Fact]
    public async Task ConcurrentArchiveAndStatusEditProduceOneVersionWinner()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var url=Root(f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-ARCH-RACE");
        var calls=await Task.WhenAll(
            Send(manager,HttpMethod.Patch,url+"/"+id,new{workflowStatus="Booked",expectedVersion=1L}),
            Send(analyst,HttpMethod.Delete,url+"/"+id,new{expectedVersion=1L}));
        try
        {
            Assert.Single(calls, x=>x.StatusCode==HttpStatusCode.Conflict);
            Assert.Single(calls, x=>x.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
        }
        finally {foreach(var c in calls)c.Dispose();}
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(2L,(await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id)).Version);
        var count=await db.ShipmentStatusChanges.CountAsync(x=>x.ShipmentId==id);
        Assert.InRange(count,1,2);
        var archived=(await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id)).RecordStatus=="Archived";
        Assert.Equal(archived?1:2,count);
    }

    [Fact]
    public async Task DuplicateConcurrentCreationCommitsExactlyOneShipment()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var api=Root(f.A);
        var payload=new{referenceCode="G5-DUPLICATE-RACE",originPortId=a,destinationPortId=b};
        var requests=await Task.WhenAll(Send(manager,HttpMethod.Post,api,payload),
            Send(analyst,HttpMethod.Post,api,payload));
        try
        {
            Assert.Single(requests, x=>x.StatusCode==HttpStatusCode.Created);
            Assert.Single(requests, x=>x.StatusCode==HttpStatusCode.Conflict);
        }
        finally {foreach(var r in requests)r.Dispose();}
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var rows=await db.Shipments.AsNoTracking().Where(x=>
            x.OrganizationId==f.A&&x.ReferenceCode=="G5-DUPLICATE-RACE").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(1,await db.ShipmentStatusChanges.CountAsync(x=>x.ShipmentId==rows[0].Id));
    }

    [Fact]
    public async Task ArchivedRestoreAndStaleEditCannotBothCommit()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var(a,b)=await Ports(manager,f.A);
        var url=Root(f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-RESTORE-RACE");
        using(var archive=await Send(manager,HttpMethod.Delete,url+"/"+id,new{expectedVersion=1L}))
            Assert.Equal(HttpStatusCode.NoContent,archive.StatusCode);
        var competing=await Task.WhenAll(
            Send(manager,HttpMethod.Patch,url+"/"+id,new{recordStatus="Active",expectedVersion=2L}),
            Send(analyst,HttpMethod.Patch,url+"/"+id,new{displayName="Illegal",expectedVersion=2L}));
        try
        {
            Assert.Single(competing, x=>x.StatusCode==HttpStatusCode.OK);
            Assert.Single(competing, x=>x.StatusCode==HttpStatusCode.Conflict);
        }finally{foreach(var item in competing)item.Dispose();}
        using(var replay=await Send(manager,HttpMethod.Patch,url+"/"+id,
            new{recordStatus="Active",expectedVersion=2L}))
            Assert.Equal(HttpStatusCode.Conflict,replay.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row=await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(3L,row.Version);
        Assert.Equal("Active",row.RecordStatus);
        Assert.Null(row.DisplayName);
        Assert.Equal(1,await db.ShipmentStatusChanges.CountAsync(x=>x.ShipmentId==id));
    }

    [Fact]
    public async Task DuplicateArchiveAttemptsCannotBothCommit()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var(a,b)=await Ports(manager,f.A);
        var url=Root(f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-DUP-ARCHIVE");
        var calls=await Task.WhenAll(
            Send(manager,HttpMethod.Delete,url+"/"+id,new{expectedVersion=1L}),
            Send(analyst,HttpMethod.Delete,url+"/"+id,new{expectedVersion=1L}));
        try{
            Assert.Single(calls, x=>x.StatusCode==HttpStatusCode.NoContent);
            Assert.Single(calls, x=>x.StatusCode==HttpStatusCode.Conflict);
        }finally{foreach(var item in calls)item.Dispose();}
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row=await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(2L,row.Version);
        Assert.Equal("Archived",row.RecordStatus);
        Assert.Single(await db.ShipmentStatusChanges.AsNoTracking()
            .Where(x=>x.ShipmentId==id).ToListAsync());
    }

    [Fact]
    public async Task SameParentAndUnknownPortsRejectCreation()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var(a,b)=await Ports(manager,f.A);
        using(var same=await Send(analyst,HttpMethod.Post,Root(f.A),new{
            referenceCode="G5-SAME-PORT",originPortId=a,destinationPortId=a}))
            Assert.Equal(HttpStatusCode.BadRequest,same.StatusCode);
        using(var unknown=await Send(analyst,HttpMethod.Post,Root(f.A),new{
            referenceCode="G5-UNKNOWN-PORT",originPortId=Guid.NewGuid(),destinationPortId=b}))
            Assert.Equal(HttpStatusCode.NotFound,unknown.StatusCode);
    }

    [Fact]
    public async Task AnonymousAllSixShipmentRoutesRejectAccess()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var(a,b)=await Ports(manager,f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-ANON");
        using var anonymous=factory.CreateClient();
        var url=Root(f.A);
        foreach(var target in new[]{url,url+"/"+id,url+"/"+id+"/status-changes"})
        {
            using var response=await anonymous.GetAsync(target);
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
            // Framework-generated challenges are never cacheable either.
            Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
        }
        foreach(var action in new[]{HttpMethod.Post,HttpMethod.Patch,HttpMethod.Delete})
        {
            var target=action==HttpMethod.Post?url:url+"/"+id;
            using var request=new HttpRequestMessage(action,target);
            request.Headers.TryAddWithoutValidation("Origin","http://localhost");
            request.Content=JsonContent.Create(new{referenceCode="G5-ANON-NEW",
                originPortId=a,destinationPortId=b,expectedVersion=1L});
            using var response=await anonymous.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
            Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
        }
    }

    [Fact]
    public async Task CancelledQueuedShipmentMutationNeverCommitsHistory()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var (a,b)=await Ports(manager,f.A);
        var id=await Shipment(manager,f.A,a,b,"G5-CANCEL-WAIT");
        using var tokenResponse=await manager.GetAsync("/api/v1/auth/csrf");
        var token=(await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        await using var connection=new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        await using var hold=await connection.BeginTransactionAsync();
        await using(var command=new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE",connection,hold))
        {
            command.Parameters.AddWithValue("id",f.A);
            Assert.Equal(f.A,(Guid)(await command.ExecuteScalarAsync())!);
        }
        using var request=new HttpRequestMessage(HttpMethod.Patch,Root(f.A)+"/"+id);
        request.Headers.TryAddWithoutValidation("Origin","http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",token);
        request.Content=JsonContent.Create(new{workflowStatus="Booked",expectedVersion=1L});
        using var cancellation=new CancellationTokenSource();
        var task=manager.SendAsync(request,cancellation.Token);
        bool blocked=false;
        await using(var observer=new NpgsqlConnection(TestDatabase.ConnectionString()))
        {
            await observer.OpenAsync();
            for(int i=0;i<100;i++)
            {
                await using var check=new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() "+
                    "AND wait_event_type='Lock' AND query ILIKE '%organizations%'",observer);
                if(Convert.ToInt64(await check.ExecuteScalarAsync())>0){blocked=true;break;}
                if(task.IsCompleted) break;
                await Task.Delay(100);
            }
        }
        try
        {
            Assert.True(blocked,"Cancellation test must observe the blocked database writer");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await task);
        }
        finally {await hold.CommitAsync();}
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1L,(await db.Shipments.AsNoTracking().SingleAsync(x=>x.Id==id)).Version);
        Assert.Equal(1,await db.ShipmentStatusChanges.CountAsync(x=>x.ShipmentId==id));
    }
}
