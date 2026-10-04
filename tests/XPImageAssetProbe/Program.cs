using System.Reflection;
using XPScript.Compiler;

var transpiler = new XPScriptTranspiler();
var generated = transpiler.Transpile("""
Sub Main()
    Dim image As XPImage
    Set image = XPImage.Load("assets/probe.png")
    If image.Width <> 1 Or image.Height <> 1 Then Error 5, "asset image dimensions"
    Print "XPIMAGE-ASSET=OK"
End Sub
""", Path.Combine(Path.GetTempPath(), "xpscript-asset-probe", "probe.xps"), "linux-x64");

foreach (var expected in new[]
{
    "XPScriptFileSystemRuntime.ResolvePath(source)",
    "normalized.StartsWith(\"assets/\"",
    "Application asset path escapes the assets directory."
})
{
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Generated XPImage asset runtime is missing: " + expected);
}

var runtimeType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.XPImageRuntimeSource", throwOnError: true)!;
var runtime = (string)(runtimeType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("XPImageRuntimeSource.Code was not found."));
if (!runtime.Contains("var resolved = XPScriptFileSystemRuntime.ResolvePath(source);", StringComparison.Ordinal))
    throw new Exception("XPImage local sources must resolve through the shared filesystem runtime.");

Console.WriteLine("XPIMAGE_ASSET_ROUTING_OK");
