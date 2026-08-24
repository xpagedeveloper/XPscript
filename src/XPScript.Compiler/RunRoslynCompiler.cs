using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

internal static class RunRoslynCompiler
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> FrameworkReferences = new(CreateFrameworkReferences, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool CanCompile(
        string generatedSource,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.ManagedReference> managedReferences)
    {
        if (managedReferences.Count > 0) return false;

        return !generatedSource.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) &&
               !generatedSource.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) &&
               !generatedSource.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal) &&
               !generatedSource.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal) &&
               !generatedSource.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal);
    }

    public static async Task<string> CompileAsync(
        string generatedSource,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generatedSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(outputRoot);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            generatedSource,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: "Program.cs",
            encoding: Encoding.UTF8,
            cancellationToken: cancellationToken);

        var compilation = CSharpCompilation.Create(
            "Generated",
            [syntaxTree],
            FrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));

        var assemblyPath = Path.Combine(outputRoot, "Generated.dll");
        var pdbPath = Path.Combine(outputRoot, "Generated.pdb");
        await using var assemblyStream = new FileStream(assemblyPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var pdbStream = new FileStream(pdbPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var emit = compilation.Emit(assemblyStream, pdbStream, cancellationToken: cancellationToken);
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
        return builder.MoveToImmutable();
    }

    private static string BuildDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length == 0) return "Generated code failed to compile.";

        var lines = new List<string>(errors.Length);
        foreach (var diagnostic in errors)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            if (span.IsValid)
            {
                var path = string.IsNullOrWhiteSpace(span.Path) ? "Program.cs" : span.Path;
                lines.Add($"{path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): error {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
            else
            {
                lines.Add($"Program.cs(0,0): error {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
        }
        return "Generated code failed to compile." + Environment.NewLine + string.Join(Environment.NewLine, lines);
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
