using System.Reflection;
using XPScript.Compiler;

if (!CompilerDriver.SupportedRuntimes.Contains("android-arm64", StringComparer.OrdinalIgnoreCase))
    throw new Exception("android-arm64 is not a supported compiler target.");

var type = typeof(CompilerDriver);
var method = type.GetMethod("BuildGeneratedProject", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("BuildGeneratedProject was not found.");
var stagedType = type.GetNestedType("StagedManagedReference", BindingFlags.NonPublic)
    ?? throw new Exception("StagedManagedReference was not found.");
var emptyReferences = Array.CreateInstance(stagedType, 0);
var project = (string)(method.Invoke(null, new object?[] { "android-arm64", false, emptyReferences, false, false, "AndroidSmoke" })
    ?? throw new Exception("Android project generation returned null."));

foreach (var expected in new[] { "<TargetFramework>net10.0-android</TargetFramework>", "<SupportedOSPlatformVersion>30.0</SupportedOSPlatformVersion>", "<AndroidSupportedAbis>arm64-v8a</AndroidSupportedAbis>", "<AndroidPackageFormat>apk</AndroidPackageFormat>" })
    if (!project.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android generated project is missing: " + expected);

var hostType = type.Assembly.GetType("XPScript.Compiler.AndroidHostSource", throwOnError: true)!;
var code = (string)(hostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidHostSource.Code was not found."));
foreach (var expected in new[] { "AndroidEntryActivity", "MainLauncher = true", "Console.SetOut", "Console.SetError", "\"XPScript\"" })
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host is missing: " + expected);

Console.WriteLine("ANDROID-COMPILER-PROBE=OK");
