using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceAdditionalContractTests
{
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object? body = null) =>
        ReferenceDataHttpTests.Send(client, method, path, body);

    [Fact]
    public async Task VesselFullLifecycleAndArchivedIdentifierReservation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var url = $"/api/v1/organizations/{f.A}/vessels";
        using var created = await Send(manager, HttpMethod.Post, url,
            new { referenceCode = "VESSEL-1", name = "Synthetic Current Vessel",
                imoNumber = "9074729", mmsi = "012345678" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        var path = url + "/" + id;

        using var edit = await Send(manager, HttpMethod.Patch, path,
            new { name = "Synthetic Updated Vessel", mmsi = (string?)null, expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(2, (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
        using var stale = await Send(manager, HttpMethod.Patch, path,
            new { name = "Stale", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var patchArchive = await Send(manager, HttpMethod.Patch, path,
            new { status = "Archived", expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.Conflict, patchArchive.StatusCode);
        using var archive = await Send(manager, HttpMethod.Delete, path,
            new { expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        using var archiveAgain = await Send(manager, HttpMethod.Delete, path,
            new { expectedVersion = 3 });
        Assert.Equal(HttpStatusCode.Conflict, archiveAgain.StatusCode);
        using var editArchived = await Send(manager, HttpMethod.Patch, path,
            new { name = "Not allowed", expectedVersion = 3 });
        Assert.Equal(HttpStatusCode.Conflict, editArchived.StatusCode);
        var archived = await manager.GetFromJsonAsync<JsonElement>(url + "?status=Archived");
        Assert.Contains(archived.GetProperty("vessels").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == id);
        using var duplicateCode = await Send(manager, HttpMethod.Post, url,
            new { referenceCode = "VESSEL-1", name = "Recycled" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateCode.StatusCode);
        using var restore = await Send(manager, HttpMethod.Patch, path,
            new { status = "Active", expectedVersion = 3 });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(4, (await restore.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
        using var restoreAgain = await Send(manager, HttpMethod.Patch, path,
            new { status = "Active", expectedVersion = 4 });
        Assert.Equal(HttpStatusCode.Conflict, restoreAgain.StatusCode);
        var persisted = await manager.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Synthetic Updated Vessel", persisted.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, persisted.GetProperty("mmsi").ValueKind);
    }

    [Fact]
    public async Task MalformedBodiesUnknownPropertiesAndOversizedJsonFailWithSanitized400()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var url = $"/api/v1/organizations/{f.A}/ports";
        async Task Check(string payload, string type = "application/json")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            using var tokenResponse = await manager.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                token.GetProperty("token").GetString());
            request.Content = new StringContent(payload, Encoding.UTF8);
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(type);
            using var response = await manager.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_request", body.GetProperty("code").GetString());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("correlationId").GetString()));
        }
        await Check("{");
        await Check("[]");
        await Check("null");
        await Check("{}");
        await Check("{\"name\":\"A\",\"name\":\"B\",\"referenceCode\":\"ABC\",\"countryCode\":\"US\"}");
        await Check("{\"referenceCode\":\"ABC\",\"name\":\"A\",\"countryCode\":\"US\",\"organizationId\":\"" + f.B + "\"}");
        await Check("{\"referenceCode\":\"ABC\",\"name\":\"A\",\"countryCode\":\"US\"}", "text/plain");
        await Check("{\"referenceCode\":\"ABC\",\"name\":\"" + new string('X', 8200) +
            "\",\"countryCode\":\"US\"}");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.Ports.CountAsync(x => x.OrganizationId == f.A));
    }
}
