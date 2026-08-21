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

    var generated = new XPScriptTranspiler().Transpile(source, sourcePath, CompilerDriver.CurrentRuntimeIdentifier());
    Require(generated.Contains("__xps_application_title", StringComparison.Ordinal), "Application.Title was not mapped to application state");
    Require(generated.Contains("__xps_application_icon", StringComparison.Ordinal), "Application.Icon was not mapped to application state");
    Require(generated.Contains("__xps_application_width", StringComparison.Ordinal), "Application.Width was not mapped to application state");
    Require(generated.Contains("__xps_application_height", StringComparison.Ordinal), "Application.Height was not mapped to application state");
    Require(generated.Contains("applicationTitle = XPScriptRuntime.CStr", StringComparison.Ordinal), "application title was not added to UI bridge metadata");
    Require(generated.Contains("applicationIcon = XPScriptRuntime.CStr", StringComparison.Ordinal), "application icon was not added to UI bridge metadata");
    Require(generated.Contains("ReadApplicationDimension(\"__xps_application_width\"", StringComparison.Ordinal), "application default width was not added to desktop UI metadata");
    Require(generated.Contains("ReadApplicationDimension(\"__xps_application_height\"", StringComparison.Ordinal), "application default height was not added to desktop UI metadata");
    Require(generated.Contains("XPScriptApplicationMetadataRuntime.WrapWebHtml", StringComparison.Ordinal), "web application title/favicon metadata was not installed");
    Require(generated.Contains("__XPSCRIPT_APPLICATION_ICON_BUILD__=", StringComparison.Ordinal), "Windows executable icon build marker was not emitted");

    if (OperatingSystem.IsWindows())
    {
        var output = Path.Combine(root, "probe.exe");
        await new CompilerDriver().CompileAsync(sourcePath, output, selfContained: false, runtimeIdentifier: CompilerDriver.CurrentRuntimeIdentifier());
        Require(File.Exists(output), "Windows executable was not produced with Application.Icon configured");
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
