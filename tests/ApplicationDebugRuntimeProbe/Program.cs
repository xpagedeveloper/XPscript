using XPScript.Compiler;

var source = Path.GetFullPath("application-debug.xps");
var generated = new XPScriptTranspiler().Transpile(
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
    CompilerDriver.CurrentRuntimeIdentifier());

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
