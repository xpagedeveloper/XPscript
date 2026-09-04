using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-multiwindow-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var sourcePath = Path.Combine(root, "multiwindow.xps");
    await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]

Sub Main()
    Dim first As New UIForm("First browser form", 480, 320, True)
    Dim second As New UIForm("Second browser form", 480, 320, True)

    Call first.AddTextField("name", "Name")
    Call second.AddTextField("value", "Value")

    Call first.Show(False)
    Call second.Show(False)

    If first.Modal Then Error 5, "First form should be modeless"
    If second.Modal Then Error 5, "Second form should be modeless"
End Sub
""");

    var compiler = new XpsWebCompiler();
    await using var unit = await compiler.CompileAsync(sourcePath, root);

    if (!unit.Routes.ContainsKey("Index"))
        throw new Exception("Browser WASM multi-window compile did not produce the synthetic Index route.");
    if (!unit.Routes.ContainsKey(XpsWebPathResolver.BrowserWasmAssetRoute))
        throw new Exception("Browser WASM multi-window compile did not produce the asset route.");

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm");
    var mainJs = Directory.Exists(cacheRoot)
        ? Directory.EnumerateFiles(cacheRoot, "main.js", SearchOption.AllDirectories).FirstOrDefault()
        : null;
    if (mainJs is null)
        throw new Exception("Browser WASM multi-window publish did not produce main.js.");

    var bootstrap = await File.ReadAllTextAsync(mainJs);
    if (!bootstrap.Contains("XPScript.UI.Browser.dll", StringComparison.Ordinal))
        throw new Exception("Browser WASM bootstrap did not register XPScript.UI.Browser.dll.");
    if (!bootstrap.Contains("BrowserFormHost.DispatchEvent", StringComparison.Ordinal))
        throw new Exception("Browser WASM bootstrap did not register the BrowserFormHost event bridge.");

    var appRoot = Directory.GetParent(mainJs)?.FullName
        ?? throw new Exception("Unable to determine the persisted browser-WASM app root.");
    if (!File.Exists(Path.Combine(appRoot, "_framework", "dotnet.js")))
        throw new Exception("Browser WASM multi-window publish did not include _framework/dotnet.js.");
    if (!File.Exists(Path.Combine(appRoot, "xpscript-browser.js")))
        throw new Exception("Browser WASM multi-window publish did not include xpscript-browser.js.");

    Console.WriteLine("browser-wasm multi-window smoke passed");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
