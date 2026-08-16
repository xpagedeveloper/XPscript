using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-web-compiler-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var sourcePath = Path.Combine(root, "index.xps");

await File.WriteAllTextAsync(sourcePath, """
[Anonymous]
[Get]
Sub WebMain()
End Sub

[Authenticated]
[Post]
[Rule:admin]
[Rule:!blocked]
Sub Save()
End Sub
""");

try
{
    var parser = new XpsWebRouteMetadataParser();
    var parsed = parser.Parse(await File.ReadAllTextAsync(sourcePath));
    if (parsed.Source.Contains("[Anonymous]", StringComparison.Ordinal)) throw new Exception("Web attributes were not stripped from compiler source.");
    if (!parsed.Routes.TryGetValue("WebMain", out var webMain)) throw new Exception("WebMain route metadata missing.");
    if (!webMain.Policy.AllowAnonymous || !webMain.Policy.Methods.Contains("GET")) throw new Exception("Anonymous GET metadata mismatch.");
    if (!parsed.Routes.TryGetValue("Save", out var save)) throw new Exception("Save route metadata missing.");
    if (save.Policy.AllowAnonymous || !save.Policy.Methods.Contains("POST")) throw new Exception("Authenticated POST metadata mismatch.");
    if (!save.Policy.RequiredRules.Contains("admin") || !save.Policy.ForbiddenRules.Contains("blocked")) throw new Exception("Route rule metadata mismatch.");

    AssertMetadataFailure("""
[Anonymous]
[Authenticated]
[Get]
Sub Bad()
End Sub
""");
    AssertMetadataFailure("""
[Anonymous]
Sub MissingMethod()
End Sub
""");
    AssertMetadataFailure("""
[Unknown]
[Get]
Sub BadAttribute()
End Sub
""");

    await using var unit = await new XpsWebCompiler().CompileAsync(sourcePath);
    if (!unit.Routes.ContainsKey("WebMain") || !unit.Routes.ContainsKey("Save"))
        throw new Exception("Compiled route table is incomplete.");

    var request = new XpsWebRequest(
        "GET", "/", "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var context = new XpsWebContext(
        request,
        new XpsWebResponse(),
        new XpsServerInfo("compiler-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());

    await unit.InvokeAsync("WebMain", context);
    try
    {
        await unit.InvokeAsync("NotExported", context);
        throw new Exception("Unexported procedure invocation was not rejected.");
    }
    catch (XpsWebRouteException)
    {
    }

    Console.WriteLine("WEB-COMPILER-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static void AssertMetadataFailure(string source)
{
    try
    {
        _ = new XpsWebRouteMetadataParser().Parse(source);
    }
    catch (XpsWebRouteMetadataException)
    {
        return;
    }
    throw new Exception("Invalid web route metadata was accepted.");
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    public object? Get(string name) => null;
    public void Set(string name, object? value) { }
    public bool Remove(string name) => false;
    public void Clear() { }
}
