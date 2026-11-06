$ErrorActionPreference = 'Stop'
Write-Host 'AST_CLI_CHECKPOINT=shard-5-start'
Write-Host 'AST_CLI_CHECKPOINT=shard-5-constructors'
& (Join-Path $PSScriptRoot 'run-ast-constructor.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Shard 5 constructor probe failed.' }
Write-Host 'AST_CLI_CHECKPOINT=shard-5-destructors'
& (Join-Path $PSScriptRoot 'run-ast-destructor.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Shard 5 destructor probe failed.' }
Write-Host 'AST_CLI_CHECKPOINT=shard-5-xpimage'
& (Join-Path $PSScriptRoot 'run-ast-image-get.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Shard 5 XPImage probe failed.' }
Write-Host 'AST_CLI_CHECKPOINT=shard-5-complete'
