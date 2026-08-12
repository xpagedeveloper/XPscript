from pathlib import Path

path = Path('todo/runtime-reference-todo.md')
text = path.read_text(encoding='utf-8')
replacements = {
    '- [>] `JsonDocument.Parse`, `JsonDocument.Stringify`': '- [x] `JsonDocument.Parse`, `JsonDocument.Stringify`',
    '- [>] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`': '- [x] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`',
    '- [>] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`': '- [x] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`',
    '- [>] `JsonElement.Type`, `JsonElement.Value`': '- [x] `JsonElement.Type`, `JsonElement.Value`',
    '- [>] `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`': '- [x] `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`\n- [x] end-to-end regression: `samples/native-json-regression.xps`; manual gate: `Native JSON Compatibility`; implementation uses .NET `System.Text.Json`'
}
for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f'Missing TODO text: {old}')
    text = text.replace(old, new, 1)
path.write_text(text, encoding='utf-8')
