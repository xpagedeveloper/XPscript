using System.Diagnostics;
using XPScript.Compiler.Syntax;

// This is a correctness-first baseline, not a performance claim. Keep the
// corpus small and stable so future incremental-parser work can compare the
// same clean parse results and diagnostics.
var corpus = new[]
{
    new Case("module", "Option Declare\nConst Answer As Integer = 42\nSub Main()\nPrint Answer\nEnd Sub", 3, 0),
    new Case("control-flow", "Sub Main()\nDim value As Integer\nIf value > 0 Then\nvalue = value + 1\nElse\nvalue = 0\nEnd If\nEnd Sub", 1, 0),
    new Case("class-lifecycle", "Class Child Extend Base\nPrivate Value As Integer\nSub New(ByVal value As Integer)\nMe.Value = value\nEnd Sub\nSub Delete()\nEnd Sub\nEnd Class", 1, 0),
    new Case("invalid", "Sub Broken(\nEnd Sub", 1, 1)
};

const int warmup = 2;
const int measured = 10;
foreach (var item in corpus)
{
    for (var i = 0; i < warmup; i++) _ = Parse(item.Source);

    var stopwatch = Stopwatch.StartNew();
    (DeclarationParser Parser, CompilationUnitSyntax Tree) last = default;
    for (var i = 0; i < measured; i++) last = Parse(item.Source);
    stopwatch.Stop();

    if (last.Tree.Declarations.Count != item.Declarations)
        throw new InvalidOperationException($"{item.Name}: declaration count changed.");
    if (last.Parser.Diagnostics.Count != item.Diagnostics)
        throw new InvalidOperationException($"{item.Name}: diagnostic count changed (expected {item.Diagnostics}, got {last.Parser.Diagnostics.Count}).");

    Console.WriteLine($"AST_PARSE_BASELINE={item.Name}; declarations={item.Declarations}; diagnostics={item.Diagnostics}; iterations={measured}; elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F3}");
}

Console.WriteLine("AST_PARSE_BASELINE=OK; clean non-incremental parser corpus verified");

static (DeclarationParser Parser, CompilationUnitSyntax Tree) Parse(string source)
{
    var parser = new DeclarationParser(source);
    return (parser, parser.ParseCompilationUnit());
}

file sealed record Case(string Name, string Source, int Declarations, int Diagnostics);
