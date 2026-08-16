using System.Text.RegularExpressions;
using XPScript.Compiler;

var compilerTempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
Directory.CreateDirectory(compilerTempRoot);

// Keep source/output under the checked-out repository so macOS /var -> /private/var
// temp-path indirection does not exercise the separate output-symlink policy here.
var probeRoot = Path.Combine(Directory.GetCurrentDirectory(), ".workspace-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);

// A sibling directory under the compiler temp root must never be removed by another
// invocation's cleanup. It deliberately does not look like a compiler GUID workspace.
var sentinelRoot = Path.Combine(compilerTempRoot, "cleanup-sentinel-" + Guid.NewGuid().ToString("N"));
var sentinelFile = Path.Combine(sentinelRoot, "keep.txt");
Directory.CreateDirectory(sentinelRoot);
await File.WriteAllTextAsync(sentinelFile, "KEEP-ME");

// Simulate a compiler process that was killed after creating its invocation workspace.
// The directory deliberately has the exact 32-hex GUID shape used by real workspaces.
// A later compile must never treat this stale state as trusted or reusable.
var staleWorkspace = Path.Combine(compilerTempRoot, Guid.NewGuid().ToString("N"));
var staleMarker = Path.Combine(staleWorkspace, "stale-marker.txt");
var staleProgram = Path.Combine(staleWorkspace, "Program.cs");
Directory.CreateDirectory(staleWorkspace);
await File.WriteAllTextAsync(staleMarker, "STALE-WORKSPACE-MUST-NOT-BE-TOUCHED");
await File.WriteAllTextAsync(staleProgram, "throw new Exception(\"STALE WORKSPACE REUSED\");");

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
    AssertSentinelUnchanged();
    AssertStaleWorkspaceUnchanged(first);

    var second = await RunAndObserveAsync(sourcePath, Path.Combine(probeRoot, "second-output.exe"));
    AssertSentinelUnchanged();
    AssertStaleWorkspaceUnchanged(second);

    if (first.Equals(second, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Two compiler invocations reused the same temporary workspace: " + first);

    Console.WriteLine("COMPILER-WORKSPACE-FIRST=" + Path.GetFileName(first));
    Console.WriteLine("COMPILER-WORKSPACE-SECOND=" + Path.GetFileName(second));
    Console.WriteLine("COMPILER-CLEANUP-OWNERSHIP=OK");
    Console.WriteLine("COMPILER-STALE-WORKSPACE-NONREUSE=OK");
    Console.WriteLine("COMPILER-GUID-WORKSPACE=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
    try { Directory.Delete(sentinelRoot, recursive: true); } catch { }
    try { Directory.Delete(staleWorkspace, recursive: true); } catch { }
}

void AssertSentinelUnchanged()
{
    if (!Directory.Exists(sentinelRoot))
        throw new Exception("Compiler cleanup removed an unrelated sibling directory under the XPScript temp root.");
    if (!File.Exists(sentinelFile))
        throw new Exception("Compiler cleanup removed a sentinel file from an unrelated sibling directory.");
    if (File.ReadAllText(sentinelFile) != "KEEP-ME")
        throw new Exception("Compiler cleanup modified a sentinel file in an unrelated sibling directory.");
}

void AssertStaleWorkspaceUnchanged(string observedWorkspace)
{
    if (observedWorkspace.Equals(staleWorkspace, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        throw new Exception("Compiler reused a stale GUID workspace left by a previous crashed invocation.");
    if (!Directory.Exists(staleWorkspace))
        throw new Exception("Compiler cleanup removed a stale workspace owned by another invocation.");
    if (!File.Exists(staleMarker) || File.ReadAllText(staleMarker) != "STALE-WORKSPACE-MUST-NOT-BE-TOUCHED")
        throw new Exception("Compiler modified the stale workspace marker.");
    if (!File.Exists(staleProgram) || File.ReadAllText(staleProgram) != "throw new Exception(\"STALE WORKSPACE REUSED\");")
        throw new Exception("Compiler modified generated source in a stale workspace.");
}

async Task<string> RunAndObserveAsync(string sourcePath, string outputPath)
{
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
