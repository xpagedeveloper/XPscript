using System.Net;

namespace XPScript.Web.FastCgi;

public sealed class XpsFastCgiOptions
{
    public IPAddress Address { get; init; } = IPAddress.Loopback;
    public int Port { get; init; } = 9000;
    public int MaxConcurrentConnections { get; init; } = 128;
    public int MaxParamsBytes { get; init; } = 64 * 1024;
    public int MaxParamCount { get; init; } = 256;
    public int MaxParamNameBytes { get; init; } = 1024;
    public int MaxParamValueBytes { get; init; } = 16 * 1024;
    public int MaxRequestBodyBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxHeaderCount { get; init; } = 128;
    public int MaxHeaderValueBytes { get; init; } = 16 * 1024;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Address);
        if (Port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (MaxConcurrentConnections is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConnections));
        if (MaxParamsBytes is < 1024 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxParamsBytes));
        if (MaxParamCount is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaxParamCount));
        if (MaxParamNameBytes is < 1 or > MaxParamsBytes) throw new ArgumentOutOfRangeException(nameof(MaxParamNameBytes));
        if (MaxParamValueBytes is < 1 or > MaxParamsBytes) throw new ArgumentOutOfRangeException(nameof(MaxParamValueBytes));
        if (MaxRequestBodyBytes is < 0 or > 256 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxRequestBodyBytes));
        if (MaxHeaderCount is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(MaxHeaderCount));
        if (MaxHeaderValueBytes is < 1 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxHeaderValueBytes));
    }
}
