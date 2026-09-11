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

ExpectIteratorSuccess();

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
    Dim archive As New Archive()
    archive.Create("zip")
    archive.AddText("one.txt", "1")
    archive.AddText("two.txt", "2")

    Dim count As Integer
    Dim item As Variant
    ForAll item In archive.Entries
        count = count + 1
    End ForAll

    Dim entry As ArchiveEntry
    Set entry = archive.GetFirstEntry()
    While entry Is Not Nothing
        If entry.IsFile Then Print entry.FullName
        If entry.IsFolder Then Print entry.FullName
        Set entry = archive.GetNextEntry(entry)
    Wend
End Sub
""";

    var generated = transpiler.Transpile(source, "archive-iterator-probe.xps", "win-x64");
    if (!generated.Contains("XpsCompilerGeneratedArchiveGetFirstEntry", StringComparison.Ordinal)
        || !generated.Contains("XpsCompilerGeneratedArchiveGetNextEntry", StringComparison.Ordinal))
        throw new Exception("Archive iterator helpers were not emitted.");
    if (generated.Contains(".IsFile", StringComparison.Ordinal)
        || generated.Contains(".IsFolder", StringComparison.Ordinal))
        throw new Exception("ArchiveEntry IsFile/IsFolder aliases were not lowered to IsDirectory.");
    if (!generated.Contains(".Entries", StringComparison.Ordinal))
        throw new Exception("Existing Archive.Entries surface disappeared while enabling iterator aliases.");
}
