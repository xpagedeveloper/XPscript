var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var source = File.ReadAllText(Path.Combine(root, "src", "XPScript.Cli", "AndroidCommand.cs"));

foreach (var expected in new[]
{
    "XPSCRIPT_ADB",
    "XPSCRIPT-EXIT=0",
    "XPSCRIPT-EXIT=1",
    "WaitForCompletionAsync",
    "TimeSpan.FromSeconds(30)",
    "Task.Delay(250)",
    "Multiple Android devices/emulators are ready",
    "is unauthorized",
    "is offline",
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

var fixedDelay = "await Task.Delay(1000);";
if (source.Contains(fixedDelay, StringComparison.Ordinal))
    throw new Exception("Android run still relies on the old fixed one-second completion delay.");

Console.WriteLine("ANDROID-CLI-PROBE=OK");
