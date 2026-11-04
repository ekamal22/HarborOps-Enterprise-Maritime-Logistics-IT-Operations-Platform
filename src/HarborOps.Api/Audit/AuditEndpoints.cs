using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Audit;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Audit;

public static class AuditEndpoints
{
    private static IResult Problem(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] },
            statusCode: status);

    private static IResult Denied(HttpContext ctx, OrganizationAccessError error) =>
        error switch
        {
            OrganizationAccessError.SelectionRequired =>
                Problem(ctx, 409, "organization_selection_required", "Select an organization to continue"),
            OrganizationAccessError.Revoked =>
                Problem(ctx, 403, "organization_access_revoked", "Organization access is no longer available"),
            OrganizationAccessError.Forbidden =>
                Problem(ctx, 403, "access_denied", "Access denied"),
            _ => Problem(ctx, 404, "resource_not_found", "Resource not found")
        };

    private static object Entry(AuditEntry entry) => new
    {
        id = entry.Id,
        organizationId = entry.OrganizationId,
        eventType = entry.EventType,
        schemaVersion = entry.EventSchemaVersion,
        outcome = entry.Outcome,
        actorKind = entry.ActorKind,
        actorUserId = entry.ActorUserId,
        actorRole = entry.ActorRole,
        resourceType = entry.ResourceType,
        resourceId = entry.ResourceId,
        correlationId = entry.CorrelationId,
        occurredAtUtc = entry.OccurredAtUtc,
        metadata = System.Text.Json.JsonDocument.Parse(entry.MetadataJson).RootElement.Clone()
    };

    private static async Task RecordDeniedAsync(AuditAccessRecorder recorder,
        HttpContext ctx, Guid organizationId, CurrentSession session, Guid? auditId, CancellationToken ct)
    {
        try
        {
            await recorder.RecordAsync(organizationId, session.UserId, session.SessionId,
                auditId, ctx.Items["CorrelationId"]?.ToString(), false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // An already-denied request must not become authorized because
            // its separately attempted denial evidence could not be stored.
        }
    }

    private static async Task<OrganizationAccessError> AccessAsync(
        HttpContext ctx, OrganizationStore organizations, Guid organizationId, CancellationToken ct)
    {
        var session = (CurrentSession)ctx.Items["IdentitySession"]!;
        var result = await organizations.ResolveAsync(session.UserId, session.SessionId,
            organizationId, OrganizationPermission.ViewAuditEvents, ct);
        return result.Error;
    }

    public static void MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId}/audit-events")
            .RequireAuthorization()
            .RequireRateLimiting("audit-read");

        group.MapGet("/", async (string organizationId, HttpContext ctx,
            OrganizationStore organizations, AuditReader reader,
            AuditAccessRecorder recorder, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Guid.TryParse(organizationId, out var orgId) || orgId == Guid.Empty)
                return Problem(ctx, 400, "invalid_request", "Invalid request");
            if (!AuthenticationSetup.IsAllowedTransport(ctx,
                    ctx.RequestServices.GetRequiredService<IConfiguration>()))
                return Problem(ctx, 403, "transport_required", "Secure transport required");

            var session = (CurrentSession)ctx.Items["IdentitySession"]!;
            var access = await AccessAsync(ctx, organizations, orgId, ct);
            if (access != OrganizationAccessError.None)
            {
                await RecordDeniedAsync(recorder, ctx, orgId, session, null, ct);
                return Denied(ctx, access);
            }

            var pageText = ctx.Request.Query["page"].ToString();
            var limitText = ctx.Request.Query["limit"].ToString();
            if (pageText.Length > 0 && (!int.TryParse(pageText, out var parsedPage) ||
                parsedPage is < 1 or > 1000))
                return Problem(ctx, 400, "invalid_request", "Invalid page");
            if (limitText.Length > 0 && (!int.TryParse(limitText, out var parsedLimit) ||
                parsedLimit is < 1 or > 100))
                return Problem(ctx, 400, "invalid_request", "Invalid limit");
            var page = pageText.Length == 0 ? 1 : int.Parse(pageText);
            var limit = limitText.Length == 0 ? 25 : int.Parse(limitText);
            var result = await reader.ListAsync(orgId, session.UserId, session.SessionId, page, limit, ct);
            var checkedAgain = await AccessAsync(ctx, organizations, orgId, ct);
            if (checkedAgain != OrganizationAccessError.None)
            {
                await RecordDeniedAsync(recorder, ctx, orgId, session, null, ct);
                return Denied(ctx, checkedAgain);
            }
            await recorder.RecordAsync(orgId, session.UserId, session.SessionId,
                null, ctx.Items["CorrelationId"]?.ToString(), true, ct);
            return Results.Ok(new { entries = result.Entries.Select(Entry).ToArray(),
                page, limit, nextPage = result.NextPage });
        });

        group.MapGet("/{auditId}", async (string organizationId, string auditId,
            HttpContext ctx, OrganizationStore organizations, AuditReader reader,
            AuditAccessRecorder recorder, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Guid.TryParse(organizationId, out var orgId) || orgId == Guid.Empty ||
                !Guid.TryParse(auditId, out var id) || id == Guid.Empty)
                return Problem(ctx, 400, "invalid_request", "Invalid request");
            if (!AuthenticationSetup.IsAllowedTransport(ctx,
                    ctx.RequestServices.GetRequiredService<IConfiguration>()))
                return Problem(ctx, 403, "transport_required", "Secure transport required");
            var session = (CurrentSession)ctx.Items["IdentitySession"]!;
            var access = await AccessAsync(ctx, organizations, orgId, ct);
            if (access != OrganizationAccessError.None)
            {
                await RecordDeniedAsync(recorder, ctx, orgId, session, id, ct);
                return Denied(ctx, access);
            }
            var entry = await reader.GetAsync(orgId, session.UserId, session.SessionId, id, ct);
            var checkedAgain = await AccessAsync(ctx, organizations, orgId, ct);
            if (entry is null || checkedAgain != OrganizationAccessError.None)
            {
                await RecordDeniedAsync(recorder, ctx, orgId, session, id, ct);
                return Problem(ctx, 404, "resource_not_found", "Resource not found");
            }
            await recorder.RecordAsync(orgId, session.UserId, session.SessionId,
                id, ctx.Items["CorrelationId"]?.ToString(), true, ct);
            return Results.Ok(Entry(entry));
        });
    }
}
