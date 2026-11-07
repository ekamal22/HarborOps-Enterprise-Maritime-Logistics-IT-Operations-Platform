using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using HarborOps.Domain.Shipments;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.WebUtilities;

namespace HarborOps.Api.Search;

public sealed record ShipmentCursor(Guid OrganizationId, Guid ActorId, Guid SessionId,
    string Fingerprint, Guid After, DateTimeOffset AsOfUtc, DateTimeOffset ExpiresAtUtc);

public sealed class ShipmentCursorCodec(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector =
        provider.CreateProtector("HarborOps.M12.ShipmentSearchCursor.v1");

    public string Encode(Guid org, Guid actor, Guid session, string fingerprint,
        Guid after, DateTimeOffset asOfUtc, DateTimeOffset now)
    {
        var body = JsonSerializer.Serialize(new ShipmentCursor(org, actor, session, fingerprint,
            after, asOfUtc, now.AddMinutes(15)));
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protector.Protect(body)));
    }

    public bool TryDecode(string token, Guid org, Guid actor, Guid session,
        string fingerprint, DateTimeOffset now, out ShipmentCursor? cursor)
    {
        cursor = null;
        if (token.Length is < 10 or > 2048) return false;
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(token);
            var payload = protector.Unprotect(Encoding.UTF8.GetString(bytes));
            var parsed = JsonSerializer.Deserialize<ShipmentCursor>(payload);
            if (parsed is null || parsed.OrganizationId != org ||
                parsed.ActorId != actor || parsed.SessionId != session ||
                parsed.Fingerprint != fingerprint || parsed.After == Guid.Empty ||
                parsed.ExpiresAtUtc <= now || parsed.AsOfUtc > now.AddMinutes(1))
                return false;
            cursor = parsed;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException
            or CryptographicException)
        {
            return false;
        }
    }
}

public sealed class ShipmentExportGate
{
    private readonly ConcurrentDictionary<Guid, byte> active = new();
    public bool TryEnter(Guid userId) => active.TryAdd(userId, 0);
    public void Leave(Guid userId) => active.TryRemove(userId, out _);
}

