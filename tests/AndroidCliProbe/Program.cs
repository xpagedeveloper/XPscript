var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var source = File.ReadAllText(Path.Combine(root, "src", "XPScript.Cli", "AndroidCommand.cs"));

foreach (var expected in new[]
{
    "--platform",
    "android-arm64",
    "android-x64",
    "XPSCRIPT_ADB",
    "XPSCRIPT-EXIT=0",
    "XPSCRIPT-EXIT=1",
    "WaitForCompletionAsync",
    "TimeSpan.FromSeconds(30)",
    "Task.Delay(250)",
    "Multiple Android targets are ready. Select one with --serial SERIAL.",
    "State is \"unauthorized\" or \"offline\"",
    "State.Equals(\"unauthorized\"",
    "State.Equals(\"offline\"",
    "\"arm64-v8a\" => \"android-arm64\"",
    "\"x86_64\" => \"android-x64\"",
    "\"install\", \"-r\"",
    "\"logcat\", \"-c\"",
    "\"monkey\", \"-p\"",
    "\"logcat\", \"-d\", \"-s\", \"XPScript:I\""
})
{
    if (!source.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android CLI regression is missing: " + expected);
}

var waitMethodStart = source.IndexOf("private static async Task<int> WaitForCompletionAsync", StringComparison.Ordinal);
if (waitMethodStart < 0)
    throw new Exception("Android completion wait method is missing.");
var waitMethodEnd = source.IndexOf("private static async Task<int> LaunchAsync", waitMethodStart, StringComparison.Ordinal);
if (waitMethodEnd < 0)
    throw new Exception("Android completion wait method boundary is missing.");
var waitMethod = source[waitMethodStart..waitMethodEnd];
if (waitMethod.Contains("await Task.Delay(1000);", StringComparison.Ordinal))
    throw new Exception("Android completion wait still relies on the old fixed one-second delay.");

Console.WriteLine("ANDROID-CLI-PROBE=OK");


var androidCommand = File.ReadAllText(Path.Combine(root, "src", "XPScript.Cli", "AndroidCommand.cs"));
foreach (var expected in new[]
{
    "--device auto|emulator|physical",
    "--serial SERIAL",
    "--avd NAME",
    "Invalid --device value",
    "--avd can only be used with --device emulator",
    "--serial cannot be combined with --device emulator"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android device selection regression is missing: " + expected);
}


foreach (var expected in new[]
{
    "ResolveAndroidTool(\"emulator\")",
    "-list-avds",
    "Multiple AVDs are configured",
    "Starting Android emulator",
    "sys.boot_completed",
    "did not finish booting within 180 seconds"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android emulator selection regression is missing: " + expected);
}


foreach (var expected in new[]
{
    "var debug = false;",
    "args[i] == \"--debug\"",
    "debug ? new[] { \"--debug\" }",
    "[--debug]"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android --debug regression is missing: " + expected);
}


foreach (var expected in new[]
{
    "ParseGeneratedCompilerDiagnostics",
    "CompilerDiagnosticCodes.GeneratedCodeCompilationFailed",
    "generatedDiagnostics",
    "Generated code failed to compile."
})
{
    if (!compilerDriver.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Generated Android compiler diagnostic regression is missing: " + expected);
}
