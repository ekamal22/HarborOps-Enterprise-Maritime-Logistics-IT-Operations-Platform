using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace HarborOps.Api.Incidents;

public sealed record IncidentCursor(Guid OrganizationId, Guid ActorId, Guid SessionId,
    DateTimeOffset AsOfUtc, DateTimeOffset AfterUpdatedUtc, Guid AfterId,
    DateTimeOffset ExpiresAtUtc);

public sealed class IncidentCursorCodec(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector=
        provider.CreateProtector("HarborOps.M18.IncidentCursor.v1");

    public string Encode(Guid org,Guid actor,Guid session,DateTimeOffset asOf,
        DateTimeOffset afterAt,Guid afterId,DateTimeOffset now)
    {
        var json=JsonSerializer.Serialize(new IncidentCursor(org,actor,session,
            asOf,afterAt,afterId,now.AddMinutes(15)));
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protector.Protect(json)));
    }
    public bool TryDecode(string input,Guid org,Guid actor,Guid session,
        DateTimeOffset now,out IncidentCursor? cursor)
    {
        cursor=null;
        if(input.Length is <10 or >2048)return false;
        try
        {
            var json=protector.Unprotect(
                Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input)));
            var value=JsonSerializer.Deserialize<IncidentCursor>(json);
            if(value is null||value.OrganizationId!=org||value.ActorId!=actor||
                value.SessionId!=session||value.AfterId==Guid.Empty||
                value.ExpiresAtUtc<=now||value.AsOfUtc>now.AddMinutes(1)||
                value.AfterUpdatedUtc>value.AsOfUtc)return false;
            cursor=value;
            return true;
        }
        catch(Exception ex) when(ex is FormatException or JsonException or
            ArgumentException or CryptographicException)return false;
    }
}
