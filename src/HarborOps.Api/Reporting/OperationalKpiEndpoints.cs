using System.Security.Cryptography;
using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.Reporting;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Reporting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Reporting;

public static class OperationalKpiEndpoints
{
    private sealed record ReportToken(Guid Id, Guid Organization, Guid Actor,
        Guid Session, string FiltersSha256, string PolicyVersion);

    private static CurrentSession Session(HttpContext context) =>
        (CurrentSession)context.Items["IdentitySession"]!;

    private static bool Id(string? input, out Guid id) =>
        Guid.TryParse(input, out id) && id != Guid.Empty;

    private static bool SingleQuery(HttpContext ctx, params string[] allowed) =>
        ctx.Request.Query.All(kv =>
            allowed.Contains(kv.Key, StringComparer.Ordinal) && kv.Value.Count == 1);

    private static string? Q(HttpContext ctx, string key) =>
        ctx.Request.Query.TryGetValue(key, out var v) ? v.ToString() : null;

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider) =>
        provider.CreateProtector("HarborOps.M13.ReportToken.v1").ToTimeLimitedDataProtector();

    private static string Token(IDataProtectionProvider provider,
        KpiReport report, CurrentSession session) =>
        Protector(provider).Protect(JsonSerializer.Serialize(
            new ReportToken(report.Id, report.OrganizationId,
                session.UserId, session.SessionId, report.FiltersSha256,
                OperationalKpiPolicy.Version)),
            OperationalKpiPolicy.ProvisionalReportLifetime);

    private static bool TryToken(HttpContext ctx, IDataProtectionProvider provider,
        Guid org, Guid report, out ReportToken? token)
    {
        token = null;
        var raw = ctx.Request.Headers["X-HarborOps-Report"].ToString();
        if (raw.Length is < 16 or > 4096) return false;
        try
        {
            token = JsonSerializer.Deserialize<ReportToken>(Protector(provider).Unprotect(raw));
            var session = Session(ctx);
            return token is not null && token.Id == report &&
                token.Organization == org && token.Actor == session.UserId &&
                token.Session == session.SessionId &&
                token.PolicyVersion == OperationalKpiPolicy.Version &&
                token.FiltersSha256.Length == 64;
        }
        catch (CryptographicException) { return false; }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    private static IResult Fail(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] },
            statusCode: status);

    private static IResult Access(HttpContext ctx, OrganizationAccessError error,
        string? failure = null) => error switch
    {
        OrganizationAccessError.SelectionRequired =>
            Fail(ctx, 409, "organization_selection_required", "Select an active organization"),
        OrganizationAccessError.NotFound =>
            Fail(ctx, 404, "resource_not_found", "Resource not found"),
        OrganizationAccessError.Revoked =>
            Fail(ctx, 403, "organization_access_revoked", "Organization access has changed"),
        OrganizationAccessError.Forbidden =>
            Fail(ctx, 403, "access_denied", "Access denied"),
        OrganizationAccessError.Conflict when failure == "report_capacity_exceeded" =>
            Fail(ctx, 413, failure, "Report is too large. Narrow the filters"),
        OrganizationAccessError.Conflict when failure == "report_tenant_capacity_exceeded" =>
            Fail(ctx, 429, failure, "The organization's reporting capacity is currently in use"),
        OrganizationAccessError.Conflict when failure == "report_user_capacity_exceeded" =>
            Fail(ctx, 429, failure, "You already hold the maximum number of active reports"),
        OrganizationAccessError.Conflict when failure == "report_in_progress" =>
            Fail(ctx, 429, failure, "A report is already being built"),
        OrganizationAccessError.Conflict when failure == "report_expired" =>
            Fail(ctx, 409, failure, "This report has expired. Refresh the overview"),
        OrganizationAccessError.Conflict =>
            Fail(ctx, 409, failure ?? "conflict", "Report could not be completed"),
        _ => Fail(ctx, 400, "invalid_request", "Invalid reporting request")
    };

    private static bool TryFilters(HttpContext ctx, TimeProvider clock,
        out OperationalKpiFilters? filters)
    {
        filters = null;
        if (!SingleQuery(ctx, "fromUtc", "beforeUtc", "defaultWindow", "bucket",
            "originPortId", "destinationPortId", "voyageId", "recordSource"))
            return false;
        var from = Q(ctx, "fromUtc");
        var before = Q(ctx, "beforeUtc");
        var defaultWindow = Q(ctx, "defaultWindow");
        var bucket = Q(ctx, "bucket") ?? "day";
        if (bucket is not ("day" or "week")) return false;
        DateTimeOffset fromUtc;
        DateTimeOffset beforeUtc;
        if (from is null && before is null)
        {
            if (defaultWindow is not (null or "last7CompleteDays")) return false;
            var window = OperationalKpiPolicy.DefaultWindow(clock.GetUtcNow());
            fromUtc = window.FromUtc;
            beforeUtc = window.BeforeUtc;
        }
        else
        {
            if (defaultWindow is not null ||
                !OperationalKpiPolicy.TryUtc(from, out fromUtc) ||
                !OperationalKpiPolicy.TryUtc(before, out beforeUtc))
                return false;
        }
        if (!OperationalKpiPolicy.ValidWindow(fromUtc, beforeUtc)) return false;
        Guid? origin = null, destination = null, voyage = null;
        if (Q(ctx, "originPortId") is { } originValue)
        {
            if (!Id(originValue, out var parsed)) return false;
            origin = parsed;
        }
        if (Q(ctx, "destinationPortId") is { } destinationValue)
        {
            if (!Id(destinationValue, out var parsed)) return false;
            destination = parsed;
        }
        if (Q(ctx, "voyageId") is { } voyageValue)
        {
            if (!Id(voyageValue, out var parsed)) return false;
            voyage = parsed;
        }
        var source = Q(ctx, "recordSource");
        if (source is not (null or "UserProvided" or "Synthetic")) return false;
        filters = new OperationalKpiFilters(fromUtc, beforeUtc,
            bucket, origin, destination, voyage, source);
        return true;
    }

    private static async Task<bool?> ReadGrantAsync(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is > 256) return null;
        using var ms = new MemoryStream();
        var buffer = new byte[257];
        int read;
        while ((read = await ctx.Request.Body.ReadAsync(buffer.AsMemory(),
            ctx.RequestAborted)) > 0)
        {
            if (ms.Length + read > 256) return null;
            ms.Write(buffer, 0, read);
        }
        try
        {
            using var doc = JsonDocument.Parse(ms.ToArray());
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var fields = doc.RootElement.EnumerateObject().ToArray();
            if (fields.Length != 1 || fields[0].Name != "enabled" ||
                fields[0].Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return null;
            return fields[0].Value.GetBoolean();
        }
        catch (JsonException) { return null; }
    }

    private static async Task<IResult> ExecuteAsync(HttpContext ctx, Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        { return Results.StatusCode(StatusCodes.Status499ClientClosedRequest); }
        catch (OperationCanceledException)
        { return Fail(ctx, 503, "report_timeout", "Report request exceeded its internal budget"); }
        catch (Npgsql.PostgresException e) when (e.SqlState == "42501")
        { return Fail(ctx, 403, "access_denied", "Access denied"); }
        catch (Npgsql.PostgresException e) when (e.SqlState == "23514")
        { return Fail(ctx, 409, "grant_conflict", "Grant eligibility changed"); }
        catch (Npgsql.PostgresException e) when (e.SqlState == "57014")
        { return Fail(ctx, 503, "report_timeout", "The query exceeded its reporting budget"); }
        catch (TimeoutException)
        { return Fail(ctx, 503, "report_timeout", "The reporting dependency timed out"); }
        catch (Npgsql.NpgsqlException e) when (e.IsTransient)
        { return Fail(ctx, 503, "report_dependency_unavailable", "The reporting database is unavailable"); }
    }

    public static void MapOperationalKpiEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/organizations/{organizationId}/analytics")
            .RequireAuthorization();

        routes.MapGet("/catalog", (string organizationId, HttpContext ctx,
            OperationalKpiStore store, CancellationToken ct) => ExecuteAsync(ctx, async () =>
        {
            if (!Id(organizationId, out var org) || !SingleQuery(ctx))
                return Fail(ctx, 400, "invalid_request", "Invalid catalog request");
            var result = await store.CanViewAsync(Session(ctx), org, ct);
            return result.Value is not null
                ? Results.Ok(new { policyVersion = OperationalKpiPolicy.Version,
                    definitions = OperationalKpiPolicy.Definitions,
                    maximumWindowDays = OperationalKpiPolicy.MaximumDays })
                : Access(ctx, result.Error);
        })).RequireRateLimiting("m13-read");

        routes.MapGet("/overview", (string organizationId, HttpContext ctx,
            OperationalKpiStore store, IDataProtectionProvider provider, TimeProvider clock,
            CancellationToken ct) => ExecuteAsync(ctx, async () =>
        {
            if (!Id(organizationId, out var org) ||
                !TryFilters(ctx, clock, out var filters) || filters is null)
                return Fail(ctx, 400, "invalid_filters", "Invalid UTC reporting filters");
            var result = await store.BuildAsync(Session(ctx), org, filters, ct);
            if (result.Value is not { } report) return Access(ctx, result.Error, result.FailureCode);
            return Results.Ok(new {
                policyVersion = OperationalKpiPolicy.Version,
                reportId = report.Id,
                reportToken = Token(provider, report, Session(ctx)),
                report.FromUtc, report.BeforeUtc, report.EvaluatedAtUtc,
                report.ExpiresAtUtc, report.MemberCount,
                filters.Bucket, filters.OriginPortId, filters.DestinationPortId,
                filters.VoyageId, filters.SourceKind,
                counts = report.Counts
            });
        })).RequireRateLimiting("m13-report");

        routes.MapGet("/reports/{reportId}/members",
            (string organizationId, string reportId, HttpContext ctx,
             OperationalKpiStore store, IDataProtectionProvider provider,
             CancellationToken ct) => ExecuteAsync(ctx, async () =>
        {
            if (!Id(organizationId, out var org) || !Id(reportId, out var report) ||
                !SingleQuery(ctx, "metricCode", "dimension", "bucketStartUtc", "after", "limit"))
                return Fail(ctx, 400, "invalid_request", "Invalid member request");
            if (!TryToken(ctx, provider, org, report, out var protectedReport))
                return Fail(ctx, 409, "report_expired", "Report token is invalid or expired");
            var metric = Q(ctx, "metricCode") ?? "";
            var dimension = Q(ctx, "dimension") ?? "";
            if (!OperationalKpiPolicy.ValidMetric(metric) ||
                !OperationalKpiPolicy.ValidDimension(metric, dimension))
                return Fail(ctx, 400, "invalid_metric", "Invalid KPI metric or dimension");
            DateTimeOffset? bucket = null;
            if (Q(ctx, "bucketStartUtc") is { } supplied)
            {
                if (metric != "K12" || !OperationalKpiPolicy.TryUtc(supplied, out var parsed))
                    return Fail(ctx, 400, "invalid_bucket", "Invalid UTC bucket");
                bucket = parsed;
            }
            Guid? after = null;
            if (Q(ctx, "after") is { } last)
            {
                if (!Id(last, out var parsed))
                    return Fail(ctx, 400, "invalid_cursor", "Invalid member cursor");
                after = parsed;
            }
            if (!int.TryParse(Q(ctx, "limit") ?? "50", out var limit) ||
                limit is < 1 or > OperationalKpiPolicy.MaximumMemberPage)
                return Fail(ctx, 400, "invalid_limit", "Invalid page size");
            var result = await store.MembersAsync(Session(ctx), org, report,
                protectedReport!.FiltersSha256, metric, dimension, bucket, after, limit, ct);
            return result.Value is { } page
                ? Results.Ok(new { items = page.Items, nextAfter = page.NextAfter,
                    metricCode = metric, dimension, bucketStartUtc = bucket,
                    reportId = report, policyVersion = OperationalKpiPolicy.Version })
                : Access(ctx, result.Error, result.FailureCode);
        })).RequireRateLimiting("m13-read");

        routes.MapGet("/grants", (string organizationId, HttpContext ctx,
            OperationalKpiStore store, CancellationToken ct) => ExecuteAsync(ctx, async () =>
        {
            if (!Id(organizationId, out var org) || !SingleQuery(ctx))
                return Fail(ctx, 400, "invalid_request", "Invalid grant request");
            var result = await store.GrantsAsync(Session(ctx), org, ct);
            return result.Value is { } grants ? Results.Ok(new { grants }) :
                Access(ctx, result.Error, result.FailureCode);
        })).RequireRateLimiting("m13-read");

        routes.MapPut("/grants/{targetId}",
            (string organizationId, string targetId, HttpContext ctx,
             OperationalKpiStore store, IAntiforgery antiforgery, CancellationToken ct) =>
            ExecuteAsync(ctx, async () =>
        {
            if (!Id(organizationId, out var org) || !Id(targetId, out var target) ||
                !SingleQuery(ctx) ||
                !AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration) ||
                !await AuthenticationSetup.ValidMutationAsync(ctx, antiforgery))
                return Fail(ctx, 403, "mutation_denied", "Grant update denied");
            var enabled = await ReadGrantAsync(ctx);
            if (!enabled.HasValue)
                return Fail(ctx, 400, "invalid_request", "Use an enabled boolean");
            var result = await store.SetGrantAsync(Session(ctx), org, target, enabled.Value, ct);
            return result.Value is { } version
                ? Results.Ok(new { targetUserId = target, enabled, version })
                : Access(ctx, result.Error, result.FailureCode);
        })).RequireRateLimiting("m13-grants");
    }
}