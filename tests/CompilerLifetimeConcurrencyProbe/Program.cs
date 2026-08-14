using System.Diagnostics;
using XPScript.Compiler;

var root = Path.Combine(Directory.GetCurrentDirectory(), ".lifetime-concurrency-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    await VerifySameFilenameDiagnosticIsolationAsync();
    await VerifyKilledCompilerDoesNotAffectSiblingAsync();
    Console.WriteLine("COMPILER-LIFETIME-CONCURRENCY=OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

async Task VerifySameFilenameDiagnosticIsolationAsync()
{
    var alphaDir = Path.Combine(root, "alpha");
    var betaDir = Path.Combine(root, "beta");
    Directory.CreateDirectory(alphaDir);
    Directory.CreateDirectory(betaDir);

    var alphaSource = Path.Combine(alphaDir, "same.xps");
    var betaSource = Path.Combine(betaDir, "same.xps");
    await File.WriteAllTextAsync(alphaSource, """
Option Declare
Sub Main()
    Call MissingAlpha()
End Sub
""");
    await File.WriteAllTextAsync(betaSource, """
Option Declare
Sub Main()
    Call MissingBeta()
End Sub
""");

    var alphaCompiler = new CompilerDriver();
    var betaCompiler = new CompilerDriver();
    var rid = CompilerDriver.CurrentRuntimeIdentifier();

    var alphaTask = alphaCompiler.CompileWithResultAsync(alphaSource, Path.Combine(alphaDir, OutputName("alpha")), false, rid);
    var betaTask = betaCompiler.CompileWithResultAsync(betaSource, Path.Combine(betaDir, OutputName("beta")), false, rid);
    await Task.WhenAll(alphaTask, betaTask);

    var alpha = await alphaTask;
    var beta = await betaTask;
    if (alpha.Success || beta.Success)
        throw new Exception("Expected both same-filename diagnostic compilations to fail.");

    var alphaText = string.Join("\n", alpha.Errors.Select(e => e.Description + "\n" + e.Code + "\n" + e.MarkedCode));
    var betaText = string.Join("\n", beta.Errors.Select(e => e.Description + "\n" + e.Code + "\n" + e.MarkedCode));

    if (!alphaText.Contains("MissingAlpha", StringComparison.Ordinal) || alphaText.Contains("MissingBeta", StringComparison.Ordinal))
        throw new Exception("Alpha diagnostics crossed invocation boundaries: " + alphaText);
    if (!betaText.Contains("MissingBeta", StringComparison.Ordinal) || betaText.Contains("MissingAlpha", StringComparison.Ordinal))
        throw new Exception("Beta diagnostics crossed invocation boundaries: " + betaText);

    Console.WriteLine("SAME-FILENAME-DIAGNOSTICS=OK");
}

async Task VerifyKilledCompilerDoesNotAffectSiblingAsync()
{
    var killDir = Path.Combine(root, "kill-process");
    var survivorDir = Path.Combine(root, "survivor-process");
    Directory.CreateDirectory(killDir);
    Directory.CreateDirectory(survivorDir);

    var killSource = Path.Combine(killDir, "same.xps");
    var survivorSource = Path.Combine(survivorDir, "same.xps");
    const string validSource = "Option Declare\nSub Main()\n    Print \"PROCESS=OK\"\nEnd Sub\n";
    await File.WriteAllTextAsync(killSource, validSource);
    await File.WriteAllTextAsync(survivorSource, validSource);

    var builtCompiler = Path.Combine(
        Directory.GetCurrentDirectory(),
        "src", "XPScript.Compiler", "bin", "Release", "net10.0", "xpscriptc.dll");
    var compilerAssembly = File.Exists(builtCompiler) ? builtCompiler : typeof(CompilerDriver).Assembly.Location;
    if (string.IsNullOrWhiteSpace(compilerAssembly) || !File.Exists(compilerAssembly))
        throw new Exception("Unable to locate compiler assembly for process-level isolation test.");

    var runtimeConfig = Path.ChangeExtension(compilerAssembly, ".runtimeconfig.json");
    if (!File.Exists(runtimeConfig))
        throw new Exception("Compiler runtimeconfig is missing for process-level isolation test: " + runtimeConfig);

    var tempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
    Directory.CreateDirectory(tempRoot);
    var baseline = Directory.EnumerateDirectories(tempRoot).Select(Path.GetFullPath).ToHashSet(PathComparer());

    var killOutput = Path.Combine(killDir, OutputName("killed"));
    var survivorOutput = Path.Combine(survivorDir, OutputName("survivor"));
    using var killed = StartCompilerProcess(compilerAssembly, killSource, killOutput);
    using var survivor = StartCompilerProcess(compilerAssembly, survivorSource, survivorOutput);

    var deadline = DateTime.UtcNow.AddMinutes(3);
    while (DateTime.UtcNow < deadline)
    {
        var newWorkspaces = Directory.EnumerateDirectories(tempRoot)
            .Select(Path.GetFullPath)
            .Where(path => !baseline.Contains(path))
            .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _))
            .ToArray();
        if (newWorkspaces.Length >= 2) break;
        if (killed.HasExited || survivor.HasExited) break;
        await Task.Delay(25);
    }

    if (killed.HasExited)
        throw new Exception("Compiler selected for kill exited before its workspace was observed.");
    if (survivor.HasExited)
        throw new Exception("Survivor compiler exited before process isolation could be exercised.");

    killed.Kill(entireProcessTree: true);
    await killed.WaitForExitAsync();

    using var survivorTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    await survivor.WaitForExitAsync(survivorTimeout.Token);
    var survivorStdout = await survivor.StandardOutput.ReadToEndAsync();
    var survivorStderr = await survivor.StandardError.ReadToEndAsync();
    if (survivor.ExitCode != 0)
        throw new Exception($"Survivor compiler failed after sibling process was killed. Exit={survivor.ExitCode}\nSTDOUT:\n{survivorStdout}\nSTDERR:\n{survivorStderr}");
    if (!File.Exists(survivorOutput))
        throw new Exception("Survivor compiler did not produce its output after sibling process kill.");

    // A hard-killed process may leave its private GUID workspace behind. This is expected.
    // The production compiler does not sweep other workspaces by age alone, because age cannot
    // prove inactivity. This probe cleans only GUID workspaces created after its own baseline,
    // and only after both child compiler processes are no longer active.
    foreach (var workspace in Directory.EnumerateDirectories(tempRoot).Select(Path.GetFullPath).Where(path => !baseline.Contains(path)))
    {
        if (!Guid.TryParseExact(Path.GetFileName(workspace), "N", out _)) continue;
        try { Directory.Delete(workspace, recursive: true); } catch { }
    }

    Console.WriteLine("KILLED-COMPILER-SIBLING-UNAFFECTED=OK");
}

Process StartCompilerProcess(string compilerAssembly, string source, string output)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(source)!
    };
    psi.ArgumentList.Add(compilerAssembly);
    psi.ArgumentList.Add(source);
    psi.ArgumentList.Add("-o");
    psi.ArgumentList.Add(output);
    psi.ArgumentList.Add("--framework-dependent");
    return Process.Start(psi) ?? throw new Exception("Unable to start compiler child process.");
}

string OutputName(string stem) => OperatingSystem.IsWindows() ? stem + ".exe" : stem;
StringComparer PathComparer() => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
