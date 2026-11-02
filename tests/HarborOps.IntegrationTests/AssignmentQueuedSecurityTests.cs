using System.Net;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentQueuedSecurityTests
{
    private static async Task WaitForOrganizationLock(Guid organization, Task pending)
    {
        await using var observer=new NpgsqlConnection(TestDatabase.ConnectionString());
        await observer.OpenAsync();
        for(int attempt=0;attempt<120;attempt++)
        {
            await using var query=new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity WHERE datname=current_database()
                  AND wait_event_type='Lock' AND query ILIKE '%organizations%'
                """,observer);
            if(Convert.ToInt64(await query.ExecuteScalarAsync())>0)return;
            Assert.False(pending.IsCompleted,"Request completed without reaching organization lock");
            await Task.Delay(100);
        }
        Assert.Fail("Assignment request did not queue behind the held organization lock");
    }
    private static async Task ChangeAuthorization(
        ReferenceDataHttpTests.ApiFactory factory,
        ReferenceDataHttpTests.Fixture fixture,
        NpgsqlConnection connection,NpgsqlTransaction transaction,string change)
    {
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user=await db.AuthUsers.AsNoTracking().SingleAsync(x=>
            x.NormalizedEmail==IdentityStore.NormalizeEmail(fixture.ManagerEmail));
        if(change=="selection")
            await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.SelectedOrganizationId,(Guid?)fixture.B));
        if(change=="session")
            await db.AuthSessions.Where(x=>x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.RevokedAtUtc,DateTimeOffset.UtcNow));
        if(change=="account")
            await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Disabled"));
        if(change=="security")
            await db.AuthUsers.Where(x=>x.Id==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.SecurityVersion,x=>x.SecurityVersion+1));
        if(change=="membership")
            await db.OrganizationMemberships.Where(x=>
                x.OrganizationId==fixture.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Removed"));
        if(change=="downgrade")
            await db.OrganizationMemberships.Where(x=>
                x.OrganizationId==fixture.A&&x.UserId==user.Id)
                .ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Role,"Support"));
        if(change=="suspension")
        {
            await using var update=new NpgsqlCommand(
                "UPDATE organizations SET status='Suspended' WHERE id=@org",connection,transaction);
            update.Parameters.AddWithValue("org",fixture.A);
            Assert.Equal(1,await update.ExecuteNonQueryAsync());
        }
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
    public async Task QueuedAssignmentWriteRechecksEveryAuthorizationDependency(
        string change,bool allowed)
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var seeded=await AssignmentHttpTests.Seed(f);
        var url=seeded.Root+"/"+seeded.Shipment+"/container-assignments";
        await using var blocker=new NpgsqlConnection(TestDatabase.ConnectionString());
        await blocker.OpenAsync();
        await using var hold=await blocker.BeginTransactionAsync();
        await using(var query=new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE",blocker,hold))
        {
            query.Parameters.AddWithValue("id",f.A);
            Assert.Equal(f.A,(Guid)(await query.ExecuteScalarAsync())!);
        }
        var pending=ReferenceDataHttpTests.Send(manager,HttpMethod.Post,url,
            new{containerId=seeded.Container,startsAtUtc="2038-01-01T10:00:00Z"});
        try
        {
            await WaitForOrganizationLock(f.A,pending);
            await ChangeAuthorization(factory,f,blocker,hold,change);
        }finally{await hold.CommitAsync();}
        using var response=await pending;
        if(allowed)Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        else Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(allowed?1:0,await db.ShipmentContainerAssignments.AsNoTracking()
            .CountAsync(x=>x.OrganizationId==f.A&&x.ShipmentId==seeded.Shipment));
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
    public async Task QueuedAssignmentReadRechecksEveryAuthorizationDependency(
        string change,bool allowed)
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var admin=f.Admin;using var manager=f.Manager;using var analyst=f.Analyst;
        var seeded=await AssignmentHttpTests.Seed(f);
        var url=seeded.Root+"/"+seeded.Shipment+"/container-assignments";
        using(var created=await ReferenceDataHttpTests.Send(analyst,HttpMethod.Post,url,
            new{containerId=seeded.Container,startsAtUtc="2038-01-01T10:00:00Z"}))
            Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        await using var blocker=new NpgsqlConnection(TestDatabase.ConnectionString());
        await blocker.OpenAsync();
        await using var hold=await blocker.BeginTransactionAsync();
        await using(var query=new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE",blocker,hold))
        {
            query.Parameters.AddWithValue("id",f.A);
            Assert.Equal(f.A,(Guid)(await query.ExecuteScalarAsync())!);
        }
        var pending=manager.GetAsync(url);
        try
        {
            await WaitForOrganizationLock(f.A,pending);
            await ChangeAuthorization(factory,f,blocker,hold,change);
        }finally{await hold.CommitAsync();}
        using var response=await pending;
        if(allowed)Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        else Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        if(!allowed)Assert.DoesNotContain("containerId",await response.Content.ReadAsStringAsync());
    }
}
