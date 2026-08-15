using System.Diagnostics;
using XPScript.Compiler;

const string secret = "TOP-SECRET-PERMISSION-PATH";
var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-permission-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var source = """
Sub Main()
End Sub
""";

    var generated = new XPScriptTranspiler().Transpile(source, "evaluate-permission-probe.xps");
    generated += $$"""

internal static class EvaluatePermissionProbeEntry
{
    public static void Main()
    {
        const string secret = "{{secret}}";
        var sanitized = XPScriptEvaluateSemanticsRuntime.Sanitize(new UnauthorizedAccessException(secret));
        if (sanitized is not XPScriptRuntimeException runtime)
            throw new Exception("Evaluate permission mapping did not produce an XPScriptRuntimeException.");
        if (runtime.Number != 70)
            throw new Exception("Evaluate permission mapping expected error 70 but got " + runtime.Number + ".");
        if (!string.Equals(runtime.Message, "Evaluate access or permission denied.", StringComparison.Ordinal))
            throw new Exception("Evaluate permission mapping returned an unexpected sanitized description: " + runtime.Message);
        if (runtime.Message.Contains(secret, StringComparison.Ordinal))
            throw new Exception("Evaluate permission mapping leaked the original UnauthorizedAccessException message.");

        Console.WriteLine("EVALUATE-PERMISSION-MAPPING=OK");
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
    <StartupObject>EvaluatePermissionProbeEntry</StartupObject>
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

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated Evaluate permission probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated Evaluate permission probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-PERMISSION-MAPPING=OK", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate permission probe did not report success.\nSTDOUT:\n" + stdout);
    if (stdout.Contains(secret, StringComparison.Ordinal) || stderr.Contains(secret, StringComparison.Ordinal))
        throw new Exception("Generated Evaluate permission probe leaked the original permission message to process output.");

    Console.WriteLine("EVALUATE-PERMISSION-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
