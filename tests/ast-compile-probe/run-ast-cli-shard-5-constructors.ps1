$ErrorActionPreference = 'Stop'
Write-Host 'AST_CLI_CHECKPOINT=shard-5-constructors-start'
& (Join-Path $PSScriptRoot 'run-ast-constructor.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Shard 5 constructor probe failed.' }
Write-Host 'AST_CLI_CHECKPOINT=shard-5-destructors'
& (Join-Path $PSScriptRoot 'run-ast-destructor.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Shard 5 destructor probe failed.' }
Write-Host 'AST_CLI_SHARD_5_CONSTRUCTORS_OK'
