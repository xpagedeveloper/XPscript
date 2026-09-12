using XPScript.Compiler;

var transpiler = new XPScriptTranspiler();

ExpectExtendedFailure(
    "zip-password",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("secure.zip")
    archive.Password = "secret"
End Sub
""");

ExpectExtendedFailure(
    "zip-encryption-detection",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("secure.zip", False)
    Print CStr(archive.IsEncrypted)
End Sub
""");

ExpectExtendedFailure(
    "zip-create-gzip",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("data.gz")
    archive.Create("gzip")
End Sub
""");

ExpectSuccess(
    "extended-password",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("secure.zip", True)
    archive.Password = "secret"
    archive.Open()
End Sub
""");

ExpectUnsupportedEncryptedZipWrite(
    "extended-password-write",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("secure.zip", True)
    archive.Password = "secret"
    archive.AddText("payload.txt", "hello")
End Sub
""");

ExpectSuccess(
    "extended-gzip",
    """
Option Declare
Sub Main()
    Dim archive As New Archive("data.gz", True)
    archive.Create("gzip")
    archive.AddText("payload.txt", "hello")
End Sub
""");

foreach (var format in new[] { "rar", "bzip2", "lzip", "xz", "zstd" })
    ExpectUnsupportedCreate(format);

ExpectIteratorSuccess();
ExpectChainedEntryAliases();
ExpectRemovedIsDirectory();
ExpectRemovedChainedIsDirectory();
ExpectExtendedDiagnosticNormalization();

Console.WriteLine("ARCHIVE-CAPABILITY-PROBE=OK");

void ExpectExtendedFailure(string name, string source)
{
    try
    {
        _ = transpiler.Transpile(source, name + ".xps", "win-x64");
        throw new Exception(name + " unexpectedly compiled without extended Archive support.");
    }
    catch (CompilerException ex)
    {
        if (!ex.Message.Contains("requires extended archive support", StringComparison.Ordinal)
            || !ex.Message.Contains("New Archive(..., True)", StringComparison.Ordinal)
            || ex.Message.Contains("SharpCompress", StringComparison.OrdinalIgnoreCase))
            throw new Exception(name + " returned the wrong compile diagnostic: " + ex.Message);
    }
}

void ExpectUnsupportedEncryptedZipWrite(string name, string source)
{
    try
    {
        _ = transpiler.Transpile(source, name + ".xps", "win-x64");
        throw new Exception(name + " unexpectedly compiled password-protected ZIP writing.");
    }
    catch (CompilerException ex)
    {
        if (!ex.Message.Contains("Password-protected ZIP writing is not supported", StringComparison.Ordinal)
            || ex.Message.Contains("SharpCompress", StringComparison.OrdinalIgnoreCase))
            throw new Exception(name + " returned the wrong encrypted ZIP write diagnostic: " + ex.Message);
    }
}

void ExpectUnsupportedCreate(string format)
{
    var source = $$"""
Option Declare
Sub Main()
    Dim archive As New Archive("data.{{format}}", True)
    archive.Create("{{format}}")
End Sub
""";

    try
    {
        _ = transpiler.Transpile(source, "unsupported-create-" + format + ".xps", "win-x64");
        throw new Exception(format + " unexpectedly compiled as a writable Archive format.");
    }
    catch (CompilerException ex)
    {
        if (!ex.Message.Contains("is not supported for writing", StringComparison.Ordinal)
            || !ex.Message.Contains("can only be opened/read", StringComparison.Ordinal)
            || ex.Message.Contains("SharpCompress", StringComparison.OrdinalIgnoreCase))
            throw new Exception(format + " returned the wrong write capability diagnostic: " + ex.Message);
    }
}

void ExpectSuccess(string name, string source)
{
    var generated = transpiler.Transpile(source, name + ".xps", "win-x64");
    if (!generated.Contains("XPScriptExtendedArchiveWriterFactory", StringComparison.Ordinal))
        throw new Exception(name + " did not emit extended Archive support.");
}

void ExpectIteratorSuccess()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New arCHive()
    archive.cReAtE("zip")
    archive.aDdTeXt("one.txt", "1")
    archive.ADDTEXT("two.txt", "2")

    Dim count As Integer
    Dim item As Variant
    ForAll item In archive.eNtRiEs
        count = count + 1
    End ForAll

    Dim entry As aRcHiVeEnTrY
    Set entry = archive.gEtFiRsTeNtRy()
    While entry Is Not Nothing
        If entry.iSfIlE Then Print entry.fUlLnAmE
        If entry.ISFOLDER Then Print entry.FullName
        Set entry = archive.GETNEXTENTRY(entry)
    Wend
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-iterator-probe.xps", "win-x64");
    if (!generated.Contains("XPScriptArchiveIteratorRuntime.GetFirstEntry", StringComparison.Ordinal)
        || !generated.Contains("XPScriptArchiveIteratorRuntime.GetNextEntry", StringComparison.Ordinal)
        || !generated.Contains("ConditionalWeakTable<object, IteratorState>", StringComparison.Ordinal))
        throw new Exception("Archive stable iterator runtime was not emitted for mixed-case API usage.");
    if (generated.Contains(".IsFile", StringComparison.OrdinalIgnoreCase)
        || generated.Contains(".IsFolder", StringComparison.OrdinalIgnoreCase))
        throw new Exception("ArchiveEntry IsFile/IsFolder aliases were not case-insensitively lowered to the internal entry type flag.");
    if (!generated.Contains(".eNtRiEs", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Existing Archive.Entries surface disappeared while enabling iterator aliases.");
}

void ExpectChainedEntryAliases()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive()
    archive.Create("zip")
    archive.AddText("one.txt", "1")
    If archive.GetEntry("one.txt").IsFile Then Print "file"
    If archive.GetEntry("one.txt").IsFolder Then Print "folder"
    If archive.GetFirstEntry().IsFile Then Print "first-file"
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-entry-chained-aliases.xps", "win-x64");
    if (generated.Contains(".IsFile", StringComparison.OrdinalIgnoreCase)
        || generated.Contains(".IsFolder", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Chained ArchiveEntry IsFile/IsFolder aliases were not lowered.");
    if (!generated.Contains(".IsDirectory", StringComparison.Ordinal))
        throw new Exception("Chained ArchiveEntry aliases did not lower to the internal directory flag.");
}

void ExpectRemovedIsDirectory()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive()
    archive.Create("zip")
    archive.AddText("one.txt", "1")
    Dim entry As ArchiveEntry
    Set entry = archive.GetFirstEntry()
    If entry Is Not Nothing Then Print CStr(entry.IsDirectory)
End Sub
""";

    ExpectRemovedIsDirectoryDiagnostic("archive-entry-isdirectory.xps", source);
}

