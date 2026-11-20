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
    'tests/ApiSurfaceAudit/xphttpdbsupabase-full-surface.xps',
    'tests/ApiSurfaceAudit/xphttpdbdomino-full-surface.xps',
    'tests/ApiSurfaceAudit/xpdbsupabase-full-surface.xps',
    'tests/ast-compile-probe/application-native.xps',
    'samples/application-crypto.xps',
    'samples/application-systemlog.xps',
    'samples/application-registry-secrets.xps',
    'samples/web-session-api.xps'
)
foreach ($relative in $fixtures) {
    $path = Join-Path $Root $relative
    Write-Host "API_SURFACE_FIXTURE_START=$relative"
    if ($relative -eq 'samples/web-session-api.xps') {
        & dotnet $CompilerPath compile $path --target webiis --runtime false --security=off
    } else {
        & dotnet $CompilerPath compile $path --security=off
    }
    if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $relative" }
    Write-Host "API_SURFACE_FIXTURE_COMPILE_OK=$relative"
}
$sqlite = Join-Path $Root 'tests/ApiSurfaceAudit/xpdbsqlite-full-surface.xps'
$runOutput = @(& dotnet $CompilerPath run $sqlite --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($runOutput -notmatch 'XPDBSQLITE_FULL_SURFACE=OK')) {
    throw 'SQLite full-surface runtime verification failed.'
}
Write-Host 'API_SURFACE_FIXTURE_RUNTIME_OK=XPDBSQLite'
$appSession = Join-Path $Root 'tests/ApiSurfaceAudit/application-session-combined.xps'
& dotnet $CompilerPath compile $appSession --security=off
if ($LASTEXITCODE -ne 0) { throw 'Combined Application/Session fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_COMPILE_OK=Application+Session'
$combined = Join-Path $Root 'tests/ApiSurfaceAudit/httpdb-domino-supabase-combined.xps'
& dotnet $CompilerPath compile $combined --security=off
if ($LASTEXITCODE -ne 0) { throw 'Combined Domino/Supabase fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_COMPILE_OK=Domino+Supabase+XPJson'
$unused = Join-Path $Root 'tests/ApiSurfaceAudit/unused-db-http-family.xps'
$unusedOutput = @(& dotnet $CompilerPath compile $unused --security=off --info 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'Unused-family fixture failed.' }
if (($unusedOutput -join "`n") -match 'SQLite|Npgsql|MySqlConnector|Microsoft.Data.Sqlite') {
    throw 'Unused database dependencies were staged for a source that uses no database objects.'
}
Write-Host 'API_SURFACE_UNUSED_DB_HTTP_DEPENDENCIES_PRUNED=OK'
Write-Host 'API_SURFACE_FIXTURE_BATCH_OK'
