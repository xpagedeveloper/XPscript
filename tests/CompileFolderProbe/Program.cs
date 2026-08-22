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
    Request.State.Set("customerId", "42")
    Call Navigate("nested/orders")
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
    Require(result.Source.Contains("Public Sub Navigate(target As String)", StringComparison.Ordinal), "single-target Navigate API is missing");
    Require(!result.Source.Contains("parameterName", StringComparison.OrdinalIgnoreCase), "compiled navigation still contains parameter support");
    Require(!result.Source.Contains("parameterValue", StringComparison.OrdinalIgnoreCase), "compiled navigation still contains parameter support");
    var targetIndex = result.Source.IndexOf("xpsCompilerGeneratedTarget = \"customers\"", StringComparison.Ordinal);
    var boundaryIndex = result.Source.IndexOf("XPScriptRequestRuntime.BeforeCompiledNavigation()", targetIndex, StringComparison.Ordinal);
    Require(targetIndex >= 0 && boundaryIndex > targetIndex, "Request.State boundary runs before a navigation target has matched");

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
    Require(!generated.Contains("_navigationParameterName", StringComparison.Ordinal), "generated UIForm runtime still contains navigation parameter-name state");
    Require(!generated.Contains("_navigationParameterValue", StringComparison.Ordinal), "generated UIForm runtime still contains navigation parameter-value state");
    Require(!generated.Contains("Navigate(object? target, object? parameterName", StringComparison.Ordinal), "generated UIForm runtime still exposes the removed navigation parameter overload");
    Require(!generated.Contains("__xps_navigation_parameter_name", StringComparison.Ordinal), "desktop UI bridge still reads navigation parameter-name state");
    Require(!generated.Contains("__xps_navigation_parameter_value", StringComparison.Ordinal), "desktop UI bridge still reads navigation parameter-value state");
    Require(!generated.Contains("DispatchCompiledNavigation(string target, string parameterName", StringComparison.Ordinal), "desktop UI bridge still exposes parameter-based navigation dispatch");
    Require(generated.Contains("method.Invoke(null, [target]);", StringComparison.Ordinal), "desktop UI bridge does not invoke the target-only compiled navigation dispatcher");

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

    var moduleDetectedRoot = Path.Combine(root, "module-detected-desktop");
    var moduleDetectedApp = Path.Combine(moduleDetectedRoot, "app");
    Directory.CreateDirectory(moduleDetectedApp);
    var moduleDetectedMainPath = Path.Combine(moduleDetectedRoot, "main.xps");
    var moduleDetectedMain = """
[Compile:app]
Sub Main()
    Call Navigate("page")
End Sub
""";
    File.WriteAllText(moduleDetectedMainPath, moduleDetectedMain);
    File.WriteAllText(Path.Combine(moduleDetectedApp, "page.xps"), """
Sub Main()
    Dim form As New UIForm("Page")
End Sub
""");
    Require(CompileFolderSourcePreprocessor.IsDesktopProject(moduleDetectedMain, moduleDetectedMainPath), "desktop UI in a compiled module was not detected");
    var moduleDetectedGenerated = new XPScriptTranspiler().Transpile(moduleDetectedMain, moduleDetectedMainPath, CompilerDriver.CurrentRuntimeIdentifier());
    Require(moduleDetectedGenerated.Contains("XpsCompilerGeneratedNavigationDispatch", StringComparison.Ordinal), "compile-folder was ignored when desktop UI existed only in a child module");

    var ambiguousRoot = Path.Combine(root, "ambiguous-entry");
    var ambiguousApp = Path.Combine(ambiguousRoot, "app");
    Directory.CreateDirectory(ambiguousApp);
    var ambiguousMainPath = Path.Combine(ambiguousRoot, "main.xps");
    var ambiguousMain = """
[Compile:app]
Sub Main()
    Dim form As New UIForm("Ambiguous")
End Sub
""";
    File.WriteAllText(ambiguousMainPath, ambiguousMain);
    File.WriteAllText(Path.Combine(ambiguousApp, "ambiguous.xps"), "Sub First()\nEnd Sub\nSub Second()\nEnd Sub\n");
    RequireThrows<CompilerException>(
        () => preprocessor.Transform(ambiguousMain, ambiguousMainPath, enableModules: true),
        "requires one navigable entry point");

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
