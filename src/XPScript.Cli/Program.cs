using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using XPScript.Compiler;
using XPScript.Web.Compiler;
using XPScript.Web.FastCgi;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

ConfigureRuntimeDiagnosticEnvironment(args);

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    WriteHelp();
    return 0;
}

try
{
    if (args[0].EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
        return await XPScriptCompilerCommandLine.CompileAsync(args);

    return args[0].ToLowerInvariant() switch
    {
        "compile" => await XPScriptCompilerCommandLine.CompileAsync(args[1..]),
        "run" => await XPScriptCompilerCommandLine.RunScriptAsync(args),
        "new" => XpsScaffolder.Run(args[1..]),
        "openapi" => XPScript.Cli.XpsOpenApiCommand.Run(args[1..]),
        "service" => await XPScript.Cli.ServiceCommand.RunAsync(args[1..]),
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

static void ConfigureRuntimeDiagnosticEnvironment(string[] arguments)
{
    if (arguments.Length == 0 || !arguments[0].Equals("run", StringComparison.OrdinalIgnoreCase))
        return;

    var separator = Array.IndexOf(arguments, "--");
    var optionCount = separator < 0 ? arguments.Length : separator;
    var explicitInfo = arguments.Take(optionCount).Any(value => value.Equals("--info", StringComparison.OrdinalIgnoreCase));
    Environment.SetEnvironmentVariable("XPSCRIPT_RUNTIME_INFO", explicitInfo ? "1" : null);
}

static async Task<int> RunWebAsync(string[] commandArgs)
{
    commandArgs = XpsHostConfig.Apply("web", commandArgs);
    var root = RequireRoot(commandArgs);
    var address = IPAddress.Loopback;
    var port = 8080;
    var defaultDocument = "index.xps";
    var allowedHosts = new List<string>();
    var enableHealth = false;
    var enableMetrics = false;
    var enableSessions = false;
    var sessionOptionSpecified = false;
    var sessionCookieName = "XPSID";
    var sessionIdleSeconds = 20 * 60;
    var sessionSameSite = "Lax";
    var sessionSecure = false;
    var operationalExternal = false;
    var enableStaticFiles = false;
    long? staticMaxBytes = null;
    string? structuredLogPath = null;
    string? httpsCertificatePath = null;
    string? httpsCertificatePasswordEnvironment = null;
    var protocols = HttpProtocols.Http1AndHttp2;

    for (var i = 0; i < commandArgs.Length; i++)
    {
        switch (commandArgs[i])
        {
            case "--root":
                i++;
                break;
            case "--default-document":
                defaultDocument = RequireValue(commandArgs, ref i);
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
            case "--https-cert":
                httpsCertificatePath = Path.GetFullPath(RequireValue(commandArgs, ref i));
                break;
            case "--https-cert-password-env":
                httpsCertificatePasswordEnvironment = RequireValue(commandArgs, ref i);
                break;
            case "--protocols":
                protocols = ParseHttpProtocols(RequireValue(commandArgs, ref i));
                break;
            case "--health":
                enableHealth = true;
                break;
            case "--metrics":
                enableMetrics = true;
                break;
            case "--sessions":
                enableSessions = true;
                break;
            case "--session-cookie":
                sessionOptionSpecified = true;
                sessionCookieName = RequireValue(commandArgs, ref i);
                break;
            case "--session-timeout-seconds":
                sessionOptionSpecified = true;
                sessionIdleSeconds = ParsePositiveInt(RequireValue(commandArgs, ref i), "--session-timeout-seconds", 10, 30 * 24 * 60 * 60);
                break;
            case "--session-same-site":
                sessionOptionSpecified = true;
                sessionSameSite = RequireValue(commandArgs, ref i);
                break;
            case "--session-secure":
                sessionOptionSpecified = true;
                sessionSecure = true;
                break;
            case "--operational-external":
                operationalExternal = true;
                break;
            case "--structured-log":
                structuredLogPath = Path.GetFullPath(RequireValue(commandArgs, ref i));
                break;
            case "--static-files":
                enableStaticFiles = true;
                break;
            case "--static-max-bytes":
                staticMaxBytes = ParsePositiveLong(RequireValue(commandArgs, ref i), "--static-max-bytes");
                break;
            default:
                throw new ArgumentException("Unknown web argument: " + commandArgs[i]);
        }
    }

    if (operationalExternal && !enableHealth && !enableMetrics)
        throw new ArgumentException("--operational-external requires --health and/or --metrics.");
    if (httpsCertificatePasswordEnvironment is not null && httpsCertificatePath is null)
        throw new ArgumentException("--https-cert-password-env requires --https-cert.");
    if (staticMaxBytes is not null && !enableStaticFiles)
        throw new ArgumentException("--static-max-bytes requires --static-files.");
    if (sessionOptionSpecified && !enableSessions)
        throw new ArgumentException("Session configuration options require --sessions.");

    string? httpsCertificatePassword = null;
    if (httpsCertificatePasswordEnvironment is not null)
    {
        if (string.IsNullOrWhiteSpace(httpsCertificatePasswordEnvironment) || httpsCertificatePasswordEnvironment.IndexOfAny(['\r', '\n', '\0', '=']) >= 0)
            throw new ArgumentException("--https-cert-password-env must name one environment variable.");
        httpsCertificatePassword = Environment.GetEnvironmentVariable(httpsCertificatePasswordEnvironment)
            ?? throw new InvalidOperationException("HTTPS certificate password environment variable is not set: " + httpsCertificatePasswordEnvironment);
    }

    var defaults = new XpsKestrelOptions();
    var options = new XpsKestrelOptions
    {
        Address = address,
        Port = port,
        HttpsCertificatePath = httpsCertificatePath,
        HttpsCertificatePassword = httpsCertificatePassword,
        Protocols = protocols,
        AllowedHosts = allowedHosts.Count > 0 ? allowedHosts.AsReadOnly() : defaults.AllowedHosts,
        EnableHealthEndpoint = enableHealth,
        EnableMetricsEndpoint = enableMetrics,
        OperationalEndpointsLocalOnly = !operationalExternal,
        EnableStaticFiles = enableStaticFiles,
        MaxStaticFileBytes = staticMaxBytes ?? defaults.MaxStaticFileBytes
    };
    options.Validate();

    StreamWriter? structuredLogWriter = null;
    try
    {
        XpsWebTelemetry? telemetry = null;
        if (structuredLogPath is not null)
        {
            var logDirectory = Path.GetDirectoryName(structuredLogPath);
            if (!string.IsNullOrWhiteSpace(logDirectory)) Directory.CreateDirectory(logDirectory);
            structuredLogWriter = new StreamWriter(new FileStream(
                structuredLogPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: false)) { AutoFlush = true };
            telemetry = new XpsWebTelemetry(new XpsWebJsonLineEventSink(structuredLogWriter));
        }

        await using var dispatcher = new XpsWebDispatcher(root, defaultDocumentName: defaultDocument);
        var server = CreateServerInfo(root, XpsWebHostingMode.Kestrel, address.ToString(), port);
        var sessions = enableSessions
            ? new XpsSessionStore(new XpsSessionOptions
            {
                CookieName = sessionCookieName,
                IdleTimeout = TimeSpan.FromSeconds(sessionIdleSeconds),
                SameSite = sessionSameSite,
                RequireSecureCookie = sessionSecure
            })
            : null;
        var app = XpsKestrelAdapter.Build(options, server, dispatcher, sessions: sessions, telemetry: telemetry);

        using var shutdown = CreateShutdownToken();
        var scheme = options.HttpsEnabled ? "https" : "http";
        Console.WriteLine($"XPScript web root: {root}");
        Console.WriteLine($"Default document: {defaultDocument}");
        Console.WriteLine($"Listening: {scheme}://{FormatAddress(address)}:{port}");
        Console.WriteLine($"HTTP protocols: {FormatHttpProtocols(options.Protocols)}");
        if (options.HttpsEnabled) Console.WriteLine($"TLS certificate: {Path.GetFileName(options.HttpsCertificatePath)}");
        if (allowedHosts.Count == 0 && !IPAddress.IsLoopback(address))
            Console.WriteLine("Allowed hosts remain loopback-only. Use --host for external Host values.");
        if (enableSessions)
            Console.WriteLine($"Sessions: enabled, in-memory store, cookie {sessionCookieName}, timeout {sessionIdleSeconds}s, SameSite={sessionSameSite}, Secure={sessionSecure}");
        if (enableHealth) Console.WriteLine($"Health endpoint: {options.HealthPath} ({(options.OperationalEndpointsLocalOnly ? "loopback only" : "network accessible")})");
        if (enableMetrics) Console.WriteLine($"Metrics endpoint: {options.MetricsPath} ({(options.OperationalEndpointsLocalOnly ? "loopback only" : "network accessible")})");
        if (structuredLogPath is not null) Console.WriteLine($"Structured request log: {structuredLogPath}");
        if (options.EnableStaticFiles) Console.WriteLine($"Static files: enabled, max {options.MaxStaticFileBytes} bytes");

        await app.StartAsync(shutdown.Token);
        await WaitForShutdownAsync(shutdown.Token);
        await app.StopAsync();
        await app.DisposeAsync();
        return 0;
    }
    finally
    {
        structuredLogWriter?.Dispose();
    }
}

static async Task<int> RunFastCgiAsync(string[] commandArgs)
{
    commandArgs = XpsHostConfig.Apply("fastcgi", commandArgs);
    var root = RequireRoot(commandArgs);
    var address = IPAddress.Loopback;
    var port = 9000;
    var defaultDocument = "index.xps";
    string? unixSocket = null;

    for (var i = 0; i < commandArgs.Length; i++)
    {
        switch (commandArgs[i])
        {
            case "--root":
                i++;
                break;
            case "--default-document":
                defaultDocument = RequireValue(commandArgs, ref i);
                break;
            case "--listen":
            {
                var listenValue = RequireValue(commandArgs, ref i);
                ParseEndpoint(listenValue, out address, out port);
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

    await using var dispatcher = new XpsWebDispatcher(root, defaultDocumentName: defaultDocument);
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
        Console.WriteLine($"Default document: {defaultDocument}");
        Console.WriteLine($"Listening Unix socket: {unixSocket}");
        await WaitForShutdownAsync(shutdown.Token);
        await listener.StopAsync();
        return 0;
    }

    await adapter.StartAsync(shutdown.Token);
    var localEndpoint = adapter.LocalEndpoint;
    Console.WriteLine($"XPScript FastCGI root: {root}");
    Console.WriteLine($"Default document: {defaultDocument}");
    Console.WriteLine($"Listening: {localEndpoint?.Address}:{localEndpoint?.Port}");
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

static int ParsePositiveInt(string value, string optionName, int min, int max)
{
    if (!int.TryParse(value, out var number) || number < min || number > max)
        throw new ArgumentException($"{optionName} must be between {min} and {max}.");
    return number;
}

static long ParsePositiveLong(string value, string optionName)
{
    if (!long.TryParse(value, out var number) || number < 1 || number > 1024L * 1024L * 1024L)
        throw new ArgumentException(optionName + " must be between 1 and 1073741824 bytes.");
    return number;
}

static HttpProtocols ParseHttpProtocols(string value) => value.Trim().ToLowerInvariant() switch
{
    "http1" or "http/1" or "http/1.1" => HttpProtocols.Http1,
    "http2" or "http/2" => HttpProtocols.Http2,
    "http1+2" or "http1and2" or "http/1.1+2" => HttpProtocols.Http1AndHttp2,
    _ => throw new ArgumentException("--protocols must be http1, http2 or http1+2.")
};

static string FormatHttpProtocols(HttpProtocols protocols) => protocols switch
{
    HttpProtocols.Http1 => "HTTP/1.1",
    HttpProtocols.Http2 => "HTTP/2",
    HttpProtocols.Http1AndHttp2 => "HTTP/1.1 + HTTP/2",
    _ => protocols.ToString()
};

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
XPScript CLI
(c) xpagedeveloper.com 2026

One executable is used for compiler, runtime execution, project scaffolding and web hosting.

Usage:
  xpscript compile <source.xps> [-o output] [--runtime RID] [--framework-dependent] [--result-format text|json|xml]
  xpscript run <source.xps> [--runtime RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]
  xpscript <source.xps> [-o output] [--runtime RID] [compiler options...]
  xpscript new <rest|web|desktop> <directory>
  xpscript openapi generate <spec.yaml|spec.yml|spec.json> [-o output.xps] [--force]
  xpscript service install <compiled-service> --name NAME --display-name "DISPLAY NAME" [--start auto|manual|disabled]
  xpscript web <directory> [--default-document FILE.xps] [--address IP] [--port PORT] [--host HOST ...] [--protocols http1|http2|http1+2]
                [--https-cert FILE] [--https-cert-password-env NAME]
                [--health] [--metrics] [--sessions]
                [--session-cookie NAME] [--session-timeout-seconds SECONDS]
                [--session-same-site Strict|Lax|None] [--session-secure]
                [--structured-log FILE] [--operational-external]
                [--static-files] [--static-max-bytes BYTES]
  xpscript web [--config FILE] --root DIR [web options...]
  xpscript fastcgi [--config FILE] --root DIR [--default-document FILE.xps] [--listen ADDRESS:PORT]
  xpscript fastcgi [--config FILE] --root DIR [--default-document FILE.xps] --unix-socket PATH

Command model:
  compile  Compile an XPScript source file.
  run      Compile to an isolated temporary output and execute on the current OS/architecture.
  new      Create a REST, web or desktop starter in a required target directory. Use . for the current directory.
  openapi  Generate XPScript REST server source from OpenAPI 3.0/3.1 YAML or JSON.
  service  Install compiled XPScript services using the native service manager.
  web      Run the standalone Kestrel web runtime.
  fastcgi  Run the FastCGI web runtime.

The same xpscript executable owns all command modes. The XPScript.Compiler project provides shared compiler services and command handling.

Config:
  --config FILE loads JSON host settings from the selected file.
  Without --config, web.cfg is loaded automatically from the directory containing the xpscript executable when that file exists.
  Paths inside the config file are resolved relative to the config file directory.
  Explicit command-line values override matching values from the config file.

Scaffolding:
  A target directory is mandatory. Use . to create the starter in the current directory.
  Missing directories are created automatically.
  Existing index.xps or main.xps files are never overwritten.

Examples:
  xpscript new rest ./myapi
  xpscript new web ./mysite
  xpscript new desktop ./myapp
  xpscript new rest .
  xpscript openapi generate ./openapi.yaml
  xpscript openapi generate ./petstore.yaml -o ./generated/petstore.xps
  xpscript compile hello.xps
  xpscript compile hello.xps --runtime linux-x64 -o hello
  xpscript run hello.xps
  xpscript run hello.xps -- --runtime passed-to-script
  xpscript service install ./worker --name xps-worker --display-name "XPScript Worker" --start auto
  xpscript web ./site
  xpscript web --config ./production.cfg
  xpscript web --root ./site --sessions
  xpscript web --root ./site --default-document home.xps
  xpscript web --root ./site --static-files
  xpscript web --root ./site --health --metrics
  xpscript web --root ./site --structured-log ./logs/web.jsonl
  xpscript web --root ./site --address 0.0.0.0 --port 8080 --host www.example.com
  xpscript fastcgi --config ./production.cfg
  xpscript fastcgi --root /srv/xpsite --default-document home.xps --listen 127.0.0.1:9000
  xpscript fastcgi --root /srv/xpsite --unix-socket /run/xpscript/site.sock

Security defaults:
  Compile/run retain the existing restricted Include and source-root controls.
  Service install validates service names and refuses to overwrite an existing service with the same name.
  Kestrel binds to loopback by default.
  Kestrel accepts loopback Host values by default. Add --host explicitly for external host names.
  Sessions are disabled by default.
  Health and metrics are disabled by default.
  Static file serving is disabled by default.
  XPScript source files are never served by the static-file middleware.
  FastCGI binds to 127.0.0.1:9000 by default.
  Unix-domain sockets are supported on Linux and macOS only.
""");
}
