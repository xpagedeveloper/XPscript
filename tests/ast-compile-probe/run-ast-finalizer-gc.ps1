$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path ([System.IO.Path]::GetTempPath()) ('ast-finalizer-gc-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $PSScriptRoot 'finalizer-gc-next.xps'
dotnet $compiler ast-compile $fixture -o $output
if ($LASTEXITCODE -ne 0) { throw 'GC finalizer fixture compilation failed.' }
$assembly = [System.Reflection.Assembly]::LoadFile((Join-Path $output 'Generated.dll'))
$assembly.EntryPoint.Invoke($null, @([string[]]@()))
if ($LASTEXITCODE -ne 0) { throw 'GC finalizer fixture entrypoint failed.' }
$seen = $false
for ($attempt = 1; $attempt -le 20 -and -not $seen; $attempt++) {
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
    Start-Sleep -Milliseconds 50
}
Write-Host 'AST_FINALIZER_GC_HARNESS_COMPLETED'
