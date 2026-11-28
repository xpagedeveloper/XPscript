using XPScript.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-application-ui-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var iconPath = Path.Combine(root, "app.ico");
    File.WriteAllBytes(iconPath, CreateTinyIco());

    var sourcePath = Path.Combine(root, "app.xps");
    var source = """
Sub Main()
    Application.Title = "XPscript Metadata Probe"
    Application.Icon = "app.ico"
    Application.Width = 1280
    Application.Height = 800
    Dim form As New UIForm("Fallback title")
    Print Application.Title
    Print Application.Icon
    Print Application.Width
    Print Application.Height
End Sub
""";
    File.WriteAllText(sourcePath, source);

    var generated = await AstCompiler.GenerateSourceAsync(
        sourcePath, Path.Combine(root, "generated"), CompilerDriver.CurrentRuntimeIdentifier());
    Require(generated.Contains("__XPSCRIPT_APPLICATION_ICON_BUILD__=", StringComparison.Ordinal),
        "Application.Icon was not passed through the AST build metadata pipeline");

    var missingIconPath = Path.Combine(root, "missing-icon.xps");
    File.WriteAllText(missingIconPath, """
Sub Main()
    Application.Icon = "missing.ico"
End Sub
""");
    await RequireThrowsAsync<CompilerException>(
        () => AstCompiler.CompileAsync(
            missingIconPath,
            Path.Combine(root, "missing-icon-output"),
            CompilerDriver.CurrentRuntimeIdentifier()),
        "Application.Executable.Icon file was not found");

    var emptyIconPath = Path.Combine(root, "empty-icon.xps");
    File.WriteAllText(emptyIconPath, """
Sub Main()
    Application.Icon = ""
    Print Application.Icon
End Sub
""");
    var emptyGenerated = await AstCompiler.GenerateSourceAsync(
        emptyIconPath, Path.Combine(root, "empty-generated"), CompilerDriver.CurrentRuntimeIdentifier());
    Require(!string.IsNullOrWhiteSpace(emptyGenerated), "empty Application.Icon did not emit AST source");

    if (OperatingSystem.IsWindows())
    {
        var output = Path.Combine(root, "probe-output");
        var assemblyPath = await AstCompiler.CompileAsync(sourcePath, output, CompilerDriver.CurrentRuntimeIdentifier());
        Require(File.Exists(assemblyPath), "Windows executable was not produced with Application.Icon configured");

        var emptyOutput = Path.Combine(root, "empty-output");
        var emptyAssemblyPath = await AstCompiler.CompileAsync(emptyIconPath, emptyOutput, CompilerDriver.CurrentRuntimeIdentifier());
        Require(File.Exists(emptyAssemblyPath), "Windows executable was not produced with an empty Application.Icon");
    }

    Console.WriteLine("APPLICATION-UI-METADATA=OK");
    return 0;
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task RequireThrowsAsync<T>(Func<Task> action, string messagePart) where T : Exception
{
    try
    {
        await action();
    }
    catch (T ex)
    {
        if (!ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected error containing '{messagePart}', got '{ex.Message}'.");
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(T).Name} containing '{messagePart}'.");
}

static byte[] CreateTinyIco()
{
    var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII=");
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)1);
    writer.Write((byte)1);
    writer.Write((byte)1);
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(png.Length);
    writer.Write(22);
    writer.Write(png);
    return stream.ToArray();
}
