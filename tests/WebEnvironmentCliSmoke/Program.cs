using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "xps-web-env-cli-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Server.Environment)
End Sub
""");

try
{
    var cliDll = FindCliDll();
    await VerifyEnvironmentAsync(cliDll, root, null, "Production");
    await VerifyEnvironmentAsync(cliDll, root, "Development", "Development");
    await VerifyConfigEnvironmentAsync(cliDll, root);
    await VerifyCliOverrideAsync(cliDll, root);
    await VerifyInvalidEnvironmentAsync(cliDll, root);
    Console.WriteLine("WEB_ENVIRONMENT_CLI_OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

static async Task VerifyEnvironmentAsync(string cliDll, string root, string? environment, string expected)
{
    var port = GetFreePort();
    var args = new List<string> { "web", "--root", root, "--port", port.ToString() };
    if (environment is not null)
    {
        args.Add("--environment");
        args.Add(environment);
    }

    using var process = Start(cliDll, args);
    try
    {
        await WaitForTcpAsync(port, process);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var value = await client.GetStringAsync($"http://127.0.0.1:{port}/");
        if (!string.Equals(value, expected, StringComparison.Ordinal))
            throw new Exception($"Expected environment {expected}, got {value}.");
    }
    finally { Stop(process); }
}

static async Task VerifyConfigEnvironmentAsync(string cliDll, string root)
{
    var port = GetFreePort();
    var config = Path.Combine(root, "development.cfg");
    await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new
    {
        web = new { root, port, environment = "Development" }
    }));

    using var process = Start(cliDll, ["web", "--config", config]);
    try
    {
        await WaitForTcpAsync(port, process);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var value = await client.GetStringAsync($"http://127.0.0.1:{port}/");
        if (value != "Development") throw new Exception("web.cfg environment was not applied.");
    }
    finally { Stop(process); }
}

static async Task VerifyCliOverrideAsync(string cliDll, string root)
{
    var port = GetFreePort();
    var config = Path.Combine(root, "override.cfg");
    await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new
    {
        web = new { root, port, environment = "Development" }
    }));

    using var process = Start(cliDll, ["web", "--config", config, "--environment", "Production"]);
    try
    {
        await WaitForTcpAsync(port, process);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var value = await client.GetStringAsync($"http://127.0.0.1:{port}/");
        if (value != "Production") throw new Exception("CLI environment did not override web.cfg.");
    }
    finally { Stop(process); }
}

static async Task VerifyInvalidEnvironmentAsync(string cliDll, string root)
{
    var result = await RunShortAsync(cliDll, ["web", "--root", root, "--environment", "Staging"]);
    if (result.ExitCode == 0 || !result.Stderr.Contains("Production or Development", StringComparison.Ordinal))
        throw new Exception("Invalid environment was not rejected with a bounded diagnostic.");
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
    var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    return (process.ExitCode, await stdout, await stderr);
}

static async Task WaitForTcpAsync(int port, Process process)
{
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
    while (DateTime.UtcNow < deadline)
    {
        if (process.HasExited)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Host exited early with {process.ExitCode}. stdout={stdout} stderr={stderr}");
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
            await Task.Delay(100);
        }
    }
    throw new TimeoutException("Host did not listen on port " + port + ".");
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
        if (File.Exists(Path.Combine(current.FullName, "XPScriptCompiler.slnx")))
        {
            var cli = Path.Combine(current.FullName, "src", "XPScript.Cli", "bin", "Release", "net10.0", "xpscript.dll");
            if (!File.Exists(cli)) throw new Exception("Built XPScript CLI was not found: " + cli);
            return cli;
        }
        current = current.Parent;
    }
    throw new Exception("Unable to locate repository root.");
}
