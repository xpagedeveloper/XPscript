$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'string-number-array-500.xps'
$resultRoot = Join-Path $root 'out/cli-compiler-benchmark'
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null

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

function Invoke-Compile([string] $mode, [string] $output) {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $outputText = @(& dotnet $compiler $mode $source --output $output --runtime=true --single-file=false --rid win-x64 --security=off 2>&1)
    $timer.Stop()
    $exitCode = $LASTEXITCODE
    $outputText | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "$mode failed with exit code $exitCode" }
    $artifact = Get-ChildItem -LiteralPath $output -Recurse -File |
        Where-Object { $_.Extension -in @('.exe', '.dll') } |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if ($null -eq $artifact) { throw "$mode produced no executable artifact." }
    [pscustomobject]@{ Mode = $mode; Seconds = [math]::Round($timer.Elapsed.TotalSeconds, 3); Bytes = $artifact.Length; Artifact = $artifact.FullName }
}

$legacy = Invoke-Compile 'legacy-compile' (Join-Path $resultRoot 'legacy')
$ast = Invoke-Compile 'ast-compile' (Join-Path $resultRoot 'ast')
$legacy | Format-List
$ast | Format-List
Write-Host "CLI_BENCHMARK_LEGACY_SECONDS=$($legacy.Seconds)"
Write-Host "CLI_BENCHMARK_AST_SECONDS=$($ast.Seconds)"
Write-Host "CLI_BENCHMARK_LEGACY_BYTES=$($legacy.Bytes)"
Write-Host "CLI_BENCHMARK_AST_BYTES=$($ast.Bytes)"
