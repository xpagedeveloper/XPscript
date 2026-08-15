using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-concurrency-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var source = """
Sub Main()
End Sub
""";

    var generated = new XPScriptTranspiler().Transpile(source, "evaluate-concurrency-probe.xps");
    generated += """

internal static class EvaluateConcurrencyProbeEntry
{
    public static void Main()
    {
        VerifyCallvarAndReturnIsolation();
        VerifyInvocationLocalBudgets();
        Console.WriteLine("EVALUATE-CONCURRENCY-ISOLATION=OK");
    }

    private static void VerifyCallvarAndReturnIsolation()
    {
        const int workerCount = 16;
        const int iterations = 8;
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, workerCount)
            .Select(worker => Task.Run(() =>
            {
                var input = new LSList<object?>();
                input["id"] = worker;
                input["marker"] = "worker-" + worker.ToString(CultureInfo.InvariantCulture);
                start.Wait();

                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var scalar = XPScriptEvaluateRuntime.Evaluate("Return callvar(\"id\")", input);
                    if (XPScriptRuntime.CInt(scalar) != worker)
                        throw new Exception("Concurrent Evaluate callvar crossed worker boundaries.");

                    var returned = XPScriptEvaluateRuntime.Evaluate("Return callvar", input);
                    if (returned is not ILSList returnedList)
                        throw new Exception("Concurrent Evaluate did not return a detached List.");

                    returnedList.SetValue("marker", "changed-" + worker.ToString(CultureInfo.InvariantCulture));
                    var originalMarker = XPScriptRuntime.CStr(input.GetValue("marker"));
                    if (!string.Equals(originalMarker, "worker-" + worker.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                        throw new Exception("Concurrent Evaluate returned shared caller List state.");
                }
            }))
            .ToArray();

        start.Set();
        Task.WaitAll(tasks);
    }

    private static void VerifyInvocationLocalBudgets()
    {
        const int workerCount = 4;
        const int elementsPerWorker = 60_000;
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, workerCount)
            .Select(worker => Task.Run(() =>
            {
                var input = new LSArray("Variant", true, [0], [elementsPerWorker - 1]);
                input.Set(worker + 100, elementsPerWorker - 1);
                start.Wait();

                var result = XPScriptEvaluateRuntime.Evaluate(
                    "Return callvar(" + (elementsPerWorker - 1).ToString(CultureInfo.InvariantCulture) + ")",
                    input);
                if (XPScriptRuntime.CInt(result) != worker + 100)
                    throw new Exception("Concurrent Evaluate budget probe returned another invocation's value.");
            }))
            .ToArray();

        start.Set();
        Task.WaitAll(tasks);
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
    <StartupObject>EvaluateConcurrencyProbeEntry</StartupObject>
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

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated Evaluate concurrency probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated Evaluate concurrency probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-CONCURRENCY-ISOLATION=OK", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate concurrency probe did not report success.\nSTDOUT:\n" + stdout);

    Console.WriteLine("EVALUATE-CONCURRENCY-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
