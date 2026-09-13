namespace XPScript.Compiler;

internal static class NetworkToolsPhase2RuntimePostProcessor
{
    private const string Anchor = "    private static XPScriptNetworkInterfaceInfo CreateInterface(System.Net.NetworkInformation.NetworkInterface item)";

    public static string Transform(string generated)
    {
        if (!generated.Contains("internal sealed class XPScriptNetworkTools", StringComparison.Ordinal)) return generated;
        if (!generated.Contains(Anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NetworkTools Phase 2 runtime surface.");
        generated = generated.Replace(Anchor, Methods + "\n" + Anchor, StringComparison.Ordinal);
        return generated + "\n\n" + ResultTypes + "\n";
    }

    private const string Methods = """
    public XPScriptNetworkHostResult ResolveHost(object? hostValue)
    {
        var host = RequiredText(hostValue, "host");
        try
        {
            var entry = System.Net.Dns.GetHostEntry(host);
            var ipv4 = entry.AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(x => x.ToString());
            var ipv6 = entry.AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).Select(x => x.ToString());
            return new XPScriptNetworkHostResult(host, entry.HostName, ipv4.ToArray(), ipv6.ToArray(), "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkHostResult(host, "", [], [], ex.GetBaseException().Message);
        }
    }

    public LSArray CheckTcpPorts(object? hostValue, object? portsValue) => CheckTcpPorts(hostValue, portsValue, 1000, 8);
    public LSArray CheckTcpPorts(object? hostValue, object? portsValue, object? timeoutValue) => CheckTcpPorts(hostValue, portsValue, timeoutValue, 8);
    public LSArray CheckTcpPorts(object? hostValue, object? portsValue, object? timeoutValue, object? concurrencyValue)
    {
        var host = RequiredText(hostValue, "host");
        var timeout = PositiveInt(timeoutValue, "timeout");
        var concurrency = Math.Min(16, PositiveInt(concurrencyValue, "concurrency"));
        var ports = PortValues(portsValue).Distinct().ToArray();
        if (ports.Length == 0) return PackObjects([]);
        if (ports.Length > 64) throw new XPScriptRuntimeException(5, "NetworkTools CheckTcpPorts accepts at most 64 explicit ports per call.");

        var results = new XPScriptNetworkPortResult[ports.Length];
        using var gate = new System.Threading.SemaphoreSlim(concurrency, concurrency);
        var tasks = ports.Select((port, index) => System.Threading.Tasks.Task.Run(async () =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try { results[index] = CheckTcpPort(host, port, timeout); }
            finally { gate.Release(); }
        })).ToArray();
        System.Threading.Tasks.Task.WhenAll(tasks).GetAwaiter().GetResult();
        return PackObjects(results.Cast<object?>());
    }

    public bool IsPrivateAddress(object? addressValue)
    {
        var ip = ParseAddress(addressValue);
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        return b.Length == 16 && (b[0] & 0xFE) == 0xFC;
    }

    public bool IsLoopbackAddress(object? addressValue) => System.Net.IPAddress.IsLoopback(ParseAddress(addressValue));

    public bool IsLinkLocalAddress(object? addressValue)
    {
        var ip = ParseAddress(addressValue);
        var b = ip.GetAddressBytes();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? b[0] == 169 && b[1] == 254
            : b.Length == 16 && b[0] == 0xFE && (b[1] & 0xC0) == 0x80;
    }

    public bool IsMulticastAddress(object? addressValue)
    {
        var ip = ParseAddress(addressValue);
        var b = ip.GetAddressBytes();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? b[0] >= 224 && b[0] <= 239
            : b.Length == 16 && b[0] == 0xFF;
    }

    public string GetAddressFamily(object? addressValue) =>
        ParseAddress(addressValue).AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "IPv4" : "IPv6";

    public bool IsInSubnet(object? addressValue, object? cidrValue) => ParseSubnet(cidrValue).Contains(addressValue);

    public XPScriptNetworkSubnetInfo ParseSubnet(object? cidrValue)
    {
        var cidr = RequiredText(cidrValue, "cidr");
        var parts = cidr.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !System.Net.IPAddress.TryParse(parts[0], out var ip) || !int.TryParse(parts[1], out var prefix))
            throw new XPScriptRuntimeException(5, "NetworkTools cidr must use address/prefix notation.");
        var bits = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefix < 0 || prefix > bits) throw new XPScriptRuntimeException(5, "NetworkTools subnet prefix is outside the address-family range.");

        var source = ip.GetAddressBytes();
        var network = ApplyPrefix(source, prefix, false);
        var last = ApplyPrefix(source, prefix, true);
        var networkAddress = new System.Net.IPAddress(network).ToString();
        var lastAddress = new System.Net.IPAddress(last).ToString();
        var mask = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? new System.Net.IPAddress(CreateMask(4, prefix)).ToString() : "";
        var broadcast = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? lastAddress : "";
        var count = (System.Numerics.BigInteger.One << (bits - prefix)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new XPScriptNetworkSubnetInfo(cidr, networkAddress, broadcast, prefix, mask, networkAddress, lastAddress, count, bits);
    }

    public LSArray CheckTlsVersions(object? hostValue) => CheckTlsVersions(hostValue, 443, 5000);
    public LSArray CheckTlsVersions(object? hostValue, object? portValue) => CheckTlsVersions(hostValue, portValue, 5000);
    public LSArray CheckTlsVersions(object? hostValue, object? portValue, object? timeoutValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        var versions = new (string Name, System.Security.Authentication.SslProtocols Protocol)[]
        {
#pragma warning disable SYSLIB0039
            ("TLS1.0", System.Security.Authentication.SslProtocols.Tls),
            ("TLS1.1", System.Security.Authentication.SslProtocols.Tls11),
#pragma warning restore SYSLIB0039
            ("TLS1.2", System.Security.Authentication.SslProtocols.Tls12),
            ("TLS1.3", System.Security.Authentication.SslProtocols.Tls13)
        };
        var results = new List<object?>();
        foreach (var version in versions)
        {
            try
            {
                using var client = ConnectTcp(host, port, timeout);
                using var ssl = new System.Net.Security.SslStream(client.GetStream(), false, (_, _, _, _) => true);
                var options = new System.Net.Security.SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = version.Protocol,
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                };
                var task = ssl.AuthenticateAsClientAsync(options);
                if (!task.Wait(timeout)) throw new TimeoutException("TLS handshake timed out.");
                task.GetAwaiter().GetResult();
                results.Add(new XPScriptNetworkTlsVersionResult(version.Name, true, ssl.SslProtocol.ToString(), ""));
            }
            catch (Exception ex)
            {
                results.Add(new XPScriptNetworkTlsVersionResult(version.Name, false, "", ex.GetBaseException().Message));
            }
        }
        return PackObjects(results);
    }

    public XPScriptNetworkCertificateResult CheckCertificate(object? hostValue) => CheckCertificate(hostValue, 443, 10000);
    public XPScriptNetworkCertificateResult CheckCertificate(object? hostValue, object? portValue) => CheckCertificate(hostValue, portValue, 10000);
    public XPScriptNetworkCertificateResult CheckCertificate(object? hostValue, object? portValue, object? timeoutValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        System.Net.Security.SslPolicyErrors policyErrors = System.Net.Security.SslPolicyErrors.None;
        try
        {
            using var client = ConnectTcp(host, port, timeout);
            using var ssl = new System.Net.Security.SslStream(client.GetStream(), false, (_, _, _, errors) => { policyErrors = errors; return true; });
            var auth = ssl.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions { TargetHost = host });
            if (!auth.Wait(timeout)) throw new TimeoutException("TLS handshake timed out.");
            auth.GetAwaiter().GetResult();
            if (ssl.RemoteCertificate is null) throw new InvalidOperationException("Remote endpoint did not provide a certificate.");
            using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(ssl.RemoteCertificate);
            using var chain = new System.Security.Cryptography.X509Certificates.X509Chain();
            var chainValid = chain.Build(cert);
            var chainErrors = chain.ChainStatus.Select(x => x.Status + ": " + x.StatusInformation.Trim()).ToArray();
            var dnsNames = CertificateDnsNames(cert).ToArray();
            var keyAlgorithm = cert.PublicKey.Oid?.FriendlyName ?? cert.PublicKey.Oid?.Value ?? "";
            var keySize = GetPublicKeySize(cert);
            var now = DateTime.Now;
            var daysRemaining = (int)Math.Floor((cert.NotAfter - now).TotalDays);
            return new XPScriptNetworkCertificateResult(host, port, true, cert.Subject, cert.Issuer, cert.Thumbprint ?? "",
                cert.SerialNumber, cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "", keyAlgorithm, keySize,
                cert.NotBefore, cert.NotAfter, daysRemaining, now > cert.NotAfter, now < cert.NotBefore,
                (policyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) == 0,
                chainValid, dnsNames, chainErrors, policyErrors.ToString(), "");
        }
        catch (Exception ex)
        {
            return XPScriptNetworkCertificateResult.Failed(host, port, ex.GetBaseException().Message);
        }
    }

    public XPScriptNetworkHttpHeadersResult GetHttpHeaders(object? urlValue) => GetHttpHeaders(urlValue, 10000);
    public XPScriptNetworkHttpHeadersResult GetHttpHeaders(object? urlValue, object? timeoutValue)
    {
        var url = RequiredHttpUrl(urlValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        try
        {
            using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = true };
            using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeout) };
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);
            using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            return new XPScriptNetworkHttpHeadersResult(url, response.RequestMessage?.RequestUri?.ToString() ?? url,
                (int)response.StatusCode, HeaderPairs(response).ToArray(), "");
        }
        catch (Exception ex) { return new XPScriptNetworkHttpHeadersResult(url, "", 0, [], ex.GetBaseException().Message); }
    }

    public XPScriptNetworkHttpSecurityHeadersResult CheckHttpSecurityHeaders(object? urlValue) => CheckHttpSecurityHeaders(urlValue, 10000);
    public XPScriptNetworkHttpSecurityHeadersResult CheckHttpSecurityHeaders(object? urlValue, object? timeoutValue)
    {
        var headers = GetHttpHeaders(urlValue, timeoutValue);
        var hsts = headers.GetHeader("Strict-Transport-Security");
        var csp = headers.GetHeader("Content-Security-Policy");
        var xcto = headers.GetHeader("X-Content-Type-Options");
        var referrer = headers.GetHeader("Referrer-Policy");
        var permissions = headers.GetHeader("Permissions-Policy");
        var coop = headers.GetHeader("Cross-Origin-Opener-Policy");
        var corp = headers.GetHeader("Cross-Origin-Resource-Policy");
        return new XPScriptNetworkHttpSecurityHeadersResult(headers.Url, headers.FinalUrl, headers.StatusCode,
            hsts.Length != 0, hsts, ParseHstsMaxAge(hsts), csp.Length != 0, csp,
            xcto.Equals("nosniff", StringComparison.OrdinalIgnoreCase), xcto,
            referrer.Length != 0, referrer, permissions.Length != 0, permissions,
            coop.Length != 0, coop, corp.Length != 0, corp, headers.Error);
    }

    public LSArray CheckRedirects(object? urlValue) => CheckRedirects(urlValue, 10000, 10);
    public LSArray CheckRedirects(object? urlValue, object? timeoutValue) => CheckRedirects(urlValue, timeoutValue, 10);
    public LSArray CheckRedirects(object? urlValue, object? timeoutValue, object? maxRedirectsValue)
    {
        var current = RequiredHttpUrl(urlValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        var maxRedirects = Math.Min(20, PositiveInt(maxRedirectsValue, "maxRedirects"));
        var results = new List<object?>();
        using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
        using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeout) };
        for (var i = 0; i <= maxRedirects; i++)
        {
            try
            {
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, current);
                using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                var location = response.Headers.Location;
                var isRedirect = (int)response.StatusCode is >= 300 and < 400 && location is not null;
                var next = "";
                if (location is not null)
                    next = location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(current), location).ToString();
                results.Add(new XPScriptNetworkRedirectResult(current, (int)response.StatusCode, next, isRedirect, ""));
                if (!isRedirect) break;
                current = next;
            }
            catch (Exception ex)
            {
                results.Add(new XPScriptNetworkRedirectResult(current, 0, "", false, ex.GetBaseException().Message));
                break;
            }
        }
        return PackObjects(results);
    }

    public XPScriptNetworkBannerResult GrabBanner(object? hostValue, object? portValue) => GrabBanner(hostValue, portValue, 2000, 8192);
    public XPScriptNetworkBannerResult GrabBanner(object? hostValue, object? portValue, object? timeoutValue) => GrabBanner(hostValue, portValue, timeoutValue, 8192);
    public XPScriptNetworkBannerResult GrabBanner(object? hostValue, object? portValue, object? timeoutValue, object? maxBytesValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        var maxBytes = PositiveInt(maxBytesValue, "maxBytes");
        if (maxBytes > 16384) throw new XPScriptRuntimeException(5, "NetworkTools GrabBanner maxBytes cannot exceed 16384.");
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = ConnectTcp(host, port, timeout);
            var address = (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "";
            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            var buffer = new byte[maxBytes];
            var count = stream.Read(buffer, 0, buffer.Length);
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, count).Replace("\0", "", StringComparison.Ordinal);
            return new XPScriptNetworkBannerResult(host, address, port, true, text, count, count == maxBytes, started.ElapsedMilliseconds, "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkBannerResult(host, "", port, false, "", 0, false, started.ElapsedMilliseconds, ex.GetBaseException().Message);
        }
    }

    public LSArray GetLocalPortExposure()
    {
        var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        var values = new List<object?>();
        foreach (var endpoint in properties.GetActiveTcpListeners()) values.Add(CreateExposure("TCP", endpoint));
        foreach (var endpoint in properties.GetActiveUdpListeners()) values.Add(CreateExposure("UDP", endpoint));
        return PackObjects(values.OrderBy(x => ((XPScriptNetworkPortExposureInfo)x!).Protocol).ThenBy(x => ((XPScriptNetworkPortExposureInfo)x!).LocalPort));
    }

    private static XPScriptNetworkPortExposureInfo CreateExposure(string protocol, System.Net.IPEndPoint endpoint)
    {
        var ip = endpoint.Address;
        var wildcard = ip.Equals(System.Net.IPAddress.Any) || ip.Equals(System.Net.IPAddress.IPv6Any);
        var loopback = System.Net.IPAddress.IsLoopback(ip);
        var family = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "IPv4" : "IPv6";
        var exposure = wildcard ? (family == "IPv4" ? "AllIPv4Interfaces" : "AllIPv6Interfaces") : loopback ? "LocalOnly" : "SpecificInterface";
        return new XPScriptNetworkPortExposureInfo(protocol, ip.ToString(), endpoint.Port, family, exposure, loopback, wildcard);
    }

    private static System.Net.Sockets.TcpClient ConnectTcp(string host, int port, int timeout)
    {
        var client = new System.Net.Sockets.TcpClient();
        try
        {
            var task = client.ConnectAsync(host, port);
            if (!task.Wait(timeout)) throw new TimeoutException("Connection timed out.");
            task.GetAwaiter().GetResult();
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    private static IEnumerable<int> PortValues(object? value)
    {
        if (value is LSArray array)
        {
            if (!array.IsAllocated || array.Rank != 1) throw new XPScriptRuntimeException(5, "NetworkTools ports must be a one-dimensional allocated array.");
            for (var i = array.LBound(); i <= array.UBound(); i++) yield return Port(array.Get(i));
            yield break;
        }
        if (value is Array clr)
        {
            foreach (var item in clr) yield return Port(item);
            yield break;
        }
        throw new XPScriptRuntimeException(5, "NetworkTools ports must be an array of explicit port numbers.");
    }

    private static System.Net.IPAddress ParseAddress(object? value)
    {
        var text = RequiredText(value, "address");
        if (!System.Net.IPAddress.TryParse(text, out var ip)) throw new XPScriptRuntimeException(5, "NetworkTools address must be a valid IPv4 or IPv6 address.");
        return ip;
    }

    private static byte[] CreateMask(int byteCount, int prefix)
    {
        var result = new byte[byteCount];
        for (var i = 0; i < result.Length; i++)
        {
            var remaining = prefix - i * 8;
            result[i] = remaining >= 8 ? (byte)255 : remaining <= 0 ? (byte)0 : (byte)(0xFF << (8 - remaining));
        }
        return result;
    }

    private static byte[] ApplyPrefix(byte[] source, int prefix, bool setHostBits)
    {
        var mask = CreateMask(source.Length, prefix);
        var result = new byte[source.Length];
        for (var i = 0; i < source.Length; i++) result[i] = setHostBits ? (byte)(source[i] | ~mask[i]) : (byte)(source[i] & mask[i]);
        return result;
    }

    private static IEnumerable<string> CertificateDnsNames(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in cert.Extensions)
        {
            if (extension.Oid?.Value != "2.5.29.17") continue;
            var formatted = extension.Format(false);
            foreach (var part in formatted.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var value = part.StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase) ? part[9..] :
                    part.StartsWith("DNS:", StringComparison.OrdinalIgnoreCase) ? part[4..] : "";
                if (value.Length != 0 && seen.Add(value)) yield return value;
            }
        }
        if (seen.Count == 0)
        {
            var dns = cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.DnsName, false);
            if (!string.IsNullOrWhiteSpace(dns)) yield return dns;
        }
    }

    private static int GetPublicKeySize(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
    {
        try
        {
            using var rsa = cert.GetRSAPublicKey();
            if (rsa is not null) return rsa.KeySize;
            using var ecdsa = cert.GetECDsaPublicKey();
            if (ecdsa is not null) return ecdsa.KeySize;
            using var dsa = cert.GetDSAPublicKey();
            return dsa?.KeySize ?? 0;
        }
        catch { return 0; }
    }

    private static string RequiredHttpUrl(object? value)
    {
        var url = RequiredText(value, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "NetworkTools URL must use http or https.");
        return uri.ToString();
    }

    private static IEnumerable<string> HeaderPairs(System.Net.Http.HttpResponseMessage response)
    {
        foreach (var item in response.Headers) yield return item.Key + ": " + string.Join(", ", item.Value);
        foreach (var item in response.Content.Headers) yield return item.Key + ": " + string.Join(", ", item.Value);
    }

    private static long ParseHstsMaxAge(string value)
    {
        foreach (var part in value.Split(';', StringSplitOptions.TrimEntries))
        {
            if (!part.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase)) continue;
            if (long.TryParse(part[8..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result)) return result;
        }
        return 0;
    }
""";

    private const string ResultTypes = """
internal sealed class XPScriptNetworkHostResult
{
    private readonly string[] _ipv4;
    private readonly string[] _ipv6;
    public XPScriptNetworkHostResult(string query, string hostName, string[] ipv4, string[] ipv6, string error) { Query = query; HostName = hostName; _ipv4 = ipv4; _ipv6 = ipv6; Error = error; }
    public string Query { get; }
    public string HostName { get; }
    public LSArray IPv4Addresses => XPScriptNetworkTools.PackStrings(_ipv4);
    public LSArray IPv6Addresses => XPScriptNetworkTools.PackStrings(_ipv6);
    public string Error { get; }
}

internal sealed class XPScriptNetworkSubnetInfo
{
    public XPScriptNetworkSubnetInfo(string cidr, string networkAddress, string broadcastAddress, int prefixLength, string subnetMask, string firstAddress, string lastAddress, string addressCount, int addressBits)
    { Cidr = cidr; NetworkAddress = networkAddress; BroadcastAddress = broadcastAddress; PrefixLength = prefixLength; SubnetMask = subnetMask; FirstAddress = firstAddress; LastAddress = lastAddress; AddressCount = addressCount; AddressBits = addressBits; }
    public string Cidr { get; }
    public string NetworkAddress { get; }
    public string BroadcastAddress { get; }
    public int PrefixLength { get; }
    public string SubnetMask { get; }
    public string FirstAddress { get; }
    public string LastAddress { get; }
    public string AddressCount { get; }
    public int AddressBits { get; }
    public bool Contains(object? addressValue)
    {
        var text = XPScriptRuntime.CStr(addressValue).Trim();
        if (!System.Net.IPAddress.TryParse(text, out var candidate) || !System.Net.IPAddress.TryParse(NetworkAddress, out var network)) return false;
        if (candidate.AddressFamily != network.AddressFamily) return false;
        var bytes = candidate.GetAddressBytes();
        var target = network.GetAddressBytes();
        var whole = PrefixLength / 8; var remaining = PrefixLength % 8;
        for (var i = 0; i < whole; i++) if (bytes[i] != target[i]) return false;
        if (remaining == 0) return true;
        var mask = (byte)(0xFF << (8 - remaining));
        return (bytes[whole] & mask) == (target[whole] & mask);
    }
}

internal sealed record XPScriptNetworkTlsVersionResult(string Version, bool Supported, string NegotiatedProtocol, string Error);

internal sealed class XPScriptNetworkCertificateResult
{
    private readonly string[] _dnsNames; private readonly string[] _chainErrors;
    public XPScriptNetworkCertificateResult(string host, int port, bool success, string subject, string issuer, string thumbprint, string serialNumber,
        string signatureAlgorithm, string publicKeyAlgorithm, int publicKeySize, DateTime notBefore, DateTime notAfter, int daysRemaining,
        bool expired, bool notYetValid, bool hostNameValid, bool chainValid, string[] dnsNames, string[] chainErrors, string policyErrors, string error)
    { Host=host; Port=port; Success=success; Subject=subject; Issuer=issuer; Thumbprint=thumbprint; SerialNumber=serialNumber; SignatureAlgorithm=signatureAlgorithm;
      PublicKeyAlgorithm=publicKeyAlgorithm; PublicKeySize=publicKeySize; NotBefore=notBefore; NotAfter=notAfter; DaysRemaining=daysRemaining; Expired=expired;
      NotYetValid=notYetValid; HostNameValid=hostNameValid; ChainValid=chainValid; _dnsNames=dnsNames; _chainErrors=chainErrors; PolicyErrors=policyErrors; Error=error; }
    public string Host { get; } public int Port { get; } public bool Success { get; } public string Subject { get; } public string Issuer { get; }
    public string Thumbprint { get; } public string SerialNumber { get; } public string SignatureAlgorithm { get; } public string PublicKeyAlgorithm { get; }
    public int PublicKeySize { get; } public DateTime NotBefore { get; } public DateTime NotAfter { get; } public int DaysRemaining { get; }
    public bool Expired { get; } public bool NotYetValid { get; } public bool HostNameValid { get; } public bool ChainValid { get; }
    public LSArray DnsNames => XPScriptNetworkTools.PackStrings(_dnsNames); public LSArray ChainErrors => XPScriptNetworkTools.PackStrings(_chainErrors);
    public string PolicyErrors { get; } public string Error { get; }
    public static XPScriptNetworkCertificateResult Failed(string host, int port, string error) => new(host, port, false, "", "", "", "", "", "", 0, DateTime.MinValue, DateTime.MinValue, 0, false, false, false, false, [], [], "", error);
}

internal sealed class XPScriptNetworkHttpHeadersResult
{
    private readonly string[] _headers;
    public XPScriptNetworkHttpHeadersResult(string url, string finalUrl, int statusCode, string[] headers, string error) { Url=url; FinalUrl=finalUrl; StatusCode=statusCode; _headers=headers; Error=error; }
    public string Url { get; } public string FinalUrl { get; } public int StatusCode { get; } public LSArray Headers => XPScriptNetworkTools.PackStrings(_headers); public string Error { get; }
    public string GetHeader(object? nameValue)
    {
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        var prefix = name + ":";
        var item = _headers.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return item is null ? "" : item[(item.IndexOf(':') + 1)..].Trim();
    }
}

internal sealed record XPScriptNetworkHttpSecurityHeadersResult(
    string Url, string FinalUrl, int StatusCode,
    bool HasHsts, string Hsts, long HstsMaxAge,
    bool HasContentSecurityPolicy, string ContentSecurityPolicy,
    bool HasXContentTypeOptions, string XContentTypeOptions,
    bool HasReferrerPolicy, string ReferrerPolicy,
    bool HasPermissionsPolicy, string PermissionsPolicy,
    bool HasCrossOriginOpenerPolicy, string CrossOriginOpenerPolicy,
    bool HasCrossOriginResourcePolicy, string CrossOriginResourcePolicy,
    string Error);

internal sealed record XPScriptNetworkRedirectResult(string Url, int StatusCode, string Location, bool IsRedirect, string Error);
internal sealed record XPScriptNetworkBannerResult(string Host, string Address, int Port, bool Success, string Banner, int BytesRead, bool Truncated, long RoundTripTime, string Error);
internal sealed record XPScriptNetworkPortExposureInfo(string Protocol, string LocalAddress, int LocalPort, string AddressFamily, string Exposure, bool IsLoopback, bool IsWildcard);
""";
}
