using System.Security.Cryptography;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingSourceSqlTests
{
    [Fact]
    public async Task DatabasePreventsCredentialIdentityMutationAndHistoryDeletion()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var issued = await store.CreateAsync(fixture.A, "sql_guard", "Protected Source", CancellationToken.None);
        var original = await db.IntegrationCredentials.AsNoTracking()
            .SingleAsync(x => x.Id == issued.CredentialId);
        var otherDigest = RandomNumberGenerator.GetBytes(32);

        var mutation = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integration_credentials SET secret_digest = {otherDigest} WHERE id = {issued.CredentialId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, mutation.SqlState);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM integration_credentials WHERE id = {issued.CredentialId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, deletion.SqlState);
        var sourceCodeChange = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integration_sources SET source_code = {"renamed"} WHERE id = {issued.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, sourceCodeChange.SqlState);
        var badName = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE integration_sources
                SET display_name = {"  "}, version = version + 1, updated_at_utc = {DateTimeOffset.UtcNow}
                WHERE id = {issued.Id}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badName.SqlState);

        var after = await db.IntegrationCredentials.AsNoTracking()
            .SingleAsync(x => x.Id == issued.CredentialId);
        Assert.Equal(original.SecretDigest, after.SecretDigest);
        Assert.Equal(original.ExpiresAtUtc, after.ExpiresAtUtc);
        Assert.NotNull(await store.ValidateAsync(issued.Bearer, CancellationToken.None));

        await store.RevokeAsync(fixture.A, "sql_guard", issued.CredentialId, CancellationToken.None);
        var unrevoke = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integration_credentials SET revoked_at_utc = {(DateTimeOffset?)null} WHERE id = {issued.CredentialId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, unrevoke.SqlState);
        Assert.Null(await store.ValidateAsync(issued.Bearer, CancellationToken.None));
    }

    [Fact]
    public async Task DatabaseRejectsThirdOverlappingCredentialAndForeignTenantSource()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var a = await store.CreateAsync(fixture.A, "overlap_a", "Source A", CancellationToken.None);
        var b = await store.CreateAsync(fixture.B, "overlap_a", "Source B", CancellationToken.None);
        Assert.Equal(fixture.B, (await store.ValidateAsync(b.Bearer, CancellationToken.None))!.OrganizationId);
        var second = await store.RotateAsync(fixture.A, "overlap_a", CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        var credentialId = Guid.NewGuid();
        var digest = RandomNumberGenerator.GetBytes(32);
        var constraint = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO integration_credentials
                (id,organization_id,source_id,secret_digest,key_version,
                 not_before_utc,expires_at_utc,created_at_utc)
                VALUES ({credentialId}, {fixture.A}, {a.Id}, {digest}, {"v1"},
                        {now}, {now.AddDays(1)}, {now})
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, constraint.SqlState);
        Assert.NotNull(await store.ValidateAsync(second.Bearer, CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO integration_credentials
                (id,organization_id,source_id,secret_digest,key_version,
                 not_before_utc,expires_at_utc,created_at_utc)
                VALUES ({Guid.NewGuid()}, {fixture.B}, {a.Id}, {digest}, {"v1"},
                        {now}, {now.AddDays(1)}, {now})
                """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreign.SqlState);
        var active = await db.IntegrationCredentials.CountAsync(x =>
            x.SourceId == a.Id && x.RevokedAtUtc == null && x.ExpiresAtUtc > now);
        Assert.Equal(2, active);
    }

    [Fact]
    public async Task ExpiredAndFutureCredentialsNeverAuthenticate()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var source = await store.CreateAsync(fixture.A, "window_a", "Window Source", CancellationToken.None);
        async Task<string> Add(DateTimeOffset from, DateTimeOffset until)
        {
            var secret = RandomNumberGenerator.GetBytes(32);
            var id = Guid.NewGuid();
            var digest = SHA256.HashData(secret);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO integration_credentials
                (id,organization_id,source_id,secret_digest,key_version,
                 not_before_utc,expires_at_utc,created_at_utc)
                VALUES ({id}, {fixture.A}, {source.Id}, {digest}, {"v1"},
                        {from}, {until}, {from})
                """);
            return IntegrationSourceStore.FormatBearer(id, secret);
        }
        var now = DateTimeOffset.UtcNow;
        var expired = await Add(now.AddDays(-5), now.AddDays(-4));
        var future = await Add(now.AddDays(3), now.AddDays(4));
        Assert.Null(await store.ValidateAsync(expired, CancellationToken.None));
        Assert.Null(await store.ValidateAsync(future, CancellationToken.None));
        Assert.NotNull(await store.ValidateAsync(source.Bearer, CancellationToken.None));
    }
}
