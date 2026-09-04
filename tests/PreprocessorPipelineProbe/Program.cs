using System.Diagnostics;
using XPScript.Compiler;

if (args.Length == 2)
{
    var inputPath = Path.GetFullPath(args[0]);
    var outputPath = Path.GetFullPath(args[1]);
    var source = File.ReadAllText(inputPath);
    var generated = new XPScriptTranspiler().Transpile(source, inputPath, "win-x64");
    File.WriteAllText(outputPath, generated);
    Console.WriteLine("PREPROCESSOR-SNAPSHOT-CHARS=" + generated.Length);
    return;
}

const string plainSource = """
Option Declare

Sub Main()
    Dim message As String
    message = "hello"
    Print message
End Sub
""";

const string notesSource = """
Option Declare

Sub Main()
    Dim session As NotesSession
    Dim db As NotesDatabase
    Set session = New NotesSession()
    Set db = session.CurrentDatabase
    Print db.Title
End Sub
""";

var transpiler = new XPScriptTranspiler();
_ = transpiler.Transpile(plainSource, "preprocessor-warmup.xps", "win-x64");
_ = transpiler.Transpile(notesSource, "preprocessor-notes-warmup.xps", "win-x64");

Measure("PLAIN", plainSource, 10);
Measure("NOTES", notesSource, 10);
VerifyVariableNamesDoNotEnableRuntimes();
VerifyFeatureProfiles();
VerifyLegacyNativeNamesDoNotEnableRuntimes();
VerifyNestedArgumentComparison();

