using System.Reflection;
using System.Runtime.Loader;

var compilerDirectory = Path.GetFullPath("src/XPScript.Compiler/bin/Release/net10.0");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var file = Path.Combine(compilerDirectory, name.Name + ".dll");
    return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
};
var compiler = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(compilerDirectory, "XPScript.Compiler.Core.dll"));
var transpilerType = compiler.GetType("XPScript.Compiler.XPScriptTranspiler", throwOnError: true)!;
var transpile = transpilerType.GetMethods().Single(m => m.Name == "Transpile" && m.GetParameters().Length == 3);
var runtime = compiler.GetType("XPScript.Compiler.CompilerDriver", throwOnError: true)!.GetMethod("CurrentRuntimeIdentifier")!.Invoke(null, null);

var source = Path.GetFullPath("application-debug.xps");
var generated = (string)transpile.Invoke(Activator.CreateInstance(transpilerType), [
    """
Sub Main()
    If Application.Debug.Enabled Then
        Call Application.Debug.Print("hello")
        Call Application.Debug.Write("partial")
        Call Application.Debug.Info("info")
        Call Application.Debug.Warning("warning")
        Call Application.Debug.Error("error")
    End If
End Sub
""",
    source,
    runtime])!;

foreach (var expected in new[]
{
    "XPScriptApplicationDebugRuntime.Enabled",
    "XPScriptApplicationDebugRuntime.Print(",
    "XPScriptApplicationDebugRuntime.Write(",
    "XPScriptApplicationDebugRuntime.Info(",
    "XPScriptApplicationDebugRuntime.Warning(",
    "XPScriptApplicationDebugRuntime.Error(",
    "Environment.GetCommandLineArgs()",
    "--appdebug",
    "--debug",
    "Android.Util.Log",
    "AllocConsole"
})
{
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Application.Debug runtime is missing: " + expected);
}

Console.WriteLine("APPLICATION-DEBUG-RUNTIME=OK");