public static class ShipmentSearchEndpoints
{
    private static IResult Error(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] },
            statusCode: status);

    private static IResult Access(HttpContext ctx, OrganizationAccessError code) => code switch
    {
        OrganizationAccessError.SelectionRequired =>
            Error(ctx, 409, "organization_selection_required", "Select an organization to continue"),
        OrganizationAccessError.Revoked =>
            Error(ctx, 403, "organization_access_revoked", "Organization access is no longer available"),
        OrganizationAccessError.Forbidden =>
            Error(ctx, 403, "access_denied", "Access denied"),
        OrganizationAccessError.NotFound =>
            Error(ctx, 404, "resource_not_found", "Resource not found"),
        OrganizationAccessError.Invalid =>
            Error(ctx, 400, "invalid_request", "Invalid request"),
        OrganizationAccessError.Conflict =>
            Error(ctx, 409, "conflict", "The requested operation conflicts with current state"),
        _ => Error(ctx, 503, "dependency_unavailable", "Search temporarily unavailable")
    };

    private static bool Id(string text, out Guid value) =>
        Guid.TryParse(text, out value) && value != Guid.Empty;

    private static bool SameOriginDownload(HttpContext ctx)
    {
        // Cookie SameSite policy does not distinguish a sibling subdomain.
        // Restrict browser-initiated downloads to the current origin as an
        // additional defense against unwanted cross-origin bulk exports.
        var site = ctx.Request.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0 && site is not ("same-origin" or "none")) return false;
        var origin = ctx.Request.Headers.Origin.ToString();
        if (origin.Length == 0) return true;
        return Uri.TryCreate(origin, UriKind.Absolute, out var parsed) &&
            string.Equals(parsed.GetLeftPart(UriPartial.Authority),
                ctx.Request.Scheme + "://" + ctx.Request.Host,
                StringComparison.OrdinalIgnoreCase);
    }

    private static ShipmentSearchFilters Parse(HttpContext ctx, bool export)
    {
        var query = ctx.Request.Query;
        string[] allowed = ["q", "recordStatus", "workflowStatus", "voyageId",
            "originPortId", "destinationPortId",
            "plannedDepartureFromUtc", "plannedDepartureBeforeUtc",
            "plannedArrivalFromUtc", "plannedArrivalBeforeUtc"];
        if (!export) allowed = [..allowed, "limit", "cursor"];
        if (query.Count > allowed.Length || query.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)) ||
            query.Any(kv => kv.Value.Count != 1))
            throw new ArgumentException("Invalid search query");
        static Guid? G(Microsoft.AspNetCore.Http.IQueryCollection values, string key)
        {
            if (!values.ContainsKey(key)) return null;
            if (!Guid.TryParse(values[key], out var value) || value == Guid.Empty)
                throw new ArgumentException("Invalid identifier");
            return value;
        }
        static DateTimeOffset? D(Microsoft.AspNetCore.Http.IQueryCollection values, string key)
        {
            if (!values.ContainsKey(key)) return null;
            return ShipmentPolicy.Utc(values[key].ToString());
        }
        var limit = 25;
        if (query.ContainsKey("limit") && (!int.TryParse(query["limit"], out limit) ||
            limit is < 1 or > 100))
            throw new ArgumentException("Invalid limit");
        return ShipmentSearchFilters.Create(query["q"].ToString(),
            query["recordStatus"].ToString(), query["workflowStatus"].ToString(),
            G(query, "voyageId"), G(query, "originPortId"), G(query, "destinationPortId"),
            D(query, "plannedDepartureFromUtc"), D(query, "plannedDepartureBeforeUtc"),
            D(query, "plannedArrivalFromUtc"), D(query, "plannedArrivalBeforeUtc"), limit);
    }

    public static void MapShipmentSearchEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/v1/organizations/{organizationId}")
            .RequireAuthorization();
        root.MapGet("/shipments/search", async (string organizationId,
            HttpContext ctx, ShipmentSearchStore store, ShipmentCursorCodec codec,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org))
                return Access(ctx, OrganizationAccessError.Invalid);
            ShipmentSearchFilters filters;
            try { filters = Parse(ctx, export: false); }
            catch (ArgumentException) { return Access(ctx, OrganizationAccessError.Invalid); }
            var session = (CurrentSession)ctx.Items["IdentitySession"]!;
            var now = clock.GetUtcNow();
            var asOf = now;
            Guid? after = null;
            var token = ctx.Request.Query["cursor"].ToString();
            if (token.Length > 0)
            {
                if (!codec.TryDecode(token, org, session.UserId, session.SessionId,
                    filters.Fingerprint(), now, out var parsed))
                    return Error(ctx, 409, "stale_or_invalid_cursor", "Restart this search");
                asOf = parsed!.AsOfUtc;
                after = parsed.After;
            }
            else if (ctx.Request.Query.ContainsKey("cursor"))
                return Access(ctx, OrganizationAccessError.Invalid);
            var result = await store.SearchAsync(session, org, filters, asOf, after, ct);
            if (result.Value is null) return Access(ctx, result.Error);
            var next = result.Value.NextAfter.HasValue
                ? codec.Encode(org, session.UserId, session.SessionId, filters.Fingerprint(),
                    result.Value.NextAfter.Value, asOf, now) : null;
            return Results.Ok(new { shipments = result.Value.Shipments,
                nextCursor = next, asOfUtc = asOf });
        });

        root.MapGet("/exports/shipments", async (string organizationId,
            HttpContext ctx, ShipmentSearchStore store, ShipmentExportGate gate,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!SameOriginDownload(ctx))
                return Error(ctx, 403, "origin_invalid", "Export requires a same-origin request");
            if (!Id(organizationId, out var org))
                return Access(ctx, OrganizationAccessError.Invalid);
            ShipmentSearchFilters filters;
            try { filters = Parse(ctx, export: true); }
            catch (ArgumentException) { return Access(ctx, OrganizationAccessError.Invalid); }
            var session = (CurrentSession)ctx.Items["IdentitySession"]!;
            if (!gate.TryEnter(session.UserId))
                return Error(ctx, 429, "export_in_progress", "Another export is in progress");
            // Hold the lease until the HTTP response completes, including the file body.
            // Releasing it immediately after returning Results.File would allow concurrent downloads.
            ctx.Response.OnCompleted(() =>
            {
                gate.Leave(session.UserId);
                return Task.CompletedTask;
            });
            var correlation = ctx.Items["CorrelationId"]?.ToString() ?? "database";
            var result = await store.ExportAsync(session, org, filters, correlation, ct);
            if (result.Value is null)
                return result.FailureCode == "export_authority_unavailable"
                    ? Error(ctx, 503, "dependency_unavailable", "Export authority temporarily unavailable")
                    : result.FailureCode is not null
                        ? Error(ctx, 413, result.FailureCode, "Export limit exceeded. Narrow the filters")
                        : Access(ctx, result.Error);
            var filename = "shipments-" + clock.GetUtcNow().UtcDateTime.ToString(
                "yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture) + ".csv";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result.Value, "text/csv; charset=utf-8", filename);
        }).RequireRateLimiting("m12-export");
    }
}