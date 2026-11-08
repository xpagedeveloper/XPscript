param([string]$CompilerPath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cli-common.ps1')
$context = Get-AstCliContext -ProbeRoot $PSScriptRoot -ProbeScriptRoot $PSScriptRoot -CompilerPath $CompilerPath -OutputRoot (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'out')
$output = Join-Path $context.OutputRoot ('ast-cli-shard-4/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
Write-AstCliCheckpoint 'shard-4-start'

function Invoke-Fixture([string]$name, [string[]]$expected) {
    $directory = Join-Path $output $name
    dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot "$name.xps") -o $directory --debug
    if ($LASTEXITCODE -ne 0) { throw "Shard 4 compilation failed: $name" }
    $result = @(dotnet (Join-Path $directory 'Generated.dll'))
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne ($expected -join "`n")) {
        throw "Shard 4 output mismatch: $name`n$($result -join "`n")"
    }
    Write-Host "AST_CLI_SHARD_4_FIXTURE_OK=$name"
}

Write-AstCliCheckpoint 'shard-4-optional'
Invoke-Fixture 'class-method-optional-next' @('5', '7')
Write-AstCliCheckpoint 'shard-4-inheritance'
Invoke-Fixture 'class-method-derived-next' @('8')
Write-AstCliCheckpoint 'shard-4-byref'
Invoke-Fixture 'class-method-list-byref-next' @('21')
Write-AstCliCheckpoint 'shard-4-properties'
Invoke-Fixture 'class-property-derived-next' @('8')
Invoke-Fixture 'class-property-derived-set-next' @('9')
Write-AstCliCheckpoint 'shard-4-complete'
