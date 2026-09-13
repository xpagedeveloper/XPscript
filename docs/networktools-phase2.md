# NetworkTools Phase 2

Phase 2 extends `NetworkTools` with security-assessment and network-inventory operations implemented only with the .NET 10 base class library. No third-party NuGet package and no operating-system command is used.

The Phase 1 API in `networktools.md` remains unchanged.

## Scope and safeguards

Phase 2 is designed for diagnostics and authorized security assessment. `CheckTcpPorts` accepts only an explicit array of ports, allows at most 64 ports per call and caps concurrency at 16. `GrabBanner` reads at most 16 KiB and always uses a timeout. The API does not implement arbitrary port ranges, raw packet injection, credential attacks or packet capture.

## ResolveHost

```xpscript
Dim net As New NetworkTools
Dim result As NetworkHostResult
Set result = net.ResolveHost("server.example.com")

Print result.HostName
Forall address In result.IPv4Addresses
    Print address
End Forall
```

`NetworkHostResult` exposes `Query`, `HostName`, `IPv4Addresses`, `IPv6Addresses` and `Error`.

Implementation: `System.Net.Dns.GetHostEntry`.

## CheckTcpPorts

```xpscript
Dim ports As Variant
ports = Array(22, 80, 443, 445)

Forall result In net.CheckTcpPorts("server01", ports, 1000, 4)
    Print result.Port & ": " & result.Open
End Forall
```

Signatures:

```text
CheckTcpPorts(host, ports)
CheckTcpPorts(host, ports, timeoutMilliseconds)
CheckTcpPorts(host, ports, timeoutMilliseconds, concurrency)
```

The function returns the existing `NetworkPortResult` objects. Port order is preserved. Duplicate requested ports are removed. A maximum of 64 explicit ports and 16 concurrent connection attempts is enforced.

## IP address classification

```text
IsPrivateAddress(address)
IsLoopbackAddress(address)
IsLinkLocalAddress(address)
IsMulticastAddress(address)
GetAddressFamily(address)
```

`IsPrivateAddress` recognizes RFC1918 IPv4 ranges and IPv6 Unique Local Addresses (`fc00::/7`). Link-local checks recognize `169.254.0.0/16` and `fe80::/10`. Multicast checks recognize IPv4 `224.0.0.0/4` and IPv6 `ff00::/8`.

These methods require literal IP addresses; they do not resolve host names implicitly.

## CIDR and subnet operations

```xpscript
If net.IsInSubnet("10.20.30.40", "10.20.0.0/16") Then
    Print "inside subnet"
End If

Dim subnet As NetworkSubnetInfo
Set subnet = net.ParseSubnet("192.168.10.42/24")
Print subnet.NetworkAddress
Print subnet.BroadcastAddress
Print subnet.SubnetMask
Print subnet.AddressCount
Print subnet.Contains("192.168.10.200")
```

`NetworkSubnetInfo` exposes:

- `Cidr`
- `NetworkAddress`
- `BroadcastAddress` (IPv4 only)
- `PrefixLength`
- `SubnetMask` (IPv4 dotted-decimal only)
- `FirstAddress`
- `LastAddress`
- `AddressCount` as a String so IPv6 counts cannot overflow XPscript numeric types
- `AddressBits`
- `Contains(address)`

Both IPv4 and IPv6 CIDR prefixes are supported.

## TLS version assessment

```xpscript
Forall result In net.CheckTlsVersions("server.example.com", 443, 3000)
    Print result.Version & ": " & result.Supported
End Forall
```

The runtime attempts isolated TLS 1.0, 1.1, 1.2 and 1.3 handshakes with `SslStream`. `NetworkTlsVersionResult` exposes `Version`, `Supported`, `NegotiatedProtocol` and `Error`.

A failed TLS version can mean that the remote server rejected it or that local operating-system/.NET crypto policy disabled that version. The result must therefore be interpreted as "usable from this runtime to this endpoint", not as a packet-level proof of every server capability.

