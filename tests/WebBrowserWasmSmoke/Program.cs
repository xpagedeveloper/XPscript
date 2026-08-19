using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var sourcePath = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]
Sub Main()
    Dim form As New UIForm("Browser Smoke")
    Dim grid As Variant
    Call form.AddTextField("name", "Name")
    Call form.AddPasswordField("password", "Password")
    Call form.AddSelect("country", "Country")
    Call form.AddOption("country", "SE")
    Call form.AddOption("country", "NO")
    Call form.SetRequired("name", True)
    Call form.SetLength("password", 8, 128)
    Set grid = form.AddGridColumns(12)
    Call grid.SetFieldPosition("name", 6)
    Call grid.SetFieldPosition("password", 6)
    Call grid.AddNewRow()
    Call grid.SetFieldPosition("country", 6)
    Call form.ShowDialog()
End Sub
""");

    var parser = new XpsWebRouteMetadataParser().Parse(await File.ReadAllTextAsync(sourcePath));
    if (!string.Equals(parser.Platform, "browser-wasm", StringComparison.Ordinal)) throw new Exception("Platform metadata was not detected.");

    var resolver = new XpsWebPathResolver(root);
    var asset = resolver.Resolve("/app.xps/_framework/dotnet.js");
    if (!asset.Found || asset.RouteFunction != XpsWebPathResolver.BrowserWasmAssetRoute) throw new Exception("WASM asset route was not resolved.");

    var compiler = new XpsWebCompiler();
    await using var unit = await compiler.CompileAsync(sourcePath, root);
    if (!unit.Routes.ContainsKey("Index") || !unit.Routes.ContainsKey(XpsWebPathResolver.BrowserWasmAssetRoute)) throw new Exception("Synthetic WASM routes are missing.");

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm");
    var index = Directory.EnumerateFiles(cacheRoot, "index.html", SearchOption.AllDirectories).FirstOrDefault();
    var dotnetJs = Directory.EnumerateFiles(cacheRoot, "dotnet.js", SearchOption.AllDirectories).FirstOrDefault();
    var browserJs = Directory.EnumerateFiles(cacheRoot, "xpscript-browser.js", SearchOption.AllDirectories).FirstOrDefault();
    if (index is null || dotnetJs is null || browserJs is null) throw new Exception("WASM publish output was not cached.");

    var browserModule = await File.ReadAllTextAsync(browserJs);
    foreach (var requiredMarker in new[] { "gridTemplateColumns", "form-select", "readOnly", "request.buttons", "xpscript:form-result" })
    {
        if (!browserModule.Contains(requiredMarker, StringComparison.Ordinal))
            throw new Exception($"Browser UIForm renderer is missing parity marker '{requiredMarker}'.");
    }

    Console.WriteLine("browser-wasm smoke passed");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
