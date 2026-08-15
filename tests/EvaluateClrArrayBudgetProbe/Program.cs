using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-clr-array-budget-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var source = """
Sub Main()
End Sub
""";

    var generated = new XPScriptTranspiler().Transpile(source, "evaluate-clr-array-budget-probe.xps");
    generated += """

internal static class EvaluateClrArrayBudgetProbeEntry
{
    public static void Main()
    {
        VerifyExactBoundaryAccepted();
        VerifyFirstOverBoundaryRejected();
        Console.WriteLine("EVALUATE-CLR-ARRAY-BUDGET=OK");
    }

    private static void VerifyExactBoundaryAccepted()
    {
        // Exactly 100000 elements with a non-zero CLR lower bound.
        var accepted = Array.CreateInstance(typeof(object), [100000], [-500]);
        accepted.SetValue(20, -500);
        accepted.SetValue(22, 99499);

        var result = XPScriptEvaluateRuntime.Evaluate("Return callvar(-500) + callvar(99499)", accepted);
        if (XPScriptRuntime.CInt(result) != 42)
            throw new Exception("Evaluate changed CLR array bounds or contents at the exact 100000-element boundary.");
    }

    private static void VerifyFirstOverBoundaryRejected()
    {
        var rejected = new object?[100001];
        try
        {
            _ = XPScriptEvaluateRuntime.Evaluate("Return callvar(0)", rejected);
            throw new Exception("Evaluate unexpectedly accepted a 100001-element CLR array.");
        }
        catch (XPScriptRuntimeException ex)
        {
            if (ex.Number != 5)
                throw new Exception("Evaluate CLR array boundary expected error 5 but got " + ex.Number + ".");
            if (!ex.Message.Contains("maximum element budget of 100000", StringComparison.Ordinal))
                throw new Exception("Evaluate CLR array boundary returned an unexpected diagnostic: " + ex.Message);
        }
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
