using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

/// <summary>Opt-in, dependency-aware reuse of a completed AST output directory.</summary>
public static class PersistentAstCompilationCache
{
    private const string ManifestName = ".xpscript-ast-cache.json";

    public static bool Enabled => string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_AST_PERSISTENT_CACHE"), "1", StringComparison.Ordinal);
    public static long Hits => Interlocked.Read(ref _hits);
    public static long Misses => Interlocked.Read(ref _misses);
    private static long _hits;
    private static long _misses;

    internal static string? TryGet(string outputDirectory, string sourcePath, string source, IncludeSourcePreprocessor.Result includes,
        string? runtimeIdentifier, ApplicationSecurityMode securityMode, bool restricted, IReadOnlyList<string>? sourceRoots,
        IReadOnlyList<string>? sourcePreprocessors, bool embedAssets, bool emitSourceOnly, bool hasGeneratedTransform)
    {
        if (!Enabled) return null;
        Interlocked.Increment(ref _misses);
        var manifestPath = Path.Combine(outputDirectory, ManifestName);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath));
            if (manifest is null || !string.Equals(manifest.Key, CreateKey(sourcePath, source, includes, runtimeIdentifier, securityMode, restricted, sourceRoots, sourcePreprocessors, embedAssets, emitSourceOnly, hasGeneratedTransform), StringComparison.Ordinal)) return null;
            var artifact = manifest.Artifact;
            if (!File.Exists(artifact)) return null;
            Interlocked.Increment(ref _hits);
            return artifact;
        }
        catch { return null; }
    }

    public static void ClearCounters()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    internal static void Store(string outputDirectory, string sourcePath, string source, IncludeSourcePreprocessor.Result includes,
        string? runtimeIdentifier, ApplicationSecurityMode securityMode, bool restricted, IReadOnlyList<string>? sourceRoots,
        IReadOnlyList<string>? sourcePreprocessors, bool embedAssets, bool emitSourceOnly, bool hasGeneratedTransform, string artifact)
    {
        if (!Enabled) return;
        Directory.CreateDirectory(outputDirectory);
        var manifest = new Manifest
        {
            Key = CreateKey(sourcePath, source, includes, runtimeIdentifier, securityMode, restricted, sourceRoots, sourcePreprocessors, embedAssets, emitSourceOnly, hasGeneratedTransform),
            Artifact = Path.GetFullPath(artifact),
            Dependencies = includes.Dependencies.Select(Path.GetFullPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            CreatedUtc = DateTimeOffset.UtcNow
        };
        File.WriteAllText(Path.Combine(outputDirectory, ManifestName), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateKey(string sourcePath, string source, IncludeSourcePreprocessor.Result includes,
        string? runtimeIdentifier, ApplicationSecurityMode securityMode, bool restricted, IReadOnlyList<string>? sourceRoots,
        IReadOnlyList<string>? sourcePreprocessors, bool embedAssets, bool emitSourceOnly, bool hasGeneratedTransform)
    {
        var builder = new StringBuilder().Append("ast-cache-v2\n").Append(Path.GetFullPath(sourcePath)).Append('\n')
            .Append(runtimeIdentifier).Append('\n').Append(securityMode).Append('\n').Append(restricted).Append('\n').Append(embedAssets).Append('\n').Append(emitSourceOnly).Append('\n').Append(hasGeneratedTransform).Append('\n');
        foreach (var value in sourceRoots ?? []) builder.Append("root:").Append(Path.GetFullPath(value)).Append('\n');
        foreach (var value in sourcePreprocessors ?? []) builder.Append("pre:").Append(value).Append('\n');
        foreach (var dependency in includes.Dependencies.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(dependency);
            builder.Append(full).Append('\n').Append(File.Exists(full) ? File.ReadAllText(full) : "<missing>").Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private sealed class Manifest
    {
        public string Key { get; set; } = string.Empty;
        public string Artifact { get; set; } = string.Empty;
        public string[] Dependencies { get; set; } = [];
        public DateTimeOffset CreatedUtc { get; set; }
    }
}
