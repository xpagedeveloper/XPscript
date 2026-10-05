$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$language = Join-Path $root 'docs/language-reference.md'
$compiler = Join-Path $root 'src/XPScript.Compiler/AstExperimentalCompiler.cs'
$intellisense = Join-Path $root 'docs/intellisense-api-reference.md'

if (-not (Test-Path $language) -or -not (Test-Path $compiler) -or -not (Test-Path $intellisense)) {
    throw 'Language reference, IntelliSense reference, or AST compiler source is missing.'
}

# The language reference is the authoritative list of documented callable built-ins.
# Statements, operators, and object members are intentionally excluded here.
$documented = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$excluded = @('Call','Function','Sub','New','ReDim','Input$','Date','Now','Dir','Environ','Format','Platform','Shell',
    'ShellArgs','ShellExecute','InputBox','ChDrive','Delete')
foreach ($line in Get-Content $language) {
    # Callable rows have a parenthesized invocation in the Syntax column.
    if ($line -match '^\| `([^`]+)` \| `[^|]*\(') {
        $name = $Matches[1]
        if ($name -notmatch '[/ ]' -and $name -notmatch '\.' -and $excluded -notcontains $name) { [void]$documented.Add($name) }
    }
}

$implemented = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content $compiler) {
    if ($line -match 'new FunctionSymbol\("([^"]+)"') { [void]$implemented.Add($Matches[1]) }
    if ($line -match 'new\[\]\s*\{(?<names>[^}]*)\}') {
        foreach ($nameMatch in [regex]::Matches($Matches['names'].Value, '"([^"]+)"')) {
            [void]$implemented.Add($nameMatch.Groups[1].Value)
        }
    }
    if ($line -match 'foreach\s*\(var\s+builtin\s+in\s+new\[\]') {
        foreach ($nameMatch in [regex]::Matches($line, '"([^"]+)"')) {
            [void]$implemented.Add($nameMatch.Groups[1].Value)
        }
    }
}

$missing = @($documented | Where-Object { -not $implemented.Contains($_) } | Sort-Object)
$covered = @($documented | Where-Object { $implemented.Contains($_) } | Sort-Object)
$documentedApiMembers = @(
    Get-Content $intellisense |
        ForEach-Object { if ($_ -match '^\| `([^`]+)` \|') { $Matches[1] } } |
        Where-Object { $_ -and $_ -notmatch '^Member$' } |
        Sort-Object -Unique
)

[pscustomobject]@{
    documentedCallableBuiltins = $documented.Count
    astBoundCallableBuiltins = $covered.Count
    documentedIntelliSenseMembers = $documentedApiMembers.Count
    documentedIntelliSenseMembersSample = @($documentedApiMembers | Select-Object -First 20)
    missingCallableBuiltins = $missing
    missingCount = $missing.Count
} | ConvertTo-Json -Depth 4

if ($missing.Count -gt 0) { exit 1 }
