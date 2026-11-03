using System.Security.Cryptography;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Integrations;

public sealed class IntegrationSourceStore(HarborOpsDbContext db, TimeProvider clock)
{
    public static string FormatBearer(Guid credentialId, ReadOnlySpan<byte> secret) =>
        "hop_v1_" + credentialId.ToString("N") + "_" +
        Convert.ToBase64String(secret).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? ParseSecret(string token, out Guid id)
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
            return bytes.Length == 32 ? bytes : null;
        }
        catch (FormatException) { return null; }
    }

    private static ProvisionedIntegrationSource Issue(IntegrationSource source, IntegrationCredential credential,
        ReadOnlySpan<byte> secret) => new(source.Id, source.SourceCode, credential.Id, FormatBearer(credential.Id, secret));

    private async Task<IntegrationSource> LockSourceAsync(Guid organizationId, string code, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        return await db.IntegrationSources.FromSqlInterpolated(
            $"SELECT * FROM integration_sources WHERE organization_id = {organizationId} AND source_code = {normalized} FOR UPDATE")
            .SingleAsync(ct);
    }

    public async Task<ProvisionedIntegrationSource> CreateAsync(Guid organizationId, string code,
        string name, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        var display = IntegrationSourcePolicy.ValidateName(name);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var org = await db.Organizations.FromSqlInterpolated(
            $"SELECT * FROM organizations WHERE id = {organizationId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (org?.Status != "Active") throw new InvalidOperationException("Active organization required");
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

    private static IntegrationCredential NewCredential(IntegrationSource source, byte[] secret, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(), OrganizationId = source.OrganizationId, SourceId = source.Id,
            KeyVersion = "v1", SecretDigest = SHA256.HashData(secret), NotBeforeUtc = now,
            ExpiresAtUtc = now + IntegrationSourcePolicy.MaximumLifetime, CreatedAtUtc = now
        };

    public async Task<ProvisionedIntegrationSource> RotateAsync(Guid organizationId, string code, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        var orgActive = await db.Organizations.AsNoTracking().AnyAsync(
            o => o.Id == organizationId && o.Status == "Active", ct);
        if (!orgActive || source.Status != "Active")
            throw new InvalidOperationException("Active source required");
        var now = clock.GetUtcNow();
        var active = await db.IntegrationCredentials
            .Where(c => c.OrganizationId == organizationId && c.SourceId == source.Id &&
                c.RevokedAtUtc == null && c.NotBeforeUtc <= now && c.ExpiresAtUtc > now)
            .OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        if (active.Count >= 2) throw new InvalidOperationException("Rotation overlap limit reached");
        foreach (var credential in active)
            if (credential.ExpiresAtUtc > now + IntegrationSourcePolicy.MaximumOverlap)
                credential.ExpiresAtUtc = now + IntegrationSourcePolicy.MaximumOverlap;
        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var issued = NewCredential(source, secret, now);
            db.IntegrationCredentials.Add(issued);
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Issue(source, issued, secret);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task RevokeAsync(Guid organizationId, string code, Guid credentialId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        var credential = await db.IntegrationCredentials.SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.SourceId == source.Id && x.Id == credentialId, ct)
            ?? throw new InvalidOperationException("Credential not found");
        if (credential.RevokedAtUtc is null)
        {
            var now = clock.GetUtcNow();
            credential.RevokedAtUtc = now;
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task DisableAsync(Guid organizationId, string code, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await LockSourceAsync(organizationId, code, ct);
        if (source.Status == "Active")
        {
            var now = clock.GetUtcNow();
            source.Status = "Disabled";
            source.DisabledAtUtc = now;
            source.Version++;
            source.UpdatedAtUtc = now;
            await db.IntegrationCredentials.Where(x => x.OrganizationId == organizationId &&
                x.SourceId == source.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<string> InspectAsync(Guid organizationId, string code, CancellationToken ct)
    {
        var normalized = IntegrationSourcePolicy.NormalizeCode(code);
        var source = await db.IntegrationSources.AsNoTracking().SingleAsync(
            x => x.OrganizationId == organizationId && x.SourceCode == normalized, ct);
        var count = await db.IntegrationCredentials.CountAsync(
            x => x.OrganizationId == organizationId && x.SourceId == source.Id &&
                x.RevokedAtUtc == null && x.ExpiresAtUtc > clock.GetUtcNow(), ct);
        return $"Source {source.Id} code={source.SourceCode} status={source.Status} active_credentials={count} version={source.Version}";
    }

    public async Task<ValidatedIntegrationSource?> ValidateAsync(string token, CancellationToken ct)
    {
        var secret = ParseSecret(token, out var credentialId);
        if (secret is null) return null;
        try
        {
            var credential = await db.IntegrationCredentials.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == credentialId && x.KeyVersion == "v1", ct);
            if (credential is null || credential.SecretDigest.Length != 32) return null;
            var digest = SHA256.HashData(secret);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(digest, credential.SecretDigest)) return null;
            }
            finally { CryptographicOperations.ZeroMemory(digest); }
            var now = clock.GetUtcNow();
            if (credential.RevokedAtUtc != null || credential.NotBeforeUtc > now ||
                credential.ExpiresAtUtc <= now) return null;
            var valid = await (from source in db.IntegrationSources.AsNoTracking()
                               join org in db.Organizations.AsNoTracking() on source.OrganizationId equals org.Id
                               where source.Id == credential.SourceId &&
                                     source.OrganizationId == credential.OrganizationId &&
                                     source.Status == "Active" && org.Status == "Active" &&
                                     source.Capability == IntegrationSourcePolicy.Capability
                               select new ValidatedIntegrationSource(source.Id, source.OrganizationId))
                .SingleOrDefaultAsync(ct);
            return valid;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }
}
