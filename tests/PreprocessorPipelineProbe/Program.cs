using System.Diagnostics;
using System.Reflection;
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
VerifyArchiveConstructorModes();
VerifyArchiveMemorySurface();
VerifyArchiveGZipSurface();
VerifyArchiveTraversalGuard();
VerifyArchiveDependencyInjection();
VerifyNestedArgumentComparison();
VerifyRuntimeFunctionMemberScope();
VerifyRuntimeNameScopeMatrix();
VerifyReservedIdentifierScope();
VerifyRuntimeValueIdentifierScope();
VerifySharedCallDetectionScope();
VerifySharedCallRewriteScope();

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
    Dim ArchiveName As String
    Notesdb = "NotesDatabase XPDB JSON XML CSV HTTP"
    ArchiveName = "Archive"
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
        "internal sealed class XPScriptDbSupabase",
        "internal sealed class XPScriptArchive",
        "internal sealed class XPScriptExtendedArchive",
        "internal sealed class XPScriptMemoryArchive"
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
        "ARCHIVE",
        "Dim value As Archive",
        ["internal sealed class XPScriptArchive", "internal sealed class XPScriptMemoryArchive"],
        ["internal sealed class XPScriptExtendedArchive", "internal static class XPScriptNativeHttp", "internal sealed class XPScriptDbSqlite"]);
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

void VerifyArchiveConstructorModes()
{
    const string memory = "Option Declare\nSub Main()\n    Dim a As New Archive()\nEnd Sub\n";
    const string zipDefault = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.zip\")\nEnd Sub\n";
    const string zipExplicit = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.zip\", False)\nEnd Sub\n";
    const string extended = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.rar\", True)\nEnd Sub\n";

    var generatedMemory = transpiler.Transpile(memory, "archive-memory-empty.xps", "win-x64");
    var generatedDefault = transpiler.Transpile(zipDefault, "archive-default.xps", "win-x64");
    var generatedFalse = transpiler.Transpile(zipExplicit, "archive-false.xps", "win-x64");
    var generatedTrue = transpiler.Transpile(extended, "archive-extended.xps", "win-x64");

    if (!generatedMemory.Contains("XPScriptArchiveFactory.Create()", StringComparison.Ordinal))
        throw new Exception("Archive() did not emit the in-memory factory form.");
    if (!generatedDefault.Contains("XPScriptArchiveFactory.Create(\"test.zip\")", StringComparison.Ordinal))
        throw new Exception("Archive(filename) did not emit ZIP path/memory factory form.");
    if (!generatedFalse.Contains("XPScriptArchiveFactory.Create(\"test.zip\", false)", StringComparison.Ordinal))
        throw new Exception("Archive(filename, False) did not emit explicit ZIP-only factory form.");
    if (!generatedTrue.Contains("XPScriptExtendedArchiveWriterFactory.Create(\"test.rar\")", StringComparison.Ordinal))
        throw new Exception("Archive(filename, True) did not emit extended writer factory form.");
    if (!generatedMemory.Contains("internal sealed class XPScriptMemoryArchive", StringComparison.Ordinal)
        || !generatedDefault.Contains("internal sealed class XPScriptMemoryArchive", StringComparison.Ordinal))
        throw new Exception("ZIP Archive construction did not emit the in-memory ZIP runtime.");
    if (!generatedTrue.Contains("internal sealed class XPScriptExtendedArchive", StringComparison.Ordinal)
        || !generatedTrue.Contains("internal sealed class XPScriptExtendedArchiveV2", StringComparison.Ordinal)
        || !generatedTrue.Contains("SharpCompress.Readers.ReaderFactory", StringComparison.Ordinal)
        || !generatedTrue.Contains("internal static class XPScriptArchiveGZipWriter", StringComparison.Ordinal))
        throw new Exception("Archive(filename, True) did not emit the extended reader/writer runtime.");
    if (generatedDefault.Contains("internal sealed class XPScriptExtendedArchive", StringComparison.Ordinal)
        || generatedFalse.Contains("internal sealed class XPScriptExtendedArchive", StringComparison.Ordinal)
        || generatedMemory.Contains("internal sealed class XPScriptExtendedArchive", StringComparison.Ordinal))
        throw new Exception("ZIP-only Archive unexpectedly emitted extended archive support.");

    var invalid = "Option Declare\nSub Main()\n    Dim enabled As Boolean\n    enabled = True\n    Dim a As New Archive(\"test.rar\", enabled)\nEnd Sub\n";
    try
    {
        _ = transpiler.Transpile(invalid, "archive-dynamic-extended.xps", "win-x64");
        throw new Exception("Archive accepted a non-literal extendedSupport argument.");
    }
    catch (CompilerException ex) when (ex.Message.Contains("literal True or False", StringComparison.Ordinal))
    {
    }

    Console.WriteLine("PREPROCESSOR-ARCHIVE-CONSTRUCTOR-MODES=OK");
}

