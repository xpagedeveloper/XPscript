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
Invoke-Fixture 'array-storage' @('1', '0', '1', 'True', 'True')
Write-AstCliCheckpoint 'shard-2-file-runtime'
Invoke-Fixture 'file-runtime-next' @('FILE_RUNTIME_OK')
Write-AstCliCheckpoint 'shard-2-file-aliases'
Invoke-Fixture 'file-exists-aliases-next' @('FILE_EXISTS_ALIASES_AST_OK')
Write-AstCliCheckpoint 'shard-2-file-binary'
Invoke-Fixture 'file-get-next' @('FILE_GET_OK')
Write-AstCliCheckpoint 'shard-2-runtime-globals'
$env:XPSCRIPT_RUNTIME_IDENTITY = 'identity-ok'
$globalsDirectory = Join-Path $output 'runtime-globals-next'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'runtime-globals-next.xps') -o $globalsDirectory
if ($LASTEXITCODE -ne 0) { throw 'Shard 2 runtime-globals compilation failed.' }
$globalsResult = @(dotnet (Join-Path $globalsDirectory 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($globalsResult -join "`n") -notmatch 'ENVIRON_READ=True' -or ($globalsResult -join "`n") -notmatch 'COMMAND_READ=True' -or ($globalsResult -join "`n") -notmatch 'TICKCOUNT_READ=True' -or ($globalsResult -join "`n") -notmatch 'RUNTIME_GLOBALS_OK') {
    throw "Shard 2 runtime-globals output mismatch.`n$($globalsResult -join "`n")"
}
Write-Host 'AST_CLI_SHARD_2_FIXTURE_OK=runtime-globals-next'
Write-AstCliCheckpoint 'shard-2-file-input-record'
Invoke-Fixture 'file-input-record-next' @('FILE_INPUT_RECORD_OK')
Write-AstCliCheckpoint 'shard-2-directory-enumeration'
Invoke-Fixture 'dir-next' @('DIR_AST_OK')
Write-AstCliCheckpoint 'shard-2-file-lock'
Invoke-Fixture 'file-lock-next' @('FILE_LOCK_OK')
Write-AstCliCheckpoint 'shard-2-file-text'
Invoke-Fixture 'file-text-next' @('FILE_TEXT_OK')
Write-AstCliCheckpoint 'shard-2-file-missing-paths'
Invoke-Fixture 'file-missing-path-errors' @('FILE_MISSING_PATH_ERRORS_OK')
Write-AstCliCheckpoint 'shard-2-file-info'
Invoke-Fixture 'file-info-next' @('file-exists-aliases-next.xps', '.xps', 'True', 'False', 'True', 'True', 'ast-compile-probe', 'True', 'False', 'True', 'FILE_INFO_AST_OK')
Write-AstCliCheckpoint 'shard-2-complete'
