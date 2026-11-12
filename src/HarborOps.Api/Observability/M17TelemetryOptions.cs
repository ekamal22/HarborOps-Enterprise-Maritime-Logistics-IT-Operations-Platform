using System.Net;
using Microsoft.Extensions.Configuration;

namespace HarborOps.Api.Observability;

// The exporter is deliberately limited to the owned collector or loopback.
// No public OTLP endpoint, cloud exporter, raw credentials or arbitrary URL is allowed.
public sealed record M17TelemetryOptions(bool Enabled, Uri Endpoint)
{
    public static M17TelemetryOptions FromConfiguration(IConfiguration configuration)
    {
        var fallback = new Uri("http://127.0.0.1:4317");
        if (!configuration.GetValue<bool>("M17:Enabled"))
            return new M17TelemetryOptions(false, fallback);

        var raw = configuration["M17:OtlpEndpoint"];
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp ||
            endpoint.Port != 4317 ||
            endpoint.UserInfo.Length != 0 ||
            endpoint.AbsolutePath != "/" ||
            endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0 ||
            !(string.Equals(endpoint.Host, "otelcol", StringComparison.OrdinalIgnoreCase) ||
              (IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address))))
            throw new InvalidOperationException(
                "M17 export requires the internal otelcol:4317 endpoint or loopback:4317");

        return new M17TelemetryOptions(true, endpoint);
    }
}
