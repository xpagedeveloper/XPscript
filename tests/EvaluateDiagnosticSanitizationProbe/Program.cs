using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-diagnostic-sanitize-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var generated = new XPScriptTranspiler().Transpile("Sub Main()\nEnd Sub\n", "evaluate-diagnostic-sanitization-probe.xps");
    generated += """

internal static class EvaluateDiagnosticSanitizationProbeEntry
{
    public static void Main()
    {
        VerifyRetained("Unknown identifier in Evaluate scope: variableName");
        VerifyRetained("Evaluate function Len expects 1 argument but got 2.");
        VerifyRetained("callvar is read-only inside Evaluate.");
        VerifyRetained("Evaluate collection snapshot exceeds the maximum element budget of 100000.");

        VerifyCollapsed("database password TOP-SECRET-DB-PASSWORD");
        VerifyCollapsed("raw parser token TOP-SECRET-PARSER-TOKEN");
        VerifyCollapsed("internal CLR detail TOP-SECRET-INTERNAL-VALUE");

        Console.WriteLine("EVALUATE-DIAGNOSTIC-SANITIZATION=OK");
    }

    private static void VerifyRetained(string message)
    {
        var sanitized = XPScriptEvaluateSemanticsRuntime.Sanitize(new XPScriptRuntimeException(5, message));
        if (sanitized is not XPScriptRuntimeException runtime)
            throw new Exception("Evaluate structural diagnostic did not remain an XPScript runtime error.");
        if (runtime.Number != 5)
            throw new Exception("Evaluate structural diagnostic changed error number to " + runtime.Number + ".");
        if (!string.Equals(runtime.Message, message, StringComparison.Ordinal))
            throw new Exception("Evaluate unexpectedly collapsed an allowlisted structural diagnostic: " + runtime.Message);
    }

    private static void VerifyCollapsed(string secretMessage)
    {
        var sanitized = XPScriptEvaluateSemanticsRuntime.Sanitize(new XPScriptRuntimeException(5, secretMessage));
        if (sanitized is not XPScriptRuntimeException runtime)
            throw new Exception("Evaluate unsafe diagnostic did not remain an XPScript runtime error.");
        if (runtime.Number != 5)
            throw new Exception("Evaluate unsafe diagnostic changed error number to " + runtime.Number + ".");
        if (!string.Equals(runtime.Message, "Invalid procedure call in Evaluate.", StringComparison.Ordinal))
            throw new Exception("Evaluate unsafe diagnostic was not collapsed to the stable generic description: " + runtime.Message);
        if (runtime.Message.Contains(secretMessage, StringComparison.Ordinal))
            throw new Exception("Evaluate unsafe diagnostic leaked attacker-controlled detail.");
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
    <StartupObject>EvaluateDiagnosticSanitizationProbeEntry</StartupObject>
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

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated Evaluate diagnostic sanitization probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated Evaluate diagnostic sanitization probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-DIAGNOSTIC-SANITIZATION=OK", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate diagnostic sanitization probe did not report success.\nSTDOUT:\n" + stdout);
    if (stdout.Contains("TOP-SECRET-", StringComparison.Ordinal) || stderr.Contains("TOP-SECRET-", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate diagnostic sanitization probe leaked forbidden secret text to process output.");

    Console.WriteLine("EVALUATE-DIAGNOSTIC-SANITIZATION-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
