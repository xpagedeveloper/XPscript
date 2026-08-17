using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-cli-session-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
await File.WriteAllTextAsync(Path.Combine(root, "session.xps"), """
[Anonymous]
[Get]
Sub SetValue()
    Session.Set("value", "persisted")
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Session.Id)
End Sub

[Anonymous]
[Get]
Sub GetValue()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Session.Get("value"))
End Sub
""");

try
{
    var cliDll = FindCliDll();
    await VerifyHelpAsync(cliDll);
    await VerifySessionsDisabledByDefaultAsync(cliDll, root);
    await VerifySessionsAsync(cliDll, root);
    Console.WriteLine("WEB-CLI-SESSION-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task VerifyHelpAsync(string cliDll)
{
    var result = await RunShortAsync(cliDll, ["--help"]);
    if (result.ExitCode != 0 || !result.Stdout.Contains("--sessions", StringComparison.Ordinal))
        throw new Exception("xpscript --help did not expose --sessions.");
}

static async Task VerifySessionsDisabledByDefaultAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", root, "--port", port.ToString()]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/session/SetValue");
        if (response.StatusCode != HttpStatusCode.InternalServerError)
            throw new Exception($"Session unexpectedly worked without --sessions. Status={(int)response.StatusCode}");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifySessionsAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", root, "--port", port.ToString(), "--sessions"]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookies };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        using var setResponse = await client.GetAsync($"http://127.0.0.1:{port}/session/SetValue");
        var sessionId = await setResponse.Content.ReadAsStringAsync();
        if (!setResponse.IsSuccessStatusCode || string.IsNullOrWhiteSpace(sessionId))
            throw new Exception($"Session SetValue failed with {(int)setResponse.StatusCode} body={sessionId}");
        var storedCookies = cookies.GetCookies(new Uri($"http://127.0.0.1:{port}/"));
        if (storedCookies["XPSID"] is null)
            throw new Exception("CLI session host did not issue XPSID cookie.");

        using var getResponse = await client.GetAsync($"http://127.0.0.1:{port}/session/GetValue");
        var value = await getResponse.Content.ReadAsStringAsync();
        if (!getResponse.IsSuccessStatusCode || value != "persisted")
            throw new Exception($"Session state did not survive the next request. Status={(int)getResponse.StatusCode} body={value}");
    }
    finally
    {
        Stop(process);
    }
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
