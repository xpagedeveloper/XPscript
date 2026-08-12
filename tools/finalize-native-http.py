from pathlib import Path

path = Path('todo/runtime-reference-todo.md')
text = path.read_text(encoding='utf-8')
replacements = {
    '- [>] `HttpClient`': '- [x] `HttpClient`',
    '- [>] `Get`, `Post`, `Put`, `Patch`, `Delete`': '- [x] `Get`, `Post`, `Put`, `Patch`, `Delete`',
    '- [>] `SetHeader`, `RemoveHeader`, `ClearHeaders`, `Timeout`': '- [x] `SetHeader`, `RemoveHeader`, `ClearHeaders`, `Timeout`',
    '- [>] `HttpResponse.StatusCode`, `StatusText`, `Body`, `ContentType`, `Headers`, `IsSuccess`': '- [x] `HttpResponse.StatusCode`, `StatusText`, `Body`, `ContentType`, `Headers`, `IsSuccess`',
    '- [>] source: `samples/native-http-json.xps`': '- [x] end-to-end loopback regression: `samples/native-http-regression.xps`, `tests/native_http_server.py`; manual gate: `Native HTTP Compatibility`'
}
for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f'Missing TODO text: {old}')
    text = text.replace(old, new, 1)
path.write_text(text, encoding='utf-8')
