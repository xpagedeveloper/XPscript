using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

internal static class RunRoslynCompiler
{
    private const string SdkImplicitUsings = """
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
""";

    private static readonly Lazy<ImmutableArray<MetadataReference>> FrameworkReferences = new(CreateFrameworkReferences, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool CanCompile(string generatedSource, bool hasManagedReferences)
    {
        if (hasManagedReferences) return false;

        return !generatedSource.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) &&
               !generatedSource.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) &&
               !generatedSource.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal) &&
               !generatedSource.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal) &&
               !generatedSource.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal);
    }

    public static async Task<string> CompileAsync(
        string generatedSource,
        string outputDirectory,
        bool debug = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await CompileCoreAsync(generatedSource, outputDirectory, debug, cancellationToken).ConfigureAwait(false);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CompilerException("In-process Roslyn run compilation failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static async Task<string> CompileCoreAsync(
        string generatedSource,
        string outputDirectory,
        bool debug,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generatedSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        _ = debug;

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(outputRoot);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var implicitUsingsTree = CSharpSyntaxTree.ParseText(
            SdkImplicitUsings,
            parseOptions,
            path: "ImplicitUsings.g.cs",
            encoding: Encoding.UTF8,
            cancellationToken: cancellationToken);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            generatedSource,
            parseOptions,
            path: "Program.cs",
            encoding: Encoding.UTF8,
            cancellationToken: cancellationToken);

        var compilation = CSharpCompilation.Create(
            "Generated",
            [implicitUsingsTree, syntaxTree],
            FrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                mainTypeName: "Program",
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));

        var assemblyPath = Path.Combine(outputRoot, "Generated.dll");
        var pdbPath = Path.Combine(outputRoot, "Generated.pdb");
        await using var assemblyStream = new FileStream(assemblyPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var pdbStream = new FileStream(pdbPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var emit = compilation.Emit(
            assemblyStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb),
            cancellationToken: cancellationToken);
        await assemblyStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await pdbStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (!emit.Success)
        {
            try { File.Delete(assemblyPath); } catch { }
            try { File.Delete(pdbPath); } catch { }
            throw new CompilerException(BuildDiagnostics(emit.Diagnostics));
        }

        CompilerPathSecurity.HardenTemporaryFile(assemblyPath);
        CompilerPathSecurity.HardenTemporaryFile(pdbPath);
        await WriteRuntimeConfigAsync(outputRoot, cancellationToken).ConfigureAwait(false);
        return assemblyPath;
    }

    private static ImmutableArray<MetadataReference> CreateFrameworkReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trusted))
            throw new CompilerException("The current .NET runtime did not expose trusted platform assemblies for the run fast path.");

        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!seen.Add(path) || !File.Exists(path)) continue;
            builder.Add(MetadataReference.CreateFromFile(path));
        }

        if (builder.Count == 0)
            throw new CompilerException("No .NET framework metadata references were available for the run fast path.");
        return builder.ToImmutable();
    }

    private static string BuildDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length == 0) return "Generated code failed to compile.";

        var lines = new List<string>(errors.Length * 2);
        foreach (var diagnostic in errors)
        {
            var mapped = diagnostic.Location.GetMappedLineSpan();
            if (mapped.IsValid)
            {
                var path = string.IsNullOrWhiteSpace(mapped.Path) ? "Program.cs" : mapped.Path;
                lines.Add(FormatDiagnostic(path, mapped.StartLinePosition, diagnostic));
            }
            else
            {
                lines.Add($"Program.cs(0,0): error {diagnostic.Id}: {diagnostic.GetMessage()}");
            }

            if (diagnostic.Location.SourceTree is null)
                continue;

            var sourceText = diagnostic.Location.SourceTree.GetText();
            var physicalPosition = sourceText.Lines.GetLinePosition(diagnostic.Location.SourceSpan.Start);
            var physicalPath = string.IsNullOrWhiteSpace(diagnostic.Location.SourceTree.FilePath)
                ? "Program.cs"
                : Path.GetFileName(diagnostic.Location.SourceTree.FilePath);
            var duplicatesMapped = mapped.IsValid &&
                                   string.Equals(Path.GetFileName(mapped.Path), physicalPath, StringComparison.OrdinalIgnoreCase) &&
                                   mapped.StartLinePosition.Equals(physicalPosition);
            if (!duplicatesMapped)
                lines.Add(FormatGeneratedDiagnostic(physicalPath, physicalPosition, diagnostic));
        }
        return "Generated code failed to compile." + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string FormatDiagnostic(string path, LinePosition position, Diagnostic diagnostic) =>
        $"{path}({position.Line + 1},{position.Character + 1}): error {diagnostic.Id}: {diagnostic.GetMessage()}";

    private static string FormatGeneratedDiagnostic(string path, LinePosition position, Diagnostic diagnostic)
    {
        var description = diagnostic.GetMessage().Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("|", "/", StringComparison.Ordinal);
        return $"XPSCRIPT-GENERATED-DIAGNOSTIC|{path}|{position.Line + 1}|{position.Character + 1}|{diagnostic.Id}|{description}";
    }

    private static async Task WriteRuntimeConfigAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var runtimeConfigPath = Path.Combine(outputRoot, "Generated.runtimeconfig.json");
        var frameworkVersion = $"{Environment.Version.Major}.{Environment.Version.Minor}.0";
        var payload = JsonSerializer.Serialize(new
        {
            runtimeOptions = new
            {
                tfm = $"net{Environment.Version.Major}.{Environment.Version.Minor}",
                framework = new { name = "Microsoft.NETCore.App", version = frameworkVersion },
                rollForward = "LatestPatch"
            }
        });
        await File.WriteAllTextAsync(runtimeConfigPath, payload, cancellationToken).ConfigureAwait(false);
        CompilerPathSecurity.HardenTemporaryFile(runtimeConfigPath);
    }
}
