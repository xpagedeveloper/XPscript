using System.Reflection;
using XPScript.Compiler;

foreach (var runtime in new[] { "android-arm64", "android-x64" })
    if (!CompilerDriver.SupportedRuntimes.Contains(runtime, StringComparer.OrdinalIgnoreCase))
        throw new Exception(runtime + " is not a supported compiler target.");

var type = typeof(CompilerDriver);
var method = type.GetMethod("BuildGeneratedProject", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("BuildGeneratedProject was not found.");
var stagedType = type.GetNestedType("StagedManagedReference", BindingFlags.NonPublic)
    ?? throw new Exception("StagedManagedReference was not found.");
var emptyReferences = Array.CreateInstance(stagedType, 0);
var project = (string)(method.Invoke(null, new object?[] { "android-arm64", false, emptyReferences, false, false, "AndroidSmoke" })
    ?? throw new Exception("Android project generation returned null."));

foreach (var expected in new[] { "<TargetFramework>net10.0-android</TargetFramework>", "<SupportedOSPlatformVersion>30.0</SupportedOSPlatformVersion>", "<RuntimeIdentifier>android-arm64</RuntimeIdentifier>", "<AndroidPackageFormat>apk</AndroidPackageFormat>", "<ApplicationId>com.xpscript.debugapp</ApplicationId>" })
    if (!project.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android generated project is missing: " + expected);

var emulatorProject = (string)(method.Invoke(null, new object?[] { "android-x64", false, emptyReferences, false, false, "AndroidEmulatorSmoke" })
    ?? throw new Exception("Android emulator project generation returned null."));
if (!emulatorProject.Contains("<TargetFramework>net10.0-android</TargetFramework>", StringComparison.Ordinal) ||
    !emulatorProject.Contains("<RuntimeIdentifier>android-x64</RuntimeIdentifier>", StringComparison.Ordinal))
    throw new Exception("Android x64 emulator project generation is incorrect.");

if (project.Contains("<AndroidSupportedAbis>", StringComparison.Ordinal))
    throw new Exception("Android generated project still uses obsolete AndroidSupportedAbis.");
if (project.Contains("<StartupObject>", StringComparison.Ordinal))
    throw new Exception("Android generated project must not specify StartupObject.");

var hostType = type.Assembly.GetType("XPScript.Compiler.AndroidHostSource", throwOnError: true)!;
var code = (string)(hostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidHostSource.Code was not found."));
var findPublishedExecutable = type.GetMethod("FindPublishedExecutable", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("FindPublishedExecutable was not found.");
var apkRoot = Path.Combine(Path.GetTempPath(), "XPScript-AndroidCompilerProbe-" + Guid.NewGuid().ToString("N"));
var publishDir = Path.Combine(apkRoot, "publish");
var androidOutputDir = Path.Combine(apkRoot, "bin", "Release", "net10.0-android", "android-arm64");
Directory.CreateDirectory(publishDir);
Directory.CreateDirectory(androidOutputDir);
var expectedApk = Path.Combine(androidOutputDir, "AndroidSmoke-Signed.apk");
File.WriteAllText(expectedApk, "probe");
try
{
    var foundApk = (string?)findPublishedExecutable.Invoke(null, new object?[] { apkRoot, publishDir, "android-arm64", "AndroidSmoke" });
    if (!string.Equals(foundApk, expectedApk, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Android APK discovery did not search the complete build tree.");
}
finally
{
    Directory.Delete(apkRoot, recursive: true);
}

foreach (var expected in new[] { "AndroidEntryActivity", "MainLauncher = true", "Console.AndroidLog", "\"XPScript\"", "Log.Error", "Program.Main(Array.Empty<string>())", "XPSCRIPT-EXIT=0", "XPSCRIPT-EXIT=1" })
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host is missing: " + expected);

var findPublished = type.GetMethod("FindPublishedExecutable", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("FindPublishedExecutable was not found.");
var artifactRoot = Path.Combine(Path.GetTempPath(), "xpscript-android-probe-" + Guid.NewGuid().ToString("N"));
var artifactBin = Path.Combine(artifactRoot, "bin");
var artifactPublish = Path.Combine(artifactRoot, "publish");
Directory.CreateDirectory(artifactBin);
Directory.CreateDirectory(artifactPublish);
try
{
    var signedPackage = Path.Combine(artifactPublish, "com.xpscript.debugapp-Signed.apk");
    File.WriteAllText(signedPackage, "probe");
    File.WriteAllText(Path.Combine(artifactPublish, "com.xpscript.debugapp.apk"), "probe");
    File.WriteAllText(Path.Combine(artifactBin, "com.xpscript.debugapp-Signed.apk"), "probe");
    File.WriteAllText(Path.Combine(artifactBin, "other-Signed.apk"), "probe");
    var discovered = (string?)findPublished.Invoke(null, new object?[] { artifactRoot, artifactPublish, "android-arm64", "android-debug-print" });
    if (!string.Equals(discovered, signedPackage, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Android APK discovery did not prefer the signed package from the publish directory.");
}
finally
{
    Directory.Delete(artifactRoot, recursive: true);
}

var compilerSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "CompilerDriver.cs"));
if (!compilerSource.Contains("outputPath += \".apk\";", StringComparison.Ordinal))
    throw new Exception("Android compiler output is not normalized to an .apk path.");


foreach (var expected in new[]
{
    "System.Environment.ExitCode = 0;",
    "if (System.Environment.ExitCode == 0)",
    "\"XPSCRIPT-EXIT=\" + System.Environment.ExitCode"
})
{
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host exit-code regression is missing: " + expected);
}

var runtimeDiagnosticsSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "AdvancedXPScriptTranspiler.cs");
var runtimeDiagnosticsSource = File.ReadAllText(runtimeDiagnosticsSourcePath);
if (!runtimeDiagnosticsSource.Contains("#if ANDROID", StringComparison.Ordinal) ||
    !runtimeDiagnosticsSource.Contains("Console.WriteLine(\"at \" + runtimeSource + \":", StringComparison.Ordinal) ||
    !runtimeDiagnosticsSource.Contains("Environment.ExitCode = 1;", StringComparison.Ordinal))
    throw new Exception("Android runtime diagnostics regression is missing.");

Console.WriteLine("ANDROID-COMPILER-PROBE=OK");