void Measure(string label, string source, int iterations)
{
    var samples = new double[iterations];
    var generatedLength = 0;
    for (var i = 0; i < iterations; i++)
    {
        var started = Stopwatch.GetTimestamp();
        var generated = transpiler.Transpile(source, "preprocessor-" + label.ToLowerInvariant() + ".xps", "win-x64");
        samples[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        generatedLength = generated.Length;
    }

    Array.Sort(samples);
    Console.WriteLine($"PREPROCESSOR-{label}-MEDIAN-MS={samples[samples.Length / 2]:F3}");
    Console.WriteLine($"PREPROCESSOR-{label}-MIN-MS={samples[0]:F3}");
    Console.WriteLine($"PREPROCESSOR-{label}-GENERATED-CHARS={generatedLength}");
}

void VerifyVariableNamesDoNotEnableRuntimes()
{
    const string source = """
Option Declare

Sub Main()
    Dim Notesdb As String
    Dim XPDBSupabase As String
    Dim XPJsonDocument As String
    Dim XPXmlDocument As String
    Dim XPCsvDocument As String
    Dim XPHttpClient As String
    Notesdb = "NotesDatabase XPDB JSON XML CSV HTTP"
End Sub
""";

    var generated = transpiler.Transpile(source, "preprocessor-variable-name-probe.xps", "win-x64");
    var forbidden = new[]
    {
        "internal static class XPScriptNotes",
        "internal static class XPScriptNativeJson",
        "internal static class XPScriptNativeXml",
        "internal static class XPScriptNativeCsv",
        "internal static class XPScriptNativeHttp",
        "internal sealed class XPScriptDbSupabase"
    };
    foreach (var marker in forbidden)
        if (generated.Contains(marker, StringComparison.Ordinal))
            throw new Exception("Variable-name feature detection incorrectly enabled " + marker + ".");

    Console.WriteLine("PREPROCESSOR-VARIABLE-NAMES=OK");
}

void VerifyFeatureProfiles()
{
    VerifyProfile("JSON", "Dim value As XPJsonDocument", ["internal static class XPScriptNativeJson"]);
    VerifyProfile("XML", "Dim value As XPXmlDocument", ["internal static class XPScriptNativeXml"]);
    VerifyProfile("CSV", "Dim value As XPCsvDocument", ["internal static class XPScriptNativeCsv"]);
    VerifyProfile(
        "HTTP",
        "Dim value As XPHttpClient",
        ["internal static class XPScriptNativeHttp"],
        ["internal sealed class XPScriptUIForm"]);
    VerifyProfile(
        "UI",
        "Dim value As UIForm",
        [
            "internal sealed class XPScriptUIForm",
            "internal static class XPScriptNativeHttp",
            "internal static class XPScriptNativeJson"
        ],
        ["public string XPScriptUIDialogRuntime.ShowDialog()"]);
    VerifyProfile(
        "XPDB",
        "Dim value As XPDBSQLite",
        ["internal sealed class XPScriptDbSqlite"],
        ["internal sealed class XPScriptHttpDbSupabase"]);
    VerifyProfile(
        "HTTPDB",
        "Dim value As XPHttpDbSupabase",
        ["internal sealed class XPScriptHttpDbSupabase"]);
    VerifyProfile(
        "NOTES",
        "Dim value As NotesDatabase",
        ["internal static class XPScriptNotes"],
        ["internal sealed class XPScriptNotesRichTextItem"]);
    VerifyProfile(
        "NOTES-RICH-TEXT",
        "Dim value As NotesRichTextItem",
        ["internal sealed class XPScriptNotesRichTextItem"]);
    Console.WriteLine("PREPROCESSOR-FEATURE-PROFILES=OK");
}

void VerifyLegacyNativeNamesDoNotEnableRuntimes()
{
    var probes = new (string Label, string Declaration, string Marker)[]
    {
        ("HTTP", "Dim value As HttpClient", "internal static class XPScriptNativeHttp"),
        ("JSON", "Dim value As JsonDocument", "internal static class XPScriptNativeJson"),
        ("CSV", "Dim value As CsvDocument", "internal static class XPScriptNativeCsv"),
        ("XML", "Dim value As XmlDocument", "internal static class XPScriptNativeXml"),
        ("HTTPDB", "Dim value As HTTPDBSupabase", "internal sealed class XPScriptHttpDbSupabase")
    };

    foreach (var probe in probes)
    {
        var source = "Option Declare\nSub Main()\n    " + probe.Declaration + "\nEnd Sub\n";
        var generated = transpiler.Transpile(source, "preprocessor-legacy-" + probe.Label.ToLowerInvariant() + ".xps", "win-x64");
        if (generated.Contains(probe.Marker, StringComparison.Ordinal))
            throw new Exception("Legacy " + probe.Label + " type unexpectedly enabled its native runtime.");
    }

    Console.WriteLine("PREPROCESSOR-LEGACY-NATIVE-NAMES=INACTIVE");
}

void VerifyProfile(
    string label,
    string declaration,
    IReadOnlyList<string> expectedMarkers,
    IReadOnlyList<string>? forbiddenMarkers = null)
{
    var source = "Option Declare\nSub Main()\n    " + declaration + "\nEnd Sub\n";
    var generated = transpiler.Transpile(source, "preprocessor-" + label.ToLowerInvariant() + "-profile.xps", "win-x64");
    foreach (var marker in expectedMarkers)
        if (!generated.Contains(marker, StringComparison.Ordinal))
            throw new Exception(label + " feature profile did not include " + marker + ".");
    foreach (var marker in forbiddenMarkers ?? [])
        if (generated.Contains(marker, StringComparison.Ordinal))
            throw new Exception(label + " feature profile unexpectedly included " + marker + ".");
}

void VerifyNestedArgumentComparison()
{
    const string source = """
Sub Main()
    Print CStr(FileLen("missing.txt") = 1)
End Sub
""";

    var generated = transpiler.Transpile(source, "preprocessor-nested-comparison.xps", "win-x64");
    const string expected = "XPScriptRuntime.FileLen(\"missing.txt\") == 1";
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Nested function-call comparison was not emitted as C# equality.");
    Console.WriteLine("PREPROCESSOR-NESTED-COMPARISON=OK");
}
