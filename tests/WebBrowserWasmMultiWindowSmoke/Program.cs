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

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm-bridge");
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

    var generatedProject = Directory.EnumerateFiles(cacheRoot, "BrowserApp.csproj", SearchOption.AllDirectories).FirstOrDefault();
    if (generatedProject is null)
        throw new Exception("Browser WASM multi-window compile did not preserve its generated browser project.");

    var projectText = await File.ReadAllTextAsync(generatedProject);
    if (!projectText.Contains("XPScript.UI.Browser", StringComparison.Ordinal))
        throw new Exception("Generated browser-WASM project does not reference XPScript.UI.Browser.");
    if (projectText.Contains("XPScript.UI.Desktop", StringComparison.Ordinal))
        throw new Exception("Generated browser-WASM project unexpectedly references XPScript.UI.Desktop.");

    Console.WriteLine("browser-wasm multi-window smoke passed");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
