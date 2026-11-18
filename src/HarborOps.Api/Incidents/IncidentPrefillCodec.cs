using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Incidents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace HarborOps.Api.Incidents;

// A prefill token is a short-lived, server-protected statement of one finite service
// observation. It binds the organization, user and session that asked for it, so a token
// copied to another tenant, user or session, or tampered with, never creates or links anything.
public sealed record IncidentPrefillToken(Guid OrganizationId, Guid ActorId, Guid SessionId,
    string ServiceCode, string Status, DateTimeOffset ObservedAtUtc, DateTimeOffset ExpiresAtUtc);

public sealed class IncidentPrefillCodec(IDataProtectionProvider provider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly IDataProtector protector =
        provider.CreateProtector("HarborOps.M18.IncidentPrefill.v1");

    public (string Token, DateTimeOffset ExpiresAtUtc) Encode(Guid org, Guid actor, Guid session,
        string service, string status, DateTimeOffset observed, DateTimeOffset now)
    {
        var expires = now.Add(Lifetime);
        var json = JsonSerializer.Serialize(new IncidentPrefillToken(org, actor, session,
            service, status, observed, expires));
        return (WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protector.Protect(json))), expires);
    }

    public bool TryDecode(string? input, Guid org, Guid actor, Guid session, DateTimeOffset now,
        out IncidentPrefill? prefill)
    {
        prefill = null;
        if (string.IsNullOrEmpty(input) || input.Length is < 10 or > 2048) return false;
        try
        {
            var json = protector.Unprotect(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input)));
            var value = JsonSerializer.Deserialize<IncidentPrefillToken>(json);
            if (value is null || value.OrganizationId != org || value.ActorId != actor ||
                value.SessionId != session || value.ExpiresAtUtc <= now ||
                value.ServiceCode is not ("api" or "database") ||
                value.Status is not ("healthy" or "degraded" or "unknown") ||
                value.ObservedAtUtc > now.AddSeconds(30)) return false;
            prefill = new IncidentPrefill(value.ServiceCode, value.Status, value.ObservedAtUtc);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or
            ArgumentException or CryptographicException)
        {
            return false;
        }
    }
}
