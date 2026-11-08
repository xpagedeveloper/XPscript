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
Write-AstCliCheckpoint 'shard-4-optional-byref-array'
Invoke-Fixture 'class-method-optional-byref-array-next' @('9', '9', '9')
Write-AstCliCheckpoint 'shard-4-indexed-object-property'
Invoke-Fixture 'class-property-indexed-object-next' @('CLASS_INDEXED_PROPERTY_OBJECT_OK')
Write-AstCliCheckpoint 'shard-4-virtual-property'
Invoke-Fixture 'class-property-virtual-dispatch-next' @('CLASS_PROPERTY_VIRTUAL_DISPATCH_OK')
Write-AstCliCheckpoint 'shard-4-remaining-properties-byref'
Invoke-Fixture 'class-property-parent-next' @('7')
Invoke-Fixture 'class-property-object-override-next' @('12')
Invoke-Fixture 'class-method-optional-byref-reference-next' @('0', '1')
Invoke-Fixture 'class-method-optional-byref-user-reference-next' @('0', '15')
Invoke-Fixture 'class-method-list-element-byref-next' @('11', '11')
Write-AstCliCheckpoint 'shard-4-indexed-and-dispatched-properties'
Invoke-Fixture 'class-method-optional-byref-list-next' @('0', '12', '12')
Invoke-Fixture 'class-property-derived-index-next' @('7')
Invoke-Fixture 'class-property-derived-index-set-next' @('13')
Invoke-Fixture 'class-property-setter-dispatch-next' @('CLASS_PROPERTY_SETTER_DISPATCH_OK')
Write-AstCliCheckpoint 'shard-4-complete'
