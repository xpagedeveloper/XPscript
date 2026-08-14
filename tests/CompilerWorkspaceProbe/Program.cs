using System.Text.RegularExpressions;
using XPScript.Compiler;

var compilerTempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
// Keep source/output under the checked-out repository so macOS /var -> /private/var
// temp-path indirection does not exercise the separate output-symlink policy here.
var probeRoot = Path.Combine(Directory.GetCurrentDirectory(), ".workspace-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);

try
{
    var sourcePath = Path.Combine(probeRoot, "workspace-probe.xps");
    await File.WriteAllTextAsync(sourcePath, """
Option Declare

Sub Main()
    Print "WORKSPACE=OK"
End Sub
""");

    var first = await RunAndObserveAsync(sourcePath, Path.Combine(probeRoot, "first-output.exe"));
    var second = await RunAndObserveAsync(sourcePath, Path.Combine(probeRoot, "second-output.exe"));

    if (first.Equals(second, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Two compiler invocations reused the same temporary workspace: " + first);

    Console.WriteLine("COMPILER-WORKSPACE-FIRST=" + Path.GetFileName(first));
    Console.WriteLine("COMPILER-WORKSPACE-SECOND=" + Path.GetFileName(second));
    Console.WriteLine("COMPILER-GUID-WORKSPACE=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
}

async Task<string> RunAndObserveAsync(string sourcePath, string outputPath)
{
    Directory.CreateDirectory(compilerTempRoot);
    var baseline = Directory.EnumerateDirectories(compilerTempRoot)
        .Select(Path.GetFullPath)
        .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    var driver = new CompilerDriver();
    var compileTask = driver.CompileAsync(sourcePath, outputPath, selfContained: false, CompilerDriver.CurrentRuntimeIdentifier());

    string? observedWorkspace = null;
    var sawProject = false;
    var sawProgram = false;
    var sawPublish = false;
    var deadline = DateTime.UtcNow.AddMinutes(3);

    while (!compileTask.IsCompleted && DateTime.UtcNow < deadline)
    {
        foreach (var directory in Directory.EnumerateDirectories(compilerTempRoot))
        {
            var full = Path.GetFullPath(directory);
            if (baseline.Contains(full)) continue;

            var leaf = Path.GetFileName(full);
            if (!Regex.IsMatch(leaf, "^[0-9a-fA-F]{32}$")) continue;

            observedWorkspace ??= full;
            if (!full.Equals(observedWorkspace, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                continue;

            sawProject |= File.Exists(Path.Combine(full, "Generated.csproj"));
            sawProgram |= File.Exists(Path.Combine(full, "Program.cs"));
            sawPublish |= Directory.Exists(Path.Combine(full, "publish"));
        }

        await Task.Delay(20);
    }

    await compileTask;

    if (observedWorkspace is null)
        throw new Exception("Did not observe a GUID compiler workspace beneath " + compilerTempRoot);
    if (!sawProject)
        throw new Exception("Generated.csproj was not observed inside the invocation workspace.");
    if (!sawProgram)
        throw new Exception("Program.cs was not observed inside the invocation workspace.");
    if (!sawPublish)
        throw new Exception("publish directory was not observed inside the invocation workspace.");
    if (Directory.Exists(observedWorkspace))
        throw new Exception("Compiler invocation workspace was not cleaned after successful compilation: " + observedWorkspace);
    if (!File.Exists(outputPath))
        throw new Exception("Compiler output was not produced: " + outputPath);

    return observedWorkspace;
}
