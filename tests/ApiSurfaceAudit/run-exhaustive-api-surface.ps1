param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$CompilerPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll')
)

$ErrorActionPreference = 'Stop'
$temp = Join-Path $Root 'out/api-surface-temp'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$env:XPSCRIPT_TEMP_ROOT = $temp
$fixtures = @(
    'tests/ApiSurfaceAudit/xpdbsqlite-full-surface.xps',
    'tests/ApiSurfaceAudit/xphttpdbsupabase-full-surface.xps'
)
foreach ($relative in $fixtures) {
    $path = Join-Path $Root $relative
    Write-Host "API_SURFACE_FIXTURE_START=$relative"
    & dotnet $CompilerPath compile $path --security=off
    if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $relative" }
    Write-Host "API_SURFACE_FIXTURE_COMPILE_OK=$relative"
}
$sqlite = Join-Path $Root 'tests/ApiSurfaceAudit/xpdbsqlite-full-surface.xps'
$runOutput = @(& dotnet $CompilerPath run $sqlite --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($runOutput -notmatch 'XPDBSQLITE_FULL_SURFACE=OK')) {
    throw 'SQLite full-surface runtime verification failed.'
}
Write-Host 'API_SURFACE_FIXTURE_RUNTIME_OK=XPDBSQLite'
Write-Host 'API_SURFACE_FIXTURE_BATCH_OK'
