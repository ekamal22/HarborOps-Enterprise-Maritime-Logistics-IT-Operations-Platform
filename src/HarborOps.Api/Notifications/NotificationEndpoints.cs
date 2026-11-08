using System.Security.Cryptography;
using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Notifications;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Notifications;

public static class NotificationEndpoints
{
    private sealed record ListCursor(
        Guid OrganizationId, Guid UserId, Guid SessionId, string State,
        DateTimeOffset CutoffUtc, DateTimeOffset AfterUtc, Guid AfterId);

    private static CurrentSession Session(HttpContext context) =>
        (CurrentSession)context.Items["IdentitySession"]!;

    private static bool Id(string? value, out Guid result) =>
        Guid.TryParse(value, out result) && result != Guid.Empty;

    private static IResult Fail(HttpContext ctx, int status, string code) =>
        Results.Json(new { code, message = "Request could not be completed",
            correlationId = ctx.Items["CorrelationId"] }, statusCode: status);

    private static IResult Access(HttpContext ctx, OrganizationAccessError error) => error switch
    {
        OrganizationAccessError.SelectionRequired => Fail(ctx,409,"organization_selection_required"),
        OrganizationAccessError.Revoked => Fail(ctx,403,"organization_access_revoked"),
        OrganizationAccessError.Forbidden => Fail(ctx,403,"access_denied"),
        OrganizationAccessError.NotFound => Fail(ctx,404,"resource_not_found"),
        _ => Fail(ctx,400,"invalid_request")
    };

    private static async Task<IResult?> Guard(HttpContext ctx,
        OrganizationStore organizations, Guid org, CancellationToken ct)
    {
        var principal = Session(ctx);
        var result = await organizations.ResolveAsync(principal.UserId,
            principal.SessionId,org,OrganizationPermission.ViewShipmentData,ct);
        return result.Value is null ? Access(ctx,result.Error) : null;
    }

