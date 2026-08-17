using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-cli-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(Path.Combine(root, "assets"));
await File.WriteAllTextAsync(Path.Combine(root, "assets", "site.css"), "body{color:black}");
await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.SetHeader("X-XPScript-CLI", "web")
    Response.Write("CLI-WEB-OK")
End Sub
""");
await File.WriteAllTextAsync(Path.Combine(root, "home.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("HOME-DOC-OK")
End Sub
""");

try
{
    var cliDll = FindCliDll();
    await VerifyHelpAsync(cliDll);
    await VerifyHttpsValidationAsync(cliDll, root);
    await VerifyStaticValidationAsync(cliDll, root);
    await VerifyDefaultDocumentValidationAsync(cliDll, root);
    await VerifyWebAsync(cliDll, root);
    await VerifyDefaultDocumentAsync(cliDll, root);
    await VerifyStaticWebAsync(cliDll, root);
    await VerifyFastCgiAsync(cliDll, root);
    Console.WriteLine("WEB-CLI-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task VerifyHelpAsync(string cliDll)
{
    var result = await RunShortAsync(cliDll, ["--help"]);
    if (result.ExitCode != 0 || !result.Stdout.Contains("xpscript web --root DIR", StringComparison.Ordinal))
        throw new Exception("xpscript --help did not expose the web command. stdout=" + result.Stdout + " stderr=" + result.Stderr);
    if (!result.Stdout.Contains("--default-document FILE.xps", StringComparison.Ordinal))
        throw new Exception("xpscript --help did not expose default-document configuration.");
    if (!result.Stdout.Contains("--https-cert FILE", StringComparison.Ordinal) ||
        !result.Stdout.Contains("--https-cert-password-env NAME", StringComparison.Ordinal) ||
        !result.Stdout.Contains("--protocols http1|http2|http1+2", StringComparison.Ordinal))
        throw new Exception("xpscript --help did not expose the HTTPS/protocol controls.");
    if (!result.Stdout.Contains("--static-files", StringComparison.Ordinal) ||
        !result.Stdout.Contains("--static-max-bytes BYTES", StringComparison.Ordinal))
        throw new Exception("xpscript --help did not expose static-file controls.");
}

static async Task VerifyHttpsValidationAsync(string cliDll, string root)
{
    var missingEnvironment = "XPS_TEST_MISSING_" + Guid.NewGuid().ToString("N");
    var passwordResult = await RunShortAsync(cliDll,
    [
        "web", "--root", root,
        "--https-cert", Path.Combine(root, "missing.pfx"),
        "--https-cert-password-env", missingEnvironment
    ]);
    if (passwordResult.ExitCode == 0 || !passwordResult.Stderr.Contains("environment variable is not set", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Missing HTTPS certificate password environment variable was not rejected.");

    var protocolResult = await RunShortAsync(cliDll, ["web", "--root", root, "--protocols", "http3"]);
    if (protocolResult.ExitCode == 0 || !protocolResult.Stderr.Contains("--protocols must be", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Unsupported CLI HTTP protocol value was not rejected.");
}

static async Task VerifyStaticValidationAsync(string cliDll, string root)
{
    var missingEnable = await RunShortAsync(cliDll, ["web", "--root", root, "--static-max-bytes", "1024"]);
    if (missingEnable.ExitCode == 0 || !missingEnable.Stderr.Contains("requires --static-files", StringComparison.OrdinalIgnoreCase))
        throw new Exception("--static-max-bytes without --static-files was not rejected.");

    var invalidLimit = await RunShortAsync(cliDll, ["web", "--root", root, "--static-files", "--static-max-bytes", "0"]);
    if (invalidLimit.ExitCode == 0 || !invalidLimit.Stderr.Contains("between 1 and 1073741824", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Invalid static-file byte limit was not rejected.");
}

static async Task VerifyDefaultDocumentValidationAsync(string cliDll, string root)
{
    var invalid = await RunShortAsync(cliDll, ["web", "--root", root, "--default-document", "../outside.xps"]);
    if (invalid.ExitCode == 0 || !invalid.Stderr.Contains("Default document must be a single .xps filename", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Unsafe default document was not rejected.");
}

static async Task VerifyWebAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", root, "--port", port.ToString()]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || body != "CLI-WEB-OK")
            throw new Exception($"xpscript web returned {(int)response.StatusCode} body={body}");
        if (!response.Headers.TryGetValues("X-XPScript-CLI", out var values) || values.Single() != "web")
            throw new Exception("xpscript web lost the XPScript response header.");

        using var staticDisabled = await client.GetAsync($"http://127.0.0.1:{port}/assets/site.css");
        if (await staticDisabled.Content.ReadAsStringAsync() == "body{color:black}")
            throw new Exception("Static files were served without --static-files.");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyDefaultDocumentAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", root, "--default-document", "home.xps", "--port", port.ToString()]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || body != "HOME-DOC-OK")
            throw new Exception($"Configured default document returned {(int)response.StatusCode} body={body}");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyStaticWebAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["web", "--root", root, "--port", port.ToString(), "--static-files", "--static-max-bytes", "1024"]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/assets/site.css");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || body != "body{color:black}")
            throw new Exception($"xpscript web --static-files returned {(int)response.StatusCode} body={body}");
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/css", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Static CLI response did not use CSS MIME type.");
    }
    finally
    {
        Stop(process);
    }
}

static async Task VerifyFastCgiAsync(string cliDll, string root)
{
    var port = GetFreePort();
    using var process = Start(cliDll, ["fastcgi", "--root", root, "--default-document", "home.xps", "--listen", $"127.0.0.1:{port}"]);
    try
    {
        await WaitForTcpAsync(port, process, TimeSpan.FromSeconds(30));
        if (process.HasExited)
            throw new Exception("xpscript fastcgi exited after opening the listener.");
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
