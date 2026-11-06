param([string]$CompilerPath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cli-common.ps1')
$context = Get-AstCliContext -ProbeRoot $PSScriptRoot -ProbeScriptRoot $PSScriptRoot -CompilerPath $CompilerPath -OutputRoot (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'out')
$output = Join-Path $context.OutputRoot ('ast-cli-shard-2/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
Write-AstCliCheckpoint 'shard-2-start'

function Invoke-Fixture([string]$name, [string[]]$expected) {
    $directory = Join-Path $output $name
    dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot "$name.xps") -o $directory
    if ($LASTEXITCODE -ne 0) { throw "Shard 2 compilation failed: $name" }
    $result = @(dotnet (Join-Path $directory 'Generated.dll'))
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne ($expected -join "`n")) {
        throw "Shard 2 output mismatch: $name`n$($result -join "`n")"
    }
    Write-Host "AST_CLI_SHARD_2_FIXTURE_OK=$name"
}

Write-AstCliCheckpoint 'shard-2-core-operators'
Invoke-Fixture 'operators-core' @('POW=8', 'INTDIV=2', 'MOD=2', 'XORBOOL=True', 'EQVBOOL=True', 'IMPBOOL=False', 'ANDBITS=0', 'ORBITS=15', 'XORBITS=255', 'NOTBITS=-4')
Write-AstCliCheckpoint 'shard-2-arrays'
Invoke-Fixture 'array-storage' @('1', '7', 'True', 'True')
Write-AstCliCheckpoint 'shard-2-complete'
