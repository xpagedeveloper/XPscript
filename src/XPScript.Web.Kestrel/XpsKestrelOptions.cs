using System.Net;

namespace XPScript.Web.Kestrel;

public sealed class XpsKestrelOptions
{
    public IPAddress Address { get; init; } = IPAddress.Loopback;
    public int Port { get; init; } = 8080;
    public long MaxRequestBodySize { get; init; } = 1 * 1024 * 1024;
    public long MaxConcurrentConnections { get; init; } = 256;
    public TimeSpan RequestHeadersTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyList<string> AllowedHosts { get; init; } = ["localhost", "127.0.0.1", "[::1]"];
    public IReadOnlyList<IPAddress> KnownProxies { get; init; } = [];
    public bool EnableHealthEndpoint { get; init; }
    public bool EnableMetricsEndpoint { get; init; }
    public bool OperationalEndpointsLocalOnly { get; init; } = true;
    public string HealthPath { get; init; } = "/_xps/health";
    public string MetricsPath { get; init; } = "/_xps/metrics";

    public void Validate()
    {
        if (Port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (MaxRequestBodySize is < 0 or > 1024L * 1024L * 1024L)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBodySize), "Request body limit must be between 0 and 1 GiB.");
        if (MaxConcurrentConnections is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConnections));
        if (RequestHeadersTimeout <= TimeSpan.Zero || RequestHeadersTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(RequestHeadersTimeout));
        if (KeepAliveTimeout <= TimeSpan.Zero || KeepAliveTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(KeepAliveTimeout));
        if (AllowedHosts.Count == 0) throw new ArgumentException("At least one allowed host must be configured.", nameof(AllowedHosts));
        foreach (var host in AllowedHosts)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Contains('\r') || host.Contains('\n'))
                throw new ArgumentException("Allowed host contains an invalid value.", nameof(AllowedHosts));
        }

        ValidateOperationalPath(HealthPath, nameof(HealthPath));
        ValidateOperationalPath(MetricsPath, nameof(MetricsPath));
        if (string.Equals(HealthPath, MetricsPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("HealthPath and MetricsPath must be different.");
    }

    private static void ValidateOperationalPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/', StringComparison.Ordinal) ||
            path.Contains('?') || path.Contains('#') || path.Contains('\r') || path.Contains('\n'))
            throw new ArgumentException("Operational endpoint paths must be absolute URL paths without query, fragment or line breaks.", name);
        if (path.Length > 512) throw new ArgumentOutOfRangeException(name, "Operational endpoint paths cannot exceed 512 characters.");
    }
}
