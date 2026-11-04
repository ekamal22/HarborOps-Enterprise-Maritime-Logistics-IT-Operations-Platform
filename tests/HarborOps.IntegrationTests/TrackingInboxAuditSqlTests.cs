using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxAuditSqlTests
{
    [Fact]
    public async Task PopulatedM8DowngradeIsRejectedAndPreservesImmutableHistory()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "g8_history_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema + ",public" };
        await using var administration = new NpgsqlConnection(connection);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261103090000_IntegrationSources");
            var email = "g8-history-" + Guid.NewGuid().ToString("N") + "@example.test";
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var orgs = new OrganizationStore(db, TimeProvider.System);
            await identity.ProvisionAsync(email, "Synthetic History Operator",
                "Synthetic-g8-history-passphrase-123", CancellationToken.None);
            var org = await orgs.BootstrapAsync("g8-history-" +
                Guid.NewGuid().ToString("N")[..10], "G8 History", email, CancellationToken.None);
            var sources = new IntegrationSourceStore(db, TimeProvider.System);
            var source = await sources.CreateAsync(org.Id, "m8_history", "History Source",
                CancellationToken.None);
            await db.Database.MigrateAsync();

            var raw = Encoding.UTF8.GetBytes("""
                {"sourceEventId":"history-protected","eventFamily":"Equipment",
                 "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
                 "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
                """);
            var value = TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!;
            var store = new TrackingInboxStore(db, TimeProvider.System,
                new ConfigurationBuilder().Build(), new NoopTrackingReceiptCommitHook());
            var received = await store.ReceiveAsync(value.Bind(source.Id, org.Id),
                source.Bearer, raw, "history-guard", CancellationToken.None);
            Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);

            var beforeReceipt = await db.TrackingInboxReceipts.AsNoTracking()
                .SingleAsync(x => x.Id == received.ReceiptId);
            var beforeEvent = await db.TrackingAcceptedEvents.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            var beforeWork = await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);

            var rejected = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.MigrateAsync("20261103090000_IntegrationSources"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
            Assert.Equal(10, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var persisted = await db.TrackingInboxReceipts.AsNoTracking()
                .SingleAsync(x => x.Id == received.ReceiptId);
            var accepted = await db.TrackingAcceptedEvents.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            var pending = await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            Assert.Equal(beforeReceipt.CanonicalDigest, persisted.CanonicalDigest);
            Assert.Equal(beforeReceipt.CanonicalPayloadUtf8, persisted.CanonicalPayloadUtf8);
            Assert.Equal(beforeEvent.RawPayloadUtf8, accepted.RawPayloadUtf8);
            Assert.Equal(beforeEvent.CanonicalPayloadJson, accepted.CanonicalPayloadJson);
            Assert.Equal(beforeWork.State, pending.State);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task M8DatabaseIndexesMatchRelationalModel()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UQ_m8_receipt_key"] = "tracking_inbox_receipts",
            ["UQ_m8_event_receipt"] = "tracking_accepted_events",
            ["UQ_m8_conflict_receipt"] = "tracking_receipt_conflicts"
        };
        foreach (var (index, table) in expected)
        {
            await using var command = new NpgsqlCommand("""
                SELECT indexdef
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = @table AND indexname = @index
                """, connection);
            command.Parameters.AddWithValue("table", table);
            command.Parameters.AddWithValue("index", index);
            var definition = (string?)await command.ExecuteScalarAsync();
            Assert.NotNull(definition);
            Assert.Contains("CREATE UNIQUE INDEX", definition, StringComparison.OrdinalIgnoreCase);
        }
        await using var constraints = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint
             WHERE connamespace = current_schema()::regnamespace
               AND conname IN (
                   'AK_m8_integration_credential_scope', 'AK_m8_receipt_scope',
                   'FK_m8_receipt_credential', 'FK_m8_receipt_source',
                   'FK_m8_event_receipt', 'FK_m8_work_receipt',
                   'FK_m8_conflict_receipt')
            """, connection);
        Assert.Equal(7L, Convert.ToInt64(await constraints.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task DirectSqlRejectsDuplicateKeysAndForgedTenantCredentialScopes()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var sources = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var source = await sources.CreateAsync(fixture.A, "m8_forge", "Forge Source",
            CancellationToken.None);
        var payload = Encoding.UTF8.GetBytes("""
            {"sourceEventId":"m8-fk-test","eventFamily":"Equipment","eventCode":"GTIN",
             "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """);
        var value = TrackingEventPolicy.Parse(payload, TimeProvider.System.GetUtcNow()).Value!
            .Bind(source.Id, fixture.A);
        var received = await scope.ServiceProvider.GetRequiredService<TrackingInboxStore>()
            .ReceiveAsync(value, source.Bearer, payload, "fk-scope", CancellationToken.None);
        Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_inbox_receipts
                    (id, organization_id, source_id, credential_id, profile, source_event_id,
                     canonical_digest, canonical_payload_utf8, received_at_utc, correlation_id)
                SELECT {Guid.NewGuid()}, organization_id, source_id, credential_id, profile,
                       source_event_id, canonical_digest, canonical_payload_utf8,
                       received_at_utc, correlation_id
                  FROM tracking_inbox_receipts WHERE id = {received.ReceiptId}
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

        var forged = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_inbox_receipts
                    (id, organization_id, source_id, credential_id, profile, source_event_id,
                     canonical_digest, canonical_payload_utf8, received_at_utc, correlation_id)
                SELECT {Guid.NewGuid()}, {fixture.B}, source_id, credential_id, profile,
                       {"forged-foreign-org"}, canonical_digest, canonical_payload_utf8,
                       received_at_utc, correlation_id
                  FROM tracking_inbox_receipts WHERE id = {received.ReceiptId}
                """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, forged.SqlState);

        var unrelated = await sources.CreateAsync(fixture.A, "m8_forge_other",
            "Other Scoped Credential", CancellationToken.None);
        var credentialMismatch = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_inbox_receipts
                    (id, organization_id, source_id, credential_id, profile, source_event_id,
                     canonical_digest, canonical_payload_utf8, received_at_utc, correlation_id)
                SELECT {Guid.NewGuid()}, organization_id, source_id, {unrelated.CredentialId},
                       profile, {"forged-other-credential"}, canonical_digest,
                       canonical_payload_utf8, received_at_utc, correlation_id
                  FROM tracking_inbox_receipts WHERE id = {received.ReceiptId}
                """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, credentialMismatch.SqlState);

        var twoEvents = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_accepted_events
                    (id, receipt_id, organization_id, source_id, event_family, event_code,
                     classifier, external_schema_version, occurred_at_utc,
                     equipment_reference, raw_payload_utf8, canonical_payload)
                SELECT {Guid.NewGuid()}, receipt_id, organization_id, source_id,
                       event_family, event_code, classifier, external_schema_version,
                       occurred_at_utc, equipment_reference, raw_payload_utf8, canonical_payload
                  FROM tracking_accepted_events WHERE receipt_id = {received.ReceiptId}
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, twoEvents.SqlState);

        var twoWorkItems = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tracking_inbox_work_items
                    (receipt_id, organization_id, source_id, state, attempt_count,
                     created_at_utc, updated_at_utc)
                SELECT receipt_id, organization_id, source_id, state, attempt_count,
                       created_at_utc, updated_at_utc
                  FROM tracking_inbox_work_items WHERE receipt_id = {received.ReceiptId}
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, twoWorkItems.SqlState);

        Assert.Single(await db.TrackingInboxReceipts.Where(x => x.SourceId == source.Id).ToListAsync());
        Assert.Single(await db.TrackingAcceptedEvents.Where(x => x.SourceId == source.Id).ToListAsync());
    }
}
