using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ArchiveObjectPreprocessor
{
    private const string FirstEntryHelper = "XpsCompilerGeneratedArchiveGetFirstEntry";
    private const string NextEntryHelper = "XpsCompilerGeneratedArchiveGetNextEntry";

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "Archive", "ArchiveEntry")) return source;

        new ArchiveCapabilityValidator().Validate(source, "archive.xps");

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 32);
        var archiveVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archiveEntryVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needsIteratorHelpers = false;

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
                else archiveEntryVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var forAll = Regex.Match(line, @"^ForAll\s+([A-Za-z_]\w*)\s+In\s+([A-Za-z_]\w*)\s*\.\s*(?:Entries|Files\s*\(\s*\)|Folders\s*\(\s*\))\s*$", RegexOptions.IgnoreCase);
            if (forAll.Success && archiveVariables.Contains(forAll.Groups[2].Value))
                archiveEntryVariables.Add(forAll.Groups[1].Value);

            var rewritten = Regex.Replace(line, @"\bNew\s+Archive\s*(?:\(\s*\))?", "XPScriptArchiveFactory.Create()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+Archive\s*\((.*)\)", m => CreateArchiveExpression(m.Groups[1].Value), RegexOptions.IgnoreCase);

            foreach (var archiveName in archiveVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(archiveName);
                var firstPattern = $@"\b{escaped}\s*\.\s*GetFirstEntry\s*\(\s*\)";
                if (Regex.IsMatch(rewritten, firstPattern, RegexOptions.IgnoreCase))
                {
                    rewritten = Regex.Replace(rewritten, firstPattern, $"{FirstEntryHelper}({archiveName})", RegexOptions.IgnoreCase);
                    needsIteratorHelpers = true;
                }

                var nextPattern = $@"\b{escaped}\s*\.\s*GetNextEntry\s*\(\s*([^()]*)\s*\)";
                if (Regex.IsMatch(rewritten, nextPattern, RegexOptions.IgnoreCase))
                {
                    rewritten = Regex.Replace(rewritten, nextPattern, m => $"{NextEntryHelper}({archiveName}, {m.Groups[1].Value.Trim()})", RegexOptions.IgnoreCase);
                    needsIteratorHelpers = true;
                }
            }

            foreach (var entryName in archiveEntryVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(entryName);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*IsFolder\b", $"{entryName}.IsDirectory", RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*IsFile\b", $"(Not {entryName}.IsDirectory)", RegexOptions.IgnoreCase);
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (archiveVariables.Contains(set.Groups[1].Value)
                || archiveEntryVariables.Contains(set.Groups[1].Value)
                || set.Groups[2].Value.Contains("XPScriptArchive", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains("XPScriptExtendedArchive", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains(FirstEntryHelper, StringComparison.Ordinal)
                || set.Groups[2].Value.Contains(NextEntryHelper, StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        if (needsIteratorHelpers)
        {
            output.Add("");
            output.AddRange(BuildIteratorHelpers());
        }

        return string.Join(Environment.NewLine, output);
    }

    private static IEnumerable<string> BuildIteratorHelpers()
    {
        return new[]
        {
            $"Function {FirstEntryHelper}(ByVal archive As Variant) As Variant",
            "    ForAll candidate In archive.Entries",
            $"        Set {FirstEntryHelper} = candidate",
            "        Exit Function",
            "    End ForAll",
            $"    Set {FirstEntryHelper} = Nothing",
            "End Function",
            "",
            $"Function {NextEntryHelper}(ByVal archive As Variant, ByVal previousEntry As Variant) As Variant",
            "    If previousEntry Is Nothing Then",
            $"        Set {NextEntryHelper} = {FirstEntryHelper}(archive)",
            "        Exit Function",
            "    End If",
            "    Dim foundPrevious As Boolean",
            "    foundPrevious = False",
            "    ForAll candidate In archive.Entries",
            "        If foundPrevious Then",
            $"            Set {NextEntryHelper} = candidate",
            "            Exit Function",
            "        End If",
            "        If candidate.FullName = previousEntry.FullName Then foundPrevious = True",
            "    End ForAll",
            $"    Set {NextEntryHelper} = Nothing",
            "End Function"
        };
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
