using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-clr-array-budget-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var generated = new XPScriptTranspiler().Transpile("Sub Main()\nEnd Sub\n", "evaluate-clr-array-budget-probe.xps");
    generated += """

internal static class EvaluateClrArrayBudgetProbeEntry
{
    public static void Main()
    {
        var accepted = Array.CreateInstance(typeof(object), 100000);
        accepted.SetValue(42, 99999);
        var acceptedSnapshot = XPScriptEvaluateCollectionRuntime.Snapshot(accepted);
        if (acceptedSnapshot is not Array acceptedArray)
            throw new Exception("Evaluate CLR array snapshot did not return an array.");
        if (acceptedArray.LongLength != 100000 || Convert.ToInt32(acceptedArray.GetValue(99999)) != 42)
            throw new Exception("Evaluate CLR array exact-boundary snapshot changed length or data.");

        var rejected = Array.CreateInstance(typeof(object), 100001);
        try
        {
            _ = XPScriptEvaluateCollectionRuntime.Snapshot(rejected);
            throw new Exception("Evaluate accepted a CLR array above the 100000-element budget.");
        }
        catch (XPScriptRuntimeException ex)
        {
            if (ex.Number != 5)
                throw new Exception("Evaluate CLR array over-budget expected error 5 but got " + ex.Number + ".");
            if (!ex.Message.Contains("maximum element budget of 100000", StringComparison.Ordinal))
                throw new Exception("Evaluate CLR array over-budget returned an unexpected diagnostic: " + ex.Message);
        }

        Console.WriteLine("EVALUATE-CLR-ARRAY-BUDGET=OK");
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
    <StartupObject>EvaluateClrArrayBudgetProbeEntry</StartupObject>
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

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated Evaluate CLR array budget probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated Evaluate CLR array budget probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-CLR-ARRAY-BUDGET=OK", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate CLR array budget probe did not report success.\nSTDOUT:\n" + stdout);

    Console.WriteLine("EVALUATE-CLR-ARRAY-BUDGET-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
