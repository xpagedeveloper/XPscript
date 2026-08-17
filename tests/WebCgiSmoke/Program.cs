using System.Diagnostics;
using System.Text;
using XPScript.Web.Cgi;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-cgi-smoke-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.SetHeader("X-CGI", "xpscript")
    Response.Write(Request.Method)
    Response.Write("|")
    Response.Write(Request.QueryFirst("name"))
    Response.Write("|")
    Response.Write(Server.HostingMode)
End Sub
""");

try
{
    await RunAdapterRegression(root, scriptPath);
    await RunHeadRegression(root, scriptPath);
    await RunExecutableRegression(root, scriptPath);
    await RunInvalidBodyRegression(root, scriptPath);
    Console.WriteLine("WEB-CGI-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task RunAdapterRegression(string root, string scriptPath)
{
    var environment = BaseEnvironment(root, scriptPath);
    environment["REQUEST_METHOD"] = "POST";
    environment["SCRIPT_NAME"] = "/echo.xps";
    environment["QUERY_STRING"] = "a=1";
    environment["CONTENT_TYPE"] = "text/plain";
    environment["CONTENT_LENGTH"] = "5";
    environment["HTTP_COOKIE"] = "client=abc";
    environment["HTTP_X_TEST"] = "cgi-header";

    var server = new XpsServerInfo("cgi-smoke", root, XpsWebHostingMode.Cgi, DateTimeOffset.UtcNow, "test");
    var adapter = new XpsCgiAdapter(new XpsCgiOptions(), server, new EchoHandler());
    await using var stdin = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
    await using var stdout = new MemoryStream();
    await adapter.RunAsync(stdin, stdout, environment);
    var text = Encoding.UTF8.GetString(stdout.ToArray());

    if (!text.StartsWith("Status: 201 Created\r\n", StringComparison.Ordinal))
        throw new Exception("CGI adapter did not emit a valid Status header: " + text);
    if (!text.Contains("Content-Type: text/plain; charset=utf-8\r\n", StringComparison.Ordinal))
        throw new Exception("CGI adapter did not emit Content-Type.");
    if (!text.Contains("X-Adapter: ok\r\n", StringComparison.Ordinal))
        throw new Exception("CGI adapter did not preserve response headers.");
    if (!text.EndsWith("POST|a=1|cgi-header|abc|hello", StringComparison.Ordinal))
        throw new Exception("CGI adapter request normalization failed: " + text);
}

static async Task RunHeadRegression(string root, string scriptPath)
{
    var environment = BaseEnvironment(root, scriptPath);
    environment["REQUEST_METHOD"] = "HEAD";
    environment["SCRIPT_NAME"] = "/echo.xps";

    var server = new XpsServerInfo("cgi-head", root, XpsWebHostingMode.Cgi, DateTimeOffset.UtcNow, "test");
    var adapter = new XpsCgiAdapter(new XpsCgiOptions(), server, new EchoHandler());
    await using var stdout = new MemoryStream();
    await adapter.RunAsync(Stream.Null, stdout, environment);
    var text = Encoding.UTF8.GetString(stdout.ToArray());
    var separator = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    if (separator < 0) throw new Exception("CGI HEAD response did not contain a header terminator.");
    if (text.Length != separator + 4)
        throw new Exception("CGI HEAD response unexpectedly contained a response body: " + text[(separator + 4)..]);
    if (!text.Contains("X-Adapter: ok\r\n", StringComparison.Ordinal))
        throw new Exception("CGI HEAD response lost response headers.");
}

static async Task RunExecutableRegression(string root, string scriptPath)
{
    var repoRoot = FindRepoRoot();
    var cgiDll = Path.Combine(repoRoot, "src", "XPScript.Web.Cgi", "bin", "Release", "net10.0", "XPScript.Web.Cgi.dll");
    if (!File.Exists(cgiDll)) throw new Exception("Built CGI executable assembly was not found: " + cgiDll);

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
    start.Environment["XPSCRIPT_SITE_ID"] = "cgi-process-smoke";
    start.Environment["REQUEST_METHOD"] = "GET";
    start.Environment["SCRIPT_NAME"] = "/index.xps";
    start.Environment["SCRIPT_FILENAME"] = scriptPath;
    start.Environment["QUERY_STRING"] = "name=Fredrik+Norling";
    start.Environment["SERVER_NAME"] = "localhost";
    start.Environment["SERVER_PROTOCOL"] = "HTTP/1.1";
    start.Environment["REMOTE_ADDR"] = "127.0.0.1";
    start.Environment["HTTPS"] = "off";

    using var process = Process.Start(start) ?? throw new Exception("Failed to start CGI executable.");
    process.StandardInput.Close();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    var stdout = await stdoutTask;
    var stderr = await stderrTask;

    if (process.ExitCode != 0)
        throw new Exception($"CGI executable failed with exit {process.ExitCode}. stderr={stderr} stdout={stdout}");
    if (!stdout.StartsWith("Status: 200 OK\r\n", StringComparison.Ordinal))
        throw new Exception("CGI executable did not emit status 200: " + stdout);
    if (!stdout.Contains("X-CGI: xpscript\r\n", StringComparison.Ordinal))
        throw new Exception("CGI executable lost XPScript response header.");
    if (!stdout.EndsWith("GET|Fredrik Norling|Cgi", StringComparison.Ordinal))
        throw new Exception("CGI executable did not execute the XPScript route: " + stdout);
}

static async Task RunInvalidBodyRegression(string root, string scriptPath)
{
    var environment = BaseEnvironment(root, scriptPath);
    environment["REQUEST_METHOD"] = "POST";
    environment["CONTENT_LENGTH"] = "6";
    var server = new XpsServerInfo("cgi-invalid", root, XpsWebHostingMode.Cgi, DateTimeOffset.UtcNow, "test");
    var adapter = new XpsCgiAdapter(new XpsCgiOptions { MaxRequestBodyBytes = 32 }, server, new EchoHandler());
    await using var stdin = new MemoryStream(Encoding.UTF8.GetBytes("short"));
    await using var stdout = new MemoryStream();
    try
    {
        await adapter.RunAsync(stdin, stdout, environment);
        throw new Exception("CGI adapter accepted a truncated request body.");
    }
    catch (XpsCgiException)
    {
    }

    environment["SCRIPT_FILENAME"] = Path.Combine(root, "..", "outside.xps");
    environment["CONTENT_LENGTH"] = "0";
    try
    {
        await adapter.RunAsync(Stream.Null, stdout, environment);
        throw new Exception("CGI adapter accepted SCRIPT_FILENAME outside the configured root.");
    }
    catch (XpsCgiException)
    {
    }
}

static Dictionary<string, string?> BaseEnvironment(string root, string scriptPath) => new(StringComparer.Ordinal)
{
    ["XPSCRIPT_WEB_ROOT"] = root,
    ["REQUEST_METHOD"] = "GET",
    ["SCRIPT_NAME"] = "/index.xps",
    ["SCRIPT_FILENAME"] = scriptPath,
    ["QUERY_STRING"] = "",
    ["SERVER_NAME"] = "localhost",
    ["SERVER_PROTOCOL"] = "HTTP/1.1",
    ["REMOTE_ADDR"] = "127.0.0.1",
    ["HTTPS"] = "off"
};

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

sealed class EchoHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 201;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.SetHeader("X-Adapter", "ok");
        context.Response.Write(context.Request.Method);
        context.Response.Write("|");
        context.Response.Write(context.Request.QueryString);
        context.Response.Write("|");
        context.Response.Write(context.Request.HeaderFirst("X-Test"));
        context.Response.Write("|");
        context.Response.Write(context.Request.Cookie("client"));
        context.Response.Write("|");
        context.Response.Write(context.Request.BodyText());
        return Task.CompletedTask;
    }
}