void ExpectRemovedChainedIsDirectory()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive()
    archive.Create("zip")
    archive.AddText("one.txt", "1")
    If archive.GetEntry("one.txt").IsDirectory Then Print "directory"
End Sub
""";

    ExpectRemovedIsDirectoryDiagnostic("archive-entry-chained-isdirectory.xps", source);
}

void ExpectRemovedIsDirectoryDiagnostic(string fileName, string source)
{
    try
    {
        _ = transpiler.Transpile(source, fileName, "win-x64");
        throw new Exception("ArchiveEntry.IsDirectory unexpectedly remained public.");
    }
    catch (CompilerException ex)
    {
        if (!ex.Message.Contains("ArchiveEntry.IsDirectory is not available", StringComparison.Ordinal)
            || !ex.Message.Contains("IsFile", StringComparison.Ordinal)
            || !ex.Message.Contains("IsFolder", StringComparison.Ordinal))
            throw new Exception("ArchiveEntry.IsDirectory returned the wrong diagnostic: " + ex.Message);
    }
}

void ExpectExtendedDiagnosticNormalization()
{
    const string source = """
Option Declare
Sub Main()
    Dim archive As New Archive("data.7z", True)
    archive.Open()
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-diagnostic-normalization.xps", "win-x64");
    if (!generated.Contains("return new XPScriptExtendedArchiveV5(value);", StringComparison.Ordinal)
        || !generated.Contains("return new XPScriptExtendedMemoryArchiveV4(value);", StringComparison.Ordinal)
        || !generated.Contains("message.Contains(\"SharpCompress\"", StringComparison.Ordinal)
        || !generated.Contains("Unable to process the extended archive.", StringComparison.Ordinal))
        throw new Exception("Extended Archive diagnostic normalization is not wired through the public factory.");
}
