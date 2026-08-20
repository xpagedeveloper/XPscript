using System.Diagnostics;
using System.Text.RegularExpressions;

var parent = Path.Combine(Path.GetTempPath(), "xps-cgi-state-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
var stateRoot = Path.Combine(parent, "state");
Directory.CreateDirectory(root);
Directory.CreateDirectory(stateRoot);

await File.WriteAllTextAsync(Path.Combine(root, "web.cfg"), """
{
  "cgi": {
    "sessionFolder": "../state"
  }
}
""");

await File.WriteAllTextAsync(Path.Combine(root, "state.xps"), """
[Anonymous]
[Get]
Sub Login()
    Session.Set("cart", "alpha")
    Session.Authenticate("42", "Fredrik", "admin")
    Application.Set("shared", "site-value")
    RequestScope.Set("temp", "request-one")
    Response.Write(Session.Id)
End Sub

[Authenticated]
[Rule:admin]
[Get]
Sub Private()
    Response.Write(Session.Get("cart"))
    Response.Write("|")
    Response.Write(Application.Get("shared"))
    Response.Write("|")
    Response.Write(RequestScope.Get("temp"))
End Sub

[Anonymous]
[Get]
Sub OtherUser()
    Response.Write(Session.Get("cart"))
    Response.Write("|")
    Response.Write(Application.Get("shared"))
End Sub
""");

try
{
    var login = await RunAsync(root, stateRoot, "/state/Login", null);
    AssertStatus(login, 200);
    var cookie = ExtractSessionCookie(login.Stdout);
    if (string.IsNullOrWhiteSpace(cookie)) throw new Exception("Persistent CGI login did not emit a session cookie.");

    var privateRequest = await RunAsync(root, stateRoot, "/state/Private", cookie);
    AssertStatus(privateRequest, 200);
    var privateBody = Body(privateRequest.Stdout);
    if (privateBody != "alpha|site-value|")
        throw new Exception("Persistent CGI did not preserve Session/Application or leaked RequestScope: " + privateBody);

    var otherUser = await RunAsync(root, stateRoot, "/state/OtherUser", null);
    AssertStatus(otherUser, 200);
    var otherBody = Body(otherUser.Stdout);
    if (otherBody != "|site-value")
        throw new Exception("Application was not shared across CGI users or Session leaked across users: " + otherBody);

    var privateAgain = await RunAsync(root, stateRoot, "/state/Private", cookie);
    AssertStatus(privateAgain, 200);
    if (Body(privateAgain.Stdout) != "alpha|site-value|")
        throw new Exception("Session auth/rules did not persist across multiple CGI processes.");

    var stateFiles = Directory.GetFiles(stateRoot, "*.json");
    if (stateFiles.Length != 1) throw new Exception("Expected one site-isolated CGI state file.");
    await File.WriteAllTextAsync(stateFiles[0], "{not valid json");
    var corrupt = await RunAsync(root, stateRoot, "/state/OtherUser", null);
    if (corrupt.ExitCode == 0 || !corrupt.Stdout.StartsWith("Status: 400 Bad Request\r\n", StringComparison.Ordinal))
        throw new Exception("Corrupt CGI persistent state did not fail closed.");

    Console.WriteLine("WEB-CGI-STATE-SMOKE=OK");
}
finally
{
    try { Directory.Delete(parent, recursive: true); } catch { }
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
    string root,
    string stateRoot,
    string path,
    string? cookie)
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
    start.Environment["XPSCRIPT_STATE_ROOT"] = stateRoot;
    start.Environment["XPSCRIPT_SITE_ID"] = "cgi-state-smoke";
    start.Environment["REQUEST_METHOD"] = "GET";
    start.Environment["SCRIPT_NAME"] = path;
    start.Environment["SCRIPT_FILENAME"] = Path.Combine(root, "state.xps");
    start.Environment["PATH_INFO"] = string.Empty;
    start.Environment["QUERY_STRING"] = string.Empty;
    start.Environment["SERVER_NAME"] = "state.example.test";
    start.Environment["SERVER_PROTOCOL"] = "HTTP/1.1";
    start.Environment["REMOTE_ADDR"] = "127.0.0.1";
    start.Environment["HTTPS"] = "off";
    if (!string.IsNullOrWhiteSpace(cookie)) start.Environment["HTTP_COOKIE"] = cookie;

    using var process = Process.Start(start) ?? throw new Exception("Failed to start CGI host.");
    process.StandardInput.Close();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    return (process.ExitCode, await stdoutTask, await stderrTask);
}

static void AssertStatus((int ExitCode, string Stdout, string Stderr) result, int status)
{
    if (result.ExitCode != 0)
        throw new Exception($"CGI process failed with exit {result.ExitCode}. stderr={result.Stderr} stdout={result.Stdout}");
    var prefix = $"Status: {status} ";
    if (!result.Stdout.StartsWith(prefix, StringComparison.Ordinal))
        throw new Exception($"Expected CGI HTTP {status}: {result.Stdout}");
}

static string ExtractSessionCookie(string stdout)
{
    var matches = Regex.Matches(stdout, @"(?im)^Set-Cookie:\s*(XPSID=[^;\r\n]+)");
    return matches.Count > 0 ? matches[^1].Groups[1].Value : string.Empty;
}

static string Body(string stdout)
{
    var separator = stdout.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    return separator < 0 ? string.Empty : stdout[(separator + 4)..];
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