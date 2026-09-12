# NetworkTools

`NetworkTools` provides cross-platform network diagnostics using only the .NET 10 base class library. It does not add any third-party NuGet package.

Supported native .NET areas:

- ICMP ping through `System.Net.NetworkInformation.Ping`
- traceroute-style hop discovery through repeated ICMP ping with increasing TTL
- forward and reverse DNS through `System.Net.Dns`
- TCP connectivity checks through `System.Net.Sockets.TcpClient`
- UDP datagram sending through `System.Net.Sockets.UdpClient`
- HTTP/HTTPS reachability through `System.Net.Http.HttpClient`
- TLS handshake and certificate inspection through `System.Net.Security.SslStream`
- local interfaces, addresses, DNS servers, gateways and active listeners/connections through `System.Net.NetworkInformation`

`NetworkTools` is available for normal Windows, Linux and macOS targets. It is intentionally unavailable for `browser-wasm` because browser sandboxes do not expose native ICMP, arbitrary sockets, TLS streams or local network interface APIs.

## Create the object

```xpscript
Dim net As New NetworkTools
```

The runtime is feature-gated. Applications that do not reference `NetworkTools` do not include its generated runtime code.

## Ping

```xpscript
Dim result As NetworkPingResult
Set result = net.Ping("server.example.com")

Print result.Success
Print result.Address
Print result.RoundTripTime
Print result.Status
```

Overloads:

```text
Ping(host)
Ping(host, timeoutMilliseconds)
Ping(host, timeoutMilliseconds, ttl)
```

`NetworkPingResult` properties:

| Property | Type | Description |
| --- | --- | --- |
| `Success` | Boolean | `True` when the ICMP reply status is Success. |
| `Host` | String | Requested host name or address. |
| `Address` | String | Address returned by the ping reply when available. |
| `Status` | String | .NET `IPStatus` name such as `Success`, `TimedOut` or `TtlExpired`. |
| `RoundTripTime` | Long | Round-trip time in milliseconds. |
| `Ttl` | Integer | Reply TTL when available, otherwise the requested TTL. |
| `Error` | String | Error text when the operation could not be performed. |

A failed ping normally returns a result object. It does not require exception handling for normal network failure cases.

## TraceRoute

`TraceRoute` is implemented natively with `Ping` and increasing TTL values. XPscript does not launch `tracert.exe`, `traceroute` or another operating-system command.

```xpscript
Dim hops As Variant
Dim hop As NetworkTraceHop

hops = net.TraceRoute("server.example.com", 30, 3000)

Forall hop In hops
    Print CStr(hop.Hop) & " " & hop.Address & " " & CStr(hop.RoundTripTime) & " ms " & hop.Status
End Forall
```

Overloads:

```text
TraceRoute(host)
TraceRoute(host, maxHops)
TraceRoute(host, maxHops, timeoutMilliseconds)
```

`NetworkTraceHop` properties:

- `Hop`
- `Address`
- `RoundTripTime`
- `Status`
- `Error`

A hop can legitimately have no address if an intermediate router does not return an ICMP reply.

## DNS lookup

Basic DNS uses `System.Net.Dns` and therefore resolves IP addresses without adding a DNS NuGet library.

```xpscript
Dim records As Variant
Dim record As NetworkDnsResult

records = net.DnsLookup("example.com")
Forall record In records
    Print record.Type & " " & record.Value
End Forall
```

`DnsLookup` returns `A` for IPv4 addresses and `AAAA` for IPv6 addresses.

Reverse lookup:

```xpscript
Dim record As NetworkDnsResult
Set record = net.ReverseDns("127.0.0.1")
Print record.Value
```

`NetworkDnsResult` properties:

- `Query`
- `Type`
- `Value`
- `Error`

### DNS limitation

The .NET 10 `System.Net.Dns` API does not provide a general DNS record-query API for records such as MX, TXT, SRV, CAA, NS and SOA. Those record types are therefore deliberately not implemented in the native-only `NetworkTools` object. Adding them later would require either a DNS protocol implementation or an additional library such as DnsClient.NET.

## TCP connectivity

```xpscript
Dim result As NetworkPortResult
Set result = net.CheckTcpPort("server.example.com", 443, 3000)

Print result.Open
Print result.RoundTripTime
Print result.Error
```

Overloads:

```text
CheckTcpPort(host, port)
CheckTcpPort(host, port, timeoutMilliseconds)
```

`NetworkPortResult` properties:

- `Host`
- `Address`
- `Port`
- `Open`
- `RoundTripTime`
- `Error`

`Open=True` means a TCP connection was successfully established. This is a connection check, not a port scanner.

## UDP

UDP does not have a connection handshake. Successfully sending a UDP datagram does not prove that the destination port is open.

For that reason the API is named `SendUdp`, not `CheckUdpPort`.

```xpscript
Dim result As NetworkUdpResult
Set result = net.SendUdp("127.0.0.1", 514, "test")

Print result.Success
Print result.BytesSent
```

`NetworkUdpResult` properties:

- `Host`
- `Port`
- `BytesSent`
- `Success`
- `Error`

`Success=True` only means the local UDP send operation completed successfully.

## HTTP and HTTPS

```xpscript
Dim result As NetworkHttpResult
Set result = net.CheckHttp("https://example.com", 10000)

Print result.StatusCode
Print result.FinalUrl
Print result.RoundTripTime
```

Overloads:

```text
CheckHttp(url)
CheckHttp(url, timeoutMilliseconds)
```

The check sends an HTTP `HEAD` request and reads response headers without downloading the response body.

