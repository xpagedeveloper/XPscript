param(
    [string]$BaseRef = ''
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    $BaseRef = 'HEAD^'
}

$docPath = 'docs/intellisense-api-reference.md'
if (-not (Test-Path -LiteralPath $docPath -PathType Leaf)) {
    throw "$docPath is missing."
}

function Get-ChangedDeclarationKeys {
    param([string]$Base)

    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $files = @(git diff --name-only $Base HEAD -- 'src/XPScript.Compiler/*.cs' 'src/XPScript.Compiler/**/*.cs')
    if ($LASTEXITCODE -ne 0) { throw "git diff failed for base '$Base'." }

    foreach ($file in $files) {
        if ([string]::IsNullOrWhiteSpace($file) -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }

        $lines = Get-Content -LiteralPath $file
        $diff = @(git diff --unified=0 $Base HEAD -- $file)
        if ($LASTEXITCODE -ne 0) { throw "git diff failed for '$file'." }

        $newLine = 0
        foreach ($line in $diff) {
            if ($line -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@') {
                $newLine = [int]$matches[1]
                continue
            }

            if ($line.StartsWith('+++')) { continue }

            if ($line.StartsWith('+')) {
                $text = $line.Substring(1)
                $lineNumber = $newLine
                $newLine++

                $classMatch = [regex]::Match($text, '\bclass\s+XPScript([A-Za-z_][A-Za-z0-9_]*)\b')
                if ($classMatch.Success) {
                    $className = $classMatch.Groups[1].Value
                    # Compiler infrastructure such as XPScriptTranspiler is not an XPscript runtime API.
                    if ($className -eq 'Transpiler') { continue }
                    [void]$keys.Add($className)
                    continue
                }

                if ($text -notmatch '^\s*public\s+') { continue }

                $memberMatch = [regex]::Match(
                    $text,
                    '^\s*public\s+(?:(?:static|virtual|override|sealed|async|required|readonly|partial|new)\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,?.\[\]\s:]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\{|=>)'
                )
                if (-not $memberMatch.Success) { continue }

                $member = $memberMatch.Groups[1].Value
                if ($member -like 'XPScript*') { continue }

                $owner = $null
                for ($i = [Math]::Min($lineNumber - 1, $lines.Count - 1); $i -ge 0; $i--) {
                    $ownerMatch = [regex]::Match($lines[$i], '\bclass\s+XPScript([A-Za-z_][A-Za-z0-9_]*)\b')
                    if ($ownerMatch.Success) {
                        $owner = $ownerMatch.Groups[1].Value
                        break
                    }
                }

                if ($owner -and $owner -ne 'Transpiler') {
                    [void]$keys.Add("$owner.$member")
                }
                continue
            }

            if (-not $line.StartsWith('-')) {
                $newLine++
            }
        }
    }

    return @($keys | Sort-Object)
}

$changedKeys = @(Get-ChangedDeclarationKeys -Base $BaseRef)
if ($changedKeys.Count -eq 0) {
    Write-Host 'RUNTIME-API-DOC-SYNC=NO-CHANGES'
    exit 0
}

$docDiff = @(git diff --unified=0 $BaseRef HEAD -- $docPath)
if ($LASTEXITCODE -ne 0) { throw "git diff failed for $docPath." }

$addedDocRows = @(
    $docDiff |
        Where-Object { $_.StartsWith('+|') -and -not $_.StartsWith('+++') } |
        ForEach-Object { $_.Substring(1) }
)

$errors = [System.Collections.Generic.List[string]]::new()
$currentDoc = Get-Content -LiteralPath $docPath -Raw

foreach ($key in $changedKeys) {
    $parts = $key.Split('.', 2)
    $owner = $parts[0]
    $member = if ($parts.Count -gt 1) { $parts[1] } else { $null }

    $candidates = if ($member) {
        @("$owner.$member", $member)
    } else {
        @($owner)
    }

    $currentMatch = $false
    $changedMatch = $false

    foreach ($candidate in $candidates) {
        $escaped = [regex]::Escape($candidate)
        if ($currentDoc -match "(?im)^\|\s*\x60$escaped\x60\s*\|") {
            $currentMatch = $true
        }
        if ($addedDocRows | Where-Object { $_ -match "(?i)^\|\s*\x60$escaped\x60\s*\|" }) {
            $changedMatch = $true
        }
    }

    if (-not $currentMatch) {
        $errors.Add("Runtime API declaration '$key' is not represented in $docPath.")
        continue
    }

    if (-not $changedMatch) {
        $errors.Add("Runtime API declaration '$key' changed, but its machine-readable row was not added or updated in the same change.")
    }
}

if ($errors.Count -gt 0) {
    foreach ($message in $errors) { Write-Host "ERROR: $message" }
    throw "Runtime API/documentation synchronization failed with $($errors.Count) error(s)."
}

Write-Host "RUNTIME-API-DECLARATIONS-CHECKED=$($changedKeys.Count)"
Write-Host 'RUNTIME-API-DOC-SYNC=OK'
