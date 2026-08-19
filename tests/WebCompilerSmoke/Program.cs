using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

if (args.Length > 0)
{
    foreach (var input in args)
    {
        var fullPath = Path.GetFullPath(input);
        await using var sampleUnit = await new XpsWebCompiler().CompileAsync(fullPath);
        if (sampleUnit.Routes.Count == 0)
            throw new Exception($"Web sample '{Path.GetFileName(fullPath)}' did not export any routes.");
        Console.WriteLine($"WEB-SAMPLE-COMPILE=OK {Path.GetFileName(fullPath)} routes={sampleUnit.Routes.Count}");
    }
    return;
}

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

    var originalError = Console.Error;
    using var conflictError = new StringWriter();
    try
    {
        Console.SetError(conflictError);
        var conflict = parser.Parse("""
[Anonymous]
[Authenticated]
[Get]
Sub Protected()
End Sub
""");
        if (!conflict.Routes.TryGetValue("Protected", out var protectedRoute))
            throw new Exception("Authenticated/Anonymous conflict prevented route parsing.");
        if (protectedRoute.Policy.AllowAnonymous)
            throw new Exception("[Authenticated] must take precedence over [Anonymous].");
    }
    finally
    {
        Console.SetError(originalError);
    }
    if (!conflictError.ToString().Contains("[Authenticated] takes precedence", StringComparison.Ordinal))
        throw new Exception("Authenticated/Anonymous conflict did not produce a console error.");

    AssertMetadataFailure("""
[Anonymous]
Sub MissingMethod()
End Sub
""");

    using var capturedError = new StringWriter();
    try
    {
        Console.SetError(capturedError);
        var tolerant = parser.Parse("""
[Unknown]
[Get]
Sub BadAttribute()
End Sub
""");
        if (!tolerant.Routes.ContainsKey("BadAttribute"))
            throw new Exception("Unknown web route attribute prevented compilation.");
    }
    finally
    {
        Console.SetError(originalError);
    }
    if (!capturedError.ToString().Contains("Unsupported web route attribute '[Unknown]'", StringComparison.Ordinal))
        throw new Exception("Unknown web route attribute did not produce a console error.");

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
    public int Count => 0;
    public IReadOnlyList<string> Keys => Array.Empty<string>();
    public object? Get(string name) => null;
    public void Set(string name, object? value) { }
    public bool Exists(string name) => false;
    public bool Remove(string name) => false;
    public bool Unset(string name) => false;
    public void Clear() { }
}
