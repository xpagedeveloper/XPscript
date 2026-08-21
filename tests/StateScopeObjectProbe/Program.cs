using XPScript.Compiler;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static XpsWebRequest Request(string path = "/") => new(
    "GET",
    path,
    string.Empty,
    string.Empty,
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null,
    0,
    ReadOnlyMemory<byte>.Empty,
    "localhost",
    "http",
    "127.0.0.1",
    "HTTP/1.1",
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

var serverA = new XpsServerInfo("site-a", Environment.CurrentDirectory, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var serverB = new XpsServerInfo("site-b", Environment.CurrentDirectory, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var principal = new XpsWebPrincipal(false);
var applicationA = new XpsApplicationState();
var applicationB = new XpsApplicationState();

XpsWebRuntimeObjects.Process.State.Clear();

var request1 = Request("/one");
var response1 = new XpsWebResponse();
var sessionStore = new XpsSessionStore();
var session1 = sessionStore.Bind(request1, response1);
var context1 = new XpsWebContext(request1, response1, serverA, principal, applicationA, session1);
using (XpsWebContextAccessor.Push(context1))
{
    XpsWebRuntimeObjects.Application.State.Set("shared", "site-a");
    XpsWebRuntimeObjects.Process.State.Set("process-shared", "process");
    XpsWebRuntimeObjects.Session.State.Set("session-value", "session");
    XpsWebRuntimeObjects.RequestScope.Set("request-value", "request-1");

    Assert((string?)XpsWebRuntimeObjects.Application.State.Get("shared") == "site-a", "Application.State did not store a value.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("process-shared") == "process", "Process.State did not store a value.");
    Assert((string?)XpsWebRuntimeObjects.Session.State.Get("session-value") == "session", "Session.State did not delegate to the session store.");
    Assert((string?)XpsWebRuntimeObjects.RequestScope.Get("request-value") == "request-1", "Request state did not store a value.");
}

var request2 = Request("/two");
var response2 = new XpsWebResponse();
var context2 = new XpsWebContext(request2, response2, serverA, principal, applicationA);
using (XpsWebContextAccessor.Push(context2))
{
    Assert((string?)XpsWebRuntimeObjects.Application.State.Get("shared") == "site-a", "Application.State was not shared across requests for one site.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("process-shared") == "process", "Process.State was not shared across requests.");
    Assert(!XpsWebRuntimeObjects.RequestScope.Exists("request-value"), "Request state leaked into a later request.");
}

var request3 = Request("/other-site");
var response3 = new XpsWebResponse();
var context3 = new XpsWebContext(request3, response3, serverB, principal, applicationB);
using (XpsWebContextAccessor.Push(context3))
{
    Assert(!XpsWebRuntimeObjects.Application.State.Exists("shared"), "Application.State leaked between sites.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("process-shared") == "process", "Process.State was not shared across sites in one process.");
}

var desktopSource = """
Sub Main()
    Application.State.Set("app", "value")
    Process.State.Set("proc", "value")
    Print Application.State.Get("app")
    Print Process.State.Get("proc")
End Sub
""";
var desktopGenerated = new XPScriptTranspiler().Transpile(desktopSource, "state-desktop.xps", CompilerDriver.CurrentRuntimeIdentifier());
Assert(desktopGenerated.Contains("XPScriptApplicationRuntime.State", StringComparison.Ordinal), "Application.State was not mapped to the application runtime.");
Assert(desktopGenerated.Contains("XPScriptProcessRuntime.State", StringComparison.Ordinal), "Process.State was not mapped to the process runtime.");

var webRoot = Path.Combine(Path.GetTempPath(), "XPScript-state-scope-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(webRoot);
try
{
    var webPath = Path.Combine(webRoot, "state.xps");
    await File.WriteAllTextAsync(webPath, """
[Anonymous]
[Get]
Sub Index()
    Application.State.Set("app", "value")
    Process.State.Set("proc", "value")
    Request.State.Set("request", "value")
    Session.State.Set("session", "value")
    Response.Write("ok")
End Sub
""");

    using var compiled = await new XpsWebCompiler().CompileAsync(webPath);
}
finally
{
    XpsWebRuntimeObjects.Process.State.Clear();
    try { Directory.Delete(webRoot, recursive: true); } catch { }
}

Console.WriteLine("STATE-SCOPES-OK");
