using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace XPScript.Web.Kestrel;

public sealed class XpsKestrelOptions
{
    public IPAddress Address { get; init; } = IPAddress.Loopback;
    public int Port { get; init; } = 8080;
    public string? HttpsCertificatePath { get; init; }
    public string? HttpsCertificatePassword { get; init; }
    public HttpProtocols Protocols { get; init; } = HttpProtocols.Http1AndHttp2;
    public long MaxRequestBodySize { get; init; } = 1 * 1024 * 1024;
    public long MaxConcurrentConnections { get; init; } = 256;
    public int MaxRequestLineSize { get; init; } = 8 * 1024;
    public int MaxRequestHeadersTotalSize { get; init; } = 32 * 1024;
    public TimeSpan RequestHeadersTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public double? MinRequestBodyDataRateBytesPerSecond { get; init; } = 240;
    public TimeSpan MinRequestBodyDataRateGracePeriod { get; init; } = TimeSpan.FromSeconds(5);
    public double? MinResponseDataRateBytesPerSecond { get; init; } = 240;
    public TimeSpan MinResponseDataRateGracePeriod { get; init; } = TimeSpan.FromSeconds(5);
    public IReadOnlyList<string> AllowedHosts { get; init; } = ["localhost", "127.0.0.1", "[::1]"];
    public IReadOnlyList<IPAddress> KnownProxies { get; init; } = [];
    public bool EnableHealthEndpoint { get; init; }
    public bool EnableMetricsEndpoint { get; init; }
    public bool OperationalEndpointsLocalOnly { get; init; } = true;
    public string HealthPath { get; init; } = "/_xps/health";
    public string MetricsPath { get; init; } = "/_xps/metrics";

    public bool HttpsEnabled => !string.IsNullOrWhiteSpace(HttpsCertificatePath);

    public void Validate()
    {
        if (Port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (MaxRequestBodySize is < 0 or > 1024L * 1024L * 1024L)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBodySize), "Request body limit must be between 0 and 1 GiB.");
        if (MaxConcurrentConnections is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConnections));
        if (MaxRequestLineSize is < 1024 or > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestLineSize), "Request-line limit must be between 1 KiB and 1 MiB.");
        if (MaxRequestHeadersTotalSize is < 1024 or > 4 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestHeadersTotalSize), "Request-header limit must be between 1 KiB and 4 MiB.");
        if (RequestHeadersTimeout <= TimeSpan.Zero || RequestHeadersTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(RequestHeadersTimeout));
        if (KeepAliveTimeout <= TimeSpan.Zero || KeepAliveTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(KeepAliveTimeout));
        ValidateDataRate(MinRequestBodyDataRateBytesPerSecond, MinRequestBodyDataRateGracePeriod, nameof(MinRequestBodyDataRateBytesPerSecond));
        ValidateDataRate(MinResponseDataRateBytesPerSecond, MinResponseDataRateGracePeriod, nameof(MinResponseDataRateBytesPerSecond));
        if (Protocols is not (HttpProtocols.Http1 or HttpProtocols.Http2 or HttpProtocols.Http1AndHttp2))
            throw new ArgumentOutOfRangeException(nameof(Protocols), "Initial XPScript Kestrel hosting supports HTTP/1.1, HTTP/2 or HTTP/1.1+HTTP/2. HTTP/3 requires a separate QUIC deployment contract.");

        if (HttpsEnabled)
        {
            string fullPath;
            try { fullPath = Path.GetFullPath(HttpsCertificatePath!); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ArgumentException("HTTPS certificate path is invalid.", nameof(HttpsCertificatePath), ex);
            }
            if (!File.Exists(fullPath)) throw new FileNotFoundException("HTTPS certificate file was not found.", fullPath);
        }
        else if (HttpsCertificatePassword is not null)
        {
            throw new ArgumentException("HttpsCertificatePassword requires HttpsCertificatePath.", nameof(HttpsCertificatePassword));
        }

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

    private static void ValidateDataRate(double? bytesPerSecond, TimeSpan gracePeriod, string name)
    {
        if (bytesPerSecond is not null && (double.IsNaN(bytesPerSecond.Value) || double.IsInfinity(bytesPerSecond.Value) || bytesPerSecond <= 0 || bytesPerSecond > 1024d * 1024d * 1024d))
            throw new ArgumentOutOfRangeException(name, "Minimum data rate must be null or between 0 and 1 GiB/s.");
        if (gracePeriod < TimeSpan.FromSeconds(1) || gracePeriod > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(name, "Minimum data-rate grace period must be between 1 second and 10 minutes.");
    }

    private static void ValidateOperationalPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains('?') || path.Contains('#') || path.Contains('\r') || path.Contains('\n'))
            throw new ArgumentException("Operational endpoint paths must be absolute URL paths without query, fragment or line breaks.", name);
        if (path.Length > 512) throw new ArgumentOutOfRangeException(name, "Operational endpoint paths cannot exceed 512 characters.");
    }
}
