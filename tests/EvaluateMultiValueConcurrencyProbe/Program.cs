using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-multivalue-concurrency-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var source = """
Sub Main()
End Sub
""";

    var generated = new XPScriptTranspiler().Transpile(source, "evaluate-multivalue-concurrency-probe.xps");
    generated += """

internal static class EvaluateMultiValueConcurrencyProbeEntry
{
    public static void Main()
    {
        const int workerCount = 16;
        const int iterations = 12;
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, workerCount)
            .Select(worker => Task.Run(() =>
            {
                start.Wait();
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var first = worker * 1000 + iteration;
                    var second = worker * 1000 + 500 + iteration;
                    var result = XPScriptEvaluateRuntime.Evaluate(
                        "Return callvar(0) * 100000 + callvar(1)",
                        first,
                        second);
                    var expected = first * 100000L + second;
                    if (XPScriptRuntime.CLng(result) != expected)
                        throw new Exception("Concurrent multi-value Evaluate crossed invocation boundaries.");
                }
            }))
            .ToArray();

        start.Set();
        Task.WaitAll(tasks);
        Console.WriteLine("EVALUATE-MULTIVALUE-CONCURRENCY=OK");
    }
}
""";

    File.WriteAllText(Path.Combine(workspace, "Generated.cs"), generated);
    File.WriteAllText(Path.Combine(workspace, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <StartupObject>EvaluateMultiValueConcurrencyProbeEntry</StartupObject>
  </PropertyGroup>
</Project>
""");

    var startInfo = new ProcessStartInfo("dotnet", "run --project Probe.csproj -c Release")
    {
        WorkingDirectory = workspace,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated multi-value Evaluate concurrency probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated multi-value Evaluate concurrency probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-MULTIVALUE-CONCURRENCY=OK", StringComparison.Ordinal))
        throw new Exception("Generated multi-value Evaluate concurrency probe did not report success.\nSTDOUT:\n" + stdout);

    Console.WriteLine("EVALUATE-MULTIVALUE-CONCURRENCY-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
