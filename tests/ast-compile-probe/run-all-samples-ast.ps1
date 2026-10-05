$ErrorActionPreference = 'Continue'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$samples = Join-Path $root 'samples'
$output = Join-Path $root 'out/ast-samples'
$report = Join-Path $output 'report.tsv'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem $samples -Recurse -Filter '*.xps' | Sort-Object FullName) {
    $relative = $file.FullName.Substring($samples.Length).TrimStart('\','/')
    $safe = ($relative -replace '[\\/: ]', '_') -replace '\.xps$', ''
    $target = Join-Path $output $safe
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $message = (& dotnet $compiler ast-compile $file.FullName -o $target 2>&1 | Out-String).Trim()
    $status = if ($LASTEXITCODE -eq 0) { 'OK' } else { 'FAIL'; $failures.Add($relative) }
    Add-Content -LiteralPath $report -Value ($status + "`t" + $relative + "`t" + ($message -replace "`r?`n", ' | '))
    Write-Host "$status $relative"
}
if ($failures.Count -gt 0) { throw "$($failures.Count) sample files failed AST compilation. See $report" }
Write-Host "All sample files compiled through the AST compiler."


