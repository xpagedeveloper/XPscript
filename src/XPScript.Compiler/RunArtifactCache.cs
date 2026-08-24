using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler;

internal sealed class RunArtifactCache
{
    private static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(14);
    private const int MaxEntries = 128;
    private const int MaxGenerationsPerEntry = 4;

    private readonly string _entryRoot;
    private readonly string _generationRoot;
    private readonly string _readyPath;

    private RunArtifactCache(bool enabled, string entryRoot, string generationRoot)
    {
        Enabled = enabled;
        _entryRoot = entryRoot;
        _generationRoot = generationRoot;
        OutputDirectory = enabled ? Path.Combine(generationRoot, "out") : string.Empty;
        _readyPath = enabled ? Path.Combine(entryRoot, "ready.txt") : string.Empty;
    }

    public bool Enabled { get; }
    public string OutputDirectory { get; }

    public static async Task<RunArtifactCache> CreateAsync(
        string sourcePath,
        string runtimeIdentifier,
        IReadOnlyList<string> sourcePreprocessors)
    {
        if (sourcePreprocessors.Count > 0)
            return new RunArtifactCache(false, string.Empty, string.Empty);

        var snapshot = await XPScriptCompilationSnapshotBuilder.CreateForRunAsync(
            sourcePath,
            runtimeIdentifier,
            configurationIdentity: "run-artifact-v2").ConfigureAwait(false);
        var identity = string.Join("\0",
            Path.GetFullPath(sourcePath),
            snapshot.Identity,
            typeof(CompilerDriver).Assembly.ManifestModule.ModuleVersionId.ToString("N"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();

        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot))
            localRoot = Path.GetTempPath();

        var cacheRoot = Path.Combine(localRoot, "XPScript", "run-cache");
        Directory.CreateDirectory(cacheRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(cacheRoot);
        Prune(cacheRoot);

        var entryRoot = Path.Combine(cacheRoot, hash[..32]);
        Directory.CreateDirectory(entryRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(entryRoot);
        var generationRoot = Path.Combine(entryRoot, "build-" + Guid.NewGuid().ToString("N"));
        TryTouch(entryRoot);
        return new RunArtifactCache(true, entryRoot, generationRoot);
    }

    public bool TryGetRunnable(out string executablePath)
    {
        executablePath = string.Empty;
        if (!Enabled || !File.Exists(_readyPath)) return false;

        string relative;
        try { relative = File.ReadAllText(_readyPath).Trim(); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

        if (relative.Length == 0 || Path.IsPathRooted(relative)) return false;
        var candidate = Path.GetFullPath(Path.Combine(_entryRoot, relative));
        if (!IsWithin(_entryRoot, candidate) || !File.Exists(candidate)) return false;
        var managedAssembly = Path.ChangeExtension(candidate, ".dll");
        if (!File.Exists(managedAssembly)) return false;
        TryTouch(_entryRoot);
        executablePath = candidate;
        return true;
    }

    public void PrepareOutputDirectory()
    {
        if (!Enabled) return;
        Directory.CreateDirectory(_generationRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(_generationRoot);
        Directory.CreateDirectory(OutputDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(OutputDirectory);
        TryTouch(_entryRoot);
    }

    public void MarkReady(string executablePath)
    {
        if (!Enabled) return;
        var fullExecutable = Path.GetFullPath(executablePath);
        if (!IsWithin(OutputDirectory, fullExecutable))
            throw new InvalidOperationException("Run cache executable resolved outside the cache output directory.");

        var relative = Path.GetRelativePath(_entryRoot, fullExecutable);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Run cache executable resolved outside the cache entry directory.");

        var tempReady = _readyPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tempReady, relative);
            CompilerPathSecurity.HardenTemporaryFile(tempReady);
            File.Move(tempReady, _readyPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tempReady)) File.Delete(tempReady); } catch { }
        }

        TryTouch(_entryRoot);
        PruneGenerations(_entryRoot, _generationRoot);
    }

    public void Invalidate()
    {
        if (!Enabled) return;
        try
        {
            if (!File.Exists(_readyPath)) return;
            var relative = File.ReadAllText(_readyPath).Trim();
            var current = Path.GetFullPath(Path.Combine(_entryRoot, relative));
            if (IsWithin(_generationRoot, current)) File.Delete(_readyPath);
        }
        catch { }
    }

    private static bool IsWithin(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullCandidate = Path.GetFullPath(candidate);
        return fullCandidate.Equals(fullRoot, comparison) ||
               fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static void PruneGenerations(string entryRoot, string keepGeneration)
    {
        try
        {
            var generations = Directory.EnumerateDirectories(entryRoot, "build-*", SearchOption.TopDirectoryOnly)
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .ToArray();
            var keep = Path.GetFullPath(keepGeneration);
            foreach (var generation in generations.Skip(MaxGenerationsPerEntry))
            {
                if (Path.GetFullPath(generation.FullName).Equals(keep, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    continue;
                try { generation.Delete(recursive: true); } catch { }
            }
        }
        catch { }
    }

    private static void Prune(string cacheRoot)
    {
        try
        {
            var cutoff = DateTime.UtcNow - MaxCacheAge;
            var entries = Directory.EnumerateDirectories(cacheRoot)
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .ToArray();

            foreach (var entry in entries.Where(info => info.LastWriteTimeUtc < cutoff).Concat(entries.Skip(MaxEntries)).DistinctBy(info => info.FullName))
            {
                try { entry.Delete(recursive: true); } catch { }
            }
        }
        catch { }
    }

    private static void TryTouch(string path)
    {
        try { Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow); } catch { }
    }
}
