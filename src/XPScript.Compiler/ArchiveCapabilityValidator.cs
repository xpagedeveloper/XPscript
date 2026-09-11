using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ArchiveCapabilityValidator
{
    private enum ArchiveMode
    {
        Unknown,
        ZipOnly,
        Extended
    }

    private static readonly HashSet<string> ExtendedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "7Z", "7ZIP", "SEVENZIP",
        "RAR",
        "TAR",
        "GZ", "GZIP",
        "TAR.GZ", "TGZ", "TARGZIP",
        "TAR.BZ2", "TBZ2", "TARBZIP2",
        "TAR.LZ", "TARLZIP",
        "BZ2", "BZIP2",
        "LZ", "LZIP",
        "XZ",
        "ZST", "ZSTD", "ZSTANDARD"
    };

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var modes = new Dictionary<string, ArchiveMode>(StringComparer.OrdinalIgnoreCase);
        var passwordConfigured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var original = lines[index];
            var line = StripComment(original).Trim();
            if (line.Length == 0) continue;

            var dimNew = Regex.Match(line,
                @"^Dim\s+(?<name>[A-Za-z_]\w*)\s+As\s+New\s+Archive\s*(?:\((?<args>.*)\))?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dimNew.Success)
            {
                var name = dimNew.Groups["name"].Value;
                modes[name] = DetermineMode(dimNew.Groups["args"].Value, sourceName, index + 1, original);
                passwordConfigured.Remove(name);
                continue;
            }

            var dim = Regex.Match(line,
                @"^Dim\s+(?<name>[A-Za-z_]\w*)\s+As\s+Archive\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dim.Success)
            {
                modes[dim.Groups["name"].Value] = ArchiveMode.Unknown;
                continue;
            }

            var assignment = Regex.Match(line,
                @"^(?:Set\s+)?(?<name>[A-Za-z_]\w*)\s*=\s*New\s+Archive\s*(?:\((?<args>.*)\))?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (assignment.Success && modes.ContainsKey(assignment.Groups["name"].Value))
            {
                var name = assignment.Groups["name"].Value;
                modes[name] = DetermineMode(assignment.Groups["args"].Value, sourceName, index + 1, original);
                passwordConfigured.Remove(name);
                continue;
            }

            foreach (var item in modes)
            {
                if (item.Value == ArchiveMode.ZipOnly)
                {
                    ValidateZipOnlyUse(item.Key, line, sourceName, index + 1, original);
                    continue;
                }

                if (item.Value != ArchiveMode.Extended) continue;
                var prefix = Regex.Escape(item.Key);
                if (Regex.IsMatch(line, $@"\b{prefix}\s*\.\s*Password\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    passwordConfigured.Add(item.Key);
                    continue;
                }

                if (passwordConfigured.Contains(item.Key) && IsZipMutation(prefix, line))
                    throw Diagnostic(sourceName, index + 1, original,
                        $"Password-protected ZIP writing is not supported. '{item.Key}' can use Password for reading encrypted ZIP archives, but creating or modifying an encrypted ZIP is not supported.");
            }
        }
    }

    private static ArchiveMode DetermineMode(string rawArguments, string sourceName, int lineNumber, string original)
    {
        var args = SplitArguments(rawArguments);
        if (args.Count <= 1) return ArchiveMode.ZipOnly;
        if (args.Count != 2) return ArchiveMode.Unknown;

        var extended = args[1].Trim();
        if (extended.Equals("True", StringComparison.OrdinalIgnoreCase)) return ArchiveMode.Extended;
        if (extended.Equals("False", StringComparison.OrdinalIgnoreCase)) return ArchiveMode.ZipOnly;

        throw Diagnostic(sourceName, lineNumber, original,
            "Archive extendedSupport must be the literal True or False so dependencies can be resolved at compile time.");
    }

    private static void ValidateZipOnlyUse(string variableName, string line, string sourceName, int lineNumber, string original)
    {
        var prefix = Regex.Escape(variableName);

        if (Regex.IsMatch(line, $@"\b{prefix}\s*\.\s*Password\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw RequiresExtended(sourceName, lineNumber, original, variableName,
                "Archive.Password for ZIP encryption/decryption");

        if (Regex.IsMatch(line, $@"\b{prefix}\s*\.\s*IsEncrypted\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw RequiresExtended(sourceName, lineNumber, original, variableName,
                "Archive.IsEncrypted for ZIP encryption detection");

        var createPattern = $"\\b{prefix}\\s*\\.\\s*Create\\s*(?:\\(\\s*)?\"(?<format>[^\"]+)\"";
        var create = Regex.Match(line, createPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (create.Success)
        {
            var format = create.Groups["format"].Value.Trim().TrimStart('.');
            if (!format.Equals("ZIP", StringComparison.OrdinalIgnoreCase) && ExtendedFormats.Contains(format))
                throw RequiresExtended(sourceName, lineNumber, original, variableName,
                    $"Archive.Create(\"{format}\")");
        }
    }

    private static bool IsZipMutation(string prefix, string line)
    {
        if (Regex.IsMatch(line,
            $@"\b{prefix}\s*\.\s*(?:AddFile|AddFolder|AddText|AddBytes|Remove|Rename|Save)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        var createPattern = $"\\b{prefix}\\s*\\.\\s*Create\\s*(?:\\(\\s*)?(?:\"ZIP\")?";
        return Regex.IsMatch(line, createPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static CompilerException RequiresExtended(string sourceName, int lineNumber, string original, string variableName, string feature) =>
        Diagnostic(sourceName, lineNumber, original,
            $"{feature} requires extended archive support. Change '{variableName}' to New Archive(..., True).");

    private static CompilerException Diagnostic(string sourceName, int lineNumber, string original, string description)
    {
        var masked = CompilerDiagnosticRedaction.MaskStringLiterals(original).TrimEnd();
        return new CompilerException($"{sourceName}({lineNumber},1): {description}{Environment.NewLine}  {masked}");
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
