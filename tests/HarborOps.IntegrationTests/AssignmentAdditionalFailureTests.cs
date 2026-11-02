using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentAdditionalFailureTests
{
    private sealed class Barrier : IAsyncDisposable
    {
        private readonly NpgsqlConnection owner;
        private readonly string cs;
        private readonly string name;
        private readonly long key;
        private bool released;
        private Barrier(NpgsqlConnection owner, string cs, string name, long key)
        {
            this.owner=owner;this.cs=cs;this.name=name;this.key=key;
        }
        public static async Task<Barrier> Install(string cs, Guid id)
        {
            var name="g6_close_"+Guid.NewGuid().ToString("N")[..10];
            long key=Random.Shared.Next(10000,int.MaxValue);
            var hold=new NpgsqlConnection(cs);
            await hold.OpenAsync();
            await using(var cmd=new NpgsqlCommand("SELECT pg_advisory_lock(@k)",hold))
            {
                cmd.Parameters.AddWithValue("k",key);
                await cmd.ExecuteNonQueryAsync();
            }
            await using var sql=new NpgsqlConnection(cs);
            await sql.OpenAsync();
            var statement="CREATE FUNCTION "+name+"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN "+
                "IF NEW.id='"+id+"'::uuid THEN PERFORM pg_advisory_lock("+key+"); "+
                "PERFORM pg_advisory_unlock("+key+"); END IF; RETURN NEW; END $$; "+
                "CREATE TRIGGER "+name+" AFTER UPDATE ON shipment_container_assignments "+
                "FOR EACH ROW EXECUTE FUNCTION "+name+"();";
            await using(var create=new NpgsqlCommand(statement,sql))
                await create.ExecuteNonQueryAsync();
            return new Barrier(hold,cs,name,key);
        }
        public async Task<int> WaitForInFlight()
        {
            await using var observer=new NpgsqlConnection(cs);
            await observer.OpenAsync();
            var deadline=DateTime.UtcNow.AddSeconds(30);
            while(DateTime.UtcNow<deadline)
            {
                await using var cmd=new NpgsqlCommand("""
                    SELECT l.pid FROM pg_locks l
                      JOIN pg_stat_activity a ON a.pid=l.pid
                    WHERE l.locktype='advisory' AND l.classid=0 AND l.objid=@key
                      AND l.objsubid=1 AND NOT l.granted
                      AND a.backend_xid IS NOT NULL
                      AND a.wait_event_type='Lock' AND a.wait_event='advisory'
                    """,observer);
                cmd.Parameters.AddWithValue("key",key);
                var pid=await cmd.ExecuteScalarAsync();
                if(pid is not null and not DBNull)return Convert.ToInt32(pid);
                await Task.Delay(50);
            }
            throw new TimeoutException("Assignment closure never reached the after-write SQL barrier");
        }
        public async Task Release()
        {
            if(released)return;
            await using var cmd=new NpgsqlCommand("SELECT pg_advisory_unlock(@k)",owner);
            cmd.Parameters.AddWithValue("k",key);
            await cmd.ExecuteNonQueryAsync();
            released=true;
        }
        public async ValueTask DisposeAsync()
        {
            try{await Release();}catch{}
            await owner.DisposeAsync();
            await using var connection=new NpgsqlConnection(cs);
            await connection.OpenAsync();
            await using var drop=new NpgsqlCommand(
                "DROP TRIGGER IF EXISTS "+name+" ON shipment_container_assignments; "+
                "DROP FUNCTION IF EXISTS "+name+"();",connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task<(Guid Id,string Path,ReferenceDataHttpTests.Fixture Fixture)>
        Seed(ReferenceDataHttpTests.ApiFactory factory)
    {
        var f=await ReferenceDataHttpTests.Setup(factory);
        var seed=await AssignmentHttpTests.Seed(f);
        var path=seed.Root+"/"+seed.Shipment+"/container-assignments";
        using var created=await ReferenceDataHttpTests.Send(f.Manager,HttpMethod.Post,path,
            new {containerId=seed.Container,startsAtUtc="2037-01-01T10:00:00Z"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (id,path+"/"+id,f);
    }
    private static async Task<(long Version,DateTimeOffset? End)> Row(string cs,Guid id)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var query=new NpgsqlCommand(
            "SELECT version,ends_at_utc FROM shipment_container_assignments WHERE id=@id",connection);
        query.Parameters.AddWithValue("id",id);
        await using var reader=await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0),reader.IsDBNull(1)?null:reader.GetFieldValue<DateTimeOffset>(1));
    }
    private static async Task<HttpResponseMessage> Close(HttpClient client,string path,
        CancellationToken cancellationToken=default)
    {
        using var csrf=await client.GetAsync("/api/v1/auth/csrf",cancellationToken);
        csrf.EnsureSuccessStatusCode();
        var token=(await csrf.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("token").GetString();
        using var request=new HttpRequestMessage(HttpMethod.Patch,path);
        request.Headers.TryAddWithoutValidation("Origin","http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",token);
        request.Content=JsonContent.Create(new{
            expectedVersion=1L,endsAtUtc="2037-01-01T11:00:00Z"
        });
        return await client.SendAsync(request,cancellationToken);
    }
    [Fact]
    public async Task CancelledAfterWriteAssignmentClosureRollsBack()
    {
        var cs=TestDatabase.ConnectionString();
        using var factory=new ReferenceDataHttpTests.ApiFactory(cs);
        var seed=await Seed(factory);
        using var admin=seed.Fixture.Admin;using var manager=seed.Fixture.Manager;
        using var analyst=seed.Fixture.Analyst;
        await using var barrier=await Barrier.Install(cs,seed.Id);
        using var cancel=new CancellationTokenSource();
        var request=Close(manager,seed.Path,cancel.Token);
        await barrier.WaitForInFlight();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await request);
        await barrier.Release();
        var result=await Row(cs,seed.Id);
        Assert.Equal(1L,result.Version);
        Assert.Null(result.End);
        using var retry=await Close(manager,seed.Path);
        Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        Assert.Equal(2L,(await Row(cs,seed.Id)).Version);
    }

    [Fact]
    public async Task TimedOutAfterWriteAssignmentClosureRollsBack()
    {
        var cs=TestDatabase.ConnectionString();
        var timed=new NpgsqlConnectionStringBuilder(cs){CommandTimeout=6}.ConnectionString;
        using var factory=new ReferenceDataHttpTests.ApiFactory(timed);
        var seed=await Seed(factory);
        using var admin=seed.Fixture.Admin;using var manager=seed.Fixture.Manager;
        using var analyst=seed.Fixture.Analyst;
        await using var barrier=await Barrier.Install(cs,seed.Id);
        var request=Close(manager,seed.Path);
        await barrier.WaitForInFlight();
        using(var result=await request)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,result.StatusCode);
            Assert.Contains("no-store",result.Headers.CacheControl?.ToString()??"");
            var body=await result.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("dependency_unavailable",body.GetProperty("code").GetString());
        }
        await barrier.Release();
        var row=await Row(cs,seed.Id);
        Assert.Equal(1L,row.Version);
        Assert.Null(row.End);
        using var retry=await Close(manager,seed.Path);
        Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        Assert.Equal(2L,(await Row(cs,seed.Id)).Version);
    }

    private sealed class DropCommittedResponse : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>
        {
            app.Use(async(context,pipeline)=>{
                if(!context.Request.Headers.ContainsKey("X-G6-Drop-Response"))
                {
                    await pipeline();return;
                }
                var original=context.Response.Body;
                context.Response.Body=new MemoryStream();
                await pipeline();
                context.Response.Body=original;
                context.Abort();
            });
            next(app);
        };
    }
    [Fact]
    public async Task LostResponseAfterAssignmentCommitDoesNotDuplicateClosure()
    {
        var cs=TestDatabase.ConnectionString();
        using var factory=new ReferenceDataHttpTests.ApiFactory(cs);
        var seed=await Seed(factory);
        using var admin=seed.Fixture.Admin;using var manager=seed.Fixture.Manager;
        using var analyst=seed.Fixture.Analyst;
        using var modified=factory.WithWebHostBuilder(builder=>
            builder.ConfigureTestServices(services=>services.AddSingleton<IStartupFilter,DropCommittedResponse>()));
        using var client=modified.CreateClient(new WebApplicationFactoryClientOptions{HandleCookies=true});
        await ReferenceDataHttpTests.Login(client,seed.Fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(client,seed.Fixture.A);
        using var csrf=await client.GetAsync("/api/v1/auth/csrf");
        var token=(await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var request=new HttpRequestMessage(HttpMethod.Patch,seed.Path);
        request.Headers.TryAddWithoutValidation("Origin","http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",token);
        request.Headers.TryAddWithoutValidation("X-G6-Drop-Response","1");
        request.Content=JsonContent.Create(new{expectedVersion=1L,endsAtUtc="2037-01-01T11:00:00Z"});
        HttpResponseMessage? result=null;Exception? failure=null;
        try{result=await client.SendAsync(request);}catch(Exception error){failure=error;}
        Assert.True(failure is not null || result is not {StatusCode:HttpStatusCode.OK},
            "Client unexpectedly received the committed success");
        var state=await Row(cs,seed.Id);
        Assert.Equal(2L,state.Version);
        Assert.NotNull(state.End);
        using var repeat=await Close(manager,seed.Path);
        Assert.Equal(HttpStatusCode.Conflict,repeat.StatusCode);
        Assert.Equal(2L,(await Row(cs,seed.Id)).Version);
    }
}
