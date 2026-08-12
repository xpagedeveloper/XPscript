from pathlib import Path

transpiler = Path('src/XPScript.Compiler/XPScriptTranspiler.cs')
text = transpiler.read_text(encoding='utf-8')
needle = '        source = new SourceLineMarkerPreprocessor().Transform(source);\n'
replacement = needle + '        source = new StatementSeparatorPreprocessor().Transform(source);\n'
if replacement not in text:
    if needle not in text:
        raise SystemExit('SourceLineMarker pipeline target not found')
    text = text.replace(needle, replacement, 1)
transpiler.write_text(text, encoding='utf-8')

advanced = Path('src/XPScript.Compiler/AdvancedXPScriptTranspiler.cs')
text = advanced.read_text(encoding='utf-8')
call_block = '''        // ':' is a statement separator. Handle it after single-line If/ElseIf so\n        // separator-delimited statements in a branch remain inside that branch, but\n        // before normal statement handlers such as For/Print/assignment.\n        if (TryEmitColonSeparatedStatements(sb, line)) return;\n\n'''
if call_block not in text:
    raise SystemExit('Advanced colon call block not found')
text = text.replace(call_block, '', 1)
start = text.find('    private bool TryEmitColonSeparatedStatements(StringBuilder sb, string line)\n')
end = text.find('    private bool TryEmitDim(StringBuilder sb, string line)\n', start)
if start < 0 or end < 0:
    raise SystemExit('Advanced colon helper block not found')
text = text[:start] + text[end:]
advanced.write_text(text, encoding='utf-8')
