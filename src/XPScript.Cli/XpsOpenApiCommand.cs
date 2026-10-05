using XPScript.Web.Compiler;
using System.Text.Json;

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

        if (args[0].Equals("client", StringComparison.OrdinalIgnoreCase)) return RunClient(args[1..]);

        var command = args[0].ToLowerInvariant();
        if (command is not ("generate" or "import"))
            throw new ArgumentException("openapi supports 'generate', 'import', and 'client'.");
        if (args.Length < 2)
            throw new ArgumentException($"openapi {command} requires an OpenAPI .yaml, .yml, or .json specification file.");

        var specificationPath = Path.GetFullPath(args[1]);
        string? outputPath = null;
        string? statusPath = null;
        var includedOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                case "--operation" when command == "import":
                    if (++i >= args.Length) throw new ArgumentException("--operation requires an operationId, path, or METHOD path.");
                    foreach (var item in args[i].Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) includedOperations.Add(item);
                    break;
                case "--status-file" when command == "import":
                    if (++i >= args.Length) throw new ArgumentException("--status-file requires a JSON path.");
                    statusPath = Path.GetFullPath(args[i]);
                    break;
                default:
                    throw new ArgumentException($"Unknown openapi {command} argument: " + args[i]);
            }
        }

        ValidateSpecificationPath(specificationPath);
        outputPath ??= DefaultOutput(specificationPath);
        ValidateOutputPath(outputPath);

        return command == "generate"
            ? Generate(specificationPath, outputPath, force)
            : Import(specificationPath, outputPath, statusPath, includedOperations);
    }

    private static int RunClient(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { WriteHelp(); return 0; }
        var command = args[0].ToLowerInvariant();
        if (command != "generate") throw new ArgumentException("openapi client supports 'generate'.");
        if (args.Length < 2) throw new ArgumentException($"openapi client {command} requires an OpenAPI specification file.");

        var specificationPath = Path.GetFullPath(args[1]);
        string? outputPath = null;
        string? className = null;
        var includedOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                case "--output":
                    if (++i >= args.Length) throw new ArgumentException(args[i - 1] + " requires an output .xps path.");
                    outputPath = Path.GetFullPath(args[i]);
                    break;
                case "--class":
                    if (++i >= args.Length) throw new ArgumentException("--class requires an XPScript class name.");
                    className = args[i];
                    break;
                case "--operation":
                    if (++i >= args.Length) throw new ArgumentException("--operation requires an operationId, path, or METHOD path.");
                    foreach (var item in args[i].Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) includedOperations.Add(item);
                    break;
                default:
                    throw new ArgumentException($"Unknown openapi client {command} argument: " + args[i]);
            }
        }

        ValidateSpecificationPath(specificationPath);
        outputPath ??= Path.Combine(Path.GetDirectoryName(specificationPath) ?? Environment.CurrentDirectory,
            Path.GetFileNameWithoutExtension(specificationPath) + ".client.xps");
        ValidateOutputPath(outputPath);

        if (File.Exists(outputPath) && !ConfirmOverwrite(outputPath))
        {
            Console.WriteLine("Skipped existing output: " + outputPath);
            return 0;
        }

        var result = new XpsOpenApiClientGenerator().GenerateFile(specificationPath, className, new XpsOpenApiGenerationOptions(includedOperations));
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, result.Source);
        Console.WriteLine($"Generated API consumer {outputPath}");
        Console.WriteLine($"OpenAPI {result.OpenApiVersion}: {result.ClassName}, {result.Operations.Count} operation(s), {result.Models.Count} model(s)");
        return 0;
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

    private static int Import(string specificationPath, string outputPath, string? statusPath, IReadOnlySet<string> includedOperations)
    {
        WriteStatus(statusPath, "reading", 0, 0, 0, null);
        if (!File.Exists(outputPath))
        {
            WriteStatus(statusPath, "generating", 10, 0, 0, null);
            var generated = new XpsOpenApiGenerator().GenerateFile(specificationPath, new XpsOpenApiGenerationOptions(includedOperations));
            WriteStatus(statusPath, "generated", 55, generated.Operations.Count, generated.Models.Count, null);
            ValidateAndReplace(outputPath, generated.Source, statusPath, generated.Operations.Count, generated.Models.Count);
            WriteStatus(statusPath, "completed", 100, generated.Operations.Count, generated.Models.Count, outputPath);
            Console.WriteLine($"Imported {outputPath}");
            Console.WriteLine($"OpenAPI {generated.OpenApiVersion}: created new file with {generated.Operations.Count} endpoint(s), {generated.Models.Count} model(s)");
            return 0;
        }
        var existing = File.ReadAllText(outputPath);
        WriteStatus(statusPath, "importing", 10, 0, 0, null);
        var result = new XpsOpenApiImporter().ImportFile(specificationPath, existing, new XpsOpenApiGenerationOptions(includedOperations));
        if (result.Changed) ValidateAndReplace(outputPath, result.Source, statusPath, result.AddedProcedures.Count, result.AddedClasses.Count);
        WriteStatus(statusPath, "completed", 100, result.AddedProcedures.Count, result.AddedClasses.Count, outputPath);
        Console.WriteLine($"OpenAPI {result.OpenApiVersion} additive import: {(result.Changed ? "updated" : "no additions")}");
        Console.WriteLine($"Added: {result.AddedClasses.Count} class(es), {result.AddedProperties.Count} class property/properties, {result.AddedProcedures.Count} procedure(s)");
        foreach (var item in result.AddedClasses) Console.WriteLine("  + class " + item);
        foreach (var item in result.AddedProperties) Console.WriteLine("  + property " + item);
        foreach (var item in result.AddedProcedures) Console.WriteLine("  + " + item);
        foreach (var warning in result.Warnings) Console.Error.WriteLine("warning: " + warning);
        Console.WriteLine("Existing declarations were preserved.");
        return 0;
    }

    private static void ValidateAndReplace(string outputPath, string source, string? statusPath = null, int operations = 0, int models = 0)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory)) directory = Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, "." + Path.GetFileName(outputPath) + ".openapi-import-" + Guid.NewGuid().ToString("N") + ".xps");
        try
        {
            File.WriteAllText(tempPath, source);
            WriteStatus(statusPath, "compiling", 70, operations, models, null);
            var unit = new XpsWebCompiler().CompileAsync(tempPath, directory).GetAwaiter().GetResult();
            try { }
            finally { unit.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
    }

    private static void WriteStatus(string? path, string phase, int progress, int operations, int models, string? output)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var payload = new { phase, progress, operations, models, output, updatedUtc = DateTimeOffset.UtcNow };
        File.WriteAllText(path, JsonSerializer.Serialize(payload) + Environment.NewLine);
    }

    private static bool ConfirmOverwrite(string outputPath)
    {
        Console.Write($"Output already exists: {outputPath}. Overwrite? [y/N] ");
        var answer = Console.ReadLine();
        return string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string DefaultOutput(string specificationPath) => Path.Combine(
        Path.GetDirectoryName(specificationPath) ?? Environment.CurrentDirectory,
        Path.GetFileNameWithoutExtension(specificationPath) + ".xps");

    private static void ValidateOutputPath(string outputPath)
    {
        if (!Path.GetExtension(outputPath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpenAPI generated output must use the .xps extension.");
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
  xpscript openapi import <spec.yaml|spec.yml|spec.json> [-o output.xps] [--operation operationId|path] [--status-file status.json]
  xpscript openapi client generate <spec.yaml|spec.yml|spec.json> [-o output.xps] [--class ApiClass]

`generate` and `import` retain their existing REST server behavior.
`client generate` creates an XPHttpClient-based API consumer.

Examples:
  xpscript openapi generate petstore.yaml -o ./generated/petstore.xps
  xpscript openapi import petstore.yaml -o ./generated/petstore.xps
  xpscript openapi client generate petstore.yaml -o ./generated/petstore.client.xps --class PetStoreApi
""");
    }
}
