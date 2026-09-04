from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SKIP = {
    '.github/workflows/apply-xp-native-prefixes.yml',
    'scripts/apply-xp-native-prefixes.py',
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

PUBLIC_TEXT_SUFFIXES = {'.md', '.xps', '.ps1'}
PUBLIC_TEXT_NAMES = {'README.md', 'AGENTS.md', 'COMPATIBILITY.md'}
CS_FILES = {
    'src/XPScript.Compiler/NativeHttpJsonPreprocessor.cs',
    'src/XPScript.Compiler/RuntimeFeatures.cs',
    'src/XPScript.Compiler/ReservedIdentifierPreprocessor.cs',
    'src/XPScript.Compiler/NativeCsvPreprocessor.cs',
    'src/XPScript.Compiler/NativeCsvRuntimeSource.cs',
    'src/XPScript.Compiler/NativeXmlPreprocessor.cs',
    'src/XPScript.Compiler/NativeXmlRuntimeSource.cs',
    'src/XPScript.Compiler/HttpDbRuntimeSource.cs',
    'tests/PreprocessorPipelineProbe/Program.cs',
}

# Do not rename framework types in implementation code or documentation.
PROTECTED_PREFIXES = {
    'HttpClient': ['System.Net.Http.'],
    'HttpResponse': ['System.Net.Http.'],
    'JsonDocument': ['System.Text.Json.', 'System.Text.Json.Nodes.'],
    'JsonObject': ['System.Text.Json.Nodes.'],
    'JsonArray': ['System.Text.Json.Nodes.'],
    'JsonElement': ['System.Text.Json.'],
    'XmlDocument': ['System.Xml.'],
    'XmlElement': ['System.Xml.', 'System.Xml.Linq.'],
    'XmlNode': ['System.Xml.'],
    'XmlAttribute': ['System.Xml.', 'System.Xml.Linq.'],
}

def replace_token(text: str, old: str, new: str) -> str:
    protected = PROTECTED_PREFIXES.get(old, [])
    pattern = rf'\b{re.escape(old)}\b'
    def repl(match: re.Match[str]) -> str:
        start = match.start()
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
    if rel in SKIP or rel.startswith('.git/'):
        continue
    if not (path.suffix.lower() in PUBLIC_TEXT_SUFFIXES or path.name in PUBLIC_TEXT_NAMES or rel in CS_FILES):
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
