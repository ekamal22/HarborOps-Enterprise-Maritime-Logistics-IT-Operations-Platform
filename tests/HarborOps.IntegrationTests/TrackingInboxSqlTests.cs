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

public sealed class TrackingInboxSqlTests
{
    [Fact]
    public async Task DirectSqlCannotRewriteAcceptedEventReceiptOrWork()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var source = await store.CreateAsync(fixture.A, "m8_sql", "SQL Invariant", CancellationToken.None);
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", source.Bearer);
        req.Headers.Add("X-HarborOps-Tracking-Profile", "tnt-2.2-harborops-v1");
        req.Content = new StringContent(
            "{\"sourceEventId\":\"m8-sql\",\"eventFamily\":\"Equipment\",\"eventCode\":\"GTIN\"," +
            "\"classifier\":\"ACT\",\"occurredAtUtc\":\"2026-11-03T10:00:00Z\"," +
            "\"equipmentReference\":\"SYN-BOX-ABCD\",\"externalSchemaVersion\":\"2.2.0\"}",
            Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("receiptId").GetGuid();

        var receiptChange = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tracking_inbox_receipts SET correlation_id = {"changed"} WHERE id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, receiptChange.SqlState);
        var eventChange = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM tracking_accepted_events WHERE receipt_id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, eventChange.SqlState);
        var workChange = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tracking_inbox_work_items SET attempt_count = {1} WHERE receipt_id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, workChange.SqlState);

        // An orphan receipt cannot commit because its deferred trigger requires
        // both the accepted event and the pending work item in the same transaction.
        var missing = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_inbox_receipts
                    (id, organization_id, source_id, credential_id, profile, source_event_id,
                     canonical_digest, canonical_payload_utf8, received_at_utc, correlation_id)
                SELECT {Guid.NewGuid()}, organization_id, source_id, credential_id, profile,
                       {"m8-orphan"}, canonical_digest, canonical_payload_utf8,
                       received_at_utc, correlation_id
                  FROM tracking_inbox_receipts WHERE id = {id}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, missing.SqlState);
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == id));
    }
}
