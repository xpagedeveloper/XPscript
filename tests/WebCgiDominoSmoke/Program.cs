using System.Diagnostics;

var parent = Path.Combine(Path.GetTempPath(), "xps-cgi-domino-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
var cgiDir = Path.Combine(parent, "domino", "cgi-bin");
Directory.CreateDirectory(root);
Directory.CreateDirectory(cgiDir);

var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.SetHeader("X-Domino-CGI", "ok")
    Response.Write(Request.Path)
    Response.Write("|")
    Response.Write(Request.QueryFirst("name"))
End Sub
""");

try
{
    var ok = await RunAsync(root, cgiDir, "/index.xps", "name=Domino");
    if (ok.ExitCode != 0)
        throw new Exception($"Domino-style CGI request failed with exit {ok.ExitCode}. stderr={ok.Stderr} stdout={ok.Stdout}");
    if (!ok.Stdout.StartsWith("Status: 200 OK\r\n", StringComparison.Ordinal))
        throw new Exception("Domino-style CGI request did not return HTTP 200: " + ok.Stdout);
    if (!ok.Stdout.Contains("X-Domino-CGI: ok\r\n", StringComparison.Ordinal))
        throw new Exception("Domino-style CGI request lost the XPScript response header.");
    if (!ok.Stdout.EndsWith("/index.xps|Domino", StringComparison.Ordinal))
        throw new Exception("Domino PATH_INFO did not map to the XPScript route: " + ok.Stdout);

    var escape = await RunAsync(root, cgiDir, "/../outside.xps", string.Empty);
    if (escape.ExitCode == 0)
        throw new Exception("Domino-style CGI accepted PATH_INFO traversal outside the XPScript root.");
    if (!escape.Stdout.StartsWith("Status: 400 Bad Request\r\n", StringComparison.Ordinal))
        throw new Exception("Domino PATH_INFO traversal did not fail as a generic bad request: " + escape.Stdout);

    Console.WriteLine("WEB-CGI-DOMINO-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
    string root,
    string cgiDir,
    string pathInfo,
    string query)
{
    var repoRoot = FindRepoRoot();
    var cgiDll = Path.Combine(repoRoot, "src", "XPScript.Web.Cgi", "bin", "Release", "net10.0", "XPScript.Web.Cgi.dll");
    if (!File.Exists(cgiDll)) throw new Exception("Built CGI host assembly was not found: " + cgiDll);

    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    start.ArgumentList.Add(cgiDll);
    start.Environment["XPSCRIPT_WEB_ROOT"] = root;
    start.Environment["XPSCRIPT_SITE_ID"] = "domino-cgi-smoke";
    start.Environment["REQUEST_METHOD"] = "GET";
    start.Environment["SCRIPT_NAME"] = "/xps-bin/XPScript.Web.Cgi.exe";
    start.Environment["SCRIPT_FILENAME"] = Path.Combine(cgiDir, "XPScript.Web.Cgi.exe");
    start.Environment["PATH_INFO"] = pathInfo;
    start.Environment["PATH_TRANSLATED"] = Path.Combine(root, pathInfo.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
    start.Environment["QUERY_STRING"] = query;
    start.Environment["SERVER_NAME"] = "domino.example.test";
    start.Environment["SERVER_PROTOCOL"] = "HTTP/1.1";
    start.Environment["REMOTE_ADDR"] = "127.0.0.1";
    start.Environment["HTTPS"] = "off";

    using var process = Process.Start(start) ?? throw new Exception("Failed to start CGI host.");
    process.StandardInput.Close();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    return (process.ExitCode, await stdoutTask, await stderrTask);
}

static string FindRepoRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "XPScriptCompiler.slnx"))) return current.FullName;
        current = current.Parent;
    }
    throw new Exception("Unable to locate repository root.");
}
