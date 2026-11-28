$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'string-number-array-500.xps'
$resultRoot = Join-Path $root 'out/cli-compiler-benchmark'
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$env:XPSCRIPT_TEMP_ROOT = Join-Path $resultRoot 'temp'
New-Item -ItemType Directory -Force -Path $env:XPSCRIPT_TEMP_ROOT | Out-Null

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('Function AddNumber(ByVal value As Long) As Long')
$lines.Add('    AddNumber = value + 7')
$lines.Add('End Function')
$lines.Add('Function AddText(ByVal value As String) As String')
$lines.Add('    AddText = value & "-x"')
$lines.Add('End Function')
$lines.Add('Function ReadArray(ByRef values() As Long, ByVal index As Long) As Long')
$lines.Add('    ReadArray = values(index)')
$lines.Add('End Function')
$lines.Add('Sub Main()')
$lines.Add('    Dim number As Long')
$lines.Add('    number = 0')
$lines.Add('    Dim text As String')
$lines.Add('    text = "start"')
$lines.Add('    Dim values(2) As Long')
for ($i = 0; $i -lt 160; $i++) {
    $lines.Add("    number = AddNumber(number)")
    $lines.Add("    text = AddText(text)")
    $lines.Add("    values(0) = number")
}
$lines.Add('    If number <= 0 Then Error 5, "number function failed"')
$lines.Add('    If text = "" Then Error 5, "string function failed"')
$lines.Add('    If ReadArray(values, 0) <> number Then Error 5, "array function failed"')
$lines.Add('    Print "CLI_BENCHMARK_OK"')
$lines.Add('End Sub')
while ($lines.Count -lt 500) { $lines.Add("' filler line $($lines.Count + 1)") }
if ($lines.Count -ne 500) { throw "Benchmark source has $($lines.Count) lines, expected 500." }
Set-Content -LiteralPath $source -Value $lines -Encoding utf8
Write-Host "CLI_BENCHMARK_SOURCE_LINES=$($lines.Count)"

function Invoke-AstCompile([string] $output) {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $outputText = @(& dotnet $compiler ast-compile $source --output $output --runtime=false --single-file=false --rid win-x64 --security=off 2>&1)
    $timer.Stop()
    $exitCode = $LASTEXITCODE
    $outputText | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "ast-compile failed with exit code $exitCode" }
    $outputItem = Get-Item -LiteralPath $output -ErrorAction SilentlyContinue
    $artifact = if ($outputItem -and -not $outputItem.PSIsContainer) {
        $outputItem
    } else {
        $generated = Get-ChildItem -LiteralPath $output -Recurse -File -Filter 'Generated.dll' | Select-Object -First 1
        if ($generated) { $generated } else {
            Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.exe' | Select-Object -First 1
        }
    }
    if ($null -eq $artifact) { throw "ast-compile produced no executable artifact." }
    $totalBytes = if ($outputItem -and $outputItem.PSIsContainer) {
        (Get-ChildItem -LiteralPath $output -Recurse -File | Measure-Object -Property Length -Sum).Sum
    } else { $artifact.Length }
    [pscustomobject]@{ Mode = 'ast-compile'; Seconds = [math]::Round($timer.Elapsed.TotalSeconds, 3); Bytes = $artifact.Length; TotalBytes = $totalBytes; Artifact = $artifact.FullName }
}

$ast = Invoke-AstCompile (Join-Path $resultRoot 'ast')
$astCached = Invoke-AstCompile (Join-Path $resultRoot 'ast-second')
$ast | Format-List
Write-Host "CLI_BENCHMARK_AST_SECONDS=$($ast.Seconds)"
Write-Host "CLI_BENCHMARK_AST_BYTES=$($ast.Bytes)"
Write-Host "CLI_BENCHMARK_AST_TOTAL_BYTES=$($ast.TotalBytes)"
Write-Host "CLI_BENCHMARK_AST_SECOND_SECONDS=$($astCached.Seconds)"
Write-Host "CLI_BENCHMARK_AST_SECOND_BYTES=$($astCached.Bytes)"
