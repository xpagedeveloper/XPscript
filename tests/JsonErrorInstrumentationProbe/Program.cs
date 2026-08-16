using XPScript.Compiler;

static void Verify(string label, string source, string needle)
{
    var generated = new XPScriptTranspiler().Transpile(source, label + ".xps");
    var call = generated.IndexOf(needle, StringComparison.Ordinal);
    if (call < 0)
        throw new Exception($"{label}: generated call was not found: {needle}");

    var tryStart = generated.LastIndexOf("try { ", call, StringComparison.Ordinal);
    var catchStart = generated.IndexOf("catch (Exception __lsEx)", call, StringComparison.Ordinal);
    var nextTry = generated.IndexOf("try { ", call + 1, StringComparison.Ordinal);

    var protectedByStatementWrapper = tryStart >= 0 && catchStart > call && (nextTry < 0 || catchStart < nextTry);
    if (!protectedByStatementWrapper)
    {
        var start = Math.Max(0, call - 500);
        var length = Math.Min(generated.Length - start, 1200);
        Console.Error.WriteLine(generated.Substring(start, length));
        throw new Exception($"{label}: generated call is not protected by the On Error statement wrapper.");
    }

    Console.WriteLine(label + "_ERROR_WRAPPER=OK");
}

Verify(
    "JSON",
    """
Option Declare
Sub Main()
    Dim document As JsonDocument
    Dim errCode As Integer
    On Error Resume Next
    Set document = JsonDocument.Parse("{bad")
    errCode = Err
    On Error GoTo 0
End Sub
""",
    "XPScriptNativeJson.Parse");

Verify(
    "HTTP",
    """
Option Declare
Sub Main()
    Dim client As New HttpClient
    Dim response As HttpResponse
    Dim errCode As Integer
    On Error Resume Next
    Set response = client.Get("http://127.0.0.1:1/")
    errCode = Err
    On Error GoTo 0
End Sub
""",
    ".Get(");
