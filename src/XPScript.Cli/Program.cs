using System.Net;
using System.Reflection;
using XPScript.Web.Compiler;
using XPScript.Web.FastCgi;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    WriteHelp();
    return 0;
}

try
{
    return args[0].ToLowerInvariant() switch
    {
        "web" => await RunWebAsync(args[1..]),
        "fastcgi" => await RunFastCgiAsync(args[1..]),
        _ => Fail("Unknown command: " + args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}

static async Task<int> RunWebAsync(string[] commandArgs)
{
    var root = RequireRoot(commandArgs);
    var address = IPAddress.Loopback;
    var port = 8080;
    var allowedHosts = new List<string>();

    for (var i = 0; i < commandArgs.Length; i++)
    {
        switch (commandArgs[i])
        {
            case "--root":
                i++;
                break;
            case "--address":
            case "--bind":
                address = ParseAddress(RequireValue(commandArgs, ref i));
                break;
            case "--port":
                port = ParsePort(RequireValue(commandArgs, ref i), allowZero: true);
                break;
            case "--host":
            case "--allowed-host":
                allowedHosts.Add(RequireValue(commandArgs, ref i));
                break;
            default:
                throw new ArgumentException("Unknown web argument: " + commandArgs[i]);
        }
    }

    var defaults = new XpsKestrelOptions();
    var options = new XpsKestrelOptions
    {
        Address = address,
        Port = port,
        AllowedHosts = allowedHosts.Count > 0 ? allowedHosts.AsReadOnly() : defaults.AllowedHosts
    };
    options.Validate();

    await using var dispatcher = new XpsWebDispatcher(root);
    var server = CreateServerInfo(root, XpsWebHostingMode.Kestrel, address.ToString(), port);
    var app = XpsKestrelAdapter.Build(options, server, dispatcher);

    using var shutdown = CreateShutdownToken();
    Console.WriteLine($"XPScript web root: {root}");
    Console.WriteLine($"Listening: http://{FormatAddress(address)}:{port}");
    if (allowedHosts.Count == 0 && !IPAddress.IsLoopback(address))
        Console.WriteLine("Allowed hosts remain loopback-only. Use --host for external Host values.");
    await app.RunAsync(shutdown.Token);
    return 0;
}

static async Task<int> RunFastCgiAsync(string[] commandArgs)
{
    var root = RequireRoot(commandArgs);
    var address = IPAddress.Loopback;
    var port = 9000;
    string? unixSocket = null;

    for (var i = 0; i < commandArgs.Length; i++)
    {
        switch (commandArgs[i])
        {
            case "--root":
                i++;
                break;
            case "--listen":
            {
                var endpoint = RequireValue(commandArgs, ref i);
                ParseEndpoint(endpoint, out address, out port);
                break;
            }
            case "--address":
            case "--bind":
                address = ParseAddress(RequireValue(commandArgs, ref i));
                break;
            case "--port":
                port = ParsePort(RequireValue(commandArgs, ref i), allowZero: true);
                break;
            case "--unix-socket":
                unixSocket = Path.GetFullPath(RequireValue(commandArgs, ref i));
                break;
            default:
                throw new ArgumentException("Unknown fastcgi argument: " + commandArgs[i]);
        }
    }

    if (unixSocket is not null && OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("--unix-socket is supported only on Linux and macOS.");

    await using var dispatcher = new XpsWebDispatcher(root);
    var options = new XpsFastCgiOptions { Address = address, Port = port };
    var server = CreateServerInfo(root, XpsWebHostingMode.FastCgi, unixSocket ?? address.ToString(), unixSocket is null ? port : null);
    await using var adapter = new XpsFastCgiAdapter(options, server, dispatcher);
    using var shutdown = CreateShutdownToken();

    if (unixSocket is not null)
    {
        await using var listener = new XpsFastCgiUnixSocketListener(adapter, new XpsFastCgiUnixSocketOptions
        {
            SocketPath = unixSocket
        });
        await listener.StartAsync(shutdown.Token);
        Console.WriteLine($"XPScript FastCGI root: {root}");
        Console.WriteLine($"Listening Unix socket: {unixSocket}");
        await WaitForShutdownAsync(shutdown.Token);
        await listener.StopAsync();
        return 0;
    }

    await adapter.StartAsync(shutdown.Token);
    var endpoint = adapter.LocalEndpoint;
    Console.WriteLine($"XPScript FastCGI root: {root}");
    Console.WriteLine($"Listening: {endpoint?.Address}:{endpoint?.Port}");
    await WaitForShutdownAsync(shutdown.Token);
    await adapter.StopAsync();
    return 0;
}

static string RequireRoot(string[] commandArgs)
{
    for (var i = 0; i < commandArgs.Length; i++)
    {
        if (commandArgs[i] != "--root") continue;
        if (i + 1 >= commandArgs.Length) throw new ArgumentException("--root requires a directory path.");
        var root = Path.GetFullPath(commandArgs[i + 1]);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Web root does not exist: " + root);
        return root;
    }
    throw new ArgumentException("--root is required.");
}

static string RequireValue(string[] values, ref int index)
{
    if (++index >= values.Length) throw new ArgumentException(values[index - 1] + " requires a value.");
    return values[index];
}

static IPAddress ParseAddress(string value)
{
    if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
    if (!IPAddress.TryParse(value, out var address))
        throw new ArgumentException("Address must be an IP address or localhost: " + value);
    return address;
}

static int ParsePort(string value, bool allowZero)
{
    if (!int.TryParse(value, out var port) || port < (allowZero ? 0 : 1) || port > 65535)
        throw new ArgumentException("Port must be between " + (allowZero ? "0" : "1") + " and 65535.");
    return port;
}

static void ParseEndpoint(string value, out IPAddress address, out int port)
{
    var separator = value.LastIndexOf(':');
    if (separator <= 0 || separator == value.Length - 1)
        throw new ArgumentException("--listen must use ADDRESS:PORT, for example 127.0.0.1:9000.");
    address = ParseAddress(value[..separator]);
    port = ParsePort(value[(separator + 1)..], allowZero: true);
}

static XpsServerInfo CreateServerInfo(string root, XpsWebHostingMode mode, string? address, int? port)
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    var siteId = "cli:" + root;
    return new XpsServerInfo(siteId, root, mode, DateTimeOffset.UtcNow, version, address, port);
}

static CancellationTokenSource CreateShutdownToken()
{
    var source = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        if (!source.IsCancellationRequested) source.Cancel();
    };
    return source;
}

static async Task WaitForShutdownAsync(CancellationToken cancellationToken)
{
    try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
}

static string FormatAddress(IPAddress address) => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
    ? "[" + address + "]"
    : address.ToString();

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    Console.Error.WriteLine("Use xpscript --help for usage.");
    return 1;
}

static void WriteHelp()
{
    Console.WriteLine("""
XPScript Web Host
(c) xpagedeveloper.com 2026

Usage:
  xpscript web --root DIR [--address IP] [--port PORT] [--host HOST ...]
  xpscript fastcgi --root DIR [--listen ADDRESS:PORT]
  xpscript fastcgi --root DIR --unix-socket PATH

Examples:
  xpscript web --root ./site
  xpscript web --root ./site --address 0.0.0.0 --port 8080 --host www.example.com
  xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
  xpscript fastcgi --root /srv/xpsite --unix-socket /run/xpscript/site.sock

Security defaults:
  Kestrel binds to loopback by default.
  Kestrel accepts loopback Host values by default. Add --host explicitly for external host names.
  FastCGI binds to 127.0.0.1:9000 by default.
  Unix-domain sockets are supported on Linux and macOS only.
""");
}
