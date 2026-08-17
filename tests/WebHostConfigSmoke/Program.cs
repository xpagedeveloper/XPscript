using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-config-" + Guid.NewGuid().ToString("N"));
var configDir = Path.Combine(parent, "config");
var siteDir = Path.Combine(configDir, "site");
Directory.CreateDirectory(siteDir);
await File.WriteAllTextAsync(Path.Combine(siteDir, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("INDEX-OK")
End Sub
""");
await File.WriteAllTextAsync(Path.Combine(siteDir, "home.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("HOME-OK")
End Sub
""");
Directory.CreateDirectory(Path.Combine(siteDir, "assets"));
await File.WriteAllTextAsync(Path.Combine(siteDir, "assets", "app.css"), "body{margin:0}");

var cliDll = FindCliDll();
var automaticConfig = Path.Combine(Path.GetDirectoryName(cliDll)!, "web.cfg");

try
{
    await VerifyExplicitWebConfigAsync(cliDll, configDir);
    await VerifyCliOverrideAsync(cliDll, configDir);
    await VerifyFastCgiConfigAsync(cliDll, configDir);
    await VerifyInvalidConfigAsync(cliDll, configDir);
    await VerifyMissingConfigAsync(cliDll, configDir);
    await VerifyAutomaticConfigAsync(cliDll, configDir, automaticConfig);
    Console.WriteLine("WEB-HOST-CONFIG-SMOKE=OK");
}
finally
{
    try { if (File.Exists(automaticConfig)) File.Delete(automaticConfig); } catch { }
    try { Directory.Delete(parent, recursive: true); } catch { }
}

static async Task VerifyExplicitWebConfigAsync(string cliDll, string configDir)
{
    var port = GetFreePort();
    var path = Path.Combine(configDir, "explicit.cfg");
    await WriteConfigAsync(path, new
    {
        web = new
        {
            root = "site",
            defaultDocument = "home.xps",
            address = "127.0.0.1",
            port,
            allowedHosts = new[] { "127.0.0.1", "localhost" },
            protocols = "http1",
            health = true,
            metrics = true,
            sessions = true,
            sessionCookie = "CFGSESSION",
            sessionTimeoutSeconds = 600,
            sessionSameSite = "Strict",
            sessionSecure = false,
            operationalExternal = false,
            structuredLog = "logs/requests.jsonl",
            staticFiles = true,
            staticMaxBytes = 4096
        }
    });

    using var process = Start(cliDll, ["web", "--config", path]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || body != "HOME-OK")
            throw new Exception($"Explicit config did not select home.xps. Status={(int)response.StatusCode} body={body}");

        using var staticResponse = await client.GetAsync($"http://127.0.0.1:{port}/assets/app.css");
        if (!staticResponse.IsSuccessStatusCode || await staticResponse.Content.ReadAsStringAsync() != "body{margin:0}")
            throw new Exception("Static-file config was not applied.");

        using var sessionResponse = await client.GetAsync($"http://127.0.0.1:{port}/");
        if (!process.HasExited && !File.Exists(Path.Combine(configDir, "logs", "requests.jsonl")))
            throw new Exception("Structured log path was not resolved relative to the config directory.");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyCliOverrideAsync(string cliDll, string configDir)
{
    var configPort = GetFreePort();
    var cliPort = GetFreePort();
    var path = Path.Combine(configDir, "override.cfg");
    await WriteConfigAsync(path, new
    {
        web = new
        {
            root = "site",
            defaultDocument = "home.xps",
            address = "127.0.0.1",
            port = configPort
        }
    });

    using var process = Start(cliDll, ["web", "--config", path, "--port", cliPort.ToString(), "--default-document", "index.xps"]);
    try
    {
        await WaitForTcpAsync(cliPort, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var body = await client.GetStringAsync($"http://127.0.0.1:{cliPort}/");
        if (body != "INDEX-OK") throw new Exception("CLI values did not override config values.");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyFastCgiConfigAsync(string cliDll, string configDir)
{
    var port = GetFreePort();
    var path = Path.Combine(configDir, "fastcgi.cfg");
    await WriteConfigAsync(path, new
    {
        fastCgi = new
        {
            root = "site",
            defaultDocument = "home.xps",
            listen = $"127.0.0.1:{port}"
        }
    });

    using var process = Start(cliDll, ["fastcgi", "--config", path]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        if (process.HasExited) throw new Exception("FastCGI config host exited after opening listener.");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyInvalidConfigAsync(string cliDll, string configDir)
{
    var path = Path.Combine(configDir, "invalid.cfg");
    await File.WriteAllTextAsync(path, "{\"web\":{\"root\":\"site\",\"porrt\":8080}}");
    var result = await RunShortAsync(cliDll, ["web", "--config", path]);
    if (result.ExitCode == 0 || !result.Stderr.Contains("Invalid web host config", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Unknown config property was not rejected. stderr=" + result.Stderr);

    var conflict = Path.Combine(configDir, "fastcgi-conflict.cfg");
    await WriteConfigAsync(conflict, new
    {
        fastCgi = new { root = "site", listen = "127.0.0.1:9000", port = 9001 }
    });
    result = await RunShortAsync(cliDll, ["fastcgi", "--config", conflict]);
    if (result.ExitCode == 0 || !result.Stderr.Contains("cannot be combined", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Conflicting FastCGI config was not rejected.");
}

static async Task VerifyMissingConfigAsync(string cliDll, string configDir)
{
    var missing = Path.Combine(configDir, "missing.cfg");
    var result = await RunShortAsync(cliDll, ["web", "--config", missing]);
    if (result.ExitCode == 0 || !result.Stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Missing explicit config file was not rejected.");
}

static async Task VerifyAutomaticConfigAsync(string cliDll, string configDir, string automaticConfig)
{
    var port = GetFreePort();
    await WriteConfigAsync(automaticConfig, new
    {
        web = new
        {
            root = Path.Combine(configDir, "site"),
            defaultDocument = "index.xps",
            address = "127.0.0.1",
            port
        }
    });

    using var process = Start(cliDll, ["web"]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var body = await client.GetStringAsync($"http://127.0.0.1:{port}/");
        if (body != "INDEX-OK") throw new Exception("Automatic web.cfg beside executable was not loaded.");
    }
    finally
    {
        Stop(process);
        File.Delete(automaticConfig);
    }
}

static Task WriteConfigAsync(string path, object value)
{
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}

static Process Start(string cliDll, IReadOnlyList<string> arguments)
{
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    start.ArgumentList.Add(cliDll);
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    return Process.Start(start) ?? throw new Exception("Unable to start XPScript CLI.");
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunShortAsync(string cliDll, IReadOnlyList<string> arguments)
{
    using var process = Start(cliDll, arguments);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    return (process.ExitCode, await stdoutTask, await stderrTask);
}

static async Task WaitForTcpAsync(int port, Process process, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    Exception? last = null;
    while (DateTime.UtcNow < deadline)
    {
        if (process.HasExited)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new Exception($"XPScript host exited early with {process.ExitCode}. stdout={stdout} stderr={stderr}");
        }
        try
        {
            using var client = new TcpClient();
            using var attempt = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await client.ConnectAsync(IPAddress.Loopback, port, attempt.Token);
            return;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            last = ex;
            await Task.Delay(100);
        }
    }
    throw new TimeoutException("Host did not listen on port " + port + ". Last error: " + last?.Message);
}

static int GetFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static void Stop(Process process)
{
    if (process.HasExited) return;
    process.Kill(entireProcessTree: true);
    process.WaitForExit(10_000);
}

static string FindCliDll()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var solution = Path.Combine(current.FullName, "XPScriptCompiler.slnx");
        if (File.Exists(solution))
        {
            var cli = Path.Combine(current.FullName, "src", "XPScript.Cli", "bin", "Release", "net10.0", "xpscript.dll");
            if (!File.Exists(cli)) throw new Exception("Built XPScript CLI was not found: " + cli);
            return cli;
        }
        current = current.Parent;
    }
    throw new Exception("Unable to locate repository root.");
}
