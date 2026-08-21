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
    Application.State.Set("app", "main")
    Process.State.Set("process", "main")
    Session.State.Set("session", "main")
    Request.State.Set("request", "main")
    Call Navigate("CUSTOMERS")
End Sub
""";
    File.WriteAllText(mainPath, mainSource);

    File.WriteAllText(Path.Combine(app, "customers.xps"), """
Include "includes/common.xps"
Sub Main()
    Print Application.State.Get("app")
    Print Process.State.Get("process")
    Print Session.State.Get("session")
    Print Request.State.Get("request")
    Request.State.Set("request", "customers")
    Call Navigate("nested/orders", "customerId", "42")
End Sub
""");

    File.WriteAllText(Path.Combine(nested, "orders.xps"), """
Sub Orders()
    Print Application.State.Get("app")
    Print Process.State.Get("process")
    Print Session.State.Get("session")
    Print Request.State.Get("request")
    Print Request.State.Get("customerId")
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
    Require(result.Source.Contains("xpsCompilerGeneratedTarget = \"customers.xps\"", StringComparison.Ordinal), "navigation alias with .xps is missing");
    Require(result.Source.Contains("xpsCompilerGeneratedTarget = \"customers\"", StringComparison.Ordinal), "extensionless navigation alias is missing");
    Require(result.Source.Contains("xpsCompilerGeneratedTarget = \"nested/orders.xps\"", StringComparison.Ordinal), "nested navigation alias with .xps is missing");
    Require(result.Source.Contains("xpsCompilerGeneratedTarget = \"nested/orders\"", StringComparison.Ordinal), "nested extensionless navigation alias is missing");
    Require(result.Source.Contains("LCase(Trim(target))", StringComparison.Ordinal), "navigation matching is not case-insensitive");
    Require(result.Source.Contains("XPScriptRequestRuntime.BeforeCompiledNavigation()", StringComparison.Ordinal), "compiled navigation did not apply the local Request.State boundary");
    Require(result.Source.Contains("Request.State.Set(parameterName, parameterValue)", StringComparison.Ordinal), "navigation parameters are not copied into Request.State");
    Require(result.Source.Contains("Public Sub Navigate(target As String, Optional parameterName As String", StringComparison.Ordinal), "Navigate optional parameter support is missing");

    var ignored = preprocessor.Transform("""
[Compile:app]
Sub Main()
    Print "plain cli"
End Sub
""", mainPath, enableModules: false);
    Require(!ignored.Enabled, "compile-folder must remain disabled for non-desktop/non-WASM source");
    Require(!ignored.Source.Contains("[Compile:", StringComparison.OrdinalIgnoreCase), "ignored Compile metadata must be removed before normal compilation");
    Require(!ignored.Source.Contains("XpsCompilerGeneratedNavigationDispatch", StringComparison.Ordinal), "ignored Compile metadata unexpectedly installed navigation");

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
    Require(generated.Contains("XpsCompilerGeneratedNavigationDispatch", StringComparison.Ordinal), "desktop transpilation did not install compiled navigation dispatch");
    Require(generated.Contains("XpsCompilerGeneratedModule_", StringComparison.Ordinal), "desktop transpilation did not compile module entry points");
    Require(generated.Contains("XPScriptApplicationRuntime.State", StringComparison.Ordinal), "Application.State was not mapped for a compiled desktop application");
    Require(generated.Contains("XPScriptProcessRuntime.State", StringComparison.Ordinal), "Process.State was not mapped for a compiled desktop application");
    Require(generated.Contains("XPScriptSessionRuntime.State", StringComparison.Ordinal), "Session.State was not mapped for a compiled desktop/WASM application");
    Require(generated.Contains("XPScriptRequestRuntime.State", StringComparison.Ordinal), "Request.State was not mapped for a compiled desktop/WASM application");
    Require(generated.Contains("XPScriptRequestRuntime.BeforeCompiledNavigation()", StringComparison.Ordinal), "compiled navigation did not apply the local Request.State boundary");
    Require(generated.Contains("optional .xps extension", StringComparison.Ordinal), "UIForm navigation still requires an explicit .xps extension");

    var missingMainRoot = Path.Combine(root, "missing-main");
    var missingMainApp = Path.Combine(missingMainRoot, "app");
    Directory.CreateDirectory(missingMainApp);
    var startPath = Path.Combine(missingMainRoot, "start.xps");
    var desktopWithoutMain = """
[Compile:app]
Sub Main()
    Dim form As New UIForm("Missing main")
End Sub
""";
    File.WriteAllText(startPath, desktopWithoutMain);
    File.WriteAllText(Path.Combine(missingMainApp, "page.xps"), "Sub Main()\nEnd Sub\n");
    RequireThrows<CompilerException>(
        () => preprocessor.Transform(desktopWithoutMain, startPath, enableModules: true),
        "require main.xps");

    var wrongEntryRoot = Path.Combine(root, "wrong-entry");
    var wrongEntryApp = Path.Combine(wrongEntryRoot, "app");
    Directory.CreateDirectory(wrongEntryApp);
    File.WriteAllText(Path.Combine(wrongEntryRoot, "main.xps"), "Sub Main()\nEnd Sub\n");
    var otherPath = Path.Combine(wrongEntryRoot, "other.xps");
    var otherSource = """
[Compile:app]
Sub Main()
    Dim form As New UIForm("Wrong entry")
End Sub
""";
    File.WriteAllText(otherPath, otherSource);
    RequireThrows<CompilerException>(
        () => preprocessor.Transform(otherSource, otherPath, enableModules: true),
        "compiled from main.xps");

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

static void RequireThrows<T>(Action action, string messagePart) where T : Exception
{
    try
    {
        action();
    }
    catch (T ex)
    {
        if (!ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected error containing '{messagePart}', got '{ex.Message}'.");
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(T).Name} containing '{messagePart}'.");
}
