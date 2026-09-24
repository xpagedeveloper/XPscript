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
""", "Notes runtime state", "ReadServerName", "Client");

            VerifyRejected(root, "module-notes.xps", """
[Platform:browser-wasm]

Dim session As NotesSession

Sub Main()
    Print "browser"
End Sub
""", "module-level state", "Notes", "Module");

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

            VerifyStructuredRejected(root, "entry-server-side.xps", """
[Platform:browser-wasm]

[ServerSide]
Sub Main()
    Print "server"
End Sub
""", "Main", "BrowserEntryPoint", "XPS3002", "execution-context");

            VerifyStructuredRejected(root, "nonserializable-server-side.xps", """
[Platform:browser-wasm]

[ServerSide]
Function ReadValue(value As UIForm) As String
    ReadValue = "server"
End Function

Sub Main()
    Print "browser"
End Sub
""", "ReadValue.value", "ServerSide", "XPS2003", "type");

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

    private static void VerifyRejected(
        string root,
        string fileName,
        string source,
        string expectedMessage,
        string? expectedSymbol = null,
        string? expectedContext = null)
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
            if (expectedSymbol is null) return;
            if (ex.DiagnosticCode != "XPS3002" || ex.Category != "execution-context")
                throw new Exception($"{fileName} did not produce structured XPS3002 execution-context metadata.");
            if (!ex.Properties.TryGetValue("symbol", out var symbol) || symbol != expectedSymbol)
                throw new Exception($"{fileName} did not identify the expected offending symbol.");
            if (!ex.Properties.TryGetValue("target", out var target) || target != "browser-wasm")
                throw new Exception($"{fileName} did not identify browser-wasm.");
            if (!ex.Properties.TryGetValue("currentContext", out var currentContext) || currentContext != expectedContext)
                throw new Exception($"{fileName} did not identify the expected current context.");
            if (!ex.Properties.TryGetValue("requiredContext", out var requiredContext) || requiredContext != "ServerSide")
                throw new Exception($"{fileName} did not identify ServerSide as the required context.");
        }
    }

    private static void VerifyStructuredRejected(
        string root,
        string fileName,
        string source,
        string expectedSymbol,
        string expectedContext,
        string expectedCode,
        string expectedCategory)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, source);
        try
        {
            var unit = new XpsWebCompiler().CompileAsync(path, root).GetAwaiter().GetResult();
            unit.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new Exception($"Browser-WASM structured boundary verification unexpectedly compiled {fileName}.");
        }
        catch (XpsWebCompilationException ex)
        {
            if (ex.DiagnosticCode != expectedCode || ex.Category != expectedCategory)
                throw new Exception($"{fileName} produced {ex.DiagnosticCode}/{ex.Category}, expected {expectedCode}/{expectedCategory}.");
            if (!ex.Properties.TryGetValue("symbol", out var symbol) || symbol != expectedSymbol)
                throw new Exception($"{fileName} did not identify {expectedSymbol}.");
            if (!ex.Properties.TryGetValue("target", out var target) || target != "browser-wasm")
                throw new Exception($"{fileName} did not identify browser-wasm.");
            if (!ex.Properties.TryGetValue("currentContext", out var currentContext) || currentContext != expectedContext)
                throw new Exception($"{fileName} did not identify {expectedContext}.");
            if (expectedCode == "XPS3002" &&
                (!ex.Properties.TryGetValue("requiredContext", out var requiredContext) || requiredContext != "ServerSide"))
                throw new Exception($"{fileName} did not identify ServerSide as required context.");
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
