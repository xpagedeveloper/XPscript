namespace XPScript.Compiler;

internal sealed class FileSystemPathIdentity
{
    private readonly Dictionary<string, bool> _caseSensitivity = new(StringComparer.Ordinal);

    public string ComparisonKey(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return IsCaseSensitive(fullPath) ? fullPath : fullPath.ToUpperInvariant();
    }

    private bool IsCaseSensitive(string path)
    {
        var directory = ExistingProbeDirectory(path);
        if (_caseSensitivity.TryGetValue(directory, out var cached))
            return cached;

        var detected = DetectCaseSensitivity(path, directory);
        _caseSensitivity[directory] = detected;
        return detected;
    }

    private static string ExistingProbeDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var current = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
            current = Path.GetDirectoryName(current);

        return Path.GetFullPath(current ?? Path.GetPathRoot(fullPath) ?? Environment.CurrentDirectory);
    }

    private static bool DetectCaseSensitivity(string originalPath, string probeDirectory)
    {
        var fullOriginal = Path.GetFullPath(originalPath);
        if ((File.Exists(fullOriginal) || Directory.Exists(fullOriginal)) &&
            TryProbeExistingEntry(fullOriginal, out var result))
            return result;

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(probeDirectory))
            {
                if (TryProbeExistingEntry(entry, out result))
                    return result;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to the conservative platform default when the directory cannot be enumerated.
        }

        return !OperatingSystem.IsWindows();
    }

    private static bool TryProbeExistingEntry(string entryPath, out bool caseSensitive)
    {
        caseSensitive = true;
        var name = Path.GetFileName(entryPath);
        if (!TrySwapCase(name, out var alternateName))
            return false;

        var parent = Path.GetDirectoryName(entryPath);
        if (string.IsNullOrWhiteSpace(parent))
            return false;

        try
        {
            var caseVariants = Directory.EnumerateFileSystemEntries(parent)
                .Select(Path.GetFileName)
                .Where(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();

            if (caseVariants > 1)
            {
                caseSensitive = true;
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var alternatePath = Path.Combine(parent, alternateName);
        caseSensitive = !(File.Exists(alternatePath) || Directory.Exists(alternatePath));
        return true;
    }

    private static bool TrySwapCase(string value, out string swapped)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i])) continue;

            var replacement = char.IsUpper(chars[i])
                ? char.ToLowerInvariant(chars[i])
                : char.ToUpperInvariant(chars[i]);
            if (replacement == chars[i]) continue;

            chars[i] = replacement;
            swapped = new string(chars);
            return true;
        }

        swapped = value;
        return false;
    }
}
