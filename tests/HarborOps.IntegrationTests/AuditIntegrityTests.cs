using HarborOps.Infrastructure.Audit;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AuditIntegrityTests
{
    [Fact]
    public async Task BusinessChangesGenerateMinimalAppendOnlyEvidence()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var entries = await db.AuditEntries.AsNoTracking()
            .Where(x => x.OrganizationId == fixture.A && x.ResourceType == "organizations")
            .ToListAsync();
        var created = Assert.Single(entries, x => x.EventType == "organizations.insert");
        Assert.Equal("Succeeded", created.Outcome);
        Assert.Equal(1, created.EventSchemaVersion);
        using (var metadata = System.Text.Json.JsonDocument.Parse(created.MetadataJson))
            Assert.Equal("insert", metadata.RootElement.GetProperty("operation").GetString());

        var payload = string.Join("|", entries.Select(e => e.MetadataJson));
        Assert.DoesNotContain("@example.test", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearer", payload, StringComparison.OrdinalIgnoreCase);

        foreach (var sql in new[]
        {
            $"UPDATE audit_entries SET outcome = 'Denied' WHERE id = '{created.Id}'",
            $"DELETE FROM audit_entries WHERE id = '{created.Id}'",
            "TRUNCATE TABLE audit_entries"
        })
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
        Assert.NotNull(await db.AuditEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == created.Id));

        var changed = await db.Organizations.Where(x => x.Id == fixture.A)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DisplayName, "G16 Changed Name")
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow));
        Assert.Equal(1, changed);
        var updatedAudit = await db.AuditEntries.AsNoTracking()
            .SingleAsync(x => x.OrganizationId == fixture.A &&
                x.ResourceType == "organizations" && x.EventType == "organizations.update");
        using var changedMetadata = System.Text.Json.JsonDocument.Parse(updatedAudit.MetadataJson);
        Assert.Contains(changedMetadata.RootElement.GetProperty("changedFields")
            .EnumerateArray().Select(x => x.GetString()), x => x == "display_name");
    }

    [Fact]
    public async Task AuditInsertFailureAbortsTheOriginalBusinessMutation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var before = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == fixture.A);
        var auditCount = await db.AuditEntries.CountAsync(e =>
            e.OrganizationId == fixture.A && e.EventType == "organizations.update");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            // A deliberately invalid server-side actor kind forces the audit
            // trigger to fail, proving that the business UPDATE cannot commit.
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('harborops.audit_actor_kind', 'InvalidActor', true)");
            var failure = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Organizations.Where(o => o.Id == fixture.A)
                    .ExecuteUpdateAsync(x => x.SetProperty(o => o.DisplayName, "Must Roll Back")
                        .SetProperty(o => o.Version, o => o.Version + 1)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            await tx.RollbackAsync();
        }
        var after = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == fixture.A);
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(auditCount, await db.AuditEntries.CountAsync(e =>
            e.OrganizationId == fixture.A && e.EventType == "organizations.update"));
    }

    [Fact]
    public async Task AuditInsertionDoesNotLockTheOrganizationRowBehindRevocations()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var blocker = new NpgsqlConnection(TestDatabase.ConnectionString());
        await blocker.OpenAsync();
        await using var blockerTx = await blocker.BeginTransactionAsync();
        await using (var lockOrg = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id = @organization FOR UPDATE", blocker, blockerTx))
        {
            lockOrg.Parameters.AddWithValue("organization", fixture.A);
            Assert.Equal(fixture.A, (Guid)(await lockOrg.ExecuteScalarAsync())!);
        }

        await using var writer = new NpgsqlConnection(TestDatabase.ConnectionString());
        await writer.OpenAsync();
        await using var writerTx = await writer.BeginTransactionAsync();
        await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '1500ms'",
            writer, writerTx))
            await timeout.ExecuteNonQueryAsync();
        await using (var mutate = new NpgsqlCommand("""
            UPDATE organization_memberships
            SET updated_at_utc = clock_timestamp()
            WHERE id = (SELECT id FROM organization_memberships
                        WHERE organization_id = @organization LIMIT 1)
            """, writer, writerTx))
        {
            mutate.Parameters.AddWithValue("organization", fixture.A);
            Assert.Equal(1, await mutate.ExecuteNonQueryAsync());
        }
        await writerTx.CommitAsync();
        await blockerTx.RollbackAsync();
    }

    [Fact]
    public async Task ReaderUsesLiveAuditorAndOrganizationScope()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<AuditReader>();
        var wrongActor = Guid.NewGuid();
        var page = await reader.ListAsync(fixture.A, wrongActor,
            Guid.NewGuid(), 1, 25, CancellationToken.None);
        Assert.Empty(page.Entries);
        Assert.Null(await reader.GetAsync(fixture.A, wrongActor, Guid.NewGuid(),
            Guid.NewGuid(), CancellationToken.None));
    }
}
