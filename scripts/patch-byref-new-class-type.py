from pathlib import Path

path = Path("src/XPScript.Compiler/CoreCompatibilityTranspiler.cs")
text = path.read_text(encoding="utf-8")
old = r'var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);'
new = r'var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*(?:As\s+(?:New\s+)?([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);'
if old not in text:
    raise SystemExit("DiscoverScalarTypes Dim pattern not found")
text = text.replace(old, new)
path.write_text(text, encoding="utf-8")
