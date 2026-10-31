using System.Security.Claims;
using HarborOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Authentication;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record DisableRequest(string? Password);

public static class AuthenticationEndpoints
{
    private static IResult Failure(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] }, statusCode: status);

    private static CurrentSession Session(HttpContext context) =>
        (CurrentSession)context.Items["IdentitySession"]!;

    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth");
        auth.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration))
                return Failure(ctx, 400, "invalid_transport", "Secure transport required");
            var site = ctx.Request.Headers["Sec-Fetch-Site"].ToString();
            if ((site.Length > 0 && site is not ("same-origin" or "none")) ||
                (ctx.Request.Headers.Origin.Count > 0 &&
                 !AuthenticationSetup.IsSameOrigin(ctx)))
                return Failure(ctx, 403, "csrf_invalid", "Request origin rejected");
            return Results.Ok(new { token = anti.GetAndStoreTokens(ctx).RequestToken });
        });

        auth.MapPost("/login", async (LoginRequest input, HttpContext ctx, IAntiforgery anti,
            IdentityStore store, IdentityAttemptLimiter limiter, CancellationToken ct) =>
        {
            if (!AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration) ||
                !await AuthenticationSetup.ValidMutationAsync(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            var email = input.Email ?? "";
            var password = input.Password ?? "";
            if (email.Length > 320 || password.Length > 1024)
                return Failure(ctx, 401, "invalid_credentials", "Unable to sign in with these credentials");
            if (!limiter.Permit(email.Trim().ToUpperInvariant(),
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
            {
                ctx.Response.Headers.RetryAfter = "300";
                return Failure(ctx, 429, "rate_limited", "Too many sign-in attempts");
            }
            var result = await store.LoginAsync(email, password, ctx.Items["CorrelationId"]!.ToString()!, ct);
            if (result is null)
                return Failure(ctx, 401, "invalid_credentials", "Unable to sign in with these credentials");
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()));
            identity.AddClaim(new Claim("sid", result.SessionId.ToString()));
            identity.AddClaim(new Claim("sv", result.SecurityVersion.ToString()));
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = result.AbsoluteExpiry });
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { user = new { id = result.UserId, email = result.Email,
                displayName = result.DisplayName }, session = new { expiresAtUtc = result.AbsoluteExpiry } });
        }).RequireRateLimiting("login-ip");

        auth.MapGet("/session", (HttpContext ctx) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var session = Session(ctx);
            return Results.Ok(new { user = new { id = session.UserId, email = session.Email,
                displayName = session.DisplayName },
                session = new { expiresAtUtc = session.AbsoluteExpiry } });
        }).RequireAuthorization();

        auth.MapGet("/session/guard", (HttpContext ctx) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { authenticated = true });
        }).RequireAuthorization();

        auth.MapPost("/logout", async (HttpContext ctx, IAntiforgery anti, IdentityStore store,
            CancellationToken ct) =>
        {
            if (!await AuthenticationSetup.ValidMutationAsync(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            var session = Session(ctx);
            await store.RevokeCurrentAsync(session.SessionId, session.UserId,
                ctx.Items["CorrelationId"]!.ToString()!, ct);
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/logout-all", async (HttpContext ctx, IAntiforgery anti, IdentityStore store,
            CancellationToken ct) =>
        {
            if (!await AuthenticationSetup.ValidMutationAsync(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            var session = Session(ctx);
            await store.RevokeAllAsync(session.UserId, "logout_all",
                ctx.Items["CorrelationId"]!.ToString()!, ct);
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/account/disable-self", async (DisableRequest input,
            HttpContext ctx, IAntiforgery anti, IdentityStore store, CancellationToken ct) =>
        {
            if (!await AuthenticationSetup.ValidMutationAsync(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            var session = Session(ctx);
            if (input.Password is null || input.Password.Length > 1024)
                return Failure(ctx, 403, "confirmation_failed", "Password confirmation failed");
            try
            {
                if (!await store.DisableSelfAsync(session.UserId, input.Password,
                        ctx.Items["CorrelationId"]!.ToString()!, ct))
                    return Failure(ctx, 403, "confirmation_failed", "Password confirmation failed");
            }
            catch (LastActiveAdministratorException)
            {
                return Failure(ctx, 409, "last_administrator",
                    "Assign another active administrator before disabling this account");
            }
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
