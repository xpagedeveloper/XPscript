using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ModuleObjectGlobalsPreprocessor
{
    private readonly HashSet<string> _udtTypes;
    private readonly Dictionary<string, string> _objects = new(StringComparer.OrdinalIgnoreCase);

    public ModuleObjectGlobalsPreprocessor(IEnumerable<string>? udtTypes = null)
    {
        _udtTypes = udtTypes is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(udtTypes, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> Objects => _objects;

    public string Transform(string source)
    {
        _objects.Clear();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var classes = CollectClasses(lines);
        if (classes.Count == 0) return source;

        CollectModuleObjects(lines, classes);
        if (_objects.Count == 0) return source;

        RewriteUses(lines);
        return string.Join(Environment.NewLine, lines);
    }

    private HashSet<string> CollectClasses(IEnumerable<string> lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)\b", RegexOptions.IgnoreCase);
            if (match.Success && !_udtTypes.Contains(match.Groups[1].Value))
                result.Add(match.Groups[1].Value);
        }
        return result;
    }

    private void CollectModuleObjects(IList<string> lines, IReadOnlySet<string> classes)
    {
        var inClass = false;
        var inProcedure = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClass = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) { inProcedure = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; continue; }
            if (inClass || inProcedure) continue;

            var declaration = Regex.Match(line,
                @"^(Public|Private)\s+([A-Za-z_]\w*)\s+As\s+([A-Za-z_]\w*)\s*$",
                RegexOptions.IgnoreCase);
            if (!declaration.Success || !classes.Contains(declaration.Groups[3].Value)) continue;

            _objects[declaration.Groups[2].Value] = declaration.Groups[3].Value;
            lines[i] = "";
        }
    }

    private void RewriteUses(IList<string> lines)
    {
        var inClassDeclaration = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw);
            var trimmed = code.Trim();
            if (Regex.IsMatch(trimmed, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClassDeclaration = true; continue; }
            if (Regex.IsMatch(trimmed, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClassDeclaration = false; continue; }
            if (inClassDeclaration && !Regex.IsMatch(trimmed, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase))
                continue;

            var indent = Regex.Match(raw, @"^\s*").Value;
            var comment = raw.Length > code.Length ? raw[code.Length..] : "";

            foreach (var pair in _objects.OrderByDescending(x => x.Key.Length))
            {
                var name = pair.Key;
                var escaped = Regex.Escape(name);

                var set = Regex.Match(trimmed, $@"^Set\s+{escaped}\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                if (set.Success)
                {
                    var rhs = set.Groups[1].Value.Trim();
                    if (rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = indent + $"Call XPModuleObjectRuntime.Clear(\"{EscapeString(name)}\")" + comment;
                        goto NextLine;
                    }

                    var newObject = Regex.Match(rhs, @"^New\s+([A-Za-z_]\w*)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
                    if (newObject.Success)
                    {
                        var args = newObject.Groups[2].Success && !string.IsNullOrWhiteSpace(newObject.Groups[2].Value)
                            ? ", " + newObject.Groups[2].Value.Trim()
                            : "";
                        lines[i] = indent + $"Call XPModuleObjectRuntime.SetNew(\"{EscapeString(name)}\", \"{EscapeString(newObject.Groups[1].Value)}\"{args})" + comment;
                        goto NextLine;
                    }

                    if (_objects.ContainsKey(rhs))
                    {
                        lines[i] = indent + $"Call XPModuleObjectRuntime.Assign(\"{EscapeString(name)}\", \"{EscapeString(rhs)}\")" + comment;
                        goto NextLine;
                    }
                }

                if (Regex.IsMatch(trimmed, $@"^Delete\s+{escaped}$", RegexOptions.IgnoreCase))
                {
                    lines[i] = indent + $"Call XPModuleObjectRuntime.Delete(\"{EscapeString(name)}\")" + comment;
                    goto NextLine;
                }

                code = ReplaceOutsideStrings(code,
                    $@"\b{escaped}\s+Is\s+Not\s+Nothing\b",
                    $"Not XPModuleObjectRuntime.IsNothing(\"{EscapeString(name)}\")");
                code = ReplaceOutsideStrings(code,
                    $@"\b{escaped}\s+Is\s+Nothing\b",
                    $"XPModuleObjectRuntime.IsNothing(\"{EscapeString(name)}\")");

                foreach (var other in _objects.Keys.Where(x => !x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    code = ReplaceOutsideStrings(code,
                        $@"\b{escaped}\s+Is\s+{Regex.Escape(other)}\b",
                        $"XPModuleObjectRuntime.IsSame(\"{EscapeString(name)}\", \"{EscapeString(other)}\")");
                }

                code = ReplaceOutsideStrings(code,
                    $@"(?<![\w.]){escaped}\.",
                    $"XPModuleObjectRuntime.Value(\"{EscapeString(name)}\").");
            }

            lines[i] = code + comment;
        NextLine:;
        }
    }

    private static string ReplaceOutsideStrings(string input, string pattern, string replacement)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, replacement, RegexOptions.IgnoreCase);
        return string.Concat(parts);
    }

    private static string EscapeString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

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
