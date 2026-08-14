using System.Text.RegularExpressions;
using XPScript.Compiler;

var compilerTempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
var probeRoot = Path.Combine(Directory.GetCurrentDirectory(), ".process-isolation-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);
Directory.CreateDirectory(compilerTempRoot);
var preexistingWorkspace = Path.Combine(compilerTempRoot, Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(preexistingWorkspace);
var preexistingSentinel = Path.Combine(preexistingWorkspace, "do-not-reuse.txt");
await File.WriteAllTextAsync(preexistingSentinel, "existing-workspace");

try
{
    var sourcePath = Path.Combine(probeRoot, "process-isolation.xps");
    await File.WriteAllTextAsync(sourcePath, """
Option Declare

Sub Main()
    Print "PROCESS-ISOLATION=OK"
End Sub
""");

    // Force a failure only after dotnet publish has run by using an existing directory
    // as the requested executable path. CompilerOutputPublisher must reject it.
    var invalidOutputDirectory = Path.Combine(probeRoot, "invalid-output-directory");
    Directory.CreateDirectory(invalidOutputDirectory);
    var failed = await RunAndObserveAsync(sourcePath, invalidOutputDirectory, expectFailure: true);

    var successfulOutput = Path.Combine(probeRoot, OperatingSystem.IsWindows() ? "success.exe" : "success");
    var succeeded = await RunAndObserveAsync(sourcePath, successfulOutput, expectFailure: false);

    if (failed.Workspace.Equals(succeeded.Workspace, PathComparison()))
        throw new Exception("A later compiler invocation reused writable state from a failed invocation: " + failed.Workspace);
    if (failed.Workspace.Equals(preexistingWorkspace, PathComparison()) || succeeded.Workspace.Equals(preexistingWorkspace, PathComparison()))
        throw new Exception("Compiler reused a pre-existing GUID workspace directory.");
    if (!File.Exists(preexistingSentinel) || await File.ReadAllTextAsync(preexistingSentinel) != "existing-workspace")
        throw new Exception("Compiler modified or removed a pre-existing workspace candidate.");
    if (Directory.Exists(failed.Workspace) || Directory.Exists(succeeded.Workspace))
        throw new Exception("Compiler workspace remained after invocation cleanup.");

    Console.WriteLine("FAILED-WORKSPACE=" + Path.GetFileName(failed.Workspace));
    Console.WriteLine("SUCCESS-WORKSPACE=" + Path.GetFileName(succeeded.Workspace));
    Console.WriteLine("FAILED-BUILD-NO-REUSE=OK");
    Console.WriteLine("EXISTING-WORKSPACE-NO-REUSE=OK");
    Console.WriteLine("PROCESS-STATE-ISOLATION=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
    try { Directory.Delete(preexistingWorkspace, recursive: true); } catch { }
}

async Task<Observation> RunAndObserveAsync(string sourcePath, string outputPath, bool expectFailure)
{
    Directory.CreateDirectory(compilerTempRoot);
    var baseline = Directory.EnumerateDirectories(compilerTempRoot)
        .Select(Path.GetFullPath)
        .ToHashSet(PathComparer());

    var driver = new CompilerDriver();
    var compileTask = driver.CompileAsync(sourcePath, outputPath, selfContained: false, CompilerDriver.CurrentRuntimeIdentifier());

    string? workspace = null;
    var sawProject = false;
    var sawProgram = false;
    var sawPublish = false;
    var sawProcessTemp = false;
    var sawDotnetHome = false;
    var sawNugetPackages = false;
    var deadline = DateTime.UtcNow.AddMinutes(4);

    while (!compileTask.IsCompleted && DateTime.UtcNow < deadline)
    {
        foreach (var directory in Directory.EnumerateDirectories(compilerTempRoot))
        {
            var full = Path.GetFullPath(directory);
            if (baseline.Contains(full)) continue;
            if (!Regex.IsMatch(Path.GetFileName(full), "^[0-9a-fA-F]{32}$")) continue;

            workspace ??= full;
            if (!full.Equals(workspace, PathComparison())) continue;

            sawProject |= File.Exists(Path.Combine(full, "Generated.csproj"));
            sawProgram |= File.Exists(Path.Combine(full, "Program.cs"));
            sawPublish |= Directory.Exists(Path.Combine(full, "publish"));
            sawProcessTemp |= Directory.Exists(Path.Combine(full, "process-temp"));
            sawDotnetHome |= Directory.Exists(Path.Combine(full, "dotnet-home"));
            sawNugetPackages |= Directory.Exists(Path.Combine(full, "nuget-packages"));
        }
        await Task.Delay(20);
    }

    Exception? failure = null;
    try { await compileTask; }
    catch (Exception ex) { failure = ex; }

    if (expectFailure && failure is null)
        throw new Exception("Expected compiler publication failure did not occur.");
    if (!expectFailure && failure is not null)
        throw new Exception("Unexpected compiler failure.", failure);
    if (workspace is null)
        throw new Exception("Did not observe a GUID compiler workspace.");
    if (!sawProject || !sawProgram || !sawPublish)
        throw new Exception("Invocation-local generated project/source/publish state was not observed.");
    if (!sawProcessTemp || !sawDotnetHome || !sawNugetPackages)
        throw new Exception("Invocation-local process-temp, dotnet-home or NuGet package directories were not observed.");
    if (Directory.Exists(workspace))
        throw new Exception("Compiler workspace was not cleaned in finally: " + workspace);
    if (!expectFailure && !File.Exists(outputPath))
        throw new Exception("Successful compiler output was not produced: " + outputPath);

    return new Observation(workspace);
}

StringComparer PathComparer() => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
StringComparison PathComparison() => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

sealed record Observation(string Workspace);
