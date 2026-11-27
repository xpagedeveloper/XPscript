param(
    [string]$BaseRef = ''
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    $BaseRef = 'HEAD^'
}

$docRoot = 'docs'
if (-not (Test-Path -LiteralPath $docRoot -PathType Container)) {
    throw "$docRoot is missing."
}

function Get-ChangedDeclarationKeys {
    param([string]$Base)

    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $removedKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $files = @(git diff --name-only $Base HEAD -- 'src/XPScript.Compiler/*.cs' 'src/XPScript.Compiler/**/*.cs')
    if ($LASTEXITCODE -ne 0) { throw "git diff failed for base '$Base'." }

    foreach ($file in $files) {
        if ([string]::IsNullOrWhiteSpace($file) -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }

        $lines = Get-Content -LiteralPath $file
        $baseText = @(git show "${Base}:$file" 2>$null)
        $diff = @(git diff --unified=0 $Base HEAD -- $file)
        if ($LASTEXITCODE -ne 0) { throw "git diff failed for '$file'." }

        $newLine = 0
        foreach ($line in $diff) {
            if ($line -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@') {
                $newLine = [int]$matches[1]
                continue
            }

            if ($line.StartsWith('+++') -or $line.StartsWith('---')) { continue }

            if ($line.StartsWith('+')) {
                $text = $line.Substring(1)
                $lineNumber = $newLine
                $newLine++

                $classMatch = [regex]::Match($text, '\bclass\s+XPScript([A-Za-z_][A-Za-z0-9_]*)\b')
                if ($classMatch.Success) {
                    $className = $classMatch.Groups[1].Value
                    if ($className -in @('Transpiler', 'JsonSchemaTypeGenerator')) { continue }
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

                if ($owner -and $owner -notin @('Transpiler', 'NativeJson', 'JsonSchemaTypeGenerator')) {
                    [void]$keys.Add("$owner.$member")
                }
                continue
            }

            if (-not $line.StartsWith('-')) {
                $newLine++
            }
        }

        $baseOwner = $null
        foreach ($baseLine in $baseText) {
            $ownerMatch = [regex]::Match($baseLine, '\bclass\s+XPScript([A-Za-z_][A-Za-z0-9_]*)\b')
            if ($ownerMatch.Success) {
                $baseOwner = $ownerMatch.Groups[1].Value
                if ($baseOwner -notin @('Transpiler', 'NativeJson', 'JsonSchemaTypeGenerator')) {
                    [void]$removedKeys.Add($baseOwner)
                }
                continue
            }
            if (-not $baseOwner -or $baseOwner -in @('Transpiler', 'NativeJson', 'JsonSchemaTypeGenerator')) { continue }
            if ($baseLine -notmatch '^\s*public\s+') { continue }
            $memberMatch = [regex]::Match($baseLine, '^\s*public\s+(?:(?:static|virtual|override|sealed|async|required|readonly|partial|new)\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,?.\[\]\s:]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\{|=>)')
            if ($memberMatch.Success -and $memberMatch.Groups[1].Value -notlike 'XPScript*') {
                [void]$removedKeys.Add("$baseOwner.$($memberMatch.Groups[1].Value)")
            }
        }
    }

    foreach ($existingKey in $removedKeys) {
        [void]$keys.Remove($existingKey)
    }
    return @($keys | Sort-Object)
}

$changedKeys = @(Get-ChangedDeclarationKeys -Base $BaseRef)
if ($changedKeys.Count -eq 0) {
    Write-Host 'RUNTIME-API-DOC-SYNC=NO-CHANGES'
    exit 0
}

$docDiff = @(
    git diff --unified=0 $BaseRef HEAD -- 'docs/*.md' 'docs/**/*.md'
    git diff --unified=0 -- 'docs/*.md' 'docs/**/*.md'
    git diff --cached --unified=0 -- 'docs/*.md' 'docs/**/*.md'
)
if ($LASTEXITCODE -ne 0) { throw "git diff failed for documentation under $docRoot." }

$addedDocRows = @(
    $docDiff |
        Where-Object { $_.StartsWith('+|') -and -not $_.StartsWith('+++') } |
        ForEach-Object { $_.Substring(1) }
)

$errors = [System.Collections.Generic.List[string]]::new()
$currentDoc = (Get-ChildItem -LiteralPath $docRoot -Filter '*.md' -File -Recurse |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$baseDoc = @(git grep -h -E '^\||^#{1,6} ' $BaseRef -- 'docs/*.md' 'docs/**/*.md') -join "`n"
if ($LASTEXITCODE -gt 1) { throw "git grep failed for documentation under $docRoot at '$BaseRef'." }
$unmappedOwners = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$checkedKeys = 0

function Get-DocumentationOwner {
    param([string]$Owner)

    # The compiler has public CLR helpers emitted into generated apps that are
    # not XPscript language symbols. Only track declarations mapped to docs.
    $aliases = @{
        Ai = 'XPAi'; AiTool = 'XPAITool'; HttpClient = 'XPHttpClient'; HttpRequest = 'XPHttpRequest'
        HttpResponse = 'XPHttpResponse'; CsvDocument = 'XPCsvDocument'
        ApplicationRuntime = 'Application'; ApplicationExecutableRuntime = 'Application.Executable'
        ProcessRuntime = 'Process'; SessionRuntime = 'Session'; RequestRuntime = 'Request'
        UIField = 'UIForm'; UIFieldRegion = 'UIForm'; UIFormEvent = 'UIForm'; UIComponent = 'UIForm'
        UIButton = 'UIForm'; UIGrid = 'UIForm'; UITab = 'UIForm'; UITabContainer = 'UIForm'
    }
    if ($Owner -in @('CompilerCommandLine', 'FileSystemRuntime', 'Location', 'Camera', 'Notes', 'JsonObject', 'JsonArray', 'JsonElement')) {
        return $null
    }
    if ($aliases.ContainsKey($Owner)) { return $aliases[$Owner] }
    if ($Owner.EndsWith('Runtime', [StringComparison]::OrdinalIgnoreCase)) {
        return $Owner.Substring(0, $Owner.Length - 'Runtime'.Length)
    }
    return $Owner
}

foreach ($key in $changedKeys) {
    $parts = $key.Split('.', 2)
    $owner = $parts[0]
    $member = if ($parts.Count -gt 1) { $parts[1] } else { $null }

    $documentationOwner = Get-DocumentationOwner $owner
    if ([string]::IsNullOrWhiteSpace($documentationOwner)) {
        [void]$unmappedOwners.Add($owner)
        continue
    }
    $ownerPattern = [regex]::Escape($documentationOwner)
    $ownerIsDocumented = $currentDoc -match "(?im)^#{1,6}\s+.*\b$ownerPattern\b" -or
        $currentDoc -match "(?im)^\|\s*\x60(?:$ownerPattern|$ownerPattern\.[^\x60]+)\x60\s*\|"
    if (-not $ownerIsDocumented) {
        [void]$unmappedOwners.Add($owner)
        continue
    }
    $checkedKeys++

    $candidates = if ($member) {
        @("$documentationOwner.$member", $ownerPattern + '\.' + [regex]::Escape($member), $member)
    } else {
        @($documentationOwner, $owner)
    }

    $currentMatch = $false
    $changedMatch = $false
    $baseMatch = $false

    foreach ($candidate in $candidates) {
        $escaped = [regex]::Escape($candidate)
        $memberName = [regex]::Escape(($candidate -split '\.')[-1])
        $rowPattern = "(?im)^\|\s*\x60(?:$escaped|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*\.$memberName)\x60\s*\|"
        if ($currentDoc -match $rowPattern) {
            $currentMatch = $true
        }
        if ($baseDoc -match $rowPattern) {
            $baseMatch = $true
        }
        if ($addedDocRows | Where-Object { $_ -match $rowPattern }) {
            $changedMatch = $true
        }
    }

    if (-not $member) {
        $ownerRowPattern = "(?im)^\|\s*\x60$ownerPattern(?:\.[^\x60]+)?\x60\s*\|"
        if ($currentDoc -match $ownerRowPattern) { $currentMatch = $true }
        if ($baseDoc -match $ownerRowPattern) { $baseMatch = $true }
        if ($addedDocRows | Where-Object { $_ -match $ownerRowPattern }) { $changedMatch = $true }
    }

    if (-not $currentMatch) {
        $errors.Add("Runtime API declaration '$key' is not represented in a documentation table under $docRoot/.")
        continue
    }

    if (-not $baseMatch -and -not $changedMatch) {
        $errors.Add("New runtime API declaration '$key' is missing a documentation table row under $docRoot/.")
    }
}

if ($errors.Count -gt 0) {
    foreach ($message in $errors) { Write-Host "ERROR: $message" }
    throw "Runtime API/documentation synchronization failed with $($errors.Count) error(s)."
}

Write-Host "RUNTIME-API-DECLARATIONS-CHECKED=$checkedKeys"
Write-Host "RUNTIME-API-IMPLEMENTATION-DECLARATIONS-SKIPPED=$($unmappedOwners.Count)"
Write-Host 'RUNTIME-API-DOC-SYNC=OK'
