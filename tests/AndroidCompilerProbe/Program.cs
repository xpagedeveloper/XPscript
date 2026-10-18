using System.Reflection;
using XPScript.Compiler;

foreach (var rid in new[] { "android-arm64", "android-x64" })
{
    if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
        throw new Exception(rid + " is not a supported compiler target.");

    var type = typeof(CompilerDriver);
    var method = type.GetMethod("BuildGeneratedProject", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception("BuildGeneratedProject was not found.");
    var stagedType = type.GetNestedType("StagedManagedReference", BindingFlags.NonPublic)
        ?? throw new Exception("StagedManagedReference was not found.");
    var emptyReferences = Array.CreateInstance(stagedType, 0);
    var project = (string)(method.Invoke(null, new object?[] { rid, false, emptyReferences, false, false, "AndroidSmoke" })
        ?? throw new Exception("Android project generation returned null."));

    var abi = rid == "android-x64" ? "x86_64" : "arm64-v8a";
    foreach (var expected in new[] { "<TargetFramework>net10.0-android</TargetFramework>", "<SupportedOSPlatformVersion>30.0</SupportedOSPlatformVersion>", $"<AndroidSupportedAbis>{abi}</AndroidSupportedAbis>", "<AndroidPackageFormat>apk</AndroidPackageFormat>", "<ApplicationId>eu.xpscript.app</ApplicationId>" })
        if (!project.Contains(expected, StringComparison.Ordinal))
            throw new Exception(rid + " generated project is missing: " + expected);
}

var hostType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.AndroidHostSource", throwOnError: true)!;
var code = (string)(hostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidHostSource.Code was not found."));
foreach (var expected in new[] { "MainLauncher = true", "Console.AndroidLog", "\"XPScript\"", "XPSCRIPT-EXIT=" })
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host is missing: " + expected);

Console.WriteLine("ANDROID-AST-COMPILER-PROBE=OK");
