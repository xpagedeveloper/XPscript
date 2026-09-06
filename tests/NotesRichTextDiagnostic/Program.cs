using XPScript.Compiler;

if (args.Length != 2)
    throw new ArgumentException("Expected source and output paths.");

var compiler = new CompilerDriver();
await compiler.CompileAsync(
    Path.GetFullPath(args[0]),
    Path.GetFullPath(args[1]),
    selfContained: false,
    CompilerDriver.CurrentRuntimeIdentifier());
