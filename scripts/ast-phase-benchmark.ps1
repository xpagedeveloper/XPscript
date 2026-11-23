[CmdletBinding()]
param(
    [int]$Iterations = 5,
    [string]$Source = "samples/hello.xps",
    [string]$OutputRoot = "out/ast-phase-benchmark"
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo
$sourcePath = (Resolve-Path $Source).Path
$env:XPSCRIPT_TEMP_ROOT = (Join-Path $repo 'out/ast-phase-temp')
New-Item -ItemType Directory -Force -Path $env:XPSCRIPT_TEMP_ROOT | Out-Null
$rows = @()

for ($i = 1; $i -le $Iterations; $i++) {
    $output = Join-Path $OutputRoot ("run-{0}" -f $i)
    if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    $env:XPSCRIPT_AST_PHASE_TIMINGS = '1'
    $text = & dotnet run --project src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- ast-compile $sourcePath -o $output --runtime=true 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "AST benchmark iteration $i failed.`n$text" }
    $line = $text -split "`r?`n" | Where-Object { $_ -like 'AST_PHASE_TIMINGS=*' } | Select-Object -Last 1
    if (-not $line) { throw "AST benchmark iteration $i produced no AST_PHASE_TIMINGS marker.`n$text" }
    $rows += ($line.Substring('AST_PHASE_TIMINGS='.Length) | ConvertFrom-Json)
}

Remove-Item Env:XPSCRIPT_AST_PHASE_TIMINGS -ErrorAction SilentlyContinue
$properties = @('preprocessing','persistent_cache','lexing_parsing','binding','emission','roslyn_build','total_ms')
Write-Output ("AST_PHASE_BENCHMARK iterations={0} source={1}" -f $Iterations, (Split-Path $sourcePath -Leaf))
foreach ($property in $properties) {
    $values = @($rows | ForEach-Object { [double]($_.$property) } | Sort-Object)
    if ($values.Count -eq 0) { continue }
    $median = $values[[int][Math]::Floor(($values.Count - 1) / 2)]
    Write-Output ("AST_PHASE_BENCHMARK phase={0} median_ms={1:N3} min_ms={2:N3} max_ms={3:N3}" -f $property, $median, $values[0], $values[-1])
}
