using System.Reflection;
using XPScript.Compiler;

var expectedRids = new[]
{
    "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64",
    "browser-wasm", "android-arm64", "android-x64"
};

foreach (var rid in expectedRids)
    if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
        throw new Exception("Missing compiler target: " + rid);

var driverType = typeof(CompilerDriver);
var projectMethod = driverType.GetMethod("BuildGeneratedProject", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("Compiler project generator is missing.");
var stagedType = driverType.GetNestedType("StagedManagedReference", BindingFlags.NonPublic)
    ?? throw new Exception("Compiler reference staging type is missing.");
var references = Array.CreateInstance(stagedType, 0);

foreach (var rid in expectedRids)
{
    var project = (string)(projectMethod.Invoke(null, new object?[] { rid, false, references, false, false, "TargetMatrix" })
        ?? throw new Exception("No generated project for " + rid));
    if (!project.Contains($"<RuntimeIdentifier>{rid}</RuntimeIdentifier>", StringComparison.Ordinal))
        throw new Exception("Generated project lost runtime identifier " + rid);

    if (rid.StartsWith("android-", StringComparison.Ordinal))
    {
        if (!project.Contains("<TargetFramework>net10.0-android</TargetFramework>", StringComparison.Ordinal) ||
            !project.Contains("<AndroidPackageFormat>apk</AndroidPackageFormat>", StringComparison.Ordinal))
            throw new Exception("Android project metadata is incomplete for " + rid);
    }
    else if (!project.Contains("<TargetFramework>net10.0</TargetFramework>", StringComparison.Ordinal))
    {
        throw new Exception("Desktop/browser project metadata is incomplete for " + rid);
    }
}

Console.WriteLine("TARGET_MATRIX_OK");
