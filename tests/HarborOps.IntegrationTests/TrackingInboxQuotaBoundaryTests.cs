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

public sealed class TrackingInboxQuotaBoundaryTests
{
    private static string Payload(string id, string code = "GTIN") => $$"""
        {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"{{code}}",
         "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
         "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
        """;

    private static async Task<HttpResponseMessage> Post(
        HttpClient client, string bearer, string id, string code = "GTIN")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", "tnt-2.2-harborops-v1");
        request.Content = new StringContent(Payload(id, code), Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Exact9999_10000_10001AndConcurrentCrossSourceAdmission()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var a = await sources.CreateAsync(fixture.A, "m8_quota_a", "Quota A",
            CancellationToken.None);
        var b = await sources.CreateAsync(fixture.A, "m8_quota_b", "Quota B",
            CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();

        // Seed 9,999 internally complete synthetic triples in one transaction.
        // This deliberately exercises the real default 10,000 limit, not a tiny
        // test override that might conceal an off-by-one or cross-source race.
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = new NpgsqlCommand("""
                WITH seed AS MATERIALIZED (
                    SELECT gen_random_uuid() AS rid, 'bulk-g8-' || n::text AS external_id
                      FROM generate_series(1, 9999) AS n
                ),
                receipts AS (
                    INSERT INTO tracking_inbox_receipts
                        (id, organization_id, source_id, credential_id, profile,
                         source_event_id, canonical_digest, canonical_payload_utf8,
                         received_at_utc, correlation_id)
                    SELECT rid, @org, @source, @credential, 'tnt-2.2-harborops-v1',
                           external_id, decode(repeat('ab',32),'hex'),
                           convert_to('{"canonicalVersion":"m8-canonical-v1"}', 'UTF8'),
                           clock_timestamp(), 'bulk-g8'
                      FROM seed
                    RETURNING id, organization_id, source_id
                ),
                events AS (
                    INSERT INTO tracking_accepted_events
                        (id, receipt_id, organization_id, source_id, event_family, event_code,
                         classifier, external_schema_version, occurred_at_utc,
                         equipment_reference, raw_payload_utf8, canonical_payload)
                    SELECT gen_random_uuid(), r.id, r.organization_id, r.source_id,
                           'Equipment', 'GTIN', 'ACT', '2.2.0',
                           '2026-11-03T10:00:00Z'::timestamptz, 'SYN-BOX-ABCD',
                           convert_to('{"sourceEventId":"bulk"}','UTF8'),
                           '{"canonicalVersion":"m8-canonical-v1"}'::jsonb
                    FROM receipts r
                    RETURNING receipt_id
                )
                INSERT INTO tracking_inbox_work_items
                    (receipt_id, organization_id, source_id, state, attempt_count,
                     created_at_utc, updated_at_utc)
                SELECT r.id, r.organization_id, r.source_id, 'Pending', 0,
                       now(), now()
                  FROM receipts r JOIN events e ON e.receipt_id = r.id
                """, (NpgsqlConnection)db.Database.GetDbConnection());
            command.CommandTimeout = 180;
            command.Parameters.AddWithValue("org", fixture.A);
            command.Parameters.AddWithValue("source", a.Id);
            command.Parameters.AddWithValue("credential", a.CredentialId);
            Assert.Equal(9999, await command.ExecuteNonQueryAsync());
        }
        finally { await db.Database.CloseConnectionAsync(); }

        Assert.Equal(9999, await db.TrackingInboxWorkItems
            .CountAsync(x => x.OrganizationId == fixture.A && x.State == "Pending"));
        // Concurrent new keys from two different sources compete for exactly one slot.
        var replies = await Task.WhenAll(new[]
        {
            Task.Run(async () =>
            {
                using var client = factory.CreateClient();
                using var response = await Post(client, a.Bearer, "quota-a-10000");
                return (response.StatusCode, Source: a, Key: "quota-a-10000");
            }),
            Task.Run(async () =>
            {
                using var client = factory.CreateClient();
                using var response = await Post(client, b.Bearer, "quota-b-10001");
                return (response.StatusCode, Source: b, Key: "quota-b-10001");
            })
        });
        Assert.Equal(1, replies.Count(x => x.StatusCode == HttpStatusCode.Accepted));
        Assert.Equal(1, replies.Count(x => x.StatusCode == HttpStatusCode.ServiceUnavailable));
        Assert.Equal(10000, await db.TrackingInboxWorkItems
            .CountAsync(x => x.OrganizationId == fixture.A && x.State == "Pending"));

        var winner = replies.Single(x => x.StatusCode == HttpStatusCode.Accepted);
        var loser = replies.Single(x => x.StatusCode == HttpStatusCode.ServiceUnavailable);
        using var retryClient = factory.CreateClient();
        using var replay = await Post(retryClient, winner.Source.Bearer, winner.Key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("duplicate").GetBoolean());
        using var collision = await Post(retryClient, winner.Source.Bearer, winner.Key, "GTOT");
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using var blocked = await Post(retryClient, loser.Source.Bearer, loser.Key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
        Assert.Equal(10000, await db.TrackingInboxWorkItems
            .CountAsync(x => x.OrganizationId == fixture.A && x.State == "Pending"));
    }
}
