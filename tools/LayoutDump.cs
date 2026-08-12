using XPScript.Compiler;

var sourcePath = Path.GetFullPath("samples/statement-layout-audit.xps");
var source = File.ReadAllText(sourcePath);
var generated = new XPScriptTranspiler().Transpile(source, sourcePath, CompilerDriver.CurrentRuntimeIdentifier());
var lines = generated.Replace("\r\n", "\n").Split('\n');
for (var i = 0; i < lines.Length; i++)
{
    if (!lines[i].Contains("CONTINUED_IF", StringComparison.Ordinal) &&
        !lines[i].Contains("LogicalAnd", StringComparison.Ordinal)) continue;
    var start = Math.Max(0, i - 3);
    var end = Math.Min(lines.Length - 1, i + 3);
    for (var j = start; j <= end; j++) Console.WriteLine($"{j + 1}: {lines[j]}");
    Console.WriteLine("---");
}
