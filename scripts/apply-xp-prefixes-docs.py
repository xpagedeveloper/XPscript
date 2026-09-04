from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SKIP = {
    '.github/workflows/apply-xp-prefixes-docs.yml',
    'scripts/apply-xp-prefixes-docs.py',
}
MAPPINGS = [
    ('HTTPDBDominoRest', 'XPHttpDbDominoRest'),
    ('HTTPDBSupabase', 'XPHttpDbSupabase'),
    ('HttpResponse', 'XPHttpResponse'),
    ('HttpClient', 'XPHttpClient'),
    ('JsonDocument', 'XPJsonDocument'),
    ('JsonObject', 'XPJsonObject'),
    ('JsonArray', 'XPJsonArray'),
    ('JsonElement', 'XPJsonElement'),
    ('CsvHeaderCollection', 'XPCsvHeaderCollection'),
    ('CsvRowCollection', 'XPCsvRowCollection'),
    ('CsvColumnCollection', 'XPCsvColumnCollection'),
    ('CsvDocument', 'XPCsvDocument'),
    ('CsvColumn', 'XPCsvColumn'),
    ('CsvRow', 'XPCsvRow'),
    ('XmlValidationErrorCollection', 'XPXmlValidationErrorCollection'),
    ('XmlValidationResult', 'XPXmlValidationResult'),
    ('XmlValidationError', 'XPXmlValidationError'),
    ('XmlAttributeCollection', 'XPXmlAttributeCollection'),
    ('XmlNodeCollection', 'XPXmlNodeCollection'),
    ('XmlDocument', 'XPXmlDocument'),
    ('XmlAttribute', 'XPXmlAttribute'),
    ('XmlElement', 'XPXmlElement'),
    ('XmlNode', 'XPXmlNode'),
]
SUFFIXES = {'.md', '.mdx', '.astro', '.ts', '.tsx', '.js', '.mjs', '.json', '.xps', '.ps1'}
PROTECTED = {
    'HttpClient': ['System.Net.Http.'],
    'JsonDocument': ['System.Text.Json.'],
    'JsonObject': ['System.Text.Json.Nodes.'],
    'JsonArray': ['System.Text.Json.Nodes.'],
    'JsonElement': ['System.Text.Json.'],
    'XmlDocument': ['System.Xml.'],
    'XmlElement': ['System.Xml.', 'System.Xml.Linq.'],
    'XmlNode': ['System.Xml.'],
    'XmlAttribute': ['System.Xml.', 'System.Xml.Linq.'],
}

def replace_token(text, old, new):
    protected = PROTECTED.get(old, [])
    pattern = rf'\b{re.escape(old)}\b'
    def repl(m):
        start = m.start()
        for prefix in protected:
            if text[max(0, start-len(prefix)):start] == prefix:
                return old
        return new
    return re.sub(pattern, repl, text)

changed = []
for path in ROOT.rglob('*'):
    if not path.is_file():
        continue
    rel = path.relative_to(ROOT).as_posix()
    if rel in SKIP or rel.startswith('.git/') or path.suffix.lower() not in SUFFIXES:
        continue
    try:
        original = path.read_text(encoding='utf-8')
    except UnicodeDecodeError:
        continue
    updated = original
    for old, new in MAPPINGS:
        updated = replace_token(updated, old, new)
    if updated != original:
        path.write_text(updated, encoding='utf-8')
        changed.append(rel)
print(f'updated {len(changed)} files')
for rel in changed:
    print(rel)
