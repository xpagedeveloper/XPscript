using System.Diagnostics;
using System.Reflection;
using XPScript.Compiler;

var outputRoot = Path.GetFullPath(args.Length == 0 ? "out/generated-dependency-probe" : args[0]);
var backend = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.RunRoslynCompiler", true)!;
var compile = backend.GetMethod("CompileAsync", BindingFlags.Public | BindingFlags.Static)!;
async Task<string> Build(string source, string name) => await (Task<string>)compile.Invoke(null,
    [source, Path.Combine(outputRoot, name), false, CancellationToken.None])!;
var mappedDiagnosticSource = "#line 42 \"mapped-backend.xps\"\ninternal static class Program { public static void Main() { MissingType value = 1; } }";
try
{
    await Build(mappedDiagnosticSource, "mapped-diagnostic");
    throw new Exception("Generated backend diagnostic fixture unexpectedly compiled.");
}
catch (CompilerException compilerException)
{
    var diagnostic = compilerException.GeneratedDiagnostics.Single();
    if (diagnostic.File != "mapped-backend.xps" || diagnostic.Line != 42 || diagnostic.Position <= 0 ||
        diagnostic.EndLine != 42 || diagnostic.EndColumn <= diagnostic.Position || diagnostic.Category != "code-generation")
        throw new Exception("Generated backend diagnostics lost the mapped source span.");
}
async Task<string> Run(string assembly)
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    info.ArgumentList.Add(assembly);
    using var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var text = await stdout;
    if (process.ExitCode != 0) throw new Exception(await stderr);
    return text.Trim();
}

var plain = "internal static class Program { public static void Main() => System.Console.WriteLine(7); }";
if (ApplicationDependencyCatalog.Detect(plain).Count != 0) throw new Exception("Plain program selected packages.");
var plainAssembly = await Build(plain, "plain");
if (await Run(plainAssembly) != "7" || Directory.EnumerateFiles(Path.GetDirectoryName(plainAssembly)!, "Magick*", SearchOption.AllDirectories).Any())
    throw new Exception("Plain output unexpectedly acquired image dependencies.");

var source = new XPScriptTranspiler().Transpile("""
Sub Main()
    Dim image As XPImage
    Set image = New XPImage(2, 3, "#123456")
    Print image.Width
    Print image.Height
    Call image.Recycle()
End Sub
""", "dependency-image.xps", CompilerDriver.CurrentRuntimeIdentifier());
var imagePackages = ApplicationDependencyCatalog.Detect(source);
if (imagePackages.Count != 1 || imagePackages[0].Name != "Magick.NET-Q16-AnyCPU") throw new Exception("Image program selected the wrong packages.");
foreach (var name in new[] { "image", "image-cached" })
{
    var assembly = await Build(source, name);
    if ((await Run(assembly)).Replace("\r\n", "\n") != "2\n3") throw new Exception("Native image runtime did not execute.");
    var notice = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(assembly)!, "Third-party-license.txt"));
    if (!notice.Contains("Magick.NET-Q16-AnyCPU") || !notice.Contains("Magick.NET.Core") || !notice.Contains("Apache License") || notice.Contains("No package-local license text was found"))
        throw new Exception("Direct/transitive image licenses were lost, including on cached builds.");
}

// A second addon proves selection is catalog-driven, rather than an image-only branch.
var sqliteSource = """
internal sealed class XPScriptDbSqlite { }
internal static class Program
{
    public static void Main()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select 42";
        System.Console.WriteLine(command.ExecuteScalar());
    }
}
""";
var sqliteAssembly = await Build(sqliteSource, "sqlite");
if (await Run(sqliteAssembly) != "42") throw new Exception("Second addon native runtime did not execute.");
var sqliteNotice = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(sqliteAssembly)!, "Third-party-license.txt"));
if (!sqliteNotice.Contains("Microsoft.Data.Sqlite") || sqliteNotice.Contains("Magick.NET")) throw new Exception("Unrelated addon notices leaked into SQLite output.");
var combinedSource = "internal sealed class XPImage { }\n" + sqliteSource.Replace(
    "System.Console.WriteLine(command.ExecuteScalar());",
    "using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 2, 3); System.Console.WriteLine(image.Width + \":\" + command.ExecuteScalar());");
var combinedAssembly = await Build(combinedSource, "combined");
if (await Run(combinedAssembly) != "2:42") throw new Exception("Combined addons did not execute together.");
var combinedNotice = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(combinedAssembly)!, "Third-party-license.txt"));
if (!combinedNotice.Contains("Magick.NET.Core") || !combinedNotice.Contains("Microsoft.Data.Sqlite")) throw new Exception("Combined addon notices lost part of the dependency graph.");
Console.WriteLine("GENERATED_DEPENDENCIES_OK");
