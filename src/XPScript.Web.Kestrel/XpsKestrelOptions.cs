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
    }
}
