using XPScript.Compiler;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;

Environment.SetEnvironmentVariable("XPSCRIPT_AST_BOUND_CACHE", "1");
Environment.SetEnvironmentVariable("XPSCRIPT_AST_EMISSION_CACHE", "1");
BoundTreeCache.Clear();
BoundEmissionCache.Clear();
var root = Path.Combine(Path.GetTempPath(), "xpscript-ast-cache-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var include = Path.Combine(root, "shared.xps");
    var source = Path.Combine(root, "main.xps");
    File.WriteAllText(include, "Const SharedValue As Long = 7\n");
    File.WriteAllText(source, "Include \"shared.xps\"\nSub Main()\nPrint SharedValue\nEnd Sub\n");

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await AstCompiler.CompileAsync(source, Path.Combine(root, "first"));
    stopwatch.Stop();
    var cleanMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    var missesAfterFirst = BoundTreeCache.Misses;
    stopwatch.Restart();
    await AstCompiler.CompileAsync(source, Path.Combine(root, "second"));
    stopwatch.Stop();
    var cachedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    if (BoundTreeCache.Hits == 0)
        throw new InvalidOperationException("Unchanged source did not reuse a bound tree.");
    if (BoundEmissionCache.Hits == 0)
        throw new InvalidOperationException("Unchanged source did not reuse emitted C#.");

    File.WriteAllText(include, "Const SharedValue As Long = 8\n");
    stopwatch.Restart();
    await AstCompiler.CompileAsync(source, Path.Combine(root, "changed-include"));
    stopwatch.Stop();
    var includeEditMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    if (BoundTreeCache.Misses <= missesAfterFirst)
        throw new InvalidOperationException("Included-file edit did not invalidate the bound tree.");

    File.WriteAllText(source, "Include \"shared.xps\"\nSub Main()\nPrint SharedValue + 1\nEnd Sub\n");
    stopwatch.Restart();
    await AstCompiler.CompileAsync(source, Path.Combine(root, "changed-source"));
    stopwatch.Stop();
    var sourceEditMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    if (BoundTreeCache.Misses <= missesAfterFirst + 1)
        throw new InvalidOperationException("Source edit did not invalidate the bound tree.");

    Console.WriteLine($"AST_BOUND_CACHE_HIT_OK hits={BoundTreeCache.Hits} misses={BoundTreeCache.Misses}");
    Console.WriteLine($"AST_EDIT_COMPILE_BENCHMARK clean_ms={cleanMilliseconds:F1} cached_ms={cachedMilliseconds:F1} include_edit_ms={includeEditMilliseconds:F1} source_edit_ms={sourceEditMilliseconds:F1} emission_hits={BoundEmissionCache.Hits}");
}
finally
{
    Environment.SetEnvironmentVariable("XPSCRIPT_AST_BOUND_CACHE", null);
    Environment.SetEnvironmentVariable("XPSCRIPT_AST_EMISSION_CACHE", null);
    BoundTreeCache.Clear();
    BoundEmissionCache.Clear();
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}
