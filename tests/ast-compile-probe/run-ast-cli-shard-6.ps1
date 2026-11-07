param([string]$CompilerPath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cli-common.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$context = Get-AstCliContext -ProbeRoot $root -ProbeScriptRoot $PSScriptRoot -CompilerPath $CompilerPath -OutputRoot (Join-Path $root 'out')
$output = Join-Path $context.OutputRoot ('ast-cli-shard-6/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
Write-AstCliCheckpoint 'shard-6-start'

Write-AstCliCheckpoint 'shard-6-diagnostics'
$parser = @(dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'parser-error.xps') -o (Join-Path $output 'parser') 2>&1)
if ($LASTEXITCODE -ne 2 -or ($parser -join "`n") -notmatch 'Expected CloseParenToken') { throw 'Shard 6 parser diagnostic regression failed.' }
$included = @(dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'include-parser-span-error-next.xps') -o (Join-Path $output 'include') 2>&1)
if ($LASTEXITCODE -ne 2 -or ($included -join "`n") -notmatch 'helper\.xps:2:') { throw 'Shard 6 source mapping regression failed.' }

Write-AstCliCheckpoint 'shard-6-final-cli'
$cliOutput = Join-Path $output 'cli-main'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $cliOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 final CLI compilation failed.' }
$result = @(dotnet (Join-Path $cliOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST_OPTIONAL_CALL_OK' -or ($result -join "`n") -notmatch 'AST_FUNCTION_RESULT_OK') {
    throw 'Shard 6 final CLI execution regression failed.'
}
Write-Host 'AST_CLI_SHARD_6_FINAL_EXECUTION_OK'

Write-AstCliCheckpoint 'shard-6-complete'
