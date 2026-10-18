using XPScript.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xpscript-android-ast-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "android-ast-probe.xps");
    await File.WriteAllTextAsync(source, "Sub Main()\n    Print \"ANDROID_AST_BACKEND_OK\"\nEnd Sub\n");

    foreach (var rid in new[] { "android-x64", "android-arm64" })
    {
        var output = Path.Combine(root, rid);
        var assembly = await AstExperimentalCompiler.CompileAsync(source, output, rid);
        if (!File.Exists(assembly))
            throw new Exception($"AST {rid} compilation did not produce Generated.dll.");
        if (!Directory.EnumerateFiles(output, "*.apk", SearchOption.TopDirectoryOnly).Any())
            throw new Exception($"AST {rid} compilation did not produce an APK.");
    }

    Console.WriteLine("ANDROID_AST_BACKEND_OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}
