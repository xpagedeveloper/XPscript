param(
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$compiler = if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
} else {
    [System.IO.Path]::GetFullPath($CompilerPath)
}
if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler DLL not found: $compiler" }
$source = Join-Path $root 'tests/ast-compile-probe/constructor-array-rank-error.xps'

function Get-DiagnosticIdentity([string[]] $lines, [string] $mode) {
    $text = $lines -join "`n"
    if ($mode -eq 'legacy') {
        $match = [regex]::Match($text, 'diagnosticCode:\s*(?<code>XPS\d+)')
        $location = [regex]::Match($text, '(?m)^\s+line:\s+(?<line>\d+)\s*$')
        $position = [regex]::Match($text, '(?m)^\s+position:\s+(?<column>\d+)\s*$')
    }
    else {
        $match = [regex]::Match($text, '(?m)^\S+\.xps:(?<line>\d+):(?<column>\d+):\s*(?<code>XPS\d+):')
        $location = $match
        $position = $match
    }
    if (-not $match.Success -or -not $location.Success -or -not $position.Success) {
        throw "Could not parse $mode diagnostic:`n$text"
    }
    return [pscustomobject]@{
        Code = $match.Groups['code'].Value
        Line = [int]$location.Groups['line'].Value
        Column = [int]$position.Groups['column'].Value
    }
}

$legacy = @(& dotnet $compiler legacy-compile $source -o (Join-Path $root 'out/diagnostic-compat-legacy') --runtime=false 2>&1)
$legacyExit = $LASTEXITCODE
$ast = @(& dotnet $compiler ast-compile $source -o (Join-Path $root 'out/diagnostic-compat-ast') 2>&1)
$astExit = $LASTEXITCODE
if ($legacyExit -ne 2 -or $astExit -ne 2) { throw "Expected both compilers to reject the fixture. legacy=$legacyExit ast=$astExit" }

$legacyIdentity = Get-DiagnosticIdentity $legacy 'legacy'
$astIdentity = Get-DiagnosticIdentity $ast 'ast'
if ($legacyIdentity.Code -ne $astIdentity.Code -or $legacyIdentity.Line -ne $astIdentity.Line) {
    throw "Legacy/AST diagnostic identity mismatch: legacy=$($legacyIdentity.Code):$($legacyIdentity.Line) AST=$($astIdentity.Code):$($astIdentity.Line)"
}

Write-Host "AST_DIAGNOSTIC_COMPAT=OK code=$($astIdentity.Code) line=$($astIdentity.Line) legacyColumn=$($legacyIdentity.Column) astColumn=$($astIdentity.Column)"

$legacyJsonText = (& dotnet $compiler legacy-compile $source -o (Join-Path $root 'out/diagnostic-compat-legacy-json') --runtime=false --result-format json 2>&1 | Out-String)
$astJsonText = (& dotnet $compiler ast-compile $source -o (Join-Path $root 'out/diagnostic-compat-ast-json') --result-format json 2>&1 | Out-String)
$legacyJson = $legacyJsonText.Substring($legacyJsonText.IndexOf('{')) | ConvertFrom-Json
$astJson = $astJsonText.Substring($astJsonText.IndexOf('{')) | ConvertFrom-Json
$legacyError = $legacyJson.errors[0]
$astError = $astJson.errors[0]
if ($legacyError.file -ne $astError.file -or $legacyError.line -ne $astError.line -or
    $legacyError.diagnosticCode -ne $astError.diagnosticCode -or $legacyError.category -ne $astError.category) {
    throw "Structured legacy/AST diagnostic mismatch: legacy=$($legacyError.file):$($legacyError.line):$($legacyError.diagnosticCode):$($legacyError.category) AST=$($astError.file):$($astError.line):$($astError.diagnosticCode):$($astError.category)"
}
Write-Host "AST_DIAGNOSTIC_STRUCTURED_COMPAT=OK category=$($astError.category) columnDelta=$([int]$astError.position - [int]$legacyError.position)"

# AST capability improvements are allowed when the published legacy compiler
# rejects the same source. The reverse direction is not allowed and is covered
# by the legacy/AST compile-and-runtime parity harness.
$astOnlySource = Join-Path $root 'samples/null-integer-assignment-error.xps'
$astOnlyLegacy = @(& dotnet $compiler legacy-compile $astOnlySource -o (Join-Path $root 'out/diagnostic-ast-only-legacy') --runtime=false 2>&1)
$astOnlyLegacyExit = $LASTEXITCODE
$astOnlyOutput = Join-Path $root 'out/diagnostic-ast-only'
$astOnly = @(& dotnet $compiler ast-compile $astOnlySource -o $astOnlyOutput 2>&1)
$astOnlyExit = $LASTEXITCODE
if ($astOnlyLegacyExit -eq 0 -or $astOnlyExit -ne 0 -or -not (Test-Path (Join-Path $astOnlyOutput 'Generated.dll'))) {
    throw "An AST-only capability must be allowed when legacy rejects it. legacy=$astOnlyLegacyExit ast=$astOnlyExit`n$($astOnly -join "`n")"
}
Write-Host 'AST_ONLY_CAPABILITY_ALLOWED=OK'
