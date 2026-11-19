using XPScript.Compiler;

var workspace = Path.GetFullPath(Path.Combine("out", "application-debug-probe", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(workspace);
var source = Path.Combine(workspace, "application-debug.xps");
await File.WriteAllTextAsync(source,
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
""");
var generated = await AstCompiler.GenerateSourceAsync(source, workspace);

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
