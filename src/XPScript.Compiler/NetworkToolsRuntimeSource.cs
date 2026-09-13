namespace XPScript.Compiler;

internal static class NetworkToolsRuntimeSource
{
    public const string Code = """
internal static class XPScriptNetworkToolsFactory
{
    public static object Create() => new XPScriptNetworkTools();
}

internal sealed class XPScriptNetworkTools
{
    public XPScriptNetworkPingResult Ping(object? hostValue) => Ping(hostValue, 4000, 128);
    public XPScriptNetworkPingResult Ping(object? hostValue, object? timeoutValue) => Ping(hostValue, timeoutValue, 128);
    public XPScriptNetworkPingResult Ping(object? hostValue, object? timeoutValue, object? ttlValue)
    {
        var host = RequiredText(hostValue, "host");
        var timeout = PositiveInt(timeoutValue, "timeout");
        var ttl = PositiveInt(ttlValue, "ttl");
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var options = new System.Net.NetworkInformation.PingOptions(ttl, false);
            var reply = ping.Send(host, timeout, Array.Empty<byte>(), options);
            return new XPScriptNetworkPingResult(
                reply.Status == System.Net.NetworkInformation.IPStatus.Success,
                host,
                reply.Address?.ToString() ?? "",
                reply.Status.ToString(),
                reply.RoundtripTime,
                reply.Options?.Ttl ?? ttl,
                "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkPingResult(false, host, "", "Error", 0, ttl, ex.Message);
        }
    }

    public LSArray TraceRoute(object? hostValue) => TraceRoute(hostValue, 30, 3000);
    public LSArray TraceRoute(object? hostValue, object? maxHopsValue) => TraceRoute(hostValue, maxHopsValue, 3000);
    public LSArray TraceRoute(object? hostValue, object? maxHopsValue, object? timeoutValue)
    {
        var host = RequiredText(hostValue, "host");
        var maxHops = PositiveInt(maxHopsValue, "maxHops");
        var timeout = PositiveInt(timeoutValue, "timeout");
        var hops = new List<object?>();
        using var ping = new System.Net.NetworkInformation.Ping();
        for (var ttl = 1; ttl <= maxHops; ttl++)
        {
            try
            {
                var options = new System.Net.NetworkInformation.PingOptions(ttl, false);
                var reply = ping.Send(host, timeout, Array.Empty<byte>(), options);
                hops.Add(new XPScriptNetworkTraceHop(ttl, reply.Address?.ToString() ?? "", reply.RoundtripTime, reply.Status.ToString(), ""));
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success) break;
            }
            catch (Exception ex)
            {
                hops.Add(new XPScriptNetworkTraceHop(ttl, "", 0, "Error", ex.Message));
                break;
            }
        }
        return PackObjects(hops);
    }

    public LSArray DnsLookup(object? hostValue)
    {
        var host = RequiredText(hostValue, "host");
        try
        {
            var values = System.Net.Dns.GetHostAddresses(host)
                .Select(address => (object?)new XPScriptNetworkDnsResult(
                    host,
                    address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "A" : "AAAA",
                    address.ToString(),
                    ""))
                .ToArray();
            return PackObjects(values);
        }
        catch (Exception ex)
        {
            return PackObjects([new XPScriptNetworkDnsResult(host, "", "", ex.Message)]);
        }
    }

    public XPScriptNetworkDnsResult ReverseDns(object? addressValue)
    {
        var address = RequiredText(addressValue, "address");
        try
        {
            var entry = System.Net.Dns.GetHostEntry(address);
            return new XPScriptNetworkDnsResult(address, "PTR", entry.HostName, "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkDnsResult(address, "PTR", "", ex.Message);
        }
    }

    public XPScriptNetworkPortResult CheckTcpPort(object? hostValue, object? portValue) => CheckTcpPort(hostValue, portValue, 3000);
    public XPScriptNetworkPortResult CheckTcpPort(object? hostValue, object? portValue, object? timeoutValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var task = client.ConnectAsync(host, port);
            if (!task.Wait(timeout))
                return new XPScriptNetworkPortResult(host, "", port, false, started.ElapsedMilliseconds, "Connection timed out.");
            task.GetAwaiter().GetResult();
            var address = (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "";
            return new XPScriptNetworkPortResult(host, address, port, true, started.ElapsedMilliseconds, "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkPortResult(host, "", port, false, started.ElapsedMilliseconds, ex.GetBaseException().Message);
        }
    }

    public XPScriptNetworkUdpResult SendUdp(object? hostValue, object? portValue, object? textValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var text = XPScriptRuntime.CStr(textValue);
        try
        {
            using var udp = new System.Net.Sockets.UdpClient();
            udp.Connect(host, port);
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var sent = udp.Send(bytes, bytes.Length);
            return new XPScriptNetworkUdpResult(host, port, sent, true, "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkUdpResult(host, port, 0, false, ex.Message);
        }
    }

    public XPScriptNetworkHttpResult CheckHttp(object? urlValue) => CheckHttp(urlValue, 10000);
    public XPScriptNetworkHttpResult CheckHttp(object? urlValue, object? timeoutValue)
    {
        var url = RequiredText(urlValue, "url");
        var timeout = PositiveInt(timeoutValue, "timeout");
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);
            using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "";
            return new XPScriptNetworkHttpResult(
                url,
                response.RequestMessage?.RequestUri?.ToString() ?? url,
                (int)response.StatusCode,
                response.ReasonPhrase ?? "",
                response.IsSuccessStatusCode,
                started.ElapsedMilliseconds,
                contentType,
                "");
        }
        catch (Exception ex)
        {
            return new XPScriptNetworkHttpResult(url, "", 0, "", false, started.ElapsedMilliseconds, "", ex.GetBaseException().Message);
        }
    }

    public XPScriptNetworkTlsResult CheckTls(object? hostValue) => CheckTls(hostValue, 443, 10000);
    public XPScriptNetworkTlsResult CheckTls(object? hostValue, object? portValue) => CheckTls(hostValue, portValue, 10000);
    public XPScriptNetworkTlsResult CheckTls(object? hostValue, object? portValue, object? timeoutValue)
    {
        var host = RequiredText(hostValue, "host");
        var port = Port(portValue);
        var timeout = PositiveInt(timeoutValue, "timeout");
        var started = System.Diagnostics.Stopwatch.StartNew();
        System.Net.Security.SslPolicyErrors certificateErrors = System.Net.Security.SslPolicyErrors.None;
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (!connectTask.Wait(timeout))
                return XPScriptNetworkTlsResult.Failed(host, port, started.ElapsedMilliseconds, "Connection timed out.");
            connectTask.GetAwaiter().GetResult();
            using var ssl = new System.Net.Security.SslStream(
                client.GetStream(),
                false,
                (_, _, _, errors) => { certificateErrors = errors; return true; });
            ssl.ReadTimeout = timeout;
            ssl.WriteTimeout = timeout;
            ssl.AuthenticateAsClient(host);
            var certificate = ssl.RemoteCertificate is null ? null : new System.Security.Cryptography.X509Certificates.X509Certificate2(ssl.RemoteCertificate);
            return new XPScriptNetworkTlsResult(
                host,
                port,
                true,
                ssl.SslProtocol.ToString(),
                ssl.NegotiatedCipherSuite.ToString(),
                certificateErrors == System.Net.Security.SslPolicyErrors.None,
                certificate?.Subject ?? "",
                certificate?.Issuer ?? "",
                certificate?.Thumbprint ?? "",
                certificate?.NotBefore ?? DateTime.MinValue,
                certificate?.NotAfter ?? DateTime.MinValue,
                started.ElapsedMilliseconds,
                certificateErrors.ToString(),
                "");
        }
        catch (Exception ex)
        {
            return XPScriptNetworkTlsResult.Failed(host, port, started.ElapsedMilliseconds, ex.GetBaseException().Message);
        }
    }

    public LSArray GetLocalAddresses()
    {
        var host = System.Net.Dns.GetHostName();
        return PackStrings(System.Net.Dns.GetHostAddresses(host).Select(x => x.ToString()));
    }

    public LSArray GetDnsServers()
    {
        var values = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .SelectMany(x => SafeIPProperties(x).DnsAddresses)
            .Select(x => x.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return PackStrings(values);
    }

    public LSArray GetDefaultGateways()
    {
        var values = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .SelectMany(x => SafeIPProperties(x).GatewayAddresses)
            .Select(x => x.Address?.ToString() ?? "")
            .Where(x => x.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return PackStrings(values);
    }

    public LSArray GetInterfaces()
    {
        var values = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Select(x => (object?)CreateInterface(x));
        return PackObjects(values);
    }

    public LSArray GetTcpConnections()
    {
        var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        return PackObjects(properties.GetActiveTcpConnections().Select(x => (object?)new XPScriptNetworkEndpointInfo(
            "TCP", x.LocalEndPoint.Address.ToString(), x.LocalEndPoint.Port,
            x.RemoteEndPoint.Address.ToString(), x.RemoteEndPoint.Port, x.State.ToString())));
    }

    public LSArray GetTcpListeners()
    {
        var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        return PackObjects(properties.GetActiveTcpListeners().Select(x => (object?)new XPScriptNetworkEndpointInfo(
            "TCP", x.Address.ToString(), x.Port, "", 0, "Listen")));
    }

    public LSArray GetUdpListeners()
    {
        var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        return PackObjects(properties.GetActiveUdpListeners().Select(x => (object?)new XPScriptNetworkEndpointInfo(
            "UDP", x.Address.ToString(), x.Port, "", 0, "Listen")));
    }

    private static XPScriptNetworkInterfaceInfo CreateInterface(System.Net.NetworkInformation.NetworkInterface item)
    {
        var properties = SafeIPProperties(item);
        return new XPScriptNetworkInterfaceInfo(
            item.Name,
            item.Description,
            item.Id,
            item.NetworkInterfaceType.ToString(),
            item.OperationalStatus.ToString(),
            item.Speed,
            item.GetPhysicalAddress().ToString(),
            properties.UnicastAddresses.Select(x => x.Address.ToString()).ToArray(),
            properties.GatewayAddresses.Select(x => x.Address?.ToString() ?? "").Where(x => x.Length != 0).ToArray(),
            properties.DnsAddresses.Select(x => x.ToString()).ToArray(),
            item.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv4),
            item.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv6));
    }

    private static System.Net.NetworkInformation.IPInterfaceProperties SafeIPProperties(System.Net.NetworkInformation.NetworkInterface item) => item.GetIPProperties();

    private static string RequiredText(object? value, string name)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "NetworkTools " + name + " cannot be empty.");
        return text;
    }

    private static int PositiveInt(object? value, string name)
    {
        var number = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        if (number <= 0) throw new XPScriptRuntimeException(5, "NetworkTools " + name + " must be greater than zero.");
        return number;
    }

    private static int Port(object? value)
    {
        var port = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        if (port is < 1 or > 65535) throw new XPScriptRuntimeException(5, "NetworkTools port must be between 1 and 65535.");
        return port;
    }

    internal static LSArray PackStrings(IEnumerable<string> values)
    {
        var items = values.ToArray();
        if (items.Length == 0) return new LSArray("String", true);
        var result = new LSArray("String", true, [0], [items.Length - 1]);
        for (var i = 0; i < items.Length; i++) result.Set(items[i], i);
        return result;
    }

    internal static LSArray PackObjects(IEnumerable<object?> values)
    {
        var items = values.ToArray();
        if (items.Length == 0) return new LSArray("Variant", true);
        var result = new LSArray("Variant", true, [0], [items.Length - 1]);
        for (var i = 0; i < items.Length; i++) result.Set(items[i], i);
        return result;
    }
}

internal sealed record XPScriptNetworkPingResult(bool Success, string Host, string Address, string Status, long RoundTripTime, int Ttl, string Error);
internal sealed record XPScriptNetworkTraceHop(int Hop, string Address, long RoundTripTime, string Status, string Error);
internal sealed record XPScriptNetworkDnsResult(string Query, string Type, string Value, string Error);
internal sealed record XPScriptNetworkPortResult(string Host, string Address, int Port, bool Open, long RoundTripTime, string Error);
internal sealed record XPScriptNetworkUdpResult(string Host, int Port, int BytesSent, bool Success, string Error);
internal sealed record XPScriptNetworkHttpResult(string Url, string FinalUrl, int StatusCode, string ReasonPhrase, bool Success, long RoundTripTime, string ContentType, string Error);

internal sealed record XPScriptNetworkTlsResult(
    string Host, int Port, bool Success, string Protocol, string CipherSuite, bool CertificateValid,
    string CertificateSubject, string CertificateIssuer, string CertificateThumbprint,
    DateTime CertificateNotBefore, DateTime CertificateNotAfter, long RoundTripTime, string CertificateErrors, string Error)
{
    public static XPScriptNetworkTlsResult Failed(string host, int port, long elapsed, string error) =>
        new(host, port, false, "", "", false, "", "", "", DateTime.MinValue, DateTime.MinValue, elapsed, "", error);
}

internal sealed class XPScriptNetworkInterfaceInfo
{
    public XPScriptNetworkInterfaceInfo(string name, string description, string id, string type, string status, long speed, string macAddress,
        string[] addresses, string[] gateways, string[] dnsServers, bool supportsIPv4, bool supportsIPv6)
    {
        Name = name; Description = description; Id = id; Type = type; Status = status; Speed = speed; MacAddress = macAddress;
        _addresses = addresses; _gateways = gateways; _dnsServers = dnsServers; SupportsIPv4 = supportsIPv4; SupportsIPv6 = supportsIPv6;
    }
    private readonly string[] _addresses;
    private readonly string[] _gateways;
    private readonly string[] _dnsServers;
    public string Name { get; }
    public string Description { get; }
    public string Id { get; }
    public string Type { get; }
    public string Status { get; }
    public long Speed { get; }
    public string MacAddress { get; }
    public LSArray Addresses => XPScriptNetworkTools.PackStrings(_addresses);
    public LSArray Gateways => XPScriptNetworkTools.PackStrings(_gateways);
    public LSArray DnsServers => XPScriptNetworkTools.PackStrings(_dnsServers);
    public bool SupportsIPv4 { get; }
    public bool SupportsIPv6 { get; }
}

internal sealed record XPScriptNetworkEndpointInfo(string Protocol, string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort, string State);
""";
}
