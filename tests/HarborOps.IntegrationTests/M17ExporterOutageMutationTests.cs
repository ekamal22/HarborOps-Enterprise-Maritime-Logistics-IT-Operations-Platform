using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17ExporterOutageMutationTests
{
    [Fact]
    public async Task UnreachableCollectorCannotChangeAuthorizedMutationOrConflictInvariants()
    {
        var previousEvents=Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var previousLinks=Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        try
        {
            // No collector is listening at loopback:4317 in the isolated test
            // process. Business PostgreSQL runs on a separate approved fixture.
            var options=new Dictionary<string,string> {
                ["M17:Enabled"]="true",
                ["M17:OtlpEndpoint"]="http://127.0.0.1:4317",
                ["M17:LinkKey"]=Convert.ToBase64String(
                    Enumerable.Range(1,32).Select(i=>(byte)i).ToArray())
            };
            using var factory=new ReferenceDataHttpTests.ApiFactory(
                TestDatabase.ConnectionString(),settings:options);
            var tenant=await ReferenceDataHttpTests.Setup(factory);
            // The real readiness check requires a persisted bootstrap record.
            await using (var seedScope=factory.Services.CreateAsyncScope())
                await HarborOps.Infrastructure.Persistence.BootstrapSeeder.SeedAsync(
                    seedScope.ServiceProvider.GetRequiredService<HarborOps.Infrastructure.Persistence.HarborOpsDbContext>(),
                    CancellationToken.None);
            using var admin=tenant.Admin;
            using var manager=tenant.Manager;
            using var analyst=tenant.Analyst;
            var route="/api/v1/organizations/"+tenant.A+"/ports";
            var reference="G17-NOCOLLECTOR-"+Guid.NewGuid().ToString("N")[..8];
            using var valid=await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,route,
                new{referenceCode=reference,name="Synthetic Collector Outage Port",
                    countryCode="TR"});
            Assert.Equal(HttpStatusCode.Created,valid.StatusCode);
            var saved=await valid.Content.ReadFromJsonAsync<JsonElement>();
            var id=saved.GetProperty("id").GetGuid();
            Assert.Equal(tenant.A,saved.GetProperty("organizationId").GetGuid());

            using var duplicate=await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,route,
                new{referenceCode=reference,name="Duplicate Must Fail",countryCode="TR"});
            Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
            using var forbidden=await ReferenceDataHttpTests.Send(analyst,HttpMethod.Post,route,
                new{referenceCode=reference+"-FORBIDDEN",
                    name="Not Authorized",countryCode="TR"});
            Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
            using var foreign=await manager.GetAsync(
                "/api/v1/organizations/"+tenant.B+"/ports");
            Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
            var result=await manager.GetFromJsonAsync<JsonElement>(route);
            var entries=result.GetProperty("ports").EnumerateArray().ToArray();
            Assert.Single(entries);
            Assert.Equal(id,entries[0].GetProperty("id").GetGuid());
            Assert.Equal(reference.ToUpperInvariant(),entries[0].GetProperty("referenceCode").GetString());

            using var live=await manager.GetAsync("/health/live");
            using var ready=await manager.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK,live.StatusCode);
            Assert.Equal(HttpStatusCode.OK,ready.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",previousEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",previousLinks);
        }
    }
}