`NetworkHttpResult` properties:

- `Url`
- `FinalUrl`
- `StatusCode`
- `ReasonPhrase`
- `Success`
- `RoundTripTime`
- `ContentType`
- `Error`

`Success` follows `HttpResponseMessage.IsSuccessStatusCode`, so HTTP 2xx is successful. A reachable server returning 401, 403, 404 or 500 still produces a valid result with `Success=False` and the actual HTTP status code.

## TLS and certificate inspection

```xpscript
Dim tls As NetworkTlsResult
Set tls = net.CheckTls("example.com", 443, 10000)

Print tls.Success
Print tls.Protocol
Print tls.CipherSuite
Print tls.CertificateValid
Print tls.CertificateSubject
Print tls.CertificateIssuer
Print tls.CertificateNotAfter
```

Overloads:

```text
CheckTls(host)
CheckTls(host, port)
CheckTls(host, port, timeoutMilliseconds)
```

`NetworkTlsResult` properties:

- `Host`
- `Port`
- `Success`
- `Protocol`
- `CipherSuite`
- `CertificateValid`
- `CertificateSubject`
- `CertificateIssuer`
- `CertificateThumbprint`
- `CertificateNotBefore`
- `CertificateNotAfter`
- `RoundTripTime`
- `CertificateErrors`
- `Error`

The TLS diagnostic allows the handshake to complete even when certificate validation reports an error so the certificate can be inspected. The validation result is exposed through `CertificateValid` and `CertificateErrors`. Applications must not treat `Success=True` as equivalent to a trusted certificate. For trust decisions, require `CertificateValid=True`.

## Local addresses

```xpscript
Forall address In net.GetLocalAddresses()
    Print address
End Forall
```

Returns the IP addresses resolved for the local host.

## DNS servers

```xpscript
Forall address In net.GetDnsServers()
    Print address
End Forall
```

Returns distinct DNS server addresses from active local network interfaces.

## Default gateways

```xpscript
Forall address In net.GetDefaultGateways()
    Print address
End Forall
```

Returns distinct gateway addresses from active local network interfaces.

## Network interfaces

```xpscript
Dim iface As NetworkInterfaceInfo

Forall iface In net.GetInterfaces()
    Print iface.Name
    Print iface.Status
    Print iface.Speed
    Print iface.MacAddress
End Forall
```

`NetworkInterfaceInfo` properties:

| Property | Description |
| --- | --- |
| `Name` | Interface name. |
| `Description` | Platform-provided description. |
| `Id` | Interface identifier. |
| `Type` | .NET network interface type name. |
| `Status` | Operational status. |
| `Speed` | Reported link speed in bits per second. |
| `MacAddress` | Physical address as a hexadecimal string when available. |
| `Addresses` | Array of unicast IP addresses. |
| `Gateways` | Array of configured gateway addresses. |
| `DnsServers` | Array of configured DNS servers. |
| `SupportsIPv4` | Whether the interface reports IPv4 support. |
| `SupportsIPv6` | Whether the interface reports IPv6 support. |

Values are supplied by the operating system through .NET and can differ by platform, container configuration and permissions.

## Active TCP connections

```xpscript
Dim endpoint As NetworkEndpointInfo

Forall endpoint In net.GetTcpConnections()
    Print endpoint.LocalAddress & ":" & CStr(endpoint.LocalPort)
    Print endpoint.RemoteAddress & ":" & CStr(endpoint.RemotePort)
    Print endpoint.State
End Forall
```

## TCP listeners

```xpscript
Forall endpoint In net.GetTcpListeners()
    Print endpoint.LocalAddress & ":" & CStr(endpoint.LocalPort)
End Forall
```

## UDP listeners

```xpscript
Forall endpoint In net.GetUdpListeners()
    Print endpoint.LocalAddress & ":" & CStr(endpoint.LocalPort)
End Forall
```

`NetworkEndpointInfo` properties:

- `Protocol`
- `LocalAddress`
- `LocalPort`
- `RemoteAddress`
- `RemotePort`
- `State`

## Native implementation mapping

| XPscript operation | .NET 10 API |
| --- | --- |
| `Ping` | `System.Net.NetworkInformation.Ping` |
| `TraceRoute` | `Ping` plus `PingOptions.Ttl` |
| `DnsLookup` | `System.Net.Dns.GetHostAddresses` |
| `ReverseDns` | `System.Net.Dns.GetHostEntry` |
| `CheckTcpPort` | `System.Net.Sockets.TcpClient` |
| `SendUdp` | `System.Net.Sockets.UdpClient` |
| `CheckHttp` | `System.Net.Http.HttpClient` |
| `CheckTls` | `System.Net.Security.SslStream` and `X509Certificate2` |
| local interface information | `System.Net.NetworkInformation.NetworkInterface` |
| TCP/UDP connection information | `System.Net.NetworkInformation.IPGlobalProperties` |

No executable such as `ping`, `tracert`, `traceroute`, `nslookup`, `netstat` or `curl` is launched.

## Security and operational notes

`NetworkTools` is intended for application diagnostics and connectivity checks. It does not implement bulk port scanning, raw packet capture, packet injection or promiscuous network capture.

Network operations can reveal infrastructure information and can generate traffic visible to firewalls, IDS/IPS systems and remote services. Applications should validate user-supplied hosts and URLs when exposing these functions through an API or UI.

Timeouts should normally be set for operations that contact remote systems. DNS and operating-system network inventory calls use the behavior provided by the .NET 10 runtime and the host operating system.
