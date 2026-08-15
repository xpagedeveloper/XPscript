using System.Reflection;

var compilerAssembly = typeof(XPScript.Compiler.CompilerDriver).Assembly;
var preprocessorType = compilerAssembly.GetType("XPScript.Compiler.NativeLibraryPlatformPreprocessor", throwOnError: true)!;
var ctor = preprocessorType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(string)], null)
    ?? throw new Exception("NativeLibraryPlatformPreprocessor constructor not found.");
var transform = preprocessorType.GetMethod("Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    ?? throw new Exception("NativeLibraryPlatformPreprocessor.Transform not found.");

const string source = """
Declare Function NativeVersion Lib "generic-native" Alias "generic_alias" _
    WindowsLib "windows-native" WindowsAlias "windows_alias" _
    LinuxLib "linux-native" LinuxAlias "linux_alias" _
    MacOSLib "macos-native" MacOSAlias "macos_alias" _
    WindowsX64Lib "windows-x64-native" WindowsX64Alias "windows_x64_alias" _
    WindowsArm64Lib "windows-arm64-native" WindowsArm64Alias "windows_arm64_alias" _
    LinuxX64Lib "linux-x64-native" LinuxX64Alias "linux_x64_alias" _
    LinuxArm64Lib "linux-arm64-native" LinuxArm64Alias "linux_arm64_alias" _
    MacOSX64Lib "macos-x64-native" MacOSX64Alias "macos_x64_alias" _
    MacOSArm64Lib "macos-arm64-native" MacOSArm64Alias "macos_arm64_alias" _
    () As Integer
""";

var expected = new Dictionary<string, (string Lib, string Alias)>
{
    ["win-x64"] = ("windows-x64-native", "windows_x64_alias"),
    ["win-arm64"] = ("windows-arm64-native", "windows_arm64_alias"),
    ["linux-x64"] = ("linux-x64-native", "linux_x64_alias"),
    ["linux-arm64"] = ("linux-arm64-native", "linux_arm64_alias"),
    ["osx-x64"] = ("macos-x64-native", "macos_x64_alias"),
    ["osx-arm64"] = ("macos-arm64-native", "macos_arm64_alias")
};

foreach (var pair in expected)
{
    var rewritten = Rewrite(pair.Key, source);
    Require(rewritten.Contains($"Lib \"{pair.Value.Lib}\"", StringComparison.Ordinal), pair.Key + " library selection");
    Require(rewritten.Contains($"Alias \"{pair.Value.Alias}\"", StringComparison.Ordinal), pair.Key + " alias selection");
    Require(!rewritten.Contains("WindowsX64Lib", StringComparison.OrdinalIgnoreCase), pair.Key + " selectable keywords removed");
    Require(rewritten.Split('\n').Skip(1).All(string.IsNullOrWhiteSpace), pair.Key + " multiline continuation lines blanked");
}

const string osFallbackSource = """
Declare Function F Lib "generic-native" Alias "generic_alias" _
    WindowsLib "windows-native" WindowsAlias "windows_alias" _
    LinuxLib "linux-native" LinuxAlias "linux_alias" _
    MacOSLib "macos-native" MacOSAlias "macos_alias" _
    () As Integer
""";
Require(Rewrite("win-arm64", osFallbackSource).Contains("Lib \"windows-native\"", StringComparison.Ordinal), "Windows OS fallback");
Require(Rewrite("linux-arm64", osFallbackSource).Contains("Alias \"linux_alias\"", StringComparison.Ordinal), "Linux OS alias fallback");
Require(Rewrite("osx-x64", osFallbackSource).Contains("Lib \"macos-native\"", StringComparison.Ordinal), "macOS OS fallback");

const string genericFallbackSource = "Declare Function F Lib \"generic-native\" Alias \"generic_alias\" () As Integer";
foreach (var rid in expected.Keys)
{
    var rewritten = Rewrite(rid, genericFallbackSource);
    Require(rewritten.Contains("Lib \"generic-native\"", StringComparison.Ordinal), rid + " generic library fallback");
    Require(rewritten.Contains("Alias \"generic_alias\"", StringComparison.Ordinal), rid + " generic alias fallback");
}

Console.WriteLine("NATIVE-TARGET-RID-SELECTION=OK");
Console.WriteLine("NATIVE-TARGET-MULTILINE=OK");
Console.WriteLine("NATIVE-TARGET-FALLBACK=OK");

string Rewrite(string rid, string value)
{
    var instance = ctor.Invoke([rid]);
    return (string)(transform.Invoke(instance, [value]) ?? throw new Exception("Transform returned null."));
}

static void Require(bool condition, string description)
{
    if (!condition) throw new Exception("Native target resolution verification failed: " + description);
}