void VerifyArchiveMemorySurface()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive()
    archive.Create("zip")
    archive.AddText("manifest.txt", "hello")
    Dim data As Variant
    data = archive.ToBytes()
    Dim reopened As New Archive(data)
    Print reopened.ReadText("manifest.txt")
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-memory-roundtrip.xps", "win-x64");
    if (!generated.Contains("XPScriptArchiveFactory.Create()", StringComparison.Ordinal)
        || !generated.Contains("XPScriptArchiveFactory.Create(data)", StringComparison.Ordinal))
        throw new Exception("Archive memory constructors were not emitted through XPScriptArchiveFactory.");
    if (!generated.Contains("internal sealed class XPScriptMemoryArchive", StringComparison.Ordinal)
        || !generated.Contains("public LSArray ToBytes()", StringComparison.Ordinal))
        throw new Exception("Archive memory runtime surface was not emitted.");
    if (generated.Contains("SharpCompress.Readers.ReaderFactory", StringComparison.Ordinal))
        throw new Exception("In-memory ZIP Archive unexpectedly emitted SharpCompress support.");
    Console.WriteLine("PREPROCESSOR-ARCHIVE-MEMORY-SURFACE=OK");
}

void VerifyArchiveGZipSurface()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive("sample.gz", True)
    archive.Create("gzip")
    archive.AddText("sample.txt", "hello")
    archive.Save()
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-gzip-surface.xps", "win-x64");
    if (!generated.Contains("XPScriptExtendedArchiveWriterFactory.Create(\"sample.gz\")", StringComparison.Ordinal))
        throw new Exception("GZip Archive did not use the extended writer factory.");
    if (!generated.Contains("internal static class XPScriptArchiveGZipWriter", StringComparison.Ordinal)
        || !generated.Contains("GZip supports exactly one file entry", StringComparison.Ordinal)
        || !generated.Contains("File.Replace(temp, destination, null)", StringComparison.Ordinal))
        throw new Exception("GZip writer runtime or atomic replacement guard was not emitted.");
    Console.WriteLine("PREPROCESSOR-ARCHIVE-GZIP-SURFACE=OK");
}

void VerifyArchiveTraversalGuard()
{
    const string source = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.zip\")\n    a.AddFolder \"data\", \"data\", True\nEnd Sub\n";
    var generated = transpiler.Transpile(source, "archive-traversal-guard.xps", "win-x64");
    if (!generated.Contains("AddFolderTree(source, source, rootName, recursive)", StringComparison.Ordinal))
        throw new Exception("Archive runtime did not emit explicit safe folder traversal.");
    if (generated.Contains("SearchOption.AllDirectories", StringComparison.Ordinal))
        throw new Exception("Archive runtime still uses recursive SearchOption.AllDirectories traversal.");
    if (!generated.Contains("FileAttributes.ReparsePoint", StringComparison.Ordinal))
        throw new Exception("Archive runtime does not guard reparse points during folder traversal.");
    Console.WriteLine("PREPROCESSOR-ARCHIVE-TRAVERSAL-GUARD=OK");
}

