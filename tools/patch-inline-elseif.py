from pathlib import Path
p = Path('src/XPScript.Compiler/AdvancedXPScriptTranspiler.cs')
text = p.read_text(encoding='utf-8')
needle = '''        var elseif = Regex.Match(line, @"^ElseIf\\s+(.+)\\s+Then$", RegexOptions.IgnoreCase);\n        if (elseif.Success) { _indent--; Write(sb, "}"); Write(sb, $"else if ({TransformCondition(elseif.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }\n'''
replacement = r'''        var elseifInline = Regex.Match(line, @"^ElseIf\s+(.+?)\s+Then\s+(.+)$", RegexOptions.IgnoreCase);
        if (elseifInline.Success)
        {
            _indent--;
            Write(sb, "}");
            Write(sb, $"else if ({TransformCondition(elseifInline.Groups[1].Value)})");
            Write(sb, "{");
            _indent++;
            EmitStatement(sb, elseifInline.Groups[2].Value.Trim());
            return;
        }
        var elseif = Regex.Match(line, @"^ElseIf\s+(.+)\s+Then$", RegexOptions.IgnoreCase);
        if (elseif.Success) { _indent--; Write(sb, "}"); Write(sb, $"else if ({TransformCondition(elseif.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }
'''
if replacement in text:
    raise SystemExit(0)
if needle not in text:
    raise SystemExit('ElseIf insertion point not found')
text = text.replace(needle, replacement, 1)
p.write_text(text, encoding='utf-8')
