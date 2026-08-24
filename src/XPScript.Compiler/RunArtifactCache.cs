using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class RunArtifactCache
{
    private static readonly Regex ExternalInputPattern = new(
        @"(?im)^\s*(?:Include\b|Declare\b|Reference(?:Native)?\b)|\bApplication\.Icon\b",
        RegexOptions.CultureInvariant);

    private readonly string _entryRoot;
    private readonly string _readyPath;

    private RunArtifactCache(bool enabled, string entryRoot)
    {
        Enabled = enabled;
        _entryRoot = entryRoot;
        OutputDirectory = enabled ? Path.Combine(entryRoot, "out") : string.Empty;
        _readyPath = enabled ? Path.Combine(entryRoot, "ready.txt") : string.Empty;
    }

    public bool Enabled { get; }
    public string OutputDirectory { get; }

    public static async Task<RunArtifactCache> CreateAsync(
        string sourcePath,
        string runtimeIdentifier,
        IReadOnlyList<string> sourcePreprocessors)
    {
        var source = await File.ReadAllTextAsync(sourcePath).ConfigureAwait(false);
        if (sourcePreprocessors.Count > 0 || ExternalInputPattern.IsMatch(source))
            return new RunArtifactCache(false, string.Empty);

        var identity = string.Join("\0",
            Path.GetFullPath(sourcePath),
            runtimeIdentifier,
            typeof(CompilerDriver).Assembly.ManifestModule.ModuleVersionId.ToString("N"),
            source);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();

        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot))
            localRoot = Path.GetTempPath();

        var cacheRoot = Path.Combine(localRoot, "XPScript", "run-cache");
        var entryRoot = Path.Combine(cacheRoot, hash[..32]);
        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(entryRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(cacheRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(entryRoot);
        return new RunArtifactCache(true, entryRoot);
    }

    public bool TryGetRunnable(out string executablePath)
    {
        executablePath = string.Empty;
        if (!Enabled || !File.Exists(_readyPath)) return false;

        string fileName;
        try { fileName = File.ReadAllText(_readyPath).Trim(); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName)) return false;
        var candidate = Path.Combine(OutputDirectory, fileName);
        if (!File.Exists(candidate)) return false;
        executablePath = candidate;
        return true;
    }

    public void PrepareOutputDirectory()
    {
        if (!Enabled) return;
        try
        {
            if (Directory.Exists(OutputDirectory)) Directory.Delete(OutputDirectory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Directory.CreateDirectory(OutputDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(OutputDirectory);
        try { if (File.Exists(_readyPath)) File.Delete(_readyPath); } catch { }
    }

    public void MarkReady(string executablePath)
    {
        if (!Enabled) return;
        var fullOutput = Path.GetFullPath(OutputDirectory);
        var fullExecutable = Path.GetFullPath(executablePath);
        var relative = Path.GetRelativePath(fullOutput, fullExecutable);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Run cache executable resolved outside the cache output directory.");
        var fileName = Path.GetFileName(fullExecutable);
        File.WriteAllText(_readyPath, fileName);
        CompilerPathSecurity.HardenTemporaryFile(_readyPath);
    }

    public void Invalidate()
    {
        if (!Enabled) return;
        try { if (File.Exists(_readyPath)) File.Delete(_readyPath); } catch { }
    }
}
