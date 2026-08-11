using LSLite.Compiler;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
LS Lite Compiler

Usage:
  lslitec <source.ls> [-o output.exe] [--framework-dependent]

Examples:
  lslitec hello.ls
  lslitec hello.ls -o Hello.exe

The default output is a self-contained Windows x64 single-file executable.
""");
    return 0;
}

try
{
    var sourcePath = Path.GetFullPath(args[0]);

    if (!File.Exists(sourcePath))
        throw new FileNotFoundException("Source file not found.", sourcePath);

    string? outputPath = null;
    var selfContained = true;

    for (var i = 1; i < args.Length; i++)
    {
        if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
        {
            outputPath = Path.GetFullPath(args[++i]);
        }
        else if (args[i] == "--framework-dependent")
        {
            selfContained = false;
        }
        else
        {
            throw new ArgumentException($"Unknown argument: {args[i]}");
        }
    }

    outputPath ??= Path.Combine(
        Path.GetDirectoryName(sourcePath)!,
        Path.GetFileNameWithoutExtension(sourcePath) + ".exe");

    var compiler = new CompilerDriver();
    await compiler.CompileAsync(sourcePath, outputPath, selfContained);

    Console.WriteLine($"Created: {outputPath}");
    return 0;
}
catch (CompilerException ex)
{
    Console.Error.WriteLine($"Compile error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
