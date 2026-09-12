from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXT_EXTENSIONS = {'.md', '.cs', '.ps1', '.yml', '.yaml', '.html', '.txt'}
SKIP_PARTS = {'.git', 'node_modules', 'bin', 'obj'}
RIDS = ('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')

for path in ROOT.rglob('*'):
    if not path.is_file() or path.suffix.lower() not in TEXT_EXTENSIONS:
        continue
    if any(part in SKIP_PARTS for part in path.parts):
        continue
    if path.name in {'cleanup-legacy-cli-temp.py', 'cleanup-legacy-cli-temp.yml'}:
        continue

    text = path.read_text(encoding='utf-8')
    updated = text.replace('--framework-dependent', '--runtime=false')
    updated = updated.replace('--runtime RID', '--platform RID')
    for rid in RIDS:
        updated = updated.replace(f'--runtime {rid}', f'--platform {rid}')

    updated = updated.replace(
        'support explicit `--runtime` / `--rid` target selection',
        'support explicit `--platform` / `--rid` target selection')
    updated = updated.replace(
        '| `--runtime` | `--platform RID` |',
        '| `--platform` / `--rid` | `--platform RID` or `--rid RID` |')
    updated = updated.replace(
        '| `--runtime` | `--platform win-x64` | RID | Selects target runtime.',
        '| `--platform` / `--rid` | `--platform win-x64` or `--rid win-x64` | RID | Selects target operating system and architecture.')
    updated = updated.replace(
        '| `--runtime=false` | `--runtime=false` | none | Produces framework-dependent output instead of a self-contained application.',
        '| `--runtime` | `--runtime=true|false` | boolean | Includes (`true`) or excludes (`false`, default) the .NET 10 runtime from the published application.')
    updated = updated.replace(
        '| `--runtime=false` | `--runtime=false` | none | Creates framework-dependent output.',
        '| `--runtime` | `--runtime=true|false` | boolean | Includes (`true`) or excludes (`false`, default) the .NET 10 runtime.')

    if path.as_posix().endswith('docs/cli-reference.md'):
        runtime_row = '| `--runtime` | `--runtime=true|false` | boolean | Includes (`true`) or excludes (`false`, default) the .NET 10 runtime from the published application. | [hello.xps](../demo/console/hello.xps) |'
        single_row = '| `--single-file` | `--single-file=true|false` | boolean | Bundles application-managed files into the executable when `true` (default); emits them as separate files when `false`. | [hello.xps](../demo/console/hello.xps) |'
        if runtime_row in updated and single_row not in updated:
            updated = updated.replace(runtime_row, runtime_row + '\n' + single_row)

    if path.as_posix().endswith('docs/commands.md'):
        runtime_row = '| `--runtime` | `--runtime=true|false` | boolean | Includes (`true`) or excludes (`false`, default) the .NET 10 runtime. | [hello.xps](../samples/hello.xps) |'
        single_row = '| `--single-file` | `--single-file=true|false` | boolean | Bundles application libraries into the executable when `true` (default); emits separate application files when `false`. | [hello.xps](../samples/hello.xps) |'
        if runtime_row in updated and single_row not in updated:
            updated = updated.replace(runtime_row, runtime_row + '\n' + single_row)

    if path.as_posix().endswith('docs/language-reference.md'):
        runtime_row = '| `--runtime` | `--runtime=true|false` | boolean runtime inclusion. | Controls whether .NET 10 is included in publish output. | [hello.xps](../demo/console/hello.xps) |'
        single_row = '| `--single-file` | `--single-file=true|false` | boolean packaging mode. | Controls whether application libraries are bundled into one executable. | [hello.xps](../demo/console/hello.xps) |'
        if runtime_row in updated and single_row not in updated:
            updated = updated.replace(runtime_row, runtime_row + '\n' + single_row)

    if path.as_posix().endswith('src/XPScript.Cli/Program.cs'):
        updated = updated.replace(
            'xpscript compile <source.xps> [-o output] [--platform RID] [--runtime=false] [--result-format text|json|xml]',
            'xpscript compile <source.xps> [-o output] [--platform RID|--rid RID] [--single-file true|false] [--runtime true|false] [--result-format text|json|xml]')
        updated = updated.replace(
            'xpscript dependencies <source.xps> [--platform RID] [--json]',
            'xpscript dependencies <source.xps> [--platform RID|--rid RID] [--json]')
        updated = updated.replace(
            'xpscript security <source.xps> [--platform RID] [--json]',
            'xpscript security <source.xps> [--platform RID|--rid RID] [--json]')
        updated = updated.replace(
            'xpscript run <source.xps> [--platform RID] [--restricted]',
            'xpscript run <source.xps> [--platform RID|--rid RID] [--restricted]')
        updated = updated.replace(
            'xpscript <source.xps> [-o output] [--platform RID] [compiler options...]',
            'xpscript <source.xps> [-o output] [--platform RID|--rid RID] [--single-file true|false] [--runtime true|false] [compiler options...]')

    if updated != text:
        path.write_text(updated, encoding='utf-8')

validator = ROOT / 'scripts' / 'validate-docs-demos.ps1'
text = validator.read_text(encoding='utf-8')
text = text.replace(
    "'xpscriptc', 'run', '-o', '--runtime', '--runtime=false', '--result-format',",
    "'xpscriptc', 'run', '-o', '--runtime', '--single-file', '--platform', '--rid', '--result-format',")
validator.write_text(text, encoding='utf-8')

legacy_guard = ROOT / 'scripts' / 'validate-no-legacy-cli-options.ps1'
legacy_guard.write_text(r'''$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$legacy = @(
    '--framework' + '-dependent',
    '--runtime ' + 'RID',
    '--runtime ' + 'win-x64',
    '--runtime ' + 'win-arm64',
    '--runtime ' + 'linux-x64',
    '--runtime ' + 'linux-arm64',
    '--runtime ' + 'osx-x64',
    '--runtime ' + 'osx-arm64'
)
$extensions = @('.md','.cs','.ps1','.yml','.yaml','.html','.txt')
$failures = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $extensions -contains $_.Extension.ToLowerInvariant() -and
    $_.FullName -notmatch '[\\/](\.git|node_modules|bin|obj)[\\/]' -and
    $_.FullName -notlike '*validate-no-legacy-cli-options.ps1'
} | ForEach-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($pattern in $legacy) {
        if ($content.Contains($pattern, [StringComparison]::OrdinalIgnoreCase)) {
            $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
            $failures.Add("$relative contains removed syntax: $pattern")
        }
    }
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "ERROR: $_" }
    throw "Removed legacy CLI syntax remains in $($failures.Count) location(s)."
}
Write-Host 'LEGACY-CLI-SYNTAX=NONE'
''', encoding='utf-8')

compile_workflow = ROOT / '.github' / 'workflows' / 'compile.yml'
text = compile_workflow.read_text(encoding='utf-8')
needle = "      - name: Validate direct package notices\n        shell: pwsh\n        run: ./scripts/validate-license-notices.ps1"
replacement = needle + "\n      - name: Validate removed CLI syntax\n        shell: pwsh\n        run: ./scripts/validate-no-legacy-cli-options.ps1"
if 'Validate removed CLI syntax' not in text and needle in text:
    text = text.replace(needle, replacement)
    compile_workflow.write_text(text, encoding='utf-8')
