using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler;

internal static class UIFormAssetCompileContext
{
    private static readonly AsyncLocal<bool?> EmbedValue = new();

    public static bool EmbedAssets => EmbedValue.Value == true;

    public static IDisposable Push(bool embedAssets)
    {
        var previous = EmbedValue.Value;
        EmbedValue.Value = embedAssets;
        return new Scope(previous);
    }

    private sealed class Scope(bool? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EmbedValue.Value = previous;
        }
    }
}

public static class UIFormAppAssets
{
    public const string DirectoryName = "assets";
    private const long MaximumEmbeddedBytes = 64L * 1024 * 1024;

    public static bool UsesUIForm(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return false;
        var source = File.ReadAllText(sourcePath);
        return source.Contains("UIForm", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("AddImage", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("AddWebView", StringComparison.OrdinalIgnoreCase);
    }

    public static string EnsureAssetsDirectory(string sourcePath)
    {
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory);
        var assets = Path.Combine(sourceDirectory, DirectoryName);
        if (File.Exists(assets))
            throw new CompilerException("UIForm assets path identifies a file instead of a directory: assets");
        Directory.CreateDirectory(assets);
        RejectLinkedDirectory(assets, "UIForm assets directory");
        return assets;
    }

    public static string ComputeFingerprint(string sourcePath)
    {
        if (!UsesUIForm(sourcePath)) return string.Empty;
        var root = EnsureAssetsDirectory(sourcePath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in EnumerateAssetFiles(root))
        {
            var name = Encoding.UTF8.GetBytes(file.RelativePath);
            hash.AppendData(name);
            hash.AppendData([0]);
            using var stream = File.OpenRead(file.FullPath);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) hash.AppendData(buffer, 0, read);
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static void PublishExternalAssets(string sourcePath, string outputPath)
    {
        var sourceAssets = EnsureAssetsDirectory(sourcePath);
        var outputDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory);
        var destination = Path.Combine(outputDirectory, DirectoryName);
        if (PathsEqual(sourceAssets, destination)) return;
        CopyAssetTree(sourceAssets, destination);
    }

    public static void CopyAssetsToDirectory(string sourcePath, string destinationRoot)
    {
        var sourceAssets = EnsureAssetsDirectory(sourcePath);
        var destination = Path.Combine(Path.GetFullPath(destinationRoot), DirectoryName);
        if (PathsEqual(sourceAssets, destination)) return;
        CopyAssetTree(sourceAssets, destination);
    }

    public static string InstallEmbeddedAssets(string generated, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!UIFormAssetCompileContext.EmbedAssets) return generated;
        if (generated.Contains("internal static class XPScriptEmbeddedAppAssets", StringComparison.Ordinal)) return generated;

        var assetsRoot = EnsureAssetsDirectory(sourcePath);
        var files = EnumerateAssetFiles(assetsRoot);
        long total = 0;
        var entries = new StringBuilder();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file.FullPath);
            total += bytes.LongLength;
            if (total > MaximumEmbeddedBytes)
                throw new CompilerException("Embedded UIForm assets exceed the 64 MiB compile limit.");
            entries.Append("        WriteAsset(root, \"")
                .Append(EscapeCSharp(file.RelativePath))
                .Append("\", \"")
                .Append(Convert.ToBase64String(bytes))
                .AppendLine("\");");
        }

        return generated + "\n" + $$"""
internal static class XPScriptEmbeddedAppAssets
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Materialize()
    {
        var root = System.IO.Path.Combine(System.AppContext.BaseDirectory, "assets");
        System.IO.Directory.CreateDirectory(root);
{{entries}}    }

    private static void WriteAsset(string root, string relativePath, string base64)
    {
        var normalized = relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
        var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, normalized));
        var rootPrefix = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, System.OperatingSystem.IsWindows() ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal))
            throw new System.InvalidOperationException("Embedded application asset escaped the assets directory.");
        var directory = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(target, System.Convert.FromBase64String(base64));
    }
}
""";
    }

    private static IReadOnlyList<(string FullPath, string RelativePath)> EnumerateAssetFiles(string root)
    {
        RejectLinkedDirectory(root, "UIForm assets directory");
        var result = new List<(string FullPath, string RelativePath)>();
        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            var directory = new DirectoryInfo(path);
            if (info.LinkTarget is not null || directory.LinkTarget is not null ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new CompilerException("UIForm assets may not contain symbolic links or reparse points.");
            if (Directory.Exists(path)) continue;
            if (!File.Exists(path)) continue;
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.Equals("..", StringComparison.Ordinal) || relative.StartsWith("../", StringComparison.Ordinal))
                throw new CompilerException("UIForm asset path escapes the assets directory.");
            result.Add((Path.GetFullPath(path), relative));
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return result;
    }

    private static void CopyAssetTree(string sourceRoot, string destinationRoot)
    {
        RejectLinkedDirectory(sourceRoot, "UIForm assets directory");
        if (File.Exists(destinationRoot))
            throw new CompilerException("UIForm asset publication target identifies a file: assets");
        Directory.CreateDirectory(destinationRoot);
        RejectLinkedDirectory(destinationRoot, "UIForm asset publication directory");

        foreach (var file in EnumerateAssetFiles(sourceRoot))
        {
            var target = Path.GetFullPath(Path.Combine(destinationRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(rootPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new CompilerException("UIForm asset publication path escapes the assets directory.");
            var directory = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(directory);
            RejectLinkedDirectory(directory, "UIForm asset publication directory");
            File.Copy(file.FullPath, target, overwrite: true);
        }
    }

    private static void RejectLinkedDirectory(string path, string label)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (info.LinkTarget is not null || (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new CompilerException(label + " may not be a symbolic link or reparse point.");
        }
        catch (CompilerException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CompilerException("Unable to inspect " + label.ToLowerInvariant() + ".");
        }
    }

    private static bool PathsEqual(string left, string right) =>
        Path.GetFullPath(left).Equals(Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
