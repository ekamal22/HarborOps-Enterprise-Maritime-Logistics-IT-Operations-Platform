using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using HarborOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Api.Authentication;

public sealed class IdentityDependencyException : Exception { }

public static class AuthenticationSetup
{
    public static IServiceCollection AddHarborOpsAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        var localHttp = configuration.GetValue<bool>("Identity:AllowInsecureLoopbackHttp");
        var keyPath = configuration["Identity:KeyRingPath"];
        if (string.IsNullOrWhiteSpace(keyPath))
            throw new InvalidOperationException("Identity:KeyRingPath is required");
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("HarborOps")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        if (!localHttp)
        {
            var certPath = configuration["Identity:ProtectionCertificatePath"];
            var certPassword = configuration["Identity:ProtectionCertificatePassword"];
            if (string.IsNullOrWhiteSpace(certPath) || string.IsNullOrWhiteSpace(certPassword))
                throw new InvalidOperationException("Non-local identity service requires a key protection certificate");
            dataProtection.ProtectKeysWithCertificate(
                X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword,
                    X509KeyStorageFlags.EphemeralKeySet));
        }
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-HarborOps-CSRF";
            options.Cookie.Name = "HarborOps.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = localHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "HarborOps.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = localHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.Path = "/";
                options.LoginPath = "/api/v1/auth/login";
                options.SlidingExpiration = false;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.Events.OnValidatePrincipal = async context =>
                {
                    var principal = context.Principal;
                    var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var session = principal?.FindFirstValue("sid");
                    var version = principal?.FindFirstValue("sv");
                    if (!Guid.TryParse(id, out var userId) || !Guid.TryParse(session, out var sessionId) ||
                        !long.TryParse(version, out var securityVersion))
                    {
                        context.RejectPrincipal();
                        return;
                    }
                    try
                    {
                        var store = context.HttpContext.RequestServices.GetRequiredService<IdentityStore>();
                        var current = await store.ValidateAsync(userId, sessionId, securityVersion,
                            context.HttpContext.RequestAborted);
                        if (current is null) context.RejectPrincipal();
                        else context.HttpContext.Items["IdentitySession"] = current;
                    }
                    catch (Exception ex) when (ex is DbUpdateException or Npgsql.NpgsqlException or TimeoutException ||
                        ex.InnerException is Npgsql.NpgsqlException)
                    {
                        throw new IdentityDependencyException();
                    }
                };
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return context.Response.WriteAsJsonAsync(new { code = "authentication_required",
                        message = "Authentication required",
                        correlationId = context.HttpContext.Items["CorrelationId"] });
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return context.Response.WriteAsJsonAsync(new { code = "access_denied",
                        message = "Access denied",
                        correlationId = context.HttpContext.Items["CorrelationId"] });
                };
            });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login-ip", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(5), PermitLimit = 20,
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        // One partition per authenticated user; bound audit reads and enumeration probes.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("audit-read", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1), PermitLimit = 30,
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        // M10 write-heavy projection paths are scoped per authenticated principal.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("m10-mutations", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1), PermitLimit = 10,
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("m12-export", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1), PermitLimit = 10,
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        // Reporting builds are more expensive than read-only pagination.
        foreach (var (name, limit) in new[] {
            ("m13-report", 6), ("m13-read", 60), ("m13-grants", 10) })
        {
            services.AddRateLimiter(options =>
                options.AddPolicy(name, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions {
                            Window = TimeSpan.FromMinutes(1), PermitLimit = limit,
                            QueueLimit = 0, AutoReplenishment = true })));
        }
        services.AddSingleton<IdentityAttemptLimiter>();
        return services;
    }

    private static bool LocalHost(HttpContext context) =>
        context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Host.Host == "127.0.0.1" || context.Request.Host.Host == "[::1]";

    public static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);

    private static bool IsTrustedLocalPeer(HttpContext context, IConfiguration configuration)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null) return false;
        var configured = configuration["Identity:TrustedLocalHttpPeers"] ?? "";
        foreach (var item in configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (IPAddress.TryParse(item, out var peer) &&
                peer.MapToIPv6().Equals(remote.MapToIPv6()))
                return true;
        }
        return false;
    }

    public static bool IsAllowedTransport(HttpContext context, IConfiguration configuration) =>
        context.Request.IsHttps ||
        (configuration.GetValue<bool>("Identity:AllowInsecureLoopbackHttp") &&
         LocalHost(context) &&
         (IsLoopback(context) || IsTrustedLocalPeer(context, configuration)));

    public static bool IsSameOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var site = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0 && site != "same-origin" && site != "none") return false;
        if (origin.Length == 0 || !Uri.TryCreate(origin, UriKind.Absolute, out var url)) return false;
        return string.Equals(url.GetLeftPart(UriPartial.Authority),
            context.Request.Scheme + "://" + context.Request.Host, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> ValidMutationAsync(HttpContext context, IAntiforgery antiforgery) =>
        IsSameOrigin(context) && await antiforgery.IsRequestValidAsync(context);
}
