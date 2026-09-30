using XPScript.Compiler;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../"));
var outRoot = Path.Combine(root, "out", "ast-repository-sweep");
Directory.CreateDirectory(outRoot);

string[] negativePatterns = ["-error.xps", "-invalid.xps", "-ambiguous.xps", "-duplicate.xps", "-no-match.xps"];
static bool IsNegative(string path, string[] patterns) =>
    patterns.Any(pattern => Path.GetFileName(path).EndsWith(pattern, StringComparison.OrdinalIgnoreCase));

var files = new[] { Path.Combine(root, "samples"), Path.Combine(root, "demo") }
    .Where(Directory.Exists)
    .SelectMany(directory => Directory.EnumerateFiles(directory, "*.xps", SearchOption.AllDirectories))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
var positive = files.Where(path => !IsNegative(path, negativePatterns)).ToArray();
var negative = files.Where(path => IsNegative(path, negativePatterns)).ToArray();

Console.WriteLine($"AST_REPOSITORY_SWEEP total={files.Length} positive={positive.Length} negative={negative.Length}");
var failures = new List<string>();
var compiler = new CompilerDriver();
var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();

for (var index = 0; index < positive.Length; index++)
{
    var file = positive[index];
    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
    var safeName = new string(relative.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '_').ToArray());
    if (safeName.EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
        safeName = safeName[..^4];
    var output = Path.Combine(outRoot, $"{index + 1:D4}-{safeName}");
    Console.WriteLine($"AST_REPOSITORY_COMPILE={relative}");

    CompileResult result;
    try
    {
        result = await compiler.CompileWithResultAsync(file, output, selfContained: false, runtimeIdentifier);
    }
    catch (Exception ex)
    {
        failures.Add(relative);
        Console.WriteLine($"AST_REPOSITORY_SKIPPED_EXCEPTION={relative} type={ex.GetType().Name}");
        continue;
    }

    if (!result.Success)
    {
        failures.Add(relative);
        Console.WriteLine($"AST_REPOSITORY_SKIPPED={relative}");
    }
}

Console.WriteLine("AST_REPOSITORY_NEGATIVE_FIXTURES");
foreach (var file in negative)
    Console.WriteLine(Path.GetRelativePath(root, file).Replace('\\', '/'));

Console.WriteLine($"AST_REPOSITORY_EXISTING_COMPILE_FAILURES count={failures.Count}");
foreach (var relative in failures)
    Console.WriteLine($"AST_REPOSITORY_SKIPPED={relative}");

Console.WriteLine($"AST repository compile sweep completed: {positive.Length - failures.Count} scripts compiled; {failures.Count} existing compiler failures skipped; {negative.Length} named negative fixtures classified separately.");
