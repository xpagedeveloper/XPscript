using XPScript.Web.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-web-recursion-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    await AssertRejectedAsync("direct.xps", """
[Anonymous]
[Get]
Sub Index()
    Call Index()
End Sub
""");

    await AssertRejectedAsync("mutual.xps", """
[Anonymous]
[Get]
Sub Index()
    Call Worker()
End Sub

Sub Worker()
    Call Index()
End Sub
""");

    var safePath = Path.Combine(root, "safe.xps");
    await File.WriteAllTextAsync(safePath, """
[Anonymous]
[Get]
Sub Index()
    Call Worker()
End Sub

Sub Worker()
End Sub
""");
    await using var safe = await new XpsWebCompiler().CompileAsync(safePath, root);
    if (!safe.Routes.ContainsKey("Index")) throw new Exception("Non-recursive web script did not compile.");

    Console.WriteLine("WEB-COMPILER-RECURSION-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

async Task AssertRejectedAsync(string fileName, string source)
{
    var path = Path.Combine(root, fileName);
    await File.WriteAllTextAsync(path, source);
    try
    {
        await using var _ = await new XpsWebCompiler().CompileAsync(path, root);
    }
    catch (XpsWebCompilationException ex) when (ex.Message.Contains("recursive procedure cycles", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }
    throw new Exception($"Recursive web script '{fileName}' was accepted.");
}
