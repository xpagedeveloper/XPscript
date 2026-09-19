using System.Diagnostics;
using XPScript.Compiler;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CompilerValidationBenchmark <repo-root>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var sourcePath = Path.Combine(root, "samples", "null-integer-assignment-error.xps");
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine("Benchmark source not found: " + sourcePath);
    return 2;
}

var compiler = new CompilerDriver();
var rid = CompilerDriver.CurrentRuntimeIdentifier();

// Warm up JIT/compiler infrastructure before measuring reusable in-process validation.
var warmup = await compiler.ValidateWithResultAsync(sourcePath, rid);
if (warmup.Success)
{
    Console.Error.WriteLine("Expected benchmark source to fail validation.");
    return 2;
}

var samples = new List<long>();
for (var i = 0; i < 3; i++)
{
    var sw = Stopwatch.StartNew();
    var result = await compiler.ValidateWithResultAsync(sourcePath, rid);
    sw.Stop();
    if (result.Success)
    {
        Console.Error.WriteLine("Expected benchmark source to fail validation.");
        return 2;
    }
    samples.Add(sw.ElapsedMilliseconds);
}

var ordered = samples.OrderBy(x => x).ToArray();
var median = ordered[ordered.Length / 2];
Console.WriteLine($"validationInProcessMs={string.Join(",", samples)} medianMs={median}");
return 0;
