from pathlib import Path
p = Path('src/XPScript.Compiler/XPScriptTranspiler.cs')
text = p.read_text(encoding='utf-8')
after = '''        source = new SourceLineMarkerPreprocessor().Transform(source);\n        source = new IfLayoutPreprocessor().Transform(source);\n'''
before = '''        source = new IfLayoutPreprocessor().Transform(source);\n        source = new SourceLineMarkerPreprocessor().Transform(source);\n'''
if before not in text:
    if after not in text:
        raise SystemExit('Expected IfLayout/SourceLineMarker sequence not found')
    text = text.replace(after, before, 1)
p.write_text(text, encoding='utf-8')
