from pathlib import Path

# 1) Add a source-layout normalizer that preserves physical line count for split Then.
pre = Path('src/XPScript.Compiler/IfLayoutPreprocessor.cs')
pre.write_text(r'''using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IfLayoutPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i + 1 < lines.Length; i++)
        {
            var currentCode = StripComment(lines[i]).Trim();
            var nextCode = StripComment(lines[i + 1]).Trim();
            if (!nextCode.Equals("Then", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Regex.IsMatch(currentCode, @"^(?:If|ElseIf)\s+.+$", RegexOptions.IgnoreCase)) continue;
            if (Regex.IsMatch(currentCode, @"\bThen\s*$", RegexOptions.IgnoreCase)) continue;

            lines[i] = lines[i].TrimEnd() + " Then";
            var indent = Regex.Match(lines[i + 1], @"^\s*").Value;
            lines[i + 1] = indent + "' Then joined to previous If/ElseIf by compiler";
        }
        return string.Join(Environment.NewLine, lines);
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
''', encoding='utf-8')

# 2) Wire it after physical source markers so line count/source tracking remains stable.
xps = Path('src/XPScript.Compiler/XPScriptTranspiler.cs')
text = xps.read_text(encoding='utf-8')
needle = '        source = new SourceLineMarkerPreprocessor().Transform(source);\n'
replacement = needle + '        source = new IfLayoutPreprocessor().Transform(source);\n'
if replacement not in text:
    if needle not in text: raise SystemExit('XPScriptTranspiler insertion point not found')
    text = text.replace(needle, replacement, 1)
xps.write_text(text, encoding='utf-8')

# 3) Teach Advanced emitter single-line If and single-line If...Else without expanding source lines.
adv = Path('src/XPScript.Compiler/AdvancedXPScriptTranspiler.cs')
text = adv.read_text(encoding='utf-8')
needle = '''        if (TryEmitDim(sb, line)) return;\n\n        var ifMatch = Regex.Match(line, @"^If\\s+(.+)\\s+Then$", RegexOptions.IgnoreCase);\n'''
insert = r'''        if (TryEmitDim(sb, line)) return;

        if (TryEmitSingleLineIf(sb, line)) return;

        var ifMatch = Regex.Match(line, @"^If\s+(.+)\s+Then$", RegexOptions.IgnoreCase);
'''
if needle not in text: raise SystemExit('Advanced If insertion point not found')
text = text.replace(needle, insert, 1)

# Add helpers before TryEmitErase, a stable method boundary in this file.
marker = '    private bool TryEmitErase(StringBuilder sb, string line)\n'
if marker not in text: raise SystemExit('Advanced helper insertion point not found')
helper = r'''    private bool TryEmitSingleLineIf(StringBuilder sb, string line)
    {
        var match = Regex.Match(line, @"^If\s+(.+?)\s+Then\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var condition = match.Groups[1].Value.Trim();
        var tail = match.Groups[2].Value.Trim();
        var elseIndex = FindTopLevelElse(tail);
        var trueStatement = elseIndex >= 0 ? tail[..elseIndex].Trim() : tail;
        var falseStatement = elseIndex >= 0 ? tail[(elseIndex + 4)..].Trim() : null;
        if (trueStatement.Length == 0)
            throw new CompilerException("Single-line If requires a statement after Then.");
        if (falseStatement is not null && falseStatement.Length == 0)
            throw new CompilerException("Single-line If Else requires a statement after Else.");

        Write(sb, $"if ({TransformCondition(condition)})");
        Write(sb, "{");
        _indent++;
        EmitStatement(sb, trueStatement);
        _indent--;
        Write(sb, "}");

        if (falseStatement is not null)
        {
            Write(sb, "else");
            Write(sb, "{");
            _indent++;
            EmitStatement(sb, falseStatement);
            _indent--;
            Write(sb, "}");
        }
        return true;
    }

    private static int FindTopLevelElse(string value)
    {
        var inString = false;
        var depth = 0;
        for (var i = 0; i <= value.Length - 4; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth = Math.Max(0, depth - 1); continue; }
            if (depth != 0) continue;
            if (!value.AsSpan(i, 4).Equals("Else", StringComparison.OrdinalIgnoreCase)) continue;
            var beforeOk = i == 0 || char.IsWhiteSpace(value[i - 1]);
            var after = i + 4;
            var afterOk = after >= value.Length || char.IsWhiteSpace(value[after]);
            if (beforeOk && afterOk) return i;
        }
        return -1;
    }

'''
if helper not in text:
    text = text.replace(marker, helper + marker, 1)
adv.write_text(text, encoding='utf-8')
