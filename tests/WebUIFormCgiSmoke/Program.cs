using System.Diagnostics;
using System.Text;

var parent = Path.Combine(Path.GetTempPath(), "xps-uiform-cgi-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "form.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New JsonObject
    Dim form As New UIForm("Contact form")
    Dim result As String

    Call data.Set("existing", "Loaded from JSON")
    Call form.BindData(data)
    Call form.AddTextField("existing", "Existing")
    Call form.AddTextField("missing", "Missing")

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
""");

try
{
    var get = await RunCgiAsync(root, scriptPath, "GET", null);
    if (!get.Contains("Content-Type: text/html; charset=utf-8\r\n", StringComparison.Ordinal))
        throw new Exception("UIForm CGI GET did not return HTML: " + get);
    if (!get.Contains("<h1>Contact form</h1>", StringComparison.Ordinal))
        throw new Exception("UIForm CGI GET did not render the form title: " + get);
    if (!get.Contains("name=\"existing\" value=\"Loaded from JSON\"", StringComparison.Ordinal))
        throw new Exception("UIForm CGI GET did not load the existing JSON value: " + get);
    if (!get.Contains("name=\"missing\" value=\"\"", StringComparison.Ordinal))
        throw new Exception("UIForm CGI GET did not render a missing JSON field as empty: " + get);

    var postBody = "existing=Changed+value&missing=Created+by+user";
    var post = await RunCgiAsync(root, scriptPath, "POST", postBody);
    if (!post.Contains("Content-Type: application/json; charset=utf-8\r\n", StringComparison.Ordinal))
        throw new Exception("UIForm CGI POST did not return JSON: " + post);
    if (!post.Contains("\"existing\":\"Changed value\"", StringComparison.Ordinal))
        throw new Exception("UIForm CGI POST did not save the existing field: " + post);
    if (!post.Contains("\"missing\":\"Created by user\"", StringComparison.Ordinal))
        throw new Exception("UIForm CGI POST did not create the missing JSON key: " + post);

    Console.WriteLine("WEB-UIFORM-CGI=OK");
}
finally
{
    if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
}

static async Task<string> RunCgiAsync(string root, string scriptPath, string method, string? body)
{
    var repoRoot = FindRepoRoot();
    var cgiDll = Path.Combine(repoRoot, "src", "XPScript.Web.Cgi", "bin", "Release", "net10.0", "XPScript.Web.Cgi.dll");
    if (!File.Exists(cgiDll)) throw new Exception("Built CGI assembly was not found: " + cgiDll);

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
    start.Environment["XPSCRIPT_SITE_ID"] = "uiform-cgi-smoke";
    start.Environment["REQUEST_METHOD"] = method;
    start.Environment["SCRIPT_NAME"] = "/form.xps";
    start.Environment["SCRIPT_FILENAME"] = scriptPath;
    start.Environment["QUERY_STRING"] = "";
    start.Environment["SERVER_NAME"] = "localhost";
    start.Environment["SERVER_PROTOCOL"] = "HTTP/1.1";
    start.Environment["REMOTE_ADDR"] = "127.0.0.1";
    start.Environment["HTTPS"] = "off";

    var bytes = body is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
    if (body is not null)
    {
        start.Environment["CONTENT_TYPE"] = "application/x-www-form-urlencoded";
        start.Environment["CONTENT_LENGTH"] = bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    using var process = Process.Start(start) ?? throw new Exception("Failed to start CGI process.");
    if (bytes.Length > 0)
        await process.StandardInput.BaseStream.WriteAsync(bytes);
    process.StandardInput.Close();

    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
    var stdout = await stdoutTask;
    var stderr = await stderrTask;
    if (process.ExitCode != 0)
        throw new Exception($"UIForm CGI process failed with exit {process.ExitCode}. stderr={stderr} stdout={stdout}");
    return stdout;
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
