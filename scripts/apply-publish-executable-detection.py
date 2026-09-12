from pathlib import Path

p = Path('src/XPScript.Compiler/CompilerDriver.cs')
s = p.read_text(encoding='utf-8')
replacements = [
    ('var generatedExecutable = FindPublishedExecutable(publishDir, rid);',
     'var generatedExecutable = FindPublishedExecutable(publishDir, rid, OutputAssemblyName(outputPath));'),
    ('var generatedExecutable = FindPublishedExecutable(runOutputDirectory, rid);',
     'var generatedExecutable = FindPublishedExecutable(runOutputDirectory, rid, "Generated");'),
    ('''    private static string? FindPublishedExecutable(string publishDirectory, string rid)\n    {\n        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))\n            return Directory.EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();\n\n        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)\n            .Where(path => !Path.HasExtension(path))\n            .Where(path => !Path.GetFileName(path).EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))\n            .ToArray();\n        return candidates.Length == 1 ? candidates[0] : candidates.FirstOrDefault(path => Path.GetFileName(path).Equals("Generated", StringComparison.OrdinalIgnoreCase));\n    }''',
     '''    private static string? FindPublishedExecutable(string publishDirectory, string rid, string assemblyName)\n    {\n        var expectedName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase)\n            ? assemblyName + ".exe"\n            : assemblyName;\n        var expectedPath = Path.Combine(publishDirectory, expectedName);\n        if (File.Exists(expectedPath)) return expectedPath;\n\n        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))\n            return Directory.EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();\n\n        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)\n            .Where(path => !Path.HasExtension(path))\n            .Where(path => !Path.GetFileName(path).EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))\n            .ToArray();\n        return candidates.Length == 1 ? candidates[0] : null;\n    }''')
]
for old, new in replacements:
    if s.count(old) != 1:
        raise SystemExit(f'Expected one occurrence, got {s.count(old)} for {old[:80]!r}')
    s = s.replace(old, new, 1)
p.write_text(s, encoding='utf-8')
print('Executable detection fixed.')