void VerifyArchiveDependencyInjection()
{
    const string zipSource = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.zip\")\nEnd Sub\n";
    const string extendedSource = "Option Declare\nSub Main()\n    Dim a As New Archive(\"test.7z\", True)\nEnd Sub\n";

    var zipGenerated = transpiler.Transpile(zipSource, "archive-dependency-zip.xps", "win-x64");
    var extendedGenerated = transpiler.Transpile(extendedSource, "archive-dependency-extended.xps", "win-x64");
    var configureType = typeof(XPScriptTranspiler).Assembly.GetType("XPScript.Compiler.CompilerBuildEnvironment", throwOnError: true)!;
    var configure = configureType.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new Exception("CompilerBuildEnvironment.Configure was not found.");

    var root = Path.Combine(Path.GetTempPath(), "xpscript-archive-dependency-" + Guid.NewGuid().ToString("N"));
    try
    {
        var zipRoot = Path.Combine(root, "zip");
        var extendedRoot = Path.Combine(root, "extended");
        Directory.CreateDirectory(zipRoot);
        Directory.CreateDirectory(extendedRoot);
        ConfigureProbe(zipRoot, zipGenerated);
        ConfigureProbe(extendedRoot, extendedGenerated);

        var zipProps = File.ReadAllText(Path.Combine(zipRoot, "Directory.Build.props"));
        var extendedProps = File.ReadAllText(Path.Combine(extendedRoot, "Directory.Build.props"));
        if (zipProps.Contains("SharpCompress", StringComparison.Ordinal))
            throw new Exception("ZIP-only Archive unexpectedly injected SharpCompress.");
        if (!extendedProps.Contains("PackageReference Include=\"SharpCompress\" Version=\"0.50.4\"", StringComparison.Ordinal))
            throw new Exception("Extended Archive did not inject SharpCompress 0.50.4.");
    }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    Console.WriteLine("PREPROCESSOR-ARCHIVE-DEPENDENCY-INJECTION=OK");

    void ConfigureProbe(string workspace, string generated)
    {
        File.WriteAllText(Path.Combine(workspace, "Program.cs"), generated);
        File.WriteAllText(Path.Combine(workspace, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var psi = new ProcessStartInfo();
        configure.Invoke(null, [psi, workspace]);
    }
}

void VerifyProfile(
    string label,
    string declaration,
    IReadOnlyList<string> expectedMarkers,
    IReadOnlyList<string>? forbiddenMarkers = null)
{
    var source = "Option Declare\nSub Main()\n    " + declaration + "\nEnd Sub\n";
    var generated = transpiler.Transpile(source, "preprocessor-" + label.ToLowerInvariant() + ".xps", "win-x64");
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


void VerifyRuntimeFunctionMemberScope()
{
    const string source = """
Class RuntimeNameCollision
    Public JsonParse As String

    Public Function JsonStringify(value As String) As String
        JsonStringify = value
    End Function

    Public Function StrLeftBack(value As String, delimiter As String) As String
        StrLeftBack = value
    End Function

    Public Function CsvParse(value As String) As String
        CsvParse = value
    End Function

    Public Function XmlParse(value As String) As String
        XmlParse = value
    End Function
End Class

Sub Main()
    Dim item As New RuntimeNameCollision
    Dim parsed As Variant
    item.JsonParse = "member"
    Print item.JsonStringify("member")
    Print item.StrLeftBack("a/b", "/")
    Print item.CsvParse("member")
    Print item.XmlParse("member")
    Set parsed = JsonParse("{""ok"":true}")
    Print StrLeftBack("a/b", "/")
End Sub
""";

    var generated = transpiler.Transpile(source, "preprocessor-runtime-member-scope.xps", "win-x64");
    if (!generated.Contains("item.JsonStringify(\"member\")", StringComparison.Ordinal)
        || !generated.Contains("item.StrLeftBack(\"a/b\", \"/\")", StringComparison.Ordinal)
        || !generated.Contains("item.CsvParse(\"member\")", StringComparison.Ordinal)
        || !generated.Contains("item.XmlParse(\"member\")", StringComparison.Ordinal))
        throw new Exception("Runtime/global function rewriting captured a class member call.");
    if (!generated.Contains("XPScriptNativeJson.Parse(", StringComparison.Ordinal))
        throw new Exception("Unqualified JsonParse call no longer resolves to the native JSON runtime.");
    if (!generated.Contains("XPScriptReferenceRuntime.StrLeftBack(", StringComparison.Ordinal))
        throw new Exception("Unqualified StrLeftBack call no longer resolves to the reference runtime.");
    Console.WriteLine("PREPROCESSOR-RUNTIME-MEMBER-SCOPE=OK");
}


void VerifyRuntimeNameScopeMatrix()
{
    const string source = """
Class FirstScope
    Public JsonParse As String
    Public Function SHA256(value As String) As String
        SHA256 = value
    End Function
End Class

Class SecondScope
    Public JsonParse As String
    Public Function SHA256(value As String) As String
        SHA256 = value
    End Function
End Class

Class ParameterScope
    Public Function Echo(JsonParse As String) As String
        Dim SHA256 As String
        SHA256 = JsonParse
        Echo = SHA256
    End Function
End Class

Sub Main()
    Dim first As New FirstScope
    Dim second As New SecondScope
    Dim scoped As New ParameterScope
    first.JsonParse = "first"
    second.JsonParse = "second"
    Print first.JsonParse
    Print second.JsonParse
    Print first.SHA256("member-one")
    Print second.SHA256("member-two")
    Print scoped.Echo("parameter")
    Print SHA256("global")
End Sub
""";

    var generated = transpiler.Transpile(source, "preprocessor-runtime-name-scope-matrix.xps", "win-x64");
    foreach (var marker in new[]
    {
        "first.JsonParse",
        "second.JsonParse",
        "first.SHA256(\"member-one\")",
        "second.SHA256(\"member-two\")",
        "scoped.Echo(\"parameter\")"
    })
        if (!generated.Contains(marker, StringComparison.Ordinal))
            throw new Exception("Scope matrix lost user symbol/member: " + marker);

    if (!generated.Contains("XPScriptHashRuntime.SHA256(\"global\")", StringComparison.Ordinal))
        throw new Exception("Scope matrix no longer resolves an unqualified SHA256 call to the hash runtime.");

    Console.WriteLine("PREPROCESSOR-RUNTIME-NAME-SCOPE-MATRIX=OK");
}


void VerifyReservedIdentifierScope()
{
    const string legalMemberSource = """
Class XPJsonDocument
End Class
""";
    try
    {
        _ = transpiler.Transpile(legalMemberSource, "reserved-type-negative.xps", "win-x64");
        throw new Exception("Reserved runtime type name was accepted as a user type.");
    }
    catch (CompilerException ex) when (ex.Message.Contains("Type name is reserved", StringComparison.Ordinal))
    {
    }

    const string compilerStateSource = """
Sub Main()
    Dim __generated As String
End Sub
""";
    try
    {
        _ = transpiler.Transpile(compilerStateSource, "reserved-compiler-state.xps", "win-x64");
        throw new Exception("Compiler-reserved __ identifier was accepted.");
    }
    catch (CompilerException ex) when (ex.Message.Contains("compiler-generated state", StringComparison.Ordinal))
    {
    }

    const string typeVsMemberSource = """
Class UserModel
    Public XPJsonDocument As String
End Class

Sub Main()
    Dim model As New UserModel
    model.XPJsonDocument = "member"
    Print model.XPJsonDocument
End Sub
""";
    var generated = transpiler.Transpile(typeVsMemberSource, "reserved-type-vs-member.xps", "win-x64");
    if (!generated.Contains("model.XPJsonDocument", StringComparison.Ordinal))
        throw new Exception("Runtime type name was incorrectly reserved in class-member scope.");

    const string[] keywordSources =
    [
        "Class If\nEnd Class",
        "Sub Main()\n    Dim End As String\nEnd Sub"
    ];
    foreach (var keywordSource in keywordSources)
    {
        try
        {
            _ = transpiler.Transpile(keywordSource, "reserved-language-keyword.xps", "win-x64");
            throw new Exception("Language keyword was accepted as an identifier.");
        }
        catch (CompilerException)
        {
        }
    }

    Console.WriteLine("PREPROCESSOR-RESERVED-IDENTIFIER-SCOPE=OK");
}


void VerifyRuntimeValueIdentifierScope()
{
    foreach (var reserved in new[] { "Application", "Body" })
    {
        var variableSource = $"Sub Main()\n    Dim {reserved} As String\nEnd Sub";
        try
        {
            _ = transpiler.Transpile(variableSource, "reserved-runtime-value.xps", "win-x64");
            throw new Exception($"Reserved runtime value {reserved} was accepted as a local variable.");
        }
        catch (CompilerException ex) when (ex.Message.Contains("reserved by the XPScript runtime", StringComparison.Ordinal))
        {
        }

        var parameterSource = $"Sub Echo({reserved} As String)\nEnd Sub";
        try
        {
            _ = transpiler.Transpile(parameterSource, "reserved-runtime-parameter.xps", "win-x64");
            throw new Exception($"Reserved runtime value {reserved} was accepted as a parameter.");
        }
        catch (CompilerException ex) when (ex.Message.Contains("reserved by the XPScript runtime", StringComparison.Ordinal))
        {
        }

        var procedureSource = $"Sub {reserved}()\nEnd Sub";
        try
        {
            _ = transpiler.Transpile(procedureSource, "reserved-runtime-procedure.xps", "win-x64");
            throw new Exception($"Reserved runtime value {reserved} was accepted as a procedure name.");
        }
        catch (CompilerException ex) when (ex.Message.Contains("reserved by the XPScript runtime", StringComparison.Ordinal))
        {
        }
    }

    const string memberSource = """
Class RuntimeValueMembers
    Public Application As String
    Public Body As String
End Class

Sub Main()
    Dim item As New RuntimeValueMembers
    item.Application = "application-member"
    item.Body = "body-member"
    Print item.Application
    Print item.Body
End Sub
""";
    var generated = transpiler.Transpile(memberSource, "runtime-value-member-scope.xps", "win-x64");
    if (!generated.Contains("item.Application", StringComparison.Ordinal) ||
        !generated.Contains("item.Body", StringComparison.Ordinal))
        throw new Exception("Application/Body were incorrectly rejected or rewritten in receiver member scope.");

    Console.WriteLine("PREPROCESSOR-RUNTIME-VALUE-IDENTIFIER-SCOPE=OK");
}


void VerifySharedCallDetectionScope()
{
    const string memberOnly = """
Sub Main()
    Dim item As Object
    Print item.JsonParse("member")
End Sub
""";
    var code = PreprocessorFeatureGate.CodeOnly(memberOnly);
    if (PreprocessorFeatureGate.ContainsCall(code, "JsonParse"))
        throw new Exception("Shared call detection incorrectly classified member access as an unqualified runtime call.");

    const string globalCall = """
Sub Main()
    Print JsonParse("{}")
End Sub
""";
    code = PreprocessorFeatureGate.CodeOnly(globalCall);
    if (!PreprocessorFeatureGate.ContainsCall(code, "JsonParse"))
        throw new Exception("Shared call detection failed to recognize an unqualified runtime call.");

    const string lexicalNoise = """
Sub Main()
    Print "JsonParse(ignored)"
    ' JsonParse(ignored)
End Sub
""";
    code = PreprocessorFeatureGate.CodeOnly(lexicalNoise);
    if (PreprocessorFeatureGate.ContainsCall(code, "JsonParse"))
        throw new Exception("Shared call detection matched a string or comment.");

    Console.WriteLine("PREPROCESSOR-SHARED-CALL-DETECTION-SCOPE=OK");
}


void VerifySharedCallRewriteScope()
{
    const string source = """
Sub Main()
    Dim item As Object
    Print ToBase64("global")
    Print item.ToBase64("member")
    Print "ToBase64(ignored)"
    ' ToBase64(ignored)
    Rem ToBase64(ignored-rem)
    Print ToBase64$("global-dollar")
End Sub
""";
    var rewritten = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "ToBase64", "Runtime.ToBase64");
    if (!rewritten.Contains("Runtime.ToBase64(\"global\")", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter failed to rewrite an unqualified runtime call.");
    if (!rewritten.Contains("item.ToBase64(\"member\")", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter captured receiver member access.");
    if (!rewritten.Contains("\"ToBase64(ignored)\"", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter modified a string literal.");
    if (!rewritten.Contains("' ToBase64(ignored)", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter modified an apostrophe comment.");
    if (!rewritten.Contains("Rem ToBase64(ignored-rem)", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter modified a Rem comment.");
    if (!rewritten.Contains("Runtime.ToBase64(\"global-dollar\")", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter failed to rewrite a dollar-suffixed runtime call.");

    const string declaration = """
Class RuntimeNames
    Public Function ToBase64(value As String) As String
        ToBase64 = value
    End Function
End Class
""";
    var declarationRewrite = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(declaration, "ToBase64", "Runtime.ToBase64");
    if (!declarationRewrite.Contains("Function ToBase64(", StringComparison.Ordinal))
        throw new Exception("Shared call rewriter captured a function declaration.");

    Console.WriteLine("PREPROCESSOR-SHARED-CALL-REWRITE-SCOPE=OK");
}
