using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var parent = Path.Combine(Path.GetTempPath(), "xps web root smoke " + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "web");
Directory.CreateDirectory(root);
await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("ROOT-PATH-OK")
End Sub
""");

try
{
    var cliDll = FindCliDll();
    await VerifyTrailingSeparatorAsync(cliDll, root);
    await VerifyLiteralQuotesAsync(cliDll, root);
    await VerifySimilarFolderSuggestionAsync(cliDll, parent, root);
    Console.WriteLine("WEB-ROOT-PATH-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task VerifyTrailingSeparatorAsync(string cliDll, string root)
{
    var rootWithSeparator = root + Path.DirectorySeparatorChar;
    await VerifyServingAsync(cliDll, rootWithSeparator);
}

static async Task VerifyLiteralQuotesAsync(string cliDll, string root)
{
    await VerifyServingAsync(cliDll, "\"" + root + "\"");
}

static async Task VerifyServingAsync(string cliDll, string rootArgument)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", rootArgument, "--port", port.ToString()]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || body != "ROOT-PATH-OK")
            throw new Exception($"Root path regression returned {(int)response.StatusCode} body={body}");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifySimilarFolderSuggestionAsync(string cliDll, string parent, string existingRoot)
{
    var missing = Path.Combine(parent, "webb");
    var result = await RunShortAsync(cliDll, ["web", "--root", missing]);
    if (result.ExitCode == 0)
        throw new Exception("Missing web root unexpectedly succeeded.");
    if (!result.Stderr.Contains("Web root does not exist:", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Missing web root error was not reported. stderr=" + result.Stderr);
    if (!result.Stderr.Contains("Did you mean:", StringComparison.OrdinalIgnoreCase) ||
        !result.Stderr.Contains(existingRoot, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Similar sibling directory was not suggested. stderr=" + result.Stderr);
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
