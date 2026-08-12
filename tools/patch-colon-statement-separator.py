from pathlib import Path

path = Path('src/XPScript.Compiler/AdvancedXPScriptTranspiler.cs')
text = path.read_text(encoding='utf-8')

needle = '''        if (Regex.IsMatch(line, @"^End\\s+If$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }\n\n        var forAll = Regex.Match(line, @"^ForAll\\s+([A-Za-z_]\\w*)\\s+In\\s+([A-Za-z_]\\w*)$", RegexOptions.IgnoreCase);'''
replacement = '''        if (Regex.IsMatch(line, @"^End\\s+If$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }\n\n        // ':' is a statement separator. Handle it after single-line If/ElseIf so\n        // separator-delimited statements in a branch remain inside that branch, but\n        // before normal statement handlers such as For/Print/assignment.\n        if (TryEmitColonSeparatedStatements(sb, line)) return;\n\n        var forAll = Regex.Match(line, @"^ForAll\\s+([A-Za-z_]\\w*)\\s+In\\s+([A-Za-z_]\\w*)$", RegexOptions.IgnoreCase);'''
if needle not in text:
    raise SystemExit('EmitStatement insertion target not found')
text = text.replace(needle, replacement, 1)

needle2 = '''    private bool TryEmitDim(StringBuilder sb, string line)\n    {'''
addition = r'''    private bool TryEmitColonSeparatedStatements(StringBuilder sb, string line)
    {
        // A standalone label already uses ':' as part of its syntax and must not be
        // interpreted as a statement separator.
        if (Regex.IsMatch(line, @"^[A-Za-z_]\w*:\s*$", RegexOptions.IgnoreCase))
            return false;

        var statements = SplitColonStatements(line);
        if (statements.Count <= 1)
            return false;

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (trimmed.Length == 0)
                throw new CompilerException("Empty statement between ':' separators is not supported.");
            EmitStatement(sb, trimmed);
        }
        return true;
    }

    private static List<string> SplitColonStatements(string line)
    {
        var result = new List<string>();
        var start = 0;
        var inString = false;
        var depth = 0;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '(' || c == '[')
            {
                depth++;
                continue;
            }
            if (c == ')' || c == ']')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (c != ':' || depth != 0)
                continue;

            result.Add(line[start..i]);
            start = i + 1;
        }

        if (result.Count == 0)
            return [line];

        result.Add(line[start..]);
        return result;
    }

'''
if needle2 not in text:
    raise SystemExit('TryEmitDim insertion target not found')
text = text.replace(needle2, addition + needle2, 1)
path.write_text(text, encoding='utf-8')