    private static bool Query(HttpContext ctx, params string[] names) =>
        ctx.Request.Query.All(x => names.Contains(x.Key,StringComparer.Ordinal)
            && x.Value.Count == 1);

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider p) =>
        p.CreateProtector("HarborOps.M15.PersonalNotificationCursor.v1")
            .ToTimeLimitedDataProtector();

    private static bool Cursor(HttpContext ctx, IDataProtectionProvider protector,
        Guid org, CurrentSession identity, string state,
        out DateTimeOffset? cutoff, out DateTimeOffset? afterUtc, out Guid? afterId)
    {
        cutoff = null;
        afterUtc = null;
        afterId = null;
        if (!ctx.Request.Query.TryGetValue("cursor",out var text)) return true;
        if (text.Count != 1 || text.ToString().Length is < 1 or > 2048) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<ListCursor>(
                Protector(protector).Unprotect(text.ToString()));
            if (parsed is null || parsed.OrganizationId != org ||
                parsed.UserId != identity.UserId || parsed.SessionId != identity.SessionId ||
                parsed.State != state || parsed.AfterId == Guid.Empty ||
                parsed.CutoffUtc.Offset != TimeSpan.Zero ||
                parsed.AfterUtc.Offset != TimeSpan.Zero ||
                parsed.CutoffUtc == default || parsed.AfterUtc == default ||
                parsed.AfterUtc > parsed.CutoffUtc) return false;
            cutoff = parsed.CutoffUtc;
            afterUtc = parsed.AfterUtc;
            afterId = parsed.AfterId;
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    private static string? Next(IDataProtectionProvider protection,
        Guid org, CurrentSession principal, string state,
        NotificationPage page) =>
        page.NextAfterUtc.HasValue && page.NextAfterId.HasValue
            ? Protector(protection).Protect(JsonSerializer.Serialize(
                new ListCursor(org,principal.UserId,principal.SessionId,state,
                    page.SnapshotAtUtc,page.NextAfterUtc.Value,page.NextAfterId.Value)),
                TimeSpan.FromMinutes(20))
            : null;

    private static object Item(NotificationRecord row) => new
    {
        id = row.Id,
        shipmentId = row.ShipmentId,
        exceptionId = row.ExceptionId,
        eventType = row.EventType,
        ruleCode = row.RuleCode,
        title = NotificationText.Title(row.RuleCode),
        eventLabel = NotificationText.Label(row.EventType),
        createdAtUtc = row.CreatedAtUtc,
        readAtUtc = row.ReadAtUtc,
        acknowledgedAtUtc = row.AcknowledgedAtUtc,
        state = row.AcknowledgedAtUtc.HasValue ? "Acknowledged" :
            row.ReadAtUtc.HasValue ? "Read" : "Unread",
        version = row.Version
    };

    private static async Task<(string Action, long Version)?> ReadAction(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is > 512) return null;
        using var buffer = new MemoryStream();
        var block = new byte[256];
        int read;
        while ((read = await ctx.Request.Body.ReadAsync(block.AsMemory(),ctx.RequestAborted)) > 0)
        {
            if (buffer.Length + read > 512) return null;
            buffer.Write(block,0,read);
        }
        try
        {
            using var json = JsonDocument.Parse(buffer.ToArray());
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in root.EnumerateObject())
                if (!names.Add(part.Name) ||
                    part.Name is not ("action" or "expectedVersion")) return null;
            if (names.Count != 2 ||
                root.GetProperty("action").ValueKind != JsonValueKind.String ||
                root.GetProperty("expectedVersion").ValueKind != JsonValueKind.Number ||
                !root.GetProperty("expectedVersion").TryGetInt64(out var version) ||
                version < 1) return null;
            var action = root.GetProperty("action").GetString();
            return action is "MarkRead" or "Acknowledge"
                ? (action,version) : null;
        }
        catch (JsonException) { return null; }
    }

    private static IResult MutationResult(HttpContext ctx, NotificationMutation result) =>
        result.Code switch
        {
            "Forbidden" => Fail(ctx,403,"access_denied"),
            "NotFound" => Fail(ctx,404,"notification_not_found"),
            "Conflict" => Fail(ctx,409,"notification_conflict"),
            "Invalid" => Fail(ctx,422,"invalid_notification_transition"),
            _ => Fail(ctx,503,"notification_authority_unavailable")
        };

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId}/notifications")
            .RequireAuthorization().RequireRateLimiting("audit-read");

        group.MapGet("",async (
            string organizationId, HttpContext ctx, OrganizationStore organizations,
            NotificationStore store, IDataProtectionProvider protector, CancellationToken ct) =>
        {
            if (!Id(organizationId,out var org) || !Query(ctx,"limit","state","cursor"))
                return Fail(ctx,400,"invalid_request");
            var limit = 25;
            if (ctx.Request.Query.ContainsKey("limit") &&
                (!int.TryParse(ctx.Request.Query["limit"],out limit) ||
                 limit is < 1 or > 50)) return Fail(ctx,400,"invalid_request");
            var state = ctx.Request.Query["state"].ToString();
            if (state.Length == 0) state = "All";
            if (state is not ("All" or "Unread" or "Read" or "Acknowledged"))
                return Fail(ctx,400,"invalid_request");
            var denied = await Guard(ctx,organizations,org,ct);
            if (denied is not null) return denied;
            var principal = Session(ctx);
            if (!Cursor(ctx,protector,org,principal,state,
                out var cutoff,out var afterUtc,out var afterId))
                return Fail(ctx,400,"invalid_cursor");
            try
            {
                var page = await store.ListAsync(org,principal.UserId,principal.SessionId,
                    state,afterUtc,afterId,cutoff,limit,ct);
                return Results.Ok(new { items = page.Items.Select(Item),
                    nextCursor = Next(protector,org,principal,state,page),
                    snapshotAtUtc = page.SnapshotAtUtc });
            }
            catch (InvalidOperationException)
            {
                return Fail(ctx,503,"notification_authority_unavailable");
            }
        });

        group.MapGet("/unread-count",async(
            string organizationId, HttpContext ctx, OrganizationStore organizations,
            NotificationStore store, CancellationToken ct) =>
        {
            if (!Id(organizationId,out var org) || ctx.Request.Query.Count != 0)
                return Fail(ctx,400,"invalid_request");
            var denied = await Guard(ctx,organizations,org,ct);
            if (denied is not null) return denied;
            var principal = Session(ctx);
            try
            {
                var (count,asOfUtc) = await store.UnreadAsync(
                    org,principal.UserId,principal.SessionId,ct);
                return Results.Ok(new { unreadCount=count, asOfUtc });
            }
            catch (InvalidOperationException)
            {
                return Fail(ctx,503,"notification_authority_unavailable");
            }
        });

        group.MapGet("/{notificationId}",async(
            string organizationId, string notificationId, HttpContext ctx,
            OrganizationStore organizations, NotificationStore store, CancellationToken ct) =>
        {
            if (!Id(organizationId,out var org) || !Id(notificationId,out var id) ||
                ctx.Request.Query.Count != 0)
                return Fail(ctx,400,"invalid_request");
            var denied = await Guard(ctx,organizations,org,ct);
            if (denied is not null) return denied;
            var principal = Session(ctx);
            try
            {
                var result = await store.GetAsync(org,principal.UserId,principal.SessionId,id,ct);
                return result is null ? Fail(ctx,404,"notification_not_found")
                    : Results.Ok(Item(result));
            }
            catch (InvalidOperationException)
            {
                return Fail(ctx,503,"notification_authority_unavailable");
            }
        });

        group.MapPatch("/{notificationId}",async(
            string organizationId, string notificationId, HttpContext ctx,
            OrganizationStore organizations, NotificationStore store,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!Id(organizationId,out var org) || !Id(notificationId,out var id) ||
                ctx.Request.Query.Count != 0)
                return Fail(ctx,400,"invalid_request");
            if (!AuthenticationSetup.IsAllowedTransport(ctx,app.Configuration) ||
                !await AuthenticationSetup.ValidMutationAsync(ctx,antiforgery))
                return Fail(ctx,403,"csrf_invalid");
            if (!Guid.TryParse(ctx.Request.Headers["Idempotency-Key"],out var replay) ||
                replay == Guid.Empty || ctx.Request.Headers["Idempotency-Key"].Count != 1)
                return Fail(ctx,400,"invalid_request");
            var action = await ReadAction(ctx);
            if (action is null) return Fail(ctx,400,"invalid_request");
            var denied = await Guard(ctx,organizations,org,ct);
            if (denied is not null) return denied;
            var principal = Session(ctx);
            try
            {
                var result = await store.UpdateAsync(org,principal.UserId,
                    principal.SessionId,id,action.Value.Action,action.Value.Version,
                    replay,ctx.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N"),ct);
                if (result.Code is not ("Applied" or "Replayed"))
                    return MutationResult(ctx,result);
                var item = await store.GetAsync(org,principal.UserId,principal.SessionId,id,ct);
                return item is null ? Fail(ctx,404,"notification_not_found")
                    : Results.Ok(Item(item));
            }
            catch (Npgsql.PostgresException e) when (e.SqlState == "PZ001")
            {
                return Fail(ctx,503,"audit_unavailable");
            }
            catch (InvalidOperationException)
            {
                return Fail(ctx,503,"notification_authority_unavailable");
            }
        }).RequireRateLimiting("m10-mutations");
    }
}
