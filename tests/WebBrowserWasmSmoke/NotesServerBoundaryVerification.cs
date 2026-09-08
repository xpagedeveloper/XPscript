using System.Runtime.CompilerServices;
using XPScript.Web.Compiler;

internal static class NotesServerBoundaryVerification
{
    [ModuleInitializer]
    internal static void Verify()
    {
        VerifyUnannotatedNotesIsRejected();
        VerifyModuleNotesStateIsRejected();
    }

    private static void VerifyUnannotatedNotesIsRejected()
    {
        const string source = """
[Platform:browser-wasm]

Function ReadServerName() As String
    Dim session As New NotesSession
    ReadServerName = session.ServerName
End Function

Sub Main()
    Print ReadServerName()
End Sub
""";

        try
        {
            _ = BrowserWasmServerSideMetadata.ReadAnnotatedProcedures(source);
            throw new Exception("Unannotated Notes browser-WASM code passed the server-boundary verifier.");
        }
        catch (XpsWebCompilationException ex) when (
            ex.Message.Contains("Notes runtime state", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("[ServerSide]", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void VerifyModuleNotesStateIsRejected()
    {
        const string source = """
[Platform:browser-wasm]

Dim session As NotesSession

Sub Main()
    Print "browser"
End Sub
""";

        try
        {
            _ = BrowserWasmServerSideMetadata.ReadAnnotatedProcedures(source);
            throw new Exception("Module-level Notes state passed the browser-WASM server-boundary verifier.");
        }
        catch (XpsWebCompilationException ex) when (ex.Message.Contains("module-level state", StringComparison.OrdinalIgnoreCase))
        {
        }
    }
}
