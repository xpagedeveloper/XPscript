using System.Diagnostics;
using XPScript.Compiler;

var workspace = Path.Combine(Path.GetTempPath(), "xpscript-evaluate-lifetime-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);

try
{
    var source = """
Sub Main()
End Sub
""";

    var generated = new XPScriptTranspiler().Transpile(source, "evaluate-lifetime-probe.xps");
    generated += """

internal static class EvaluateLifetimeProbeEntry
{
    public static void Main()
    {
        VerifyNoStaticCallvarCache();

        var success = CreateSuccessfulCallvarWeakReference();
        ForceCollection(success, "successful Evaluate retained caller-owned callvar data");

        var failure = CreateFailingCallvarWeakReference();
        ForceCollection(failure, "failing Evaluate retained caller-owned callvar data");

        Console.WriteLine("EVALUATE-LIFETIME-ISOLATION=OK");
    }

    private static void VerifyNoStaticCallvarCache()
    {
        AssertNoMutableStaticFields(typeof(XPScriptEvaluateRuntime));
        AssertNoMutableStaticFields(typeof(XPScriptEvaluateCollectionRuntime));

        var evaluator = typeof(XPScriptEvaluateRuntime).GetNestedType(
            "Evaluator",
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new Exception("Evaluate runtime no longer exposes the expected nested evaluator type.");
        AssertNoMutableStaticFields(evaluator);
    }

    private static void AssertNoMutableStaticFields(Type type)
    {
        var fields = type.GetFields(
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        var forbidden = fields.Where(field => !field.IsLiteral).ToArray();
        if (forbidden.Length != 0)
            throw new Exception(
                "Evaluate lifetime probe found mutable/static cache state on " + type.FullName + ": " +
                string.Join(", ", forbidden.Select(field => field.Name)));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateSuccessfulCallvarWeakReference()
    {
        var input = new LSList<object?>();
        input["value"] = 123;
        var weak = new WeakReference(input);

        var result = XPScriptEvaluateRuntime.Evaluate("Return callvar(\"value\")", input);
        if (XPScriptRuntime.CInt(result) != 123)
            throw new Exception("Evaluate lifetime success probe returned the wrong value.");

        return weak;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateFailingCallvarWeakReference()
    {
        var input = new LSList<object?>();
        input["value"] = "SECRET-LIFETIME-FAILURE";
        var weak = new WeakReference(input);

        try
        {
            _ = XPScriptEvaluateRuntime.Evaluate("Return CInt(callvar(\"value\"))", input);
            throw new Exception("Evaluate lifetime failure probe unexpectedly succeeded.");
        }
        catch (XPScriptRuntimeException ex)
        {
            if (ex.Number != 13 || !string.Equals(ex.Message, "Evaluate type mismatch.", StringComparison.Ordinal))
                throw new Exception("Evaluate lifetime failure probe returned an unexpected error.");
        }

        return weak;
    }

    private static void ForceCollection(WeakReference weak, string failureMessage)
    {
        for (var attempt = 0; attempt < 8 && weak.IsAlive; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            Thread.Yield();
        }

        if (weak.IsAlive)
            throw new Exception(failureMessage);
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
    <StartupObject>EvaluateLifetimeProbeEntry</StartupObject>
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

    using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start generated Evaluate lifetime probe.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception("Generated Evaluate lifetime probe failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
    if (!stdout.Contains("EVALUATE-LIFETIME-ISOLATION=OK", StringComparison.Ordinal))
        throw new Exception("Generated Evaluate lifetime probe did not report success.\nSTDOUT:\n" + stdout);

    Console.WriteLine("EVALUATE-LIFETIME-PROBE=OK");
}
finally
{
    try { Directory.Delete(workspace, recursive: true); }
    catch { }
}
