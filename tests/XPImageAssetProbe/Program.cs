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
foreach (var expected in new[]
{
    "var resolved = XPScriptFileSystemRuntime.ResolvePath(source);",
    "XPScriptFileSystemRuntime.EnsureWritablePath(resolved);"
})
{
    if (!runtime.Contains(expected, StringComparison.Ordinal))
        throw new Exception("XPImage asset runtime contract is missing: " + expected);
}

var fileSystemType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.FileSystemPortabilityRuntimeSource", throwOnError: true)!;
var fileSystemRuntime = (string)(fileSystemType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("FileSystemPortabilityRuntimeSource.Code was not found."));
foreach (var expected in new[]
{
    "throw new XPScriptRuntimeException(5, \"Application assets are read-only.\");",
    "if (IsAssetPath(path))",
    "Access = FileAccess.Read",
    "var directory = string.IsNullOrEmpty(directoryPart) ? _scriptDirectory : ResolvePath(directoryPart);",
    "public static long FileLen(object? value) => new FileInfo(RequireExistingFile(value)).Length;",
    "public static DateTime FileDateTime(object? value) => File.GetLastWriteTime(RequireExistingFile(value));"
})
{
    if (!fileSystemRuntime.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Application asset read-only contract is missing: " + expected);
}

var coreType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.CoreCompatibilityRuntimeSource", throwOnError: true)!;
var coreRuntime = (string)(coreType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("CoreCompatibilityRuntimeSource.Code was not found."));
if (!coreRuntime.Contains("XPScriptFileSystemRuntime.EnsureWritablePath(state.Stream.Name);", StringComparison.Ordinal))
    throw new Exception("Binary/Random Put must reject writes to application assets with runtime error 5.");

Console.WriteLine("XPIMAGE_ASSET_ROUTING_OK");
