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
    public static async Task<XPScriptCompilationSnapshot> CreateAsync(
        string sourcePath,
        string allowedSourceRoot,
        string runtimeIdentifier,
        string configurationIdentity = "default",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedSourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(configurationIdentity);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullRoot = Path.GetFullPath(allowedSourceRoot);
        var rootSource = await File.ReadAllTextAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> dependencyPaths;
        using (IncludeSecurityContext.Push([fullRoot]))
        {
            var expanded = new IncludeSourcePreprocessor().Transform(rootSource, fullSourcePath);
            dependencyPaths = expanded.Dependencies;
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var dependencies = new List<XPScriptCompilationDependency>();
        foreach (var path in dependencyPaths.Distinct(comparer).OrderBy(path => path, comparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            dependencies.Add(new XPScriptCompilationDependency(Path.GetFullPath(path), Convert.ToHexString(hash)));
        }

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
}
