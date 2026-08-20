using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

IXpsApplicationState application = new XpsApplicationState(new XpsApplicationStateOptions
{
    MaxEntries = 256,
    MaxValueBytes = 1024,
    MaxTotalBytes = 1024 * 1024
});

application.Add("name", "one");
application.Add("name", "two");
if (!Equals(application.Get("name"), "two")) throw new Exception("Application.Add must overwrite existing values.");
if (application.Get("missing") is not null) throw new Exception("Application.Get must return null for a missing value.");
if (application.Remove("missing")) throw new Exception("Application.Remove must return false for a missing value.");

await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
{
    for (var n = 0; n < 100; n++)
    {
        application.Add("worker-" + i, n);
        await Task.Yield();
    }
}));
for (var i = 0; i < 32; i++)
    if (!Equals(application.Get("worker-" + i), 99)) throw new Exception("Concurrent Application updates were lost.");

var store = new XpsSessionStore(new XpsSessionOptions
{
    MaxSessions = 64,
    MaxEntriesPerSession = 256,
    MaxValueBytes = 1024,
    MaxBytesPerSession = 1024 * 1024
});
var initial = store.Bind(Request(), new XpsWebResponse());
initial.Add("name", "one");
initial.Add("name", "two");
if (!Equals(initial.Get("name"), "two")) throw new Exception("Session.Add must overwrite existing values.");
if (initial.Get("missing") is not null) throw new Exception("Session.Get must return null for a missing value.");
if (initial.Remove("missing")) throw new Exception("Session.Remove must return false for a missing value.");
var sessionId = initial.Id;

await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
{
    var session = store.Bind(Request(new Dictionary<string, string> { ["XPSID"] = sessionId }), new XpsWebResponse());
    for (var n = 0; n < 100; n++)
    {
        session.Add("worker-" + i, n);
        await Task.Yield();
    }
}));
var verifySession = store.Bind(Request(new Dictionary<string, string> { ["XPSID"] = sessionId }), new XpsWebResponse());
for (var i = 0; i < 32; i++)
    if (!Equals(verifySession.Get("worker-" + i), 99)) throw new Exception("Concurrent Session updates were lost.");

IXpsRequestState requestScope = new XpsRequestState();
requestScope.Add("name", "one");
requestScope.Add("name", "two");
if (!Equals(requestScope.Get("name"), "two")) throw new Exception("RequestScope.Add must overwrite existing values.");
if (requestScope.Get("missing") is not null) throw new Exception("RequestScope.Get must return null for a missing value.");
if (requestScope.Remove("missing")) throw new Exception("RequestScope.Remove must return false for a missing value.");

var root = Path.Combine(Path.GetTempPath(), "xps-scope-add-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var script = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(script, """
[Anonymous]
[Get]
Sub Index()
    Application.Add("app", "value")
    Session.Add("session", "value")
    RequestScope.Add("request", "value")
    Response.Write(Application.Get("app"))
End Sub
""");
try
{
    await using var unit = await new XpsWebCompiler().CompileAsync(script, root);
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

Console.WriteLine("WEB-SCOPE-SEMANTICS=OK");

static XpsWebRequest Request(IReadOnlyDictionary<string, string>? cookies = null) => new(
    "GET", "/", "", "",
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null, 0, ReadOnlyMemory<byte>.Empty,
    "localhost", "http", "127.0.0.1", "HTTP/1.1",
    cookies ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
