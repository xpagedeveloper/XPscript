using XPScript.Web.Compiler;

namespace XPScript.Cli;

internal static class XpsOpenApiCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        if (command is not ("generate" or "import"))
            throw new ArgumentException("openapi supports the 'generate' and 'import' commands.");
        if (args.Length < 2)
            throw new ArgumentException($"openapi {command} requires an OpenAPI .yaml, .yml, or .json specification file.");

        var specificationPath = Path.GetFullPath(args[1]);
        string? outputPath = null;
        var force = false;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                case "--output":
                    if (++i >= args.Length) throw new ArgumentException(args[i - 1] + " requires an output .xps path.");
                    outputPath = Path.GetFullPath(args[i]);
                    break;
                case "--force" when command == "generate":
                    force = true;
                    break;
                case "--force":
                    throw new ArgumentException("openapi import is additive and does not accept --force.");
                default:
                    throw new ArgumentException($"Unknown openapi {command} argument: " + args[i]);
            }
        }

        ValidateSpecificationPath(specificationPath);
        outputPath ??= Path.Combine(
            Path.GetDirectoryName(specificationPath) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(specificationPath) + ".xps");

        if (!Path.GetExtension(outputPath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpenAPI generated output must use the .xps extension.");

        return command == "generate"
            ? Generate(specificationPath, outputPath, force)
            : Import(specificationPath, outputPath);
    }

    private static int Generate(string specificationPath, string outputPath, bool force)
    {
        if (File.Exists(outputPath) && !force)
            throw new IOException("Generated output already exists. Use --force to overwrite: " + outputPath);

        var result = new XpsOpenApiGenerator().GenerateFile(specificationPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, result.Source);

        Console.WriteLine($"Generated {outputPath}");
        Console.WriteLine($"OpenAPI {result.OpenApiVersion}: {result.Operations.Count} endpoint(s), {result.Models.Count} model(s)");
        return 0;
    }

    private static int Import(string specificationPath, string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            var generated = new XpsOpenApiGenerator().GenerateFile(specificationPath);
            ValidateAndReplace(outputPath, generated.Source);
            Console.WriteLine($"Imported {outputPath}");
            Console.WriteLine($"OpenAPI {generated.OpenApiVersion}: created new file with {generated.Operations.Count} endpoint(s), {generated.Models.Count} model(s)");
            return 0;
        }

        var existing = File.ReadAllText(outputPath);
        var result = new XpsOpenApiImporter().ImportFile(specificationPath, existing);
        if (result.Changed)
            ValidateAndReplace(outputPath, result.Source);

        Console.WriteLine($"OpenAPI {result.OpenApiVersion} additive import: {(result.Changed ? "updated" : "no additions")}");
        Console.WriteLine($"Added: {result.AddedClasses.Count} class(es), {result.AddedProperties.Count} class property/properties, {result.AddedProcedures.Count} procedure(s)");
        foreach (var item in result.AddedClasses) Console.WriteLine("  + class " + item);
        foreach (var item in result.AddedProperties) Console.WriteLine("  + property " + item);
        foreach (var item in result.AddedProcedures) Console.WriteLine("  + " + item);
        foreach (var warning in result.Warnings) Console.Error.WriteLine("warning: " + warning);
        Console.WriteLine("Existing declarations were preserved.");
        return 0;
    }

    private static void ValidateAndReplace(string outputPath, string source)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory)) directory = Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, "." + Path.GetFileName(outputPath) + ".openapi-import-" + Guid.NewGuid().ToString("N") + ".xps");
        try
        {
            File.WriteAllText(tempPath, source);
            var unit = new XpsWebCompiler().CompileAsync(tempPath, directory).GetAwaiter().GetResult();
            try { }
            finally { unit.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static void ValidateSpecificationPath(string specificationPath)
    {
        var extension = Path.GetExtension(specificationPath);
        if (!extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpenAPI specification files must use .yaml, .yml, or .json.");
    }

    private static void WriteHelp()
    {
        Console.WriteLine("""
Usage:
  xpscript openapi generate <spec.yaml|spec.yml|spec.json> [-o output.xps] [--force]
  xpscript openapi import <spec.yaml|spec.yml|spec.json> [-o output.xps]

`generate` creates a complete XPScript REST server source file. --force replaces an existing output file.
`import` is additive. Existing classes, properties, Functions, Subs, attributes and bodies are preserved. It only adds missing classes, missing class properties and missing generated procedures. Contract drift is reported as warnings rather than rewriting existing declarations. The merged source must compile before the destination is replaced.

Examples:
  xpscript openapi generate openapi.yaml
  xpscript openapi generate petstore.yaml -o ./generated/petstore.xps
  xpscript openapi import petstore.yaml -o ./generated/petstore.xps
  xpscript openapi generate api.json --force
""");
    }
}
