using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ObjectFunctionSetPreprocessor
{
    private static readonly Regex AssignmentPattern = new(
        @"^(?<indent>\s*)Set\s+(?<target>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(?<call>(?<name>[A-Za-z_]\w*)\s*\(.*\))\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IndexedPropertyAssignmentPattern = new(
        @"^(?<indent>\s*)Set\s+(?<target>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(?<call>(?:[A-Za-z_]\w*\.)*__xp_prop_get_[A-Za-z_]\w*\s*\(.*\))\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClassPattern = new(
        @"^(?:(?:Public|Private)\s+)?Class\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FunctionPattern = new(
        @"^(?:(?:Public|Private|Static)\s+)*Function\s+(?<name>[A-Za-z_]\w*)\s*\(.*\)\s+As\s+(?<type>[A-Za-z_]\w*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EndClassPattern = new(
        @"^End\s+Class$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var classes = CollectClasses(lines);
        var objectFunctions = CollectObjectFunctions(lines, classes);

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var uncommented = StripComment(raw);

            // Indexed object Property Get declarations are lowered by IndexedPropertyPreprocessor
            // to typed helper Functions named __xp_prop_get_*. Preserve the existing rewrite,
            // including qualified helper calls.
            var indexedPropertyMatch = IndexedPropertyAssignmentPattern.Match(uncommented);
            if (indexedPropertyMatch.Success)
            {
                lines[i] = BuildAssignRef(indexedPropertyMatch);
                continue;
            }

            // A module Function declared As an XPscript Class is compiled as LSRef<T>. Set must accept
            // that typed reference result just like it accepts an existing object-reference variable.
            // Do not rewrite arbitrary calls: compatibility objects can be dynamically typed and use
            // different reference semantics.
            var match = AssignmentPattern.Match(uncommented);
            if (!match.Success || !objectFunctions.Contains(match.Groups["name"].Value)) continue;

            lines[i] = BuildAssignRef(match);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static HashSet<string> CollectClasses(IEnumerable<string> lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var match = ClassPattern.Match(StripComment(raw).Trim());
            if (match.Success) result.Add(match.Groups["name"].Value);
        }
        return result;
    }

    private static HashSet<string> CollectObjectFunctions(IEnumerable<string> lines, HashSet<string> classes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inClass = false;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (ClassPattern.IsMatch(line))
            {
                inClass = true;
                continue;
            }
            if (EndClassPattern.IsMatch(line))
            {
                inClass = false;
                continue;
            }
            if (inClass) continue;

            var match = FunctionPattern.Match(line);
            if (match.Success && classes.Contains(match.Groups["type"].Value))
                result.Add(match.Groups["name"].Value);
        }

        return result;
    }

    private static string BuildAssignRef(Match match)
    {
        var target = match.Groups["target"].Value;
        var call = match.Groups["call"].Value.Trim();
        return $"{match.Groups["indent"].Value}Call LSObjectRuntime.AssignRef(ref {target}, {call})";
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
