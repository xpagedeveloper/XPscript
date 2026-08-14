using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using XPScript.Compiler;

const int InvocationCount = 10;
var repoRoot = Directory.GetCurrentDirectory();
var sourcePath = Path.Combine(repoRoot, "samples", "include-source", "root.xps");
var sharedIncludePath = Path.Combine(repoRoot, "samples", "include-source", "lib", "common.xps");
var compilerTempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
var outputRoot = Path.Combine(repoRoot, ".parallel-workspace-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outputRoot);
Directory.CreateDirectory(compilerTempRoot);

if (!File.Exists(sourcePath) || !File.Exists(sharedIncludePath))
    throw new Exception("Shared root/include fixture was not found.");

var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
var baseline = Directory.EnumerateDirectories(compilerTempRoot)
    .Select(Path.GetFullPath)
    .ToHashSet(comparison);
var observed = new ConcurrentDictionary<string, WorkspaceState>(comparison);

try
{
    var tasks = Enumerable.Range(0, InvocationCount)
        .Select(index => new CompilerDriver().CompileAsync(
            sourcePath,
            Path.Combine(outputRoot, $"parallel-{index:D2}.exe"),
            selfContained: false,
            CompilerDriver.CurrentRuntimeIdentifier()))
        .ToArray();

    var all = Task.WhenAll(tasks);
    var deadline = DateTime.UtcNow.AddMinutes(6);
    while (!all.IsCompleted && DateTime.UtcNow < deadline)
    {
        ObserveWorkspaces();
        await Task.Delay(10);
    }

    await all;
    ObserveWorkspaces();

    if (observed.Count != InvocationCount)
        throw new Exception($"Expected {InvocationCount} unique compiler workspaces, observed {observed.Count}: " + string.Join(", ", observed.Keys.Select(Path.GetFileName)));

    foreach (var pair in observed)
    {
        if (!pair.Value.SawProject || !pair.Value.SawProgram || !pair.Value.SawPublish)
            throw new Exception("Invocation-local generated state was incomplete in workspace " + pair.Key);
        if (Directory.Exists(pair.Key))
            throw new Exception("A parallel compiler workspace remained after its invocation completed: " + pair.Key);
    }

    for (var index = 0; index < InvocationCount; index++)
    {
        var output = Path.Combine(outputRoot, $"parallel-{index:D2}.exe");
        if (!File.Exists(output))
            throw new Exception("Parallel compilation did not produce its distinct output: " + output);
    }

    Console.WriteLine("PARALLEL-COMPILATIONS=" + InvocationCount);
    Console.WriteLine("SHARED-ROOT=" + Path.GetFileName(sourcePath));
    Console.WriteLine("SHARED-INCLUDE=" + Path.GetFileName(sharedIncludePath));
    Console.WriteLine("UNIQUE-WORKSPACES=" + observed.Count);
    Console.WriteLine("COMPILER-PARALLEL-ISOLATION=OK");
}
finally
{
    try { Directory.Delete(outputRoot, recursive: true); } catch { }
}

void ObserveWorkspaces()
{
    IEnumerable<string> directories;
    try { directories = Directory.EnumerateDirectories(compilerTempRoot).ToArray(); }
    catch (DirectoryNotFoundException) { return; }

    foreach (var directory in directories)
    {
        var full = Path.GetFullPath(directory);
        if (baseline.Contains(full)) continue;
        var leaf = Path.GetFileName(full);
        if (!Regex.IsMatch(leaf, "^[0-9a-fA-F]{32}$")) continue;

        var state = observed.GetOrAdd(full, _ => new WorkspaceState());
        state.SawProject |= File.Exists(Path.Combine(full, "Generated.csproj"));
        state.SawProgram |= File.Exists(Path.Combine(full, "Program.cs"));
        state.SawPublish |= Directory.Exists(Path.Combine(full, "publish"));
    }
}

sealed class WorkspaceState
{
    public bool SawProject;
    public bool SawProgram;
    public bool SawPublish;
}
