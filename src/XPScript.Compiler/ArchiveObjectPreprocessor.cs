using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ArchiveObjectPreprocessor
{
    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "Archive", "ArchiveEntry")) return source;

        new ArchiveCapabilityValidator().Validate(source, "archive.xps");

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
                output.Add(indent + $"{name} = {CreateArchiveExpression(args)}");
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

            var rewritten = Regex.Replace(line, @"\bNew\s+Archive\s*(?:\(\s*\))?", "XPScriptArchiveFactory.Create()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+Archive\s*\((.*)\)", m => CreateArchiveExpression(m.Groups[1].Value), RegexOptions.IgnoreCase);

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (archiveVariables.Contains(set.Groups[1].Value)
                || set.Groups[2].Value.Contains("XPScriptArchive", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains("XPScriptExtendedArchive", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string CreateArchiveExpression(string rawArguments)
    {
        var args = SplitArguments(rawArguments);
        if (args.Count == 0) return "XPScriptArchiveFactory.Create()";
        if (args.Count == 1) return $"XPScriptArchiveFactory.Create({args[0]})";
        if (args.Count != 2)
            throw new CompilerException("Archive constructor expects filename or Byte array and optional extendedSupport Boolean.");

        var extended = args[1].Trim();
        if (extended.Equals("True", StringComparison.OrdinalIgnoreCase))
            return $"XPScriptExtendedArchiveWriterFactory.Create({args[0]})";
        if (extended.Equals("False", StringComparison.OrdinalIgnoreCase))
            return $"XPScriptArchiveFactory.Create({args[0]}, false)";

        throw new CompilerException("Archive extendedSupport must be the literal True or False so dependencies can be resolved at compile time.");
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
}
