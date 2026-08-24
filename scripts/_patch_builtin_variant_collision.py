from pathlib import Path
p = Path('src/XPScript.Compiler/XPScriptTranspiler.cs')
s = p.read_text()
needle = '        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);\n        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);\n'
replacement = '        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);\n        protectedSource = new CrossPlatformPreprocessor().Transform(protectedSource);\n        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);\n'
if replacement not in s:
    if needle not in s:
        raise SystemExit('variant preprocessor insertion marker not found')
    s = s.replace(needle, replacement, 1)
p.write_text(s)
print('protected cross-platform builtins before variant indexing')
