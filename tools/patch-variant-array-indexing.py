from pathlib import Path

path = Path('src/XPScript.Compiler/CoreCompatibilityTranspiler.cs')
text = path.read_text(encoding='utf-8')

replacements = [
    (
        'finalLine = RewriteArrayReads(finalLine, arrays);',
        'finalLine = RewriteArrayReads(finalLine, arrays, scalarTypes);'
    ),
    (
        'if (arrayAssignment.Success && arrays.ContainsKey(arrayAssignment.Groups[1].Value))',
        'if (arrayAssignment.Success && (arrays.ContainsKey(arrayAssignment.Groups[1].Value) || (scalarTypes.TryGetValue(arrayAssignment.Groups[1].Value, out var indexedScalarType) && indexedScalarType.Equals("Variant", StringComparison.OrdinalIgnoreCase))))'
    ),
    (
        'private string RewriteArrayReads(string line, Dictionary<string, ArrayInfo> arrays)',
        'private string RewriteArrayReads(string line, Dictionary<string, ArrayInfo> arrays, Dictionary<string, string> scalarTypes)'
    ),
]

for old, new in replacements:
    if old not in text:
        raise SystemExit(f'Expected source fragment not found: {old}')
    text = text.replace(old, new, 1)

needle = '''        foreach (var name in arrays.Keys.OrderByDescending(x => x.Length))
            line = ReplaceCall(line, name, args => $"LSArrayRuntime.Get({name}{(args.Length > 0 ? ", " + args : "")})");'''
addition = needle + '''
        foreach (var name in scalarTypes.Where(x => x.Value.Equals("Variant", StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).OrderByDescending(x => x.Length))
            line = ReplaceCall(line, name, args => $"LSArrayRuntime.Get({name}{(args.Length > 0 ? ", " + args : "")})");'''
if needle not in text:
    raise SystemExit('Expected array read lowering block not found.')
text = text.replace(needle, addition, 1)

path.write_text(text, encoding='utf-8')
