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
    "Multiple Android targets are ready. Select one with --device SERIAL or --serial SERIAL.",
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

foreach (var expected in new[]
{
    "project.ApplicationType == AndroidProjectMetadata.UiApplicationType",
    "\"pidof\", \"com.xpscript.debugapp\"",
    "Android UI application was launched but is not running on ",
    "Android UI application launched on ",
    "xpscript android logs --device "
})
{
    if (!source.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android interactive UI run regression is missing: " + expected);
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
var compilerDriver = File.ReadAllText(Path.Combine(root, "src", "XPScript.Compiler", "CompilerDriver.cs"));
var compilerCommandLine = File.ReadAllText(Path.Combine(root, "src", "XPScript.Compiler", "XPScriptCompilerCommandLine.cs"));
if (!compilerCommandLine.Contains("(args[i] == \"--rid\" || args[i] == \"--platform\")", StringComparison.Ordinal))
    throw new Exception("Compiler CLI must accept --platform for publish target selection.");

foreach (var expected in new[]
{
    "--device auto|emulator|physical|SERIAL",
    "--serial SERIAL",
    "--avd NAME",
    "requestedSerial = deviceValue",
    "--avd can only be used with --device emulator",
    "--serial cannot be combined with --device emulator"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android device selection regression is missing: " + expected);
}


foreach (var expected in new[]
{
    "ValidateAndroidBuildEnvironment",
    "AndroidCompileApiLevel = \"36\"",
    "platforms",
    "android.jar",
    "build-tools",
    "aapt2",
    "Android SDK platform API ",
    "Android SDK Build Tools are required"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android SDK preflight regression is missing: " + expected);
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
    "compilerArgs.Add(\"--debug\");",
    "[--debug]"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android --debug regression is missing: " + expected);
}


foreach (var expected in new[]
{
    "ParseGeneratedCompilerDiagnostics",
    "new CompilerException(",
    "generatedDiagnostics",
    "Generated code failed to compile."
})
{
    if (!compilerDriver.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Generated Android compiler diagnostic regression is missing: " + expected);
}

var runtimeErrorSample = File.ReadAllText(Path.Combine(root, "samples", "android-debug-runtime-error.xps"));
foreach (var expected in new[]
{
    "BEFORE-RUNTIME-ERROR",
    "Error 7001, \"Android runtime error regression\"",
    "AFTER-RUNTIME-ERROR"
})
{
    if (!runtimeErrorSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android runtime error sample regression is missing: " + expected);
}

foreach (var expected in new[]
{
    "INSTALL_FAILED_UPDATE_INCOMPATIBLE",
    "uninstall",
    "com.xpscript.debugapp"
})
{
    if (!androidCommand.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android debug APK signature replacement regression is missing: " + expected);
}
