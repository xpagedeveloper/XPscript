from pathlib import Path
p = Path('src/XPScript.Compiler/OperatorArrayCompatibilityPreprocessor.cs')
text = p.read_text(encoding='utf-8')
old = r'''        var match = Regex.Match(line, @"^(?<prefix>\s*(?:If|ElseIf)\s+)(?<condition>.+?)(?<suffix>\s+Then\s*)$", RegexOptions.IgnoreCase);
        if (!match.Success) return line;
        var condition = match.Groups["condition"].Value;
        if (!Regex.IsMatch(condition, @"(?:=|<>|<=|>=|<|>)") || !Regex.IsMatch(condition, @"\s+(?:And|Or)\s+", RegexOptions.IgnoreCase)) return line;
        var rewritten = RewriteLogicalExpression(condition);
        return match.Groups["prefix"].Value + rewritten + match.Groups["suffix"].Value;
'''
new = r'''        var match = Regex.Match(line, @"^(?<prefix>\s*(?:If|ElseIf)\s+)(?<condition>.+?)(?<then>\s+Then)(?<tail>\s+.*)?$", RegexOptions.IgnoreCase);
        if (!match.Success) return line;
        var condition = match.Groups["condition"].Value;
        if (!Regex.IsMatch(condition, @"(?:=|<>|<=|>=|<|>)") || !Regex.IsMatch(condition, @"\s+(?:And|Or)\s+", RegexOptions.IgnoreCase)) return line;
        var rewritten = RewriteLogicalExpression(condition);
        return match.Groups["prefix"].Value + rewritten + match.Groups["then"].Value + match.Groups["tail"].Value;
'''
if new in text:
    raise SystemExit(0)
if old not in text:
    raise SystemExit('Logical If rewrite block not found')
text = text.replace(old, new, 1)
p.write_text(text, encoding='utf-8')
