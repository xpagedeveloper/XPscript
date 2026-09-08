using System.Runtime.CompilerServices;
using XPScript.Web.Compiler;

internal static class NotesServerBoundaryVerification
{
    [ModuleInitializer]
    internal static void Verify()
    {
        var root = Path.Combine(Path.GetTempPath(), "xps-wasm-notes-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyRejected(root, "unannotated-notes.xps", """
[Platform:browser-wasm]

Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""", "Notes runtime state");

            VerifyRejected(root, "module-notes.xps", """
[Platform:browser-wasm]

Dim session As NotesSession

Sub Main()
    Print "browser"
End Sub
""", "module-level state");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void VerifyRejected(string root, string fileName, string source, string expectedMessage)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, source);
        try
        {
            using var unit = new XpsWebCompiler().CompileAsync(path, root).GetAwaiter().GetResult();
            throw new Exception($"Browser-WASM Notes boundary verification unexpectedly compiled {fileName}.");
        }
        catch (XpsWebCompilationException ex) when (
            ex.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
        }
    }
}
