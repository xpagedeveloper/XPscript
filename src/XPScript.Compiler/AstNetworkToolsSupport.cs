namespace XPScript.Compiler;

internal static class AstNetworkToolsSupport
{
    private static readonly IReadOnlyDictionary<string, string> RuntimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NetworkTools"] = "XPScriptNetworkTools",
        ["NetworkPingResult"] = "XPScriptNetworkPingResult",
        ["NetworkTraceHop"] = "XPScriptNetworkTraceHop",
        ["NetworkDnsResult"] = "XPScriptNetworkDnsResult",
        ["NetworkPortResult"] = "XPScriptNetworkPortResult",
        ["NetworkUdpResult"] = "XPScriptNetworkUdpResult",
        ["NetworkHttpResult"] = "XPScriptNetworkHttpResult",
        ["NetworkTlsResult"] = "XPScriptNetworkTlsResult",
        ["NetworkInterfaceInfo"] = "XPScriptNetworkInterfaceInfo",
        ["NetworkEndpointInfo"] = "XPScriptNetworkEndpointInfo",
        ["NetworkHostResult"] = "XPScriptNetworkHostResult",
        ["NetworkSubnetInfo"] = "XPScriptNetworkSubnetInfo",
        ["NetworkTlsVersionResult"] = "XPScriptNetworkTlsVersionResult",
        ["NetworkCertificateResult"] = "XPScriptNetworkCertificateResult",
        ["NetworkHttpHeadersResult"] = "XPScriptNetworkHttpHeadersResult",
        ["NetworkHttpSecurityHeadersResult"] = "XPScriptNetworkHttpSecurityHeadersResult",
        ["NetworkRedirectResult"] = "XPScriptNetworkRedirectResult",
        ["NetworkBannerResult"] = "XPScriptNetworkBannerResult",
        ["NetworkPortExposureInfo"] = "XPScriptNetworkPortExposureInfo"
    };

    internal static string? RuntimeType(string name) => RuntimeTypes.TryGetValue(name.Trim(), out var runtime) ? runtime : null;
}
