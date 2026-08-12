using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IndexedPropertyPreprocessor
{
    private sealed record IndexedProperty(string Name, string GetterName, string SetterName);

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var properties = CollectIndexedProperties(lines);
        if (properties.Count == 0) return source;

        RewriteDeclarations(lines, properties);
        RewriteUsages(lines, properties);
        return string.Join(Environment.NewLine, lines);
    }

    private static Dictionary<string, IndexedProperty> CollectIndexedProperties(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, IndexedProperty>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line,
                @"^(?:(?:Public|Private)\s+)?Property\s+(?:Get|Set)\s+([A-Za-z_]\w*)\s*\((.+)\)\s*(?:As\s+[A-Za-z_]\w*)?\s*$",
                RegexOptions.IgnoreCase);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[2].Value)) continue;
            var name = match.Groups[1].Value;
            result[name] = new IndexedProperty(name, "__xp_prop_get_" + name, "__xp_prop_set_" + name);
        }
        return result;
    }

    private static void RewriteDeclarations(IList<string> lines, IReadOnlyDictionary<string, IndexedProperty> properties)
    {
        IndexedProperty? current = null;
        var getter = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = StripComment(raw).Trim();

            if (current is null)
            {
                var match = Regex.Match(line,
                    @"^(?:(Public|Private)\s+)?Property\s+(Get|Set)\s+([A-Za-z_]\w*)\s*\((.+)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
                    RegexOptions.IgnoreCase);
                if (!match.Success || !properties.TryGetValue(match.Groups[3].Value, out current)) continue;

                getter = match.Groups[2].Value.Equals("Get", StringComparison.OrdinalIgnoreCase);
                var visibility = match.Groups[1].Success ? match.Groups[1].Value + " " : "Public ";
                var args = match.Groups[4].Value.Trim();
                if (getter)
                {
                    var returnType = match.Groups[5].Success ? " As " + match.Groups[5].Value : " As Variant";
                    lines[i] = indent + visibility + "Function " + current.GetterName + "(" + args + ")" + returnType;
                }
                else
                {
                    lines[i] = indent + visibility + "Sub " + current.SetterName + "(" + args + ")";
                }
                continue;
            }

            if (Regex.IsMatch(line, @"^End\s+Property$", RegexOptions.IgnoreCase))
            {
                lines[i] = indent + (getter ? "End Function" : "End Sub");
                current = null;
                continue;
            }

            if (getter)
            {
                // Scalar return: PropertyName = value
                // Object return: Set PropertyName = objectValue
                lines[i] = ReplaceOutsideStrings(raw,
                    $@"(?<![\w.])Set\s+{Regex.Escape(current.Name)}\s*=",
                    "Set " + current.GetterName + " =");
                lines[i] = ReplaceOutsideStrings(lines[i],
                    $@"(?<![\w.]){Regex.Escape(current.Name)}\s*=",
                    current.GetterName + " =");
            }
        }
    }

    private static void RewriteUsages(IList<string> lines, IReadOnlyDictionary<string, IndexedProperty> properties)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw);
            if (Regex.IsMatch(code.Trim(), @"^(?:(?:Public|Private)\s+)?(?:Sub|Function)\s+__xp_prop_(?:get|set)_", RegexOptions.IgnoreCase))
                continue;

            var rewritten = raw;
            foreach (var property in properties.Values.OrderByDescending(x => x.Name.Length))
            {
                // Indexed assignment:
                //   object.Property(index) = value
                //   Property(index) = value
                //   Set object.Property(index) = objectValue
                // Property Let has already been normalized to Property Set before this pass,
                // so both scalar and object setters lower to the same typed helper method.
                var setter = new Regex(
                    $@"^(?<indent>\s*)(?:Set\s+)?(?<target>(?:[A-Za-z_]\w*\.)?){Regex.Escape(property.Name)}\s*\((?<args>.*)\)\s*=\s*(?<value>.+?)\s*$",
                    RegexOptions.IgnoreCase);
                var setterMatch = setter.Match(StripComment(rewritten));
                if (setterMatch.Success)
                {
                    var args = setterMatch.Groups["args"].Value.Trim();
                    var value = setterMatch.Groups["value"].Value.Trim();
                    var target = setterMatch.Groups["target"].Value;
                    var allArgs = string.IsNullOrWhiteSpace(args) ? value : args + ", " + value;
                    rewritten = setterMatch.Groups["indent"].Value + "Call " + target + property.SetterName + "(" + allArgs + ")";
                    continue;
                }

                rewritten = ReplaceOutsideStrings(rewritten,
                    $@"(?<![\w])(?<target>(?:[A-Za-z_]\w*\.)?){Regex.Escape(property.Name)}\s*\(",
                    "${target}" + property.GetterName + "(");
            }
            lines[i] = rewritten;
        }
    }

    private static string ReplaceOutsideStrings(string input, string pattern, string replacement)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, replacement, RegexOptions.IgnoreCase);
        return string.Concat(parts);
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
