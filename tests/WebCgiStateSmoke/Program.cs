using System.Diagnostics;
using System.Text.RegularExpressions;

var parent = Path.Combine(Path.GetTempPath(), "xps-cgi-state-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
var stateRoot = Path.Combine(parent, "state");
var noSessionRoot = Path.Combine(parent, "no-session-site");
Directory.CreateDirectory(root);
Directory.CreateDirectory(stateRoot);
Directory.CreateDirectory(noSessionRoot);

await File.WriteAllTextAsync(Path.Combine(root, "web.cfg"), "{\"cgi\":{\"sessionFolder\":\"../state\"}}");
await File.WriteAllTextAsync(Path.Combine(noSessionRoot, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.Write(Session.Id)
End Sub
""");

await File.WriteAllTextAsync(Path.Combine(root, "state.xps"), """
[Anonymous]
[Get]
Sub Login()
    Session.Set("cart", "alpha")
    Session.Authenticate("42", "Fredrik", "admin")
    Call Session.SetRole("admin")
    Application.Set("shared", "site-value")
    RequestScope.Set("temp", "request-one")
    Response.Write(Session.Id)
End Sub

[Authenticated]
[Rule:admin]
[Role:admin]
[Get]
Sub Private()
    Response.Write(Session.Get("cart"))
    Response.Write("|")
    Response.Write(Application.Get("shared"))
    Response.Write("|")
    Response.Write(RequestScope.Get("temp"))
    Response.Write("|")
    Response.Write(Session.GetRole())
    Response.Write("|")
    Response.Write(CStr(Session.HasRole("admin")))
End Sub

[Authenticated]
[Get]
Sub RemoveAdminRole()
    Response.Write(Session.GetRole())
    Response.Write("|")
    Response.Write(CStr(Session.HasRole("admin")))
    Response.Write("|")
    Response.Write(CStr(Session.RemoveRole("admin")))
    Response.Write("|")
    Response.Write(CStr(Session.HasRole("admin")))
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
    var noSession = await RunAsync(noSessionRoot, "/", null);
    if (noSession.ExitCode == 0 || !noSession.Stdout.StartsWith("Status: 500 Internal Server Error\r\n", StringComparison.Ordinal))
        throw new Exception("CGI without cgi.sessionFolder unexpectedly exposed a Session object.");

    var login = await RunAsync(root, "/state/Login", null);
    AssertStatus(login, 200);
    var cookie = ExtractSessionCookie(login.Stdout);
    if (string.IsNullOrWhiteSpace(cookie)) throw new Exception("Persistent CGI login did not emit a session cookie.");

    var privateRequest = await RunAsync(root, "/state/Private", cookie);
    AssertStatus(privateRequest, 200);
    var privateBody = Body(privateRequest.Stdout);
    if (privateBody != "alpha|site-value||admin|True")
        throw new Exception("Persistent CGI did not preserve Session/Application/role or leaked RequestScope: " + privateBody);

    var otherUser = await RunAsync(root, "/state/OtherUser", null);
    AssertStatus(otherUser, 200);
    var otherBody = Body(otherUser.Stdout);
    if (otherBody != "|site-value")
        throw new Exception("Application was not shared across CGI users or Session leaked across users: " + otherBody);

    var privateAgain = await RunAsync(root, "/state/Private", cookie);
    AssertStatus(privateAgain, 200);
    if (Body(privateAgain.Stdout) != "alpha|site-value||admin|True")
        throw new Exception("Session auth/rules/roles did not persist across multiple CGI processes.");

    var removeRole = await RunAsync(root, "/state/RemoveAdminRole", cookie);
    AssertStatus(removeRole, 200);
    if (Body(removeRole.Stdout) != "admin|True|True|False")
        throw new Exception("CGI session role API did not persist or remove the role correctly: " + Body(removeRole.Stdout));

    var deniedAfterRoleRemoval = await RunAsync(root, "/state/Private", cookie);
    AssertStatus(deniedAfterRoleRemoval, 403);

    var stateFiles = Directory.GetFiles(stateRoot, "*.json");
    if (stateFiles.Length != 1) throw new Exception("Expected one site-isolated CGI state file.");
    await File.WriteAllTextAsync(stateFiles[0], "{not valid json");
    var corrupt = await RunAsync(root, "/state/OtherUser", null);
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
    start.Environment["XPSCRIPT_SITE_ID"] = "cgi-state-smoke";
    start.Environment["REQUEST_METHOD"] = "GET";
    start.Environment["SCRIPT_NAME"] = path;
    start.Environment["SCRIPT_FILENAME"] = path == "/" ? Path.Combine(root, "index.xps") : Path.Combine(root, "state.xps");
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
