using System.Diagnostics;
using XPScript.Compiler;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var fixtureProject = Path.Combine(repoRoot, "tests", "ManagedReferenceFixture", "ManagedReferenceFixture.csproj");
var fixtureDll = Path.Combine(repoRoot, "tests", "ManagedReferenceFixture", "bin", "Release", "net10.0", "ManagedReferenceFixture.dll");
var workRoot = Path.Combine(repoRoot, ".managed-null-interop-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workRoot);

try
{
    await RunAsync("dotnet", ["build", fixtureProject, "-c", "Release", "--nologo"]);
    File.Copy(fixtureDll, Path.Combine(workRoot, "ManagedReferenceFixture.dll"), true);

    var sourcePath = Path.Combine(workRoot, "managed-null-interop.xps");
    await File.WriteAllTextAsync(sourcePath, """
Reference "ManagedReferenceFixture.dll"
Option Declare

Sub Main()
    Dim emptyValue As Variant
    Dim nullValue As Variant
    nullValue = Null

    Print "EMPTY_MANAGED=" & ManagedReferenceFixture.FixtureApi.DescribeObject(emptyValue)
    Print "NULL_MANAGED=" & ManagedReferenceFixture.FixtureApi.DescribeObject(nullValue)
End Sub
""");

    var outputPath = Path.Combine(workRoot, OperatingSystem.IsWindows() ? "managed-null-interop.exe" : "managed-null-interop");
    await new CompilerDriver().CompileAsync(sourcePath, outputPath, false, CompilerDriver.CurrentRuntimeIdentifier());

    var psi = new ProcessStartInfo(outputPath)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    using var process = Process.Start(psi) ?? throw new Exception("Unable to start managed Null interop fixture.");
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception("Managed Null interop executable failed: " + stderr);

    var lines = stdout.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    if (!lines.Contains("EMPTY_MANAGED=CLR-NULL", StringComparer.Ordinal))
        throw new Exception("Managed interop did not expose XPScript EMPTY as CLR null. Output: " + stdout);
    if (!lines.Contains("NULL_MANAGED=DBNULL", StringComparer.Ordinal))
        throw new Exception("Managed interop did not expose XPScript NULL as DBNull.Value. Output: " + stdout);
    if (stdout.Contains("XPScriptNullRuntime", StringComparison.Ordinal) || stdout.Contains("NullSentinel", StringComparison.Ordinal))
        throw new Exception("Managed interop leaked an internal XPScript NULL representation.");

    Console.WriteLine("MANAGED_NULL_INTEROP=OK");
}
finally
{
    try { Directory.Delete(workRoot, recursive: true); } catch { }
}

static async Task RunAsync(string fileName, IReadOnlyList<string> args)
{
    var psi = new ProcessStartInfo(fileName)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new Exception("Unable to start process: " + fileName);
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new Exception($"Process failed: {fileName} {string.Join(' ', args)}\n{stdout}\n{stderr}");
}
