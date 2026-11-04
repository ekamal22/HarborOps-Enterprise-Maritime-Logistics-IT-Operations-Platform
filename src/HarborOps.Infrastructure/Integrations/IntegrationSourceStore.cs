using System.Data.Common;
using System.Security.Cryptography;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HarborOps.Infrastructure.Integrations;

public sealed record IntegrationAuthentication(ValidatedIntegrationSource? Source, bool Forbidden = false);

public sealed class IntegrationSourceStore(HarborOpsDbContext db, TimeProvider clock)
{
    public static string FormatBearer(Guid credentialId, ReadOnlySpan<byte> secret)
    {
        if (credentialId == Guid.Empty || secret.Length != 32)
            throw new ArgumentException("A nonempty credential ID and 256-bit secret are required");
        return "hop_v1_" + credentialId.ToString("N") + "_" +
            Convert.ToBase64String(secret).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static byte[]? ParseSecret(string token, out Guid id)
    {
        id = Guid.Empty;
        if (token.Length != 83 || !token.StartsWith("hop_v1_", StringComparison.Ordinal) ||
            token[39] != '_' || !Guid.TryParseExact(token.AsSpan(7, 32), "N", out id))
            return null;
        var encoded = token[40..];
        if (encoded.Length != 43 || encoded.Any(c =>
            !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32)
            {
                CryptographicOperations.ZeroMemory(bytes);
                return null;
            }
            if (FormatBearer(id, bytes) != token)
            {
                CryptographicOperations.ZeroMemory(bytes);
                return null;
            }
            return bytes;
        }
        catch (FormatException) { return null; }
    }

    private static ProvisionedIntegrationSource Issue(IntegrationSource source, IntegrationCredential credential,
        ReadOnlySpan<byte> secret) =>
        new(source.Id, source.SourceCode, credential.Id, FormatBearer(credential.Id, secret));

    private async Task<Organization> LockOrganizationAsync(Guid id, CancellationToken ct)
    {
        var organization = (await db.Organizations.FromSqlInterpolated(
            $"SELECT * FROM organizations WHERE id = {id} FOR UPDATE").ToListAsync(ct)).Single();
        // A tracked instance may predate an administrator's update in another context.
        // Refresh it while holding the row lock before deciding active entitlement.
        await db.Entry(organization).ReloadAsync(ct);
        return organization;
    }

    private async Task<IntegrationSource> LockSourceAsync(Guid organizationId, string code, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        var source = (await db.IntegrationSources.FromSqlInterpolated(
            $"SELECT * FROM integration_sources WHERE organization_id = {organizationId} AND source_code = {normalized} FOR UPDATE")
            .ToListAsync(ct)).Single();
        await db.Entry(source).ReloadAsync(ct);
        return source;
    }

    private DateTimeOffset NextUpdateTime(IntegrationSource source)
    {
        var now = clock.GetUtcNow();
        return now < source.UpdatedAtUtc ? source.UpdatedAtUtc : now;
    }

    private static IntegrationCredential NewCredential(IntegrationSource source, byte[] secret, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(), OrganizationId = source.OrganizationId, SourceId = source.Id,
            KeyVersion = "v1", SecretDigest = SHA256.HashData(secret),
            NotBeforeUtc = now, ExpiresAtUtc = now + IntegrationSourcePolicy.MaximumLifetime,
            CreatedAtUtc = now
        };

    public async Task<ProvisionedIntegrationSource> CreateAsync(Guid organizationId, string code,
        string name, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        var display = IntegrationSourcePolicy.ValidateName(name);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var organization = await LockOrganizationAsync(organizationId, ct);
        if (organization.Status != "Active") throw new InvalidOperationException("Active organization required");
        if (await db.IntegrationSources.AnyAsync(x => x.OrganizationId == organizationId &&
            x.SourceCode == normalized, ct)) throw new InvalidOperationException("Source code already reserved");
        var now = clock.GetUtcNow();
        var source = new IntegrationSource
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, SourceCode = normalized,
            DisplayName = display, Status = "Active", Version = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var credential = NewCredential(source, secret, now);
            db.IntegrationSources.Add(source);
            db.IntegrationCredentials.Add(credential);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Issue(source, credential, secret);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task<ProvisionedIntegrationSource> RotateAsync(Guid organizationId, string code,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var organization = await LockOrganizationAsync(organizationId, ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        if (organization.Status != "Active" || source.Status != "Active")
            throw new InvalidOperationException("Active source and organization required");
        var now = NextUpdateTime(source);
        var active = await db.IntegrationCredentials
            .Where(c => c.OrganizationId == organizationId && c.SourceId == source.Id &&
                c.RevokedAtUtc == null && c.NotBeforeUtc <= now && c.ExpiresAtUtc > now)
            .OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        if (active.Count >= 2) throw new InvalidOperationException("Rotation overlap limit reached");
        foreach (var previous in active)
        {
            var cutoff = now + IntegrationSourcePolicy.MaximumOverlap;
            if (previous.ExpiresAtUtc > cutoff) previous.ExpiresAtUtc = cutoff;
        }
        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var credential = NewCredential(source, secret, now);
            db.IntegrationCredentials.Add(credential);
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Issue(source, credential, secret);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task RevokeAsync(Guid organizationId, string code, Guid credentialId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrganizationAsync(organizationId, ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        var credential = await db.IntegrationCredentials.SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.SourceId == source.Id && x.Id == credentialId, ct)
            ?? throw new InvalidOperationException("Credential not found");
        await db.Entry(credential).ReloadAsync(ct);
        if (credential.RevokedAtUtc is null)
        {
            var now = NextUpdateTime(source);
            credential.RevokedAtUtc = now < credential.CreatedAtUtc ? credential.CreatedAtUtc : now;
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task DisableAsync(Guid organizationId, string code, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrganizationAsync(organizationId, ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        if (source.Status == "Active")
        {
            var now = NextUpdateTime(source);
            source.Status = "Disabled";
            source.DisabledAtUtc = now;
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.IntegrationCredentials.Where(x => x.OrganizationId == organizationId &&
                x.SourceId == source.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc,
                    x => x.CreatedAtUtc > now ? x.CreatedAtUtc : now), ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<string> InspectAsync(Guid organizationId, string code, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        var source = await db.IntegrationSources.AsNoTracking().SingleAsync(
            x => x.OrganizationId == organizationId && x.SourceCode == normalized, ct);
        var now = clock.GetUtcNow();
        var count = await db.IntegrationCredentials.CountAsync(
            x => x.OrganizationId == organizationId && x.SourceId == source.Id &&
                x.RevokedAtUtc == null && x.NotBeforeUtc <= now && x.ExpiresAtUtc > now, ct);
        return $"Source {source.Id} code={source.SourceCode} status={source.Status} active_credentials={count} version={source.Version}";
    }

    public async Task<IntegrationAuthentication> AuthenticateAsync(string token, CancellationToken ct)
    {
        var secret = ParseSecret(token, out var credentialId);
        if (secret is null) return new(null);
        try
        {
            // One database command locks the organization, source and credential within a
            // consistent authorization boundary. Revocation and suspension updates cannot commit
            // during the lookup. M8 repeats this check in its receipt transaction.
            // Shared locks are taken in the same order as every writer (organization, source,
            // credential). A single FOR SHARE OF c, s, o join locks in planner order and can
            // deadlock against revocation. Credential ownership is immutable, so resolving the
            // owning rows before locking is safe; the final statement rechecks all mutable state.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                SELECT 1 FROM organizations
                WHERE id = (SELECT organization_id FROM integration_credentials WHERE id = @credential_id)
                FOR SHARE;
                SELECT 1 FROM integration_sources
                WHERE id = (SELECT source_id FROM integration_credentials WHERE id = @credential_id)
                FOR SHARE;
                SELECT c.secret_digest, s.id, s.organization_id, s.status, s.capability, o.status
                FROM integration_credentials AS c
                JOIN integration_sources AS s
                  ON s.id = c.source_id AND s.organization_id = c.organization_id
                JOIN organizations AS o ON o.id = s.organization_id
                WHERE c.id = @credential_id AND c.key_version = 'v1'
                  AND c.revoked_at_utc IS NULL
                  AND c.not_before_utc <= @checked_at AND c.expires_at_utc > @checked_at
                FOR SHARE OF c, s, o
                """;
            var id = command.CreateParameter();
            id.ParameterName = "credential_id";
            id.Value = credentialId;
            command.Parameters.Add(id);
            var checkedAt = command.CreateParameter();
            checkedAt.ParameterName = "checked_at";
            checkedAt.Value = clock.GetUtcNow();
            command.Parameters.Add(checkedAt);

            IntegrationAuthentication outcome = new(null);
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                // Skip the two ordered locking statements; only the final result set is read.
                await reader.NextResultAsync(ct);
                await reader.NextResultAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    var expected = reader.GetFieldValue<byte[]>(0);
                    var digest = SHA256.HashData(secret);
                    try
                    {
                        if (expected.Length == 32 && CryptographicOperations.FixedTimeEquals(expected, digest))
                        {
                            var sourceId = reader.GetGuid(1);
                            var organizationId = reader.GetGuid(2);
                            var allowed = reader.GetString(3) == "Active" &&
                                reader.GetString(4) == IntegrationSourcePolicy.Capability &&
                                reader.GetString(5) == "Active";
                            outcome = allowed
                                ? new(new ValidatedIntegrationSource(sourceId, organizationId))
                                : new(null, Forbidden: true);
                        }
                    }
                    finally { CryptographicOperations.ZeroMemory(digest); }
                }
            }
            await transaction.CommitAsync(ct);
            return outcome;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    // Safe compatibility method for tests and internal callers that only need permitted sources.
    public async Task<ValidatedIntegrationSource?> ValidateAsync(string token, CancellationToken ct) =>
        (await AuthenticateAsync(token, ct)).Source;
}
