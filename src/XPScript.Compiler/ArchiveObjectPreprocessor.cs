using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ArchiveObjectPreprocessor
{
    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "Archive", "ArchiveEntry")) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 4);
        var archiveVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+Archive\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                archiveVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                var args = dimNew.Groups[2].Value.Trim();
                output.Add(indent + (args.Length == 0
                    ? $"{name} = new XPScriptArchive()"
                    : $"{name} = new XPScriptArchive({args})"));
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(Archive|ArchiveEntry)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                if (dim.Groups[2].Value.Equals("Archive", StringComparison.OrdinalIgnoreCase)) archiveVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+Archive\s*(?:\(\s*\))?", "new XPScriptArchive()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+Archive\s*\((.*)\)", "new XPScriptArchive($1)", RegexOptions.IgnoreCase);

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (archiveVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptArchive", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }
}