## Certificate assessment

```xpscript
Dim cert As NetworkCertificateResult
Set cert = net.CheckCertificate("server.example.com", 443, 5000)

Print cert.HostNameValid
Print cert.ChainValid
Print cert.DaysRemaining
Print cert.PublicKeyAlgorithm
Print cert.PublicKeySize
```

`NetworkCertificateResult` includes:

- subject, issuer, thumbprint and serial number
- signature algorithm
- public-key algorithm and key size
- validity dates and days remaining
- `Expired` and `NotYetValid`
- `HostNameValid`
- `ChainValid`
- DNS names from Subject Alternative Name when available
- chain errors
- TLS policy errors

The TLS diagnostic callback allows collection of an invalid certificate. Applications making trust decisions must check `HostNameValid`, `ChainValid`, validity dates and policy errors rather than only `Success`.

Implementation: `SslStream`, `X509Certificate2` and `X509Chain`.

## HTTP response headers

```xpscript
Dim headers As NetworkHttpHeadersResult
Set headers = net.GetHttpHeaders("https://example.com")
Print headers.StatusCode
Print headers.GetHeader("Content-Type")
```

Properties: `Url`, `FinalUrl`, `StatusCode`, `Headers`, `Error` and `GetHeader(name)`.

The request uses HTTP `HEAD` and follows normal redirects for this operation.

## HTTP security headers

```xpscript
Dim security As NetworkHttpSecurityHeadersResult
Set security = net.CheckHttpSecurityHeaders("https://example.com")

Print security.HasHsts
Print security.HstsMaxAge
Print security.HasContentSecurityPolicy
```

The result exposes presence and raw values for:

- `Strict-Transport-Security` plus parsed `max-age`
- `Content-Security-Policy`
- `X-Content-Type-Options` (`HasXContentTypeOptions` is true only for `nosniff`)
- `Referrer-Policy`
- `Permissions-Policy`
- `Cross-Origin-Opener-Policy`
- `Cross-Origin-Resource-Policy`

The API intentionally exposes facts instead of producing a generic security score. Policy requirements differ between applications.

## Redirect chain

```xpscript
Forall redirect In net.CheckRedirects("http://example.com", 5000, 10)
    Print redirect.StatusCode & " " & redirect.Location
End Forall
```

`NetworkRedirectResult` exposes `Url`, `StatusCode`, `Location`, `IsRedirect` and `Error`. Redirects are followed manually with automatic redirects disabled. The maximum accepted `maxRedirects` is 20.

## Banner grabbing

```xpscript
Dim banner As NetworkBannerResult
Set banner = net.GrabBanner("mail.example.com", 25, 2000, 4096)
Print banner.Banner
```

This operation only connects to a TCP service and reads bytes the service sends without authentication or protocol exploitation. It does not send protocol commands.

`NetworkBannerResult` exposes `Host`, `Address`, `Port`, `Success`, `Banner`, `BytesRead`, `Truncated`, `RoundTripTime` and `Error`.

Maximum read size is 16384 bytes.

## Local port exposure

```xpscript
Forall item In net.GetLocalPortExposure()
    Print item.Protocol & " " & item.LocalAddress & ":" & item.LocalPort & " " & item.Exposure
End Forall
```

`NetworkPortExposureInfo` exposes:

- `Protocol`
- `LocalAddress`
- `LocalPort`
- `AddressFamily`
- `Exposure`
- `IsLoopback`
- `IsWildcard`

Exposure values are:

- `LocalOnly` for loopback listeners
- `AllIPv4Interfaces` for `0.0.0.0`
- `AllIPv6Interfaces` for `::`
- `SpecificInterface` for a concrete non-loopback address

This describes socket binding, not firewall reachability. A wildcard listener can still be blocked by host or network firewalls.

## Platform support

Phase 2 follows Phase 1 platform rules: Windows, Linux and macOS are supported. `browser-wasm` remains unsupported because the required socket, TLS-stream and local network APIs are not exposed by browser sandboxes.
