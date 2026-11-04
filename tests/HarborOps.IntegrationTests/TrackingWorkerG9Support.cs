using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>Shared fixtures for the G9 scenario tests. Test-only; no production code uses it.</summary>
internal static class G9Support
{
    internal sealed record Resolvable(string Reference, Guid Container, Guid Shipment, Guid Voyage);

    internal static async Task<ProvisionedIntegrationSource> NewSource(
        ReferenceDataHttpTests.ApiFactory factory, Guid organization, string prefix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>().CreateAsync(
            organization, prefix + "_" + Guid.NewGuid().ToString("N")[..10], "G9 Source",
            CancellationToken.None);
    }

    /// <summary>Seeds a container with an M6 assignment valid from 2026-11-03T09:00Z.</summary>
    internal static async Task<Resolvable> SeedResolvable(
        ReferenceDataHttpTests.ApiFactory factory, ReferenceDataHttpTests.Fixture fixture)
    {
        var maritime = await AssignmentHttpTests.Seed(fixture);
        using var allocation = await ReferenceDataHttpTests.Send(fixture.Analyst, HttpMethod.Post,
            maritime.Root + "/" + maritime.Shipment + "/container-assignments",
            new { containerId = maritime.Container, startsAtUtc = "2026-11-03T09:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, allocation.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var reference = (await db.Containers.AsNoTracking()
            .SingleAsync(c => c.Id == maritime.Container)).ContainerIdentifier;
        return new Resolvable(reference, maritime.Container, maritime.Shipment, maritime.Voyage);
    }

    internal static string EquipmentPayload(string key, string reference,
        string occurredAt = "2026-11-03T10:00:00Z", string code = "GTIN") => $$"""
        {"sourceEventId":"{{key}}","eventFamily":"Equipment","eventCode":"{{code}}",
         "classifier":"ACT","occurredAtUtc":"{{occurredAt}}",
         "equipmentReference":"{{reference}}","externalSchemaVersion":"2.2.0"}
        """;

    internal static string TransportPayload(string key, string transport) => $$"""
        {"sourceEventId":"{{key}}","eventFamily":"Transport","eventCode":"ARRI",
         "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
         "transportReference":"{{transport}}","externalSchemaVersion":"2.2.0"}
        """;

    internal static async Task<HttpResponseMessage> Post(
        ReferenceDataHttpTests.ApiFactory factory, ProvisionedIntegrationSource source, string body)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", source.Bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", "tnt-2.2-harborops-v1");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    internal static async Task<(HttpStatusCode Status, Guid? Receipt, bool Duplicate)> Submit(
        ReferenceDataHttpTests.ApiFactory factory, ProvisionedIntegrationSource source, string body)
    {
        using var response = await Post(factory, source, body);
        if (response.StatusCode != HttpStatusCode.Accepted)
            return (response.StatusCode, null, false);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response.StatusCode, json.GetProperty("receiptId").GetGuid(),
            json.GetProperty("duplicate").GetBoolean());
    }

    internal static async Task<Guid> Accept(
        ReferenceDataHttpTests.ApiFactory factory, ProvisionedIntegrationSource source, string body)
    {
        var (status, receipt, _) = await Submit(factory, source, body);
        Assert.Equal(HttpStatusCode.Accepted, status);
        return receipt!.Value;
    }

    internal static async Task<bool> LeaseExpired(string connection, Guid receipt)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(lease_expires_at_utc <= clock_timestamp(), false) FROM tracking_inbox_work_items WHERE receipt_id=@r",
            direct);
        command.Parameters.AddWithValue("r", receipt);
        return await command.ExecuteScalarAsync() is true;
    }

    internal static async Task WaitForLeaseExpiry(string connection, Guid receipt, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await LeaseExpired(connection, receipt)) return;
            await Task.Delay(250);
        }
        Assert.Fail("The lease did not expire within the bounded wait");
    }

    internal static async Task<TrackingWorkClaim> WaitForClaim(
        TrackingWorkStore store, Guid organization, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var claim = await store.ClaimOneAsync(organization, CancellationToken.None);
            if (claim is not null) return claim;
            await Task.Delay(500);
        }
        Assert.Fail("No work became claimable within the bounded wait");
        throw new InvalidOperationException();
    }

    internal static async Task<long> Count(string connection, string sql, params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
