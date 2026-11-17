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

$ast = @(& dotnet $compiler ast-compile $source -o (Join-Path $root 'out/diagnostic-compat-ast') 2>&1)
$astExit = $LASTEXITCODE
if ($astExit -ne 2) { throw "Expected AST to reject the fixture. ast=$astExit" }
$astIdentity = Get-DiagnosticIdentity $ast 'ast'
if ($astIdentity.Code -ne 'XPS1012' -or $astIdentity.Line -ne 2 -or $astIdentity.Column -ne 25) {
    throw "AST array rank diagnostic changed: $($astIdentity.Code):$($astIdentity.Line):$($astIdentity.Column)"
}
Write-Host "AST_DIAGNOSTIC=OK code=$($astIdentity.Code) line=$($astIdentity.Line) column=$($astIdentity.Column)"

$astJsonText = (& dotnet $compiler ast-compile $source -o (Join-Path $root 'out/diagnostic-compat-ast-json') --result-format json 2>&1 | Out-String)
$astJson = $astJsonText.Substring($astJsonText.IndexOf('{')) | ConvertFrom-Json
$astError = $astJson.errors[0]
if ([System.IO.Path]::GetFileName($astError.file) -ne [System.IO.Path]::GetFileName($source) -or $astError.line -ne $astIdentity.Line -or $astError.diagnosticCode -ne $astIdentity.Code) {
    throw "Structured AST diagnostic mismatch: $($astError.file):$($astError.line):$($astError.diagnosticCode)"
}
Write-Host "AST_DIAGNOSTIC_STRUCTURED=OK category=$($astError.category)"

# AST capability improvements are allowed when the published legacy compiler
# rejects the same source. The reverse direction is not allowed and is covered
# by the legacy/AST compile-and-runtime parity harness.
$astOnlySource = Join-Path $root 'samples/null-integer-assignment-error.xps'
$astOnlyOutput = Join-Path $root 'out/diagnostic-ast-only'
$astOnly = @(& dotnet $compiler ast-compile $astOnlySource -o $astOnlyOutput 2>&1)
$astOnlyExit = $LASTEXITCODE
if ($astOnlyExit -ne 0 -or -not (Test-Path (Join-Path $astOnlyOutput 'Generated.dll'))) {
    throw "An AST-only capability must be allowed. ast=$astOnlyExit`n$($astOnly -join "`n")"
}
Write-Host 'AST_ONLY_CAPABILITY_ALLOWED=OK'
