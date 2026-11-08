param([string]$CompilerPath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cli-common.ps1')
$context = Get-AstCliContext -ProbeRoot $PSScriptRoot -ProbeScriptRoot $PSScriptRoot -CompilerPath $CompilerPath -OutputRoot (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'out')
$output = Join-Path $context.OutputRoot ('ast-cli-shard-3/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
Write-AstCliCheckpoint 'shard-3-start'

foreach ($fixture in @('native-json-regression', 'native-xml-regression')) {
    Write-AstCliCheckpoint "shard-3-$fixture"
    $directory = Join-Path $output $fixture
    dotnet $context.Compiler ast-compile (Join-Path $context.Root "samples/$fixture.xps") -o $directory
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $directory 'Generated.dll'))) {
        throw "Shard 3 compilation failed: $fixture"
    }
    Write-Host "AST_CLI_SHARD_3_COMPILE_OK=$fixture"
}
foreach ($fixture in @(
    'native-http-regression', 'native-http-json', 'native-http-null-boundaries',
    'native-http-resource-limits', 'native-xml-dom-regression', 'native-csv-regression',
    'csv-json-roundtrip', 'json-http', 'json-resource-limits', 'xpjsonschema-runtime')) {
    Write-AstCliCheckpoint "shard-3-$fixture"
    $directory = Join-Path $output $fixture
    dotnet $context.Compiler ast-compile (Join-Path $context.Root "samples/$fixture.xps") -o $directory
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $directory 'Generated.dll'))) {
        throw "Shard 3 native/service compilation failed: $fixture"
    }
    Write-Host "AST_CLI_SHARD_3_COMPILE_OK=$fixture"
}
Write-AstCliCheckpoint 'shard-3-complete'
