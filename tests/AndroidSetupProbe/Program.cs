using System.Text.RegularExpressions;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var setup = File.ReadAllText(Path.Combine(root, "Android", "setup-android-dev.xps"));
var emulator = File.ReadAllText(Path.Combine(root, "Android", "test-emulator.ps1"));

foreach (var expected in new[]
{
    "Platform() <> \"Windows\"",
    "DotNet10Installed()",
    "AndroidStudioInstalled()",
    "AndroidWorkloadInstalled()",
    "WingetAvailable()",
    "ANDROID_HOME",
    "ANDROID_SDK_ROOT",
    "\\Android\\Sdk",
    "\\platform-tools\\adb.exe",
    "ShellExecute(\"dotnet\", Array(\"--list-sdks\"))",
    "ShellExecute(\"dotnet\", Array(\"workload\", \"list\"))",
    "RunCommand(adb, Array(\"devices\"))",
    "ro.product.model",
    "ro.build.version.release",
    "ro.build.version.sdk",
    "ro.product.cpu.abi"
})
{
    if (!setup.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android setup regression is missing: " + expected);
}

foreach (var expected in new[]
{
    "Resolve-AndroidTool \"adb\"",
    "Resolve-AndroidTool \"emulator\"",
    "-list-avds",
    "Multiple AVDs found",
    "sys.boot_completed",
    "adb install failed",
    "com.xpscript.debugapp",
    "Hello from XPScript on Android",
    "Expected XPScript Android log output was not observed"
})
{
    if (!emulator.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android emulator harness regression is missing: " + expected);
}

var functionNames = Regex.Matches(setup, @"(?im)^\s*Function\s+(\w+)\s*\(")
    .Select(match => match.Groups[1].Value)
    .ToArray();

foreach (var functionName in functionNames)
{
    var bodyMatch = Regex.Match(
        setup,
        @"(?ims)^\s*Function\s+" + Regex.Escape(functionName) + @"\s*\([^\r\n]*\).*?^\s*End\s+Function\s*$");

    if (!bodyMatch.Success)
        throw new Exception("Could not inspect Android setup function: " + functionName);

    if (Regex.IsMatch(bodyMatch.Value, @"(?im)^\s*Dim\s+" + Regex.Escape(functionName) + @"\b"))
        throw new Exception("Android setup function result collides with a local variable: " + functionName);
}

Console.WriteLine("ANDROID-SETUP-PROBE=OK");
