using System.Diagnostics;
using System.IO.Compression;
using System.Text;

var repo = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var output = Path.Combine(repo, "out", "spreadsheet-ast-probe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
var compiler = Path.Combine(repo, "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll");
foreach (var name in new[] { "spreadsheet-runtime", "spreadsheet-reference", "spreadsheet-unused", "spreadsheet-csv-inferred", "spreadsheet-tocsv-inferred", "spreadsheet-fromworksheet" })
{
    var source = Copy(name, Path.Combine(repo, "tests/ast-compile-probe", name + ".xps"), "ast");
    var directory = Path.Combine(Path.GetDirectoryName(source)!, "program");
    await Run("dotnet", [compiler, "ast-compile", source, "-o", directory], output);
    var result = await Run("dotnet", [Path.Combine(directory, "Generated.dll")], Path.GetDirectoryName(source)!);
    var expected = name switch
    {
        "spreadsheet-runtime" => "SPREADSHEET_AST_OK", "spreadsheet-reference" => "SPREADSHEET_REFERENCE_OK",
        "spreadsheet-unused" => "XPSpreadsheet XPWorksheet XPCell XPRange ToCsv ToSpreadsheet", _ => "Åsa"
    };
    if (result.Trim() != expected) throw new Exception("Spreadsheet fixture result mismatch: " + name + result);
    var assembly = System.Reflection.Assembly.LoadFile(Path.Combine(directory, "Generated.dll"));
    if (name == "spreadsheet-unused")
    {
        if (assembly.GetType("XPScriptSpreadsheet") is not null) throw new Exception("Unused spreadsheet runtime leaked.");
    }
    else
    {
        if (assembly.GetType("XPScriptSpreadsheet") is null) throw new Exception("Native spreadsheet runtime missing.");
        if (name == "spreadsheet-runtime" && assembly.GetType("XPScriptCsvDocument") is not null) throw new Exception("Spreadsheet-only output pulled in CSV.");
        if (name.EndsWith("inferred") && assembly.GetType("XPScriptCsvDocument") is null) throw new Exception("Inferred CSV interoperability runtime missing.");
    }
    if (assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("ClosedXML") || a.Name.StartsWith("Magick.NET") || a.Name.StartsWith("Microsoft.Data.Sqlite")))
        throw new Exception("Spreadsheet acquired unnecessary addon references.");
    if (name == "spreadsheet-fromworksheet")
    {
        var normalSource = Copy(name, Path.Combine(repo, "tests/ast-compile-probe", name + ".xps"), "normal");
        var normalDirectory = Path.GetDirectoryName(normalSource)!;
        var normalProgram = Path.Combine(normalDirectory, OperatingSystem.IsWindows() ? "program.exe" : "program");
        await Run("dotnet", [compiler, "legacy-compile", normalSource, "-o", normalProgram, "--single-file=false", "--runtime=false"], output);
        if ((await Run(normalProgram, [], normalDirectory)).Trim() != expected)
            throw new Exception("Static FromWorksheet normal/AST parity failed.");
    }
}
foreach (var name in new[] { "xpspreadsheet-basic", "xpspreadsheet-worksheets", "xpspreadsheet-styles", "xpspreadsheet-ranges", "xpspreadsheet-formatting", "xpspreadsheet-autofilter", "xpspreadsheet-csv-interop" })
{
    string? expected = null;
    foreach (var mode in new[] { "ast", "normal" })
    {
        var source = Copy(name, Path.Combine(repo, "demo/spreadsheet", name + ".xps"), mode);
        var directory = Path.GetDirectoryName(source)!;
        var program = Path.Combine(directory, mode == "ast" ? "program" : OperatingSystem.IsWindows() ? "program.exe" : "program");
        await Run("dotnet", [compiler, mode == "ast" ? "ast-compile" : "legacy-compile", source, "-o", program, ..(mode == "normal" ? new[] { "--single-file=false", "--runtime=false" } : [])], output);
        var result = await Run(mode == "ast" ? "dotnet" : program, mode == "ast" ? [Path.Combine(program, "Generated.dll")] : [], directory);
        if (!result.Contains("=OK")) throw new Exception("Spreadsheet demo did not complete: " + name);
        if (expected is not null && expected.Replace("\r\n", "\n") != result.Replace("\r\n", "\n")) throw new Exception("Normal/AST spreadsheet demo output differs: " + name);
        expected = result;
    }
}
// Existing deterministic ZIP safety fixtures, exercised through real AST construction.
var fixtures = Path.Combine(output, "security");
await Run("dotnet", ["run", "--project", Path.Combine(repo, "tests/SpreadsheetSecurityFixtures"), "-c", "Release", "--", fixtures], repo);
foreach (var name in new[] { "malformed", "missing-workbook", "too-many-parts", "oversized-part" })
{
    var source = Path.Combine(output, "reject-" + name + ".xps");
    await File.WriteAllTextAsync(source, "Sub Main()\nDim book As New XPSpreadsheet(\"" + Path.Combine(fixtures, name + ".xlsx").Replace("\"", "\"\"") + "\")\nEnd Sub");
    var directory = Path.Combine(output, "reject-" + name);
    await Run("dotnet", [compiler, "ast-compile", source, "-o", directory], output);
    var failure = await ExecuteExpectedFailure("dotnet", [Path.Combine(directory, "Generated.dll")], output);
    if (failure.Exit == 0) throw new Exception("Unsafe XLSX accepted: " + name);
    if (!failure.Text.Contains("InvalidDataException", StringComparison.Ordinal)
        && !failure.Text.Contains("could not open", StringComparison.OrdinalIgnoreCase)
        && !failure.Text.Contains("exceeds", StringComparison.OrdinalIgnoreCase)
        && !failure.Text.Contains("too many parts", StringComparison.OrdinalIgnoreCase)
        && !failure.Text.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Unsafe XLSX produced an unexpected diagnostic: " + name + "\n" + failure.Text);
}
// Remove the marker from a real workbook: external files must remain read-only.
var original = Directory.GetFiles(output, "roundtrip.xlsx", SearchOption.AllDirectories).Single();
var external = Path.Combine(output, "external.xlsx");
using (var read = ZipFile.OpenRead(original))
using (var write = ZipFile.Open(external, ZipArchiveMode.Create))
    foreach (var entry in read.Entries.Where(e => e.FullName != "docProps/custom.xml"))
    {
        var copy = write.CreateEntry(entry.FullName);
        using var input = entry.Open(); using var target = copy.Open(); input.CopyTo(target);
    }
var before = await File.ReadAllBytesAsync(external);
var externalSource = Path.Combine(output, "external.xps");
await File.WriteAllTextAsync(externalSource, """
Sub Main()
    Dim book As New XPSpreadsheet("external.xlsx")
    If book.CanUpdate Or book.CreatedByXPScript Then Error 9991, "External workbook marked writable"
    Dim code As Long
    Dim bytes As Variant
    On Error Resume Next
    bytes = book.ToBytes()
    code = Err
    On Error GoTo 0
    If code = 0 Then Error 9992, "External workbook serialized"
    On Error Resume Next
    Call book.Save()
    code = Err
    On Error GoTo 0
    If code = 0 Then Error 9993, "External workbook overwritten"
    Call book.SaveAsSimple("converted.xlsx")
    If Not book.CanUpdate Then Error 9994, "Explicit conversion failed"
    Print "EXTERNAL_WORKBOOK_GUARD_OK"
End Sub
""");
await Run("dotnet", [compiler, "ast-compile", externalSource, "-o", Path.Combine(output, "external")], output);
if ((await Run("dotnet", [Path.Combine(output, "external/Generated.dll")], output)).Trim() != "EXTERNAL_WORKBOOK_GUARD_OK") throw new Exception("External workbook guard failed.");
var after = await File.ReadAllBytesAsync(external);
if (!before.SequenceEqual(after)) throw new Exception("External workbook bytes changed.");
Console.WriteLine("AST_SPREADSHEET_RUNTIME_OK");

string Copy(string name, string source, string mode)
{
    var directory = Path.Combine(output, name, mode); Directory.CreateDirectory(directory);
    var copy = Path.Combine(directory, name + ".xps"); File.Copy(source, copy); return copy;
}
async Task<string> Run(string file, string[] commandArguments, string directory)
{
    var result = await Execute(file, commandArguments, directory);
    if (result.Exit != 0) throw new Exception($"Process failed ({result.Exit}): {result.Text}");
    return result.Text;
}
async Task<(int Exit, string Text)> Execute(string file, string[] commandArguments, string directory)
{
    var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
    foreach (var argument in commandArguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    try
    {
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(120));
        return (process.ExitCode, await ReadOutput(stdout, stderr));
    }
    catch
    {
        TryKill(process);
        throw;
    }
}

async Task<(int Exit, string Text)> ExecuteExpectedFailure(string file, string[] commandArguments, string directory)
{
    var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
    foreach (var argument in commandArguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    try
    {
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        return (process.ExitCode, await ReadOutput(stdout, stderr));
    }
    catch (TimeoutException)
    {
        TryKill(process);
        return (-1, "Expected unsafe XLSX rejection timed out after the child reported its failure.");
    }
}

async Task<string> ReadOutput(Task<string> stdout, Task<string> stderr)
{
    try
    {
        await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
        return await stdout + await stderr;
    }
    catch (TimeoutException)
    {
        return "Process output timed out after the child exited.";
    }
}

static void TryKill(Process process)
{
    try
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
    }
    catch (InvalidOperationException) { }
    catch (System.ComponentModel.Win32Exception) { }
}
