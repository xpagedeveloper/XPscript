from pathlib import Path

path = Path('src/XPScript.Compiler/OperatorArrayCompatibilityPreprocessor.cs')
text = path.read_text(encoding='utf-8')
old = r'''    private static string RewriteLogicalComparisonCondition(string line)
    {
        // Match both block headers ending at Then and compact forms that keep the first
        // branch statement (and optional Else) on the same physical/logical line.
        // String contents are protected before this stage, so a literal containing
        // "Then" cannot steal the suffix match.
        var match = Regex.Match(line, @"^(?<prefix>\s*(?:If|ElseIf)\s+)(?<condition>.+?)(?<suffix>\s+Then(?:\s+.*)?)$", RegexOptions.IgnoreCase);
        if (!match.Success) return line;
        var condition = match.Groups["condition"].Value;
        if (!Regex.IsMatch(condition, @"(?:=|<>|<=|>=|<|>)") || !Regex.IsMatch(condition, @"\s+(?:And|Or)\s+", RegexOptions.IgnoreCase)) return line;
        var rewritten = RewriteLogicalExpression(condition);
        return match.Groups["prefix"].Value + rewritten + match.Groups["suffix"].Value;
    }
'''
new = r'''    private static string RewriteLogicalComparisonCondition(string line)
    {
        // If/ElseIf may keep their first statement (and optional Else) after Then.
        // Loop constructs carry only a condition. All of them need the same
        // comparison-aware And/Or lowering so operator precedence does not depend on
        // statement layout or on which control-flow keyword introduced the condition.
        var ifMatch = Regex.Match(line, @"^(?<prefix>\s*(?:If|ElseIf)\s+)(?<condition>.+?)(?<suffix>\s+Then(?:\s+.*)?)$", RegexOptions.IgnoreCase);
        if (ifMatch.Success)
            return RewriteConditionMatch(ifMatch, line);

        var loopMatch = Regex.Match(
            line,
            @"^(?<prefix>\s*(?:While|Do\s+(?:While|Until)|Loop\s+(?:While|Until))\s+)(?<condition>.+?)(?<suffix>\s*)$",
            RegexOptions.IgnoreCase);
        if (loopMatch.Success)
            return RewriteConditionMatch(loopMatch, line);

        return line;
    }

    private static string RewriteConditionMatch(Match match, string original)
    {
        var condition = match.Groups["condition"].Value;
        if (!Regex.IsMatch(condition, @"(?:=|<>|<=|>=|<|>)") ||
            !Regex.IsMatch(condition, @"\s+(?:And|Or)\s+", RegexOptions.IgnoreCase))
            return original;

        var rewritten = RewriteLogicalExpression(condition);
        return match.Groups["prefix"].Value + rewritten + match.Groups["suffix"].Value;
    }
'''
if old not in text:
    raise SystemExit('RewriteLogicalComparisonCondition block not found')
path.write_text(text.replace(old, new, 1), encoding='utf-8')
