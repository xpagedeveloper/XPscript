using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler;

public sealed record XPScriptCompilationDependency(string Path, string Sha256);

public sealed record XPScriptCompilationSnapshot(
    string Identity,
    string CompilerIdentity,
    string RuntimeIdentifier,
    string ConfigurationIdentity,
    IReadOnlyList<XPScriptCompilationDependency> Dependencies);

public static class XPScriptCompilationSnapshotBuilder
{
    public static Task<XPScriptCompilationSnapshot> CreateAsync(
        string sourcePath,
        string allowedSourceRoot,
        string runtimeIdentifier,
        string configurationIdentity = "default",
        long maxDependencyBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedSourceRoot);
        return CreateCoreAsync(
            sourcePath,
            runtimeIdentifier,
            configurationIdentity,
            maxDependencyBytes,
            [Path.GetFullPath(allowedSourceRoot)],
            cancellationToken);
    }

    internal static Task<XPScriptCompilationSnapshot> CreateForRunAsync(
        string sourcePath,
        string runtimeIdentifier,
        string configurationIdentity = "run",
        long maxDependencyBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
        => CreateCoreAsync(
            sourcePath,
            runtimeIdentifier,
            configurationIdentity,
            maxDependencyBytes,
            includeRoots: null,
            cancellationToken);

    private static async Task<XPScriptCompilationSnapshot> CreateCoreAsync(
        string sourcePath,
        string runtimeIdentifier,
        string configurationIdentity,
        long maxDependencyBytes,
        IReadOnlyList<string>? includeRoots,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(configurationIdentity);
        if (maxDependencyBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxDependencyBytes));

        var fullSourcePath = Path.GetFullPath(sourcePath);
        EnsureBoundedFile(fullSourcePath, maxDependencyBytes);
        var rootSource = await File.ReadAllTextAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);

        IncludeSourcePreprocessor.Result expanded;
        if (includeRoots is null)
        {
            expanded = new IncludeSourcePreprocessor().Transform(rootSource, fullSourcePath);
        }
        else
        {
            using var includeScope = IncludeSecurityContext.Push(includeRoots);
            expanded = new IncludeSourcePreprocessor().Transform(rootSource, fullSourcePath);
        }

        var managedReferences = new ManagedAssemblyReferencePreprocessor(runtimeIdentifier)
            .Transform(expanded.Source, expanded.Map, fullSourcePath);
        var nativeDependencies = new NativeDependencyPackager(runtimeIdentifier)
            .Collect(managedReferences.Source, expanded.Map, fullSourcePath);

        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(fullSourcePath) ?? Environment.CurrentDirectory);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var dependencyPaths = new HashSet<string>(expanded.Dependencies.Select(Path.GetFullPath), comparer);

        foreach (var reference in managedReferences.Managed)
            dependencyPaths.Add(CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, reference.DeclaredPath, "Managed Reference"));
        foreach (var reference in managedReferences.Native)
            dependencyPaths.Add(CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, reference.DeclaredPath, "ReferenceNative"));
        foreach (var dependency in nativeDependencies)
            dependencyPaths.Add(CompilerPathSecurity.ResolveApplicationLocalNativeFile(sourceDirectory, dependency.DeclaredPath));

        string applicationSource;
        using (ExpandedSourceContext.Begin(managedReferences.Source, fullSourcePath, expanded.Map))
            applicationSource = new ApplicationObjectPreprocessor().Transform(managedReferences.Source);
        var iconPath = TryReadApplicationIconPath(applicationSource);
        if (iconPath is not null)
            dependencyPaths.Add(iconPath);

        var orderedPaths = dependencyPaths.OrderBy(path => path, comparer).ToArray();
        var dependencyTasks = orderedPaths
            .Select(path => HashDependencyAsync(path, maxDependencyBytes, cancellationToken))
            .ToArray();
        var dependencies = await Task.WhenAll(dependencyTasks).ConfigureAwait(false);

        var assembly = typeof(XPScriptTranspiler).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var compilerIdentity = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informationalVersion;

        var identityText = new StringBuilder()
            .Append("compiler=").AppendLine(compilerIdentity)
            .Append("runtime=").AppendLine(runtimeIdentifier)
            .Append("config=").AppendLine(configurationIdentity);
        foreach (var dependency in dependencies)
            identityText.Append(dependency.Path).Append('=').AppendLine(dependency.Sha256);

        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText.ToString())));
        return new XPScriptCompilationSnapshot(identity, compilerIdentity, runtimeIdentifier, configurationIdentity, dependencies);
    }

    private static async Task<XPScriptCompilationDependency> HashDependencyAsync(
        string path,
        long maxDependencyBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        EnsureBoundedFile(fullPath, maxDependencyBytes);
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return new XPScriptCompilationDependency(fullPath, Convert.ToHexString(hash));
    }

    private static string? TryReadApplicationIconPath(string source)
    {
        var markerIndex = source.IndexOf(ApplicationObjectPreprocessor.BuildIconMarker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;
        var valueStart = markerIndex + ApplicationObjectPreprocessor.BuildIconMarker.Length;
        var valueEnd = source.IndexOfAny(['\r', '\n'], valueStart);
        var value = (valueEnd < 0 ? source[valueStart..] : source[valueStart..valueEnd]).Trim();
        value = value.TrimEnd('"', '\'', '/', '*', ' ', ';', ')');
        return value.Length == 0 ? null : Path.GetFullPath(value);
    }

    private static void EnsureBoundedFile(string path, long maxDependencyBytes)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("XPScript source dependency was not found.", path);
        if (info.Length > maxDependencyBytes)
            throw new CompilerException($"XPScript source dependency exceeds the configured {maxDependencyBytes} byte limit: {Path.GetFileName(path)}");
    }
}
