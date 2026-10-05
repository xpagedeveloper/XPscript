$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$language = Join-Path $root 'docs/language-reference.md'
$compiler = Join-Path $root 'src/XPScript.Compiler/AstExperimentalCompiler.cs'

if (-not (Test-Path $language) -or -not (Test-Path $compiler)) {
    throw 'Language reference or AST compiler source is missing.'
}

# The language reference is the authoritative list of documented callable built-ins.
# Statements, operators, and object members are intentionally excluded here.
$documented = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content $language) {
    # Callable rows have a parenthesized invocation in the Syntax column.
    if ($line -match '^\| `([^`]+)` \| `[^|]*\(') {
        $name = $Matches[1]
        if ($name -notmatch '[/ ]' -and $name -notmatch '\.') { [void]$documented.Add($name) }
    }
}

$implemented = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content $compiler) {
    if ($line -match 'new FunctionSymbol\("([^"]+)"') { [void]$implemented.Add($Matches[1]) }
}

$missing = @($documented | Where-Object { -not $implemented.Contains($_) } | Sort-Object)
$covered = @($documented | Where-Object { $implemented.Contains($_) } | Sort-Object)

[pscustomobject]@{
    documentedCallableBuiltins = $documented.Count
    astBoundCallableBuiltins = $covered.Count
    missingCallableBuiltins = $missing
    missingCount = $missing.Count
} | ConvertTo-Json -Depth 4

if ($missing.Count -gt 0) { exit 1 }
