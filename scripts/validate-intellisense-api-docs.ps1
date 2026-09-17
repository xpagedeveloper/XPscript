$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$file = Join-Path $root 'docs/intellisense-api-reference.md'
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
    throw 'docs/intellisense-api-reference.md is missing.'
}

$lines = Get-Content -LiteralPath $file
$rows = $lines | Where-Object { $_ -match '^\| `' }
foreach ($row in $rows) {
    $pipeCount = ([regex]::Matches($row, '\|')).Count
    if ($pipeCount -ne 6) {
        $errors.Add("IntelliSense API row must contain exactly five columns: $row")
        continue
    }
    if ($row -notmatch '\[[^\]]+\.xps\]\(\.\./(?:samples|demo)/[^)]+\.xps\)') {
        $errors.Add("IntelliSense API row is missing an executable .xps example: $row")
    }
}

$content = Get-Content -LiteralPath $file -Raw
if ($content -match '(?i)\bEvaluate\b') {
    $errors.Add('Machine-readable IntelliSense API reference still exposes removed Evaluate support.')
}

$required = @(
    'NotesSession.HashPassword',
    'NotesSession.VerifyPassword',
    'NotesMIMEEntity',
    'NotesMIMEEntity.ContentType',
    'NotesMIMEEntity.CreateChildEntity',
    'NotesMIMEEntity.GetNthHeader',
    'SystemInventory',
    'SystemInventory.GetSnapshot',
    'SystemInventory.GetSystemInfo',
    'SystemInventory.GetInstalledApplications',
    'SystemInventory.IsSoftwareInstalled',
    'SystemInventorySystemInfo.MachineName',
    'SystemInventoryCpuInfo.Name',
    'SystemInventoryMemoryInfo.TotalPhysicalMemory',
    'InstalledSoftwareInfo.Name'
)
foreach ($name in $required) {
    if ($content -notmatch [regex]::Escape("``$name``")) {
        $errors.Add("Machine-readable IntelliSense API reference is missing required member: $name")
    }
}

$links = [regex]::Matches($content, '\[[^\]]+\.xps\]\((\.\./(?:samples|demo)/[^)]+\.xps)\)')
foreach ($match in $links) {
    $relative = $match.Groups[1].Value.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $file) $relative))
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        $errors.Add("IntelliSense API example does not exist: $($match.Groups[1].Value)")
    }
}

if ($errors.Count -gt 0) {
    foreach ($message in $errors) { Write-Host "ERROR: $message" }
    throw "IntelliSense API documentation validation failed with $($errors.Count) error(s)."
}

Write-Host "INTELLISENSE-API-ROWS=$($rows.Count)"
Write-Host 'INTELLISENSE-API-DOCS=OK'
