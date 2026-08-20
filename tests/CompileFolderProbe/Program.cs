using XPScript.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-compile-folder-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var app = Path.Combine(root, "app");
    var includes = Path.Combine(app, "includes");
    var nested = Path.Combine(app, "nested");
    Directory.CreateDirectory(includes);
    Directory.CreateDirectory(nested);

    var mainPath = Path.Combine(root, "main.xps");
    var mainSource = """
[Compile:app]
Sub Main()
    Dim form As New UIForm("Main")
    Call Navigate("CUSTOMERS")
End Sub
""";
    File.WriteAllText(mainPath, mainSource);

    File.WriteAllText(Path.Combine(app, "customers.xps"), """
Include "includes/common.xps"
Sub Main()
    Print "customers"
End Sub
""");

    File.WriteAllText(Path.Combine(nested, "orders.xps"), """
Sub Orders()
    Print "orders"
End Sub
""");

    File.WriteAllText(Path.Combine(includes, "common.xps"), """
Sub CommonHelper()
    Print "common"
End Sub
""");

    var preprocessor = new CompileFolderSourcePreprocessor();
    var result = preprocessor.Transform(mainSource, mainPath, enableModules: true);

    Require(result.Enabled, "compile-folder should be enabled for the desktop probe");
    Require(result.Modules.Any(x => x.Equals("customers.xps", StringComparison.OrdinalIgnoreCase)), "customers.xps was not discovered");
    Require(result.Modules.Any(x => x.Equals("nested/orders.xps", StringComparison.OrdinalIgnoreCase)), "recursive subfolder module was not discovered");
    Require(!result.Modules.Any(x => x.EndsWith("common.xps", StringComparison.OrdinalIgnoreCase)), "an Include file was incorrectly compiled as a standalone module");
    Require(result.Dependencies.Any(x => Path.GetFileName(x).Equals("common.xps", StringComparison.OrdinalIgnoreCase)), "Include dependency was not tracked");
    Require(result.Source.Contains("__xpsTarget = \"customers.xps\"", StringComparison.Ordinal), "navigation alias with .xps is missing");
    Require(result.Source.Contains("__xpsTarget = \"customers\"", StringComparison.Ordinal), "extensionless navigation alias is missing");
    Require(result.Source.Contains("LCase(Trim(target))", StringComparison.Ordinal), "navigation matching is not case-insensitive");

    var ignored = preprocessor.Transform("""
[Compile:app]
Sub Main()
    Print "plain cli"
End Sub
""", mainPath, enableModules: false);
    Require(!ignored.Enabled, "compile-folder must remain disabled for non-desktop/non-WASM source");
    Require(!ignored.Source.Contains("[Compile:", StringComparison.OrdinalIgnoreCase), "ignored Compile metadata must be removed before normal compilation");
    Require(!ignored.Source.Contains("__XpsCompiledNavigationDispatch", StringComparison.Ordinal), "ignored Compile metadata unexpectedly installed navigation");

    var wasmParsed = new XpsWebRouteMetadataParser().Parse("""
[Platform:browser-wasm]
[Compile:app]
Sub Main()
End Sub
""");
    Require(wasmParsed.Platform == "browser-wasm", "browser-wasm platform metadata was not detected");
    Require(wasmParsed.Source.Contains("[Platform:browser-wasm]", StringComparison.OrdinalIgnoreCase), "browser-wasm platform metadata must survive until compiler preprocessing");
    Require(wasmParsed.Source.Contains("[Compile:app]", StringComparison.OrdinalIgnoreCase), "Compile metadata must survive browser route parsing");

    var generated = new XPScriptTranspiler().Transpile(mainSource, mainPath, CompilerDriver.CurrentRuntimeIdentifier());
    Require(generated.Contains("__XpsCompiledNavigationDispatch", StringComparison.Ordinal), "desktop transpilation did not install compiled navigation dispatch");
    Require(generated.Contains("__XpsModule_", StringComparison.Ordinal), "desktop transpilation did not compile module entry points");

    Console.WriteLine("CompileFolderProbe OK");
    return 0;
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
