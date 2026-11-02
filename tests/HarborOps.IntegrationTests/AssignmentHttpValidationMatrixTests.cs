using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentHttpValidationMatrixTests
{
    private static async Task<HttpResponseMessage> SendRaw(HttpClient client,
        HttpMethod verb,string path,string raw,string mediaType="application/json",
        bool includeCsrf=true,string origin="http://localhost")
    {
        using var message=new HttpRequestMessage(verb,path);
        message.Headers.TryAddWithoutValidation("Origin",origin);
        if(includeCsrf)
        {
            using var token=await client.GetAsync("/api/v1/auth/csrf");
            Assert.Equal(HttpStatusCode.OK,token.StatusCode);
            var value=(await token.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("token").GetString();
            message.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",value);
        }
        message.Content=new StringContent(raw,Encoding.UTF8);
        message.Content.Headers.ContentType=new MediaTypeHeaderValue(mediaType);
        return await client.SendAsync(message);
    }

    [Fact]
    public async Task MalformedJsonHeadersVersionsAndMutationsFailClosedWithoutSideEffects()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f=await ReferenceDataHttpTests.Setup(factory);
        using var manager=f.Manager;using var analyst=f.Analyst;using var admin=f.Admin;
        var seed=await AssignmentHttpTests.Seed(f);
        var route=seed.Root+"/"+seed.Shipment+"/container-assignments";
        var id="\""+seed.Container+"\"";
        var valid="{\"containerId\":"+id+",\"startsAtUtc\":\"2042-01-01T10:00:00Z\"}";
        var cases=new (string Body,string MediaType,bool Csrf,string Origin,HttpStatusCode Status)[]
        {
            ("{","application/json",true,"http://localhost",HttpStatusCode.BadRequest),
            (valid,"text/plain",true,"http://localhost",HttpStatusCode.BadRequest),
            (valid,"application/json",false,"http://localhost",HttpStatusCode.Forbidden),
            (valid,"application/json",true,"https://unrelated.example",HttpStatusCode.Forbidden),
            (valid[..^1]+",\"containerId\":"+id+"}","application/json",true,
                "http://localhost",HttpStatusCode.BadRequest),
            (valid[..^1]+",\"organizationId\":\""+f.B+"\"}","application/json",true,
                "http://localhost",HttpStatusCode.BadRequest),
            (valid[..^1]+",\"version\":1}","application/json",true,
                "http://localhost",HttpStatusCode.BadRequest),
            ("{\"containerId\":"+id+"}","application/json",true,
                "http://localhost",HttpStatusCode.BadRequest),
            ("{\"containerId\":"+id+",\"startsAtUtc\":\"2042-01-01T10:00:00+03:00\"}",
                "application/json",true,"http://localhost",HttpStatusCode.BadRequest),
            (valid[..^1]+",}","application/json",true,"http://localhost",
                HttpStatusCode.BadRequest),
            (valid+" ".PadRight(8300,'x'),"application/json",true,
                "http://localhost",HttpStatusCode.BadRequest)
        };
        foreach(var row in cases)
        {
            using var response=await SendRaw(manager,HttpMethod.Post,route,
                row.Body,row.MediaType,row.Csrf,row.Origin);
            Assert.Equal(row.Status,response.StatusCode);
            Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
            var body=await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("code",out _));
            Assert.True(body.TryGetProperty("correlationId",out _));
        }
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            Assert.Empty(await db.ShipmentContainerAssignments.AsNoTracking()
                .Where(x=>x.ShipmentId==seed.Shipment).ToListAsync());
        }

        using var created=await SendRaw(manager,HttpMethod.Post,route,valid);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var assignment=(await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var closing=route+"/"+assignment;
        foreach(var body in new[]
        {
            "{}",
            "{\"expectedVersion\":0,\"endsAtUtc\":\"2042-01-01T11:00:00Z\"}",
            "{\"expectedVersion\":\"1\",\"endsAtUtc\":\"2042-01-01T11:00:00Z\"}",
            "{\"expectedVersion\":1}",
            "{\"expectedVersion\":1,\"endsAtUtc\":null}",
            "{\"expectedVersion\":1,\"endsAtUtc\":\"2042-01-01T09:00:00Z\"}",
            "{\"expectedVersion\":1,\"endsAtUtc\":\"2042-01-01T11:00:00Z\",\"shipmentId\":\""+
                seed.Shipment+"\"}"
        })
        {
            using var response=await SendRaw(manager,HttpMethod.Patch,closing,body);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
                "Malformed or invalid closure unexpectedly accepted: "+body);
        }
        using var read=await manager.GetAsync(closing);
        Assert.Equal(HttpStatusCode.OK,read.StatusCode);
        var state=await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1L,state.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null,state.GetProperty("endsAtUtc").ValueKind);
    }
}
