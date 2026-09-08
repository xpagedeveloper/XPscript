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

            VerifyRejected(root, "invalid-spinner-delay.xps", """
[Platform:browser-wasm]

[ServerSide(SpinnerDelay=-1)]
Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""", "Invalid [ServerSide] syntax");

            VerifyRejected(root, "unknown-server-side-option.xps", """
[Platform:browser-wasm]

[ServerSide(Delay=1000)]
Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""", "Invalid [ServerSide] syntax");

            VerifyAccepted(root, "spinner-delay-zero.xps", """
[Platform:browser-wasm]

[ServerSide(SpinnerDelay=0)]
Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""");

            VerifyAccepted(root, "spinner-delay-custom.xps", """
[Platform:browser-wasm]

[ServerSide(SpinnerDelay=1000)]
Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""");
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
            var unit = new XpsWebCompiler().CompileAsync(path, root).GetAwaiter().GetResult();
            unit.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new Exception($"Browser-WASM Notes boundary verification unexpectedly compiled {fileName}.");
        }
        catch (XpsWebCompilationException ex) when (
            ex.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void VerifyAccepted(string root, string fileName, string source)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, source);
        var unit = new XpsWebCompiler().CompileAsync(path, root).GetAwaiter().GetResult();
        unit.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
