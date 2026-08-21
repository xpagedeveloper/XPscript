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
var userA = new XpsWebPrincipal(true, "user-a", "Alice");
var userB = new XpsWebPrincipal(true, "user-b", "Bob");
var applicationA = new XpsApplicationState();
var applicationB = new XpsApplicationState();
var sessionStore = new XpsSessionStore();

XpsWebRuntimeObjects.Process.State.Clear();

var userARequest = Request("/user-a");
var userAResponse = new XpsWebResponse();
var userASession = sessionStore.Bind(userARequest, userAResponse);
userASession.Authenticate("user-a", "Alice");
var userAContext = new XpsWebContext(userARequest, userAResponse, serverA, userA, applicationA, userASession);
using (XpsWebContextAccessor.Push(userAContext))
{
    XpsWebRuntimeObjects.Application.State.Set("shared", "application-from-a");
    XpsWebRuntimeObjects.Process.State.Set("shared", "process-from-a");
    XpsWebRuntimeObjects.Session.State.Set("private", "session-a");
    XpsWebRuntimeObjects.RequestScope.Set("request-value", "request-a");
}

var userBRequest = Request("/user-b");
var userBResponse = new XpsWebResponse();
var userBSession = sessionStore.Bind(userBRequest, userBResponse);
userBSession.Authenticate("user-b", "Bob");
var userBContext = new XpsWebContext(userBRequest, userBResponse, serverA, userB, applicationA, userBSession);
using (XpsWebContextAccessor.Push(userBContext))
{
    Assert((string?)XpsWebRuntimeObjects.Application.State.Get("shared") == "application-from-a", "User B could not see User A's Application.State update.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("shared") == "process-from-a", "User B could not see User A's Process.State update.");
    Assert(!XpsWebRuntimeObjects.Session.State.Exists("private"), "User B could see User A's Session.State.");
    Assert(!XpsWebRuntimeObjects.RequestScope.Exists("request-value"), "Request.State leaked from User A's request to User B's request.");

    XpsWebRuntimeObjects.Application.State.Set("shared", "application-from-b");
    XpsWebRuntimeObjects.Process.State.Set("shared", "process-from-b");
    XpsWebRuntimeObjects.Session.State.Set("private", "session-b");
}

var userARequest2 = Request("/user-a/again");
var userAResponse2 = new XpsWebResponse();
var userAContext2 = new XpsWebContext(userARequest2, userAResponse2, serverA, userA, applicationA, userASession);
using (XpsWebContextAccessor.Push(userAContext2))
{
    Assert((string?)XpsWebRuntimeObjects.Application.State.Get("shared") == "application-from-b", "User A could not see User B's Application.State update.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("shared") == "process-from-b", "User A could not see User B's Process.State update.");
    Assert((string?)XpsWebRuntimeObjects.Session.State.Get("private") == "session-a", "User A's Session.State was not isolated from User B.");
}

var userBRequest2 = Request("/user-b/again");
var userBResponse2 = new XpsWebResponse();
var userBContext2 = new XpsWebContext(userBRequest2, userBResponse2, serverA, userB, applicationA, userBSession);
using (XpsWebContextAccessor.Push(userBContext2))
{
    Assert((string?)XpsWebRuntimeObjects.Session.State.Get("private") == "session-b", "User B's Session.State was not isolated from User A.");
}

var otherSiteContext = new XpsWebContext(Request("/other-site"), new XpsWebResponse(), serverB, userA, applicationB);
using (XpsWebContextAccessor.Push(otherSiteContext))
{
    Assert(!XpsWebRuntimeObjects.Application.State.Exists("shared"), "Application.State leaked between web applications.");
    Assert((string?)XpsWebRuntimeObjects.Process.State.Get("shared") == "process-from-b", "Process.State was not shared across applications in the same process.");
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

    await using var compiled = await new XpsWebCompiler().CompileAsync(webPath);
}
finally
{
    XpsWebRuntimeObjects.Process.State.Clear();
    try { Directory.Delete(webRoot, recursive: true); } catch { }
}

Console.WriteLine("STATE-SCOPES-OK");
