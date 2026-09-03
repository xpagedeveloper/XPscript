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

        if (!args[0].Equals("generate", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("openapi supports the 'generate' command.");
        if (args.Length < 2)
            throw new ArgumentException("openapi generate requires an OpenAPI .yaml, .yml, or .json specification file.");

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
                case "--force":
                    force = true;
                    break;
                default:
                    throw new ArgumentException("Unknown openapi generate argument: " + args[i]);
            }
        }

        var extension = Path.GetExtension(specificationPath);
        if (!extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpenAPI specification files must use .yaml, .yml, or .json.");

        outputPath ??= Path.Combine(
            Path.GetDirectoryName(specificationPath) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(specificationPath) + ".xps");

        if (!Path.GetExtension(outputPath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpenAPI generated output must use the .xps extension.");
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

    private static void WriteHelp()
    {
        Console.WriteLine("""
Usage:
  xpscript openapi generate <spec.yaml|spec.yml|spec.json> [-o output.xps] [--force]

Generates XPScript REST server source from an OpenAPI 3.0.x or 3.1.x specification.
The default output path replaces the specification extension with .xps.

Examples:
  xpscript openapi generate openapi.yaml
  xpscript openapi generate petstore.yaml -o ./generated/petstore.xps
  xpscript openapi generate api.json --force
""");
    }
}
