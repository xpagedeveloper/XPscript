using XPScript.Compiler;
using XPScript.Compiler.Binding;

Environment.SetEnvironmentVariable("XPSCRIPT_AST_BOUND_CACHE", "1");
BoundTreeCache.Clear();
var root = Path.Combine(Path.GetTempPath(), "xpscript-ast-cache-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var include = Path.Combine(root, "shared.xps");
    var source = Path.Combine(root, "main.xps");
    File.WriteAllText(include, "Const SharedValue As Long = 7\n");
    File.WriteAllText(source, "Include \"shared.xps\"\nSub Main()\nPrint SharedValue\nEnd Sub\n");

    await AstCompiler.CompileAsync(source, Path.Combine(root, "first"));
    var missesAfterFirst = BoundTreeCache.Misses;
    await AstCompiler.CompileAsync(source, Path.Combine(root, "second"));
    if (BoundTreeCache.Hits == 0)
        throw new InvalidOperationException("Unchanged source did not reuse a bound tree.");

    File.WriteAllText(include, "Const SharedValue As Long = 8\n");
    await AstCompiler.CompileAsync(source, Path.Combine(root, "changed-include"));
    if (BoundTreeCache.Misses <= missesAfterFirst)
        throw new InvalidOperationException("Included-file edit did not invalidate the bound tree.");

    File.WriteAllText(source, "Include \"shared.xps\"\nSub Main()\nPrint SharedValue + 1\nEnd Sub\n");
    await AstCompiler.CompileAsync(source, Path.Combine(root, "changed-source"));
    if (BoundTreeCache.Misses <= missesAfterFirst + 1)
        throw new InvalidOperationException("Source edit did not invalidate the bound tree.");

    Console.WriteLine($"AST_BOUND_CACHE_HIT_OK hits={BoundTreeCache.Hits} misses={BoundTreeCache.Misses}");
}
finally
{
    Environment.SetEnvironmentVariable("XPSCRIPT_AST_BOUND_CACHE", null);
    BoundTreeCache.Clear();
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}
