using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.ContractTests;

/// <summary>
/// Exercises the real middleware pipeline (host filtering, forwarded headers, transport check) with
/// an explicit socket peer, because in-memory test requests otherwise carry no remote address.
/// </summary>
public sealed class TransportHardeningHttpTests
{
    private const string TrustedProxy = "172.30.254.10";
    private const string TrustedGateway = "172.30.254.1";
    private const string Attacker = "198.51.100.5";

    private sealed class PeerFactory(string? peer, bool allowLocalHttp = true, bool configureProxy = false,
        bool protectionCertificate = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:HarborOps",
                "Host=127.0.0.1;Port=1;Database=harborops_test;Username=contract;Password=contract-only;Timeout=2");
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", allowLocalHttp ? "true" : "false");
            builder.UseSetting("Identity:TrustedLocalHttpPeers", TrustedGateway + "," + TrustedProxy);
            builder.UseSetting("Identity:KeyRingPath", Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
            if (configureProxy) builder.UseSetting("ReverseProxy:KnownProxies:0", TrustedProxy);
            if (protectionCertificate)
            {
                // Hosted mode requires a key protection certificate; generate a throwaway one per run.
                var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                var path = Path.Combine(Path.GetTempPath(), "harborops-contract-" + Guid.NewGuid().ToString("N") + ".pfx");
                using var key = RSA.Create(2048);
                var request = new CertificateRequest("CN=harborops-contract-only", key,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
                builder.UseSetting("Identity:ProtectionCertificatePath", path);
                builder.UseSetting("Identity:ProtectionCertificatePassword", password);
            }
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PeerStartupFilter(peer)));
        }
    }

    private sealed class PeerStartupFilter(string? peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, proceed) =>
            {
                context.Connection.RemoteIpAddress = peer is null ? null : IPAddress.Parse(peer);
                await proceed();
            });
            next(app);
        };
    }

    private static async Task<(HttpStatusCode Status, string? Code)> CsrfAsync(
        PeerFactory factory, string host, string scheme = "http",
        IDictionary<string, string>? headers = null)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(scheme + "://localhost")
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Host = host;
        foreach (var header in headers ?? new Dictionary<string, string>())
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        using var response = await client.SendAsync(request);
        string? code = null;
        var body = await response.Content.ReadAsStringAsync();
        if (body.Length > 0 && body[0] == '{')
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("code", out var value)) code = value.GetString();
        }
        return (response.StatusCode, code);
    }

    [Theory]
    [InlineData("127.0.0.1", "localhost")]
    [InlineData("::1", "localhost")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public async Task LoopbackPeerIsAcceptedOverLocalHttp(string peer, string host)
    {
        using var factory = new PeerFactory(peer);
        var result = await CsrfAsync(factory, host);
        Assert.Equal(HttpStatusCode.OK, result.Status);
    }

    [Fact]
    public async Task MissingRemoteAddressIsRejectedByProductionTransportValidation()
    {
        using var factory = new PeerFactory(null);
        var result = await CsrfAsync(factory, "localhost");
        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal("invalid_transport", result.Code);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public async Task RemoteAttackerWithForgedLocalHostIsRejected(string host)
    {
        using var factory = new PeerFactory(Attacker);
        var result = await CsrfAsync(factory, host);
        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal("invalid_transport", result.Code);
    }

    [Theory]
    [InlineData("172.30.254.99", HttpStatusCode.BadRequest)]
    [InlineData(TrustedProxy, HttpStatusCode.OK)]
    [InlineData(TrustedGateway, HttpStatusCode.OK)]
    public async Task OnlyConfiguredDockerPeersMayUseLocalHttp(string peer, HttpStatusCode expected)
    {
        using var factory = new PeerFactory(peer);
        var result = await CsrfAsync(factory, "localhost");
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task ForwardedClientAddressFromTrustedProxyIsEvaluatedAsThePeer()
    {
        using var factory = new PeerFactory(TrustedProxy, configureProxy: true);

        var external = await CsrfAsync(factory, "localhost",
            headers: new Dictionary<string, string> { ["X-Forwarded-For"] = Attacker });
        Assert.Equal(HttpStatusCode.BadRequest, external.Status);
        Assert.Equal("invalid_transport", external.Code);

        var gateway = await CsrfAsync(factory, "localhost",
            headers: new Dictionary<string, string> { ["X-Forwarded-For"] = TrustedGateway });
        Assert.Equal(HttpStatusCode.OK, gateway.Status);
    }

    [Fact]
    public async Task ForwardedHeadersFromAnUntrustedPeerAreIgnored()
    {
        using var factory = new PeerFactory(Attacker, configureProxy: true);

        var spoofedAddress = await CsrfAsync(factory, "localhost",
            headers: new Dictionary<string, string> { ["X-Forwarded-For"] = "127.0.0.1" });
        Assert.Equal(HttpStatusCode.BadRequest, spoofedAddress.Status);

        var spoofedScheme = await CsrfAsync(factory, "localhost",
            headers: new Dictionary<string, string> { ["X-Forwarded-Proto"] = "https" });
        Assert.Equal(HttpStatusCode.BadRequest, spoofedScheme.Status);
    }

    [Fact]
    public async Task ForwardedHttpsFromTrustedProxyIsHonoured()
    {
        using var factory = new PeerFactory(TrustedProxy, configureProxy: true);
        var result = await CsrfAsync(factory, "localhost", headers: new Dictionary<string, string>
        {
            ["X-Forwarded-For"] = Attacker,
            ["X-Forwarded-Proto"] = "https"
        });
        Assert.Equal(HttpStatusCode.OK, result.Status);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("localhost.evil.example")]
    [InlineData("harborops.example")]
    public async Task UnexpectedHostIsRejectedBeforeTheApplication(string host)
    {
        using var factory = new PeerFactory("127.0.0.1");
        var result = await CsrfAsync(factory, host);
        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.NotEqual("invalid_transport", result.Code);
    }

    [Fact]
    public async Task PlainHttpIsRejectedAndHttpsAcceptedWhenLocalHttpExceptionIsDisabled()
    {
        using var factory = new PeerFactory("127.0.0.1", allowLocalHttp: false, protectionCertificate: true);

        var plain = await CsrfAsync(factory, "localhost");
        Assert.Equal(HttpStatusCode.BadRequest, plain.Status);
        Assert.Equal("invalid_transport", plain.Code);

        var secure = await CsrfAsync(factory, "localhost", scheme: "https");
        Assert.Equal(HttpStatusCode.OK, secure.Status);
    }

    [Fact]
    public void HostedModeWithoutKeyProtectionCertificateFailsToStart()
    {
        using var factory = new PeerFactory("127.0.0.1", allowLocalHttp: false);
        var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("key protection certificate", failure.Message);
    }

    [Fact]
    public async Task HostedModeIssuesSecureHttpOnlyAntiforgeryCookie()
    {
        using var factory = new PeerFactory("127.0.0.1", allowLocalHttp: false, protectionCertificate: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("HarborOps.Antiforgery=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
