using System.Reflection;
using XPScript.Compiler;

static bool IsProtected(string generated, string needle, out string snippet)
{
    var call = generated.IndexOf(needle, StringComparison.Ordinal);
    if (call < 0)
    {
        snippet = "generated call not found: " + needle;
        return false;
    }

    var tryStart = generated.LastIndexOf("try { ", call, StringComparison.Ordinal);
    var catchStart = generated.IndexOf("catch (Exception __lsEx)", call, StringComparison.Ordinal);
    var nextTry = generated.IndexOf("try { ", call + 1, StringComparison.Ordinal);
    var protectedByStatementWrapper = tryStart >= 0 && catchStart > call && (nextTry < 0 || catchStart < nextTry);

    var start = Math.Max(0, call - 500);
    var length = Math.Min(generated.Length - start, 1200);
    snippet = generated.Substring(start, length);
    return protectedByStatementWrapper;
}

static void Verify(string label, string source, string needle)
{
    var generated = new XPScriptTranspiler().Transpile(source, label + ".xps");
    if (!IsProtected(generated, needle, out var snippet))
    {
        Console.Error.WriteLine(label + " FULL PIPELINE:");
        Console.Error.WriteLine(snippet);
        throw new Exception($"{label}: generated call is not protected by the On Error statement wrapper.");
    }
    Console.WriteLine(label + "_ERROR_WRAPPER=OK");
}

static void VerifyCoreDirect()
{
    var assembly = typeof(XPScriptTranspiler).Assembly;
    var type = assembly.GetType("XPScript.Compiler.CoreCompatibilityTranspiler", throwOnError: true)!;
    var instance = Activator.CreateInstance(type, nonPublic: true)!;
    var method = type.GetMethod("Transpile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
    var source = """
Sub Main()
    Dim document As Variant
    Dim errCode As Integer
    On Error Resume Next
    document = XPScriptNativeJson.Parse("{bad")
    errCode = Err
    On Error GoTo 0
End Sub
""";
    var generated = (string)method.Invoke(instance, [source, "core-json.xps"])!;
    if (!IsProtected(generated, "XPScriptNativeJson.Parse", out var snippet))
    {
        Console.Error.WriteLine("CORE DIRECT:");
        Console.Error.WriteLine(snippet);
        throw new Exception("CoreCompatibilityTranspiler does not protect a direct JSON parse assignment.");
    }
    Console.WriteLine("CORE_DIRECT_ERROR_WRAPPER=OK");
}

VerifyCoreDirect();

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
