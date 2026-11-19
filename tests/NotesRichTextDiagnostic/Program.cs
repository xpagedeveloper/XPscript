using XPScript.Compiler;

if (args.Length != 2)
    throw new ArgumentException("Expected source and output paths.");

await AstCompiler.CompileAsync(
    Path.GetFullPath(args[0]),
    Path.GetFullPath(args[1]),
    CompilerDriver.CurrentRuntimeIdentifier());
