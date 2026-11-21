param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$CompilerPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll')
)

$ErrorActionPreference = 'Stop'
$auditOutput = @(& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-api-surface-audit.ps1') 2>&1 | Out-String)
if (($auditOutput -join "`n") -notmatch 'API_SURFACE_AUDIT_OK' -or ($auditOutput -join "`n") -notmatch 'API_SURFACE_GENERATED_FIXTURES=299') {
    throw 'Documented API surface generation audit failed.'
}
Write-Host 'API_SURFACE_GENERATION_AUDIT_OK'
$temp = Join-Path $Root 'out/api-surface-temp'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$env:XPSCRIPT_TEMP_ROOT = $temp
$staleHttpServers = Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
    Where-Object { $_.CommandLine -like '*tests/httpdb_mock_server.py*' }
foreach ($stale in $staleHttpServers) {
    Stop-Process -Id $stale.ProcessId -Force -ErrorAction SilentlyContinue
}
$httpRuntime = Join-Path $Root 'tests/ApiSurfaceAudit/run-http-runtime.py'
$fixtures = @(
    'tests/ApiSurfaceAudit/xpdbsqlite-full-surface.xps',
    'tests/ApiSurfaceAudit/xphttpdbsupabase-full-surface.xps',
    'tests/ApiSurfaceAudit/xphttpdbdomino-full-surface.xps',
    'tests/ApiSurfaceAudit/xpdbsupabase-full-surface.xps',
    'tests/ast-compile-probe/application-native.xps',
    'samples/application-crypto.xps',
    'samples/application-systemlog.xps',
    'samples/application-registry-secrets.xps',
    'samples/web-session-api.xps',
    'samples/ui-form-core.xps',
    'samples/uiform-extended-fields.xps',
    'tests/ast-compile-probe/ui-list-view-ast-core.xps',
    'tests/ast-compile-probe/ui-list-view-event-dispatch.xps',
    'samples/networktools-native-runtime-test.xps',
    'tests/ast-compile-probe/date-runtime-api-next.xps',
    'tests/ast-compile-probe/database-attachments-next.xps',
    'tests/ast-compile-probe/class-method-spreadsheet-next.xps',
    'tests/ast-compile-probe/ai-native-metadata-next.xps',
    'tests/ast-compile-probe/ai-tool-properties-next.xps',
    'tests/ast-compile-probe/ai-tool-function-next.xps',
    'tests/ast-compile-probe/ai-tool-lifecycle-next.xps',
    'tests/ast-compile-probe/resume-label-runtime.xps',
    'tests/ast-compile-probe/resume-current-next-runtime.xps',
    'tests/ast-compile-probe/redim-dynamic-nonzero-runtime.xps'
)
foreach ($relative in $fixtures) {
    $path = Join-Path $Root $relative
    Write-Host "API_SURFACE_FIXTURE_START=$relative"
    if ($relative -eq 'samples/web-session-api.xps') {
        $webOutput = @(& dotnet $CompilerPath compile $path --target webiis --runtime false --security=off 2>&1 | Out-String)
        if (($webOutput -join "`n") -notmatch 'AST webiis package succeeded') { throw "Fixture failed: $relative" }
    } else {
        & dotnet $CompilerPath compile $path --security=off
        if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $relative" }
    }
    Write-Host "API_SURFACE_FIXTURE_COMPILE_OK=$relative"
}
$applicationNative = Join-Path $Root 'tests/ast-compile-probe/application-native.xps'
$applicationNativeRun = @(& dotnet $CompilerPath run $applicationNative --security=off --Args 'Åsa "two words"' 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($applicationNativeRun -notmatch 'APPLICATION_NATIVE_OK')) { throw 'Application native runtime fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_RUNTIME_OK=Application'
$resumeLabel = Join-Path $Root 'tests/ast-compile-probe/resume-label-runtime.xps'
$resumeLabelOutput = @(& dotnet $CompilerPath run $resumeLabel --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($resumeLabelOutput -notmatch 'RESUME_LABEL_RUNTIME=OK')) { throw 'Resume-label runtime fixture failed.' }
Write-Host 'AST_ERROR_FLOW_RUNTIME_OK=ResumeLabel'
$resumeCurrentNext = Join-Path $Root 'tests/ast-compile-probe/resume-current-next-runtime.xps'
$resumeCurrentNextOutput = @(& dotnet $CompilerPath run $resumeCurrentNext --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($resumeCurrentNextOutput -notmatch 'RESUME_NEXT_RUNTIME=OK')) { throw 'Resume current/next runtime fixture failed.' }
Write-Host 'AST_ERROR_FLOW_RUNTIME_OK=ResumeCurrent+ResumeNext'
$redimNonZero = Join-Path $Root 'tests/ast-compile-probe/redim-dynamic-nonzero-runtime.xps'
$redimNonZeroOutput = @(& dotnet $CompilerPath run $redimNonZero --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($redimNonZeroOutput -notmatch 'REDIM_DYNAMIC_NONZERO_RUNTIME=OK')) { throw 'Dynamic non-zero ReDim runtime fixture failed.' }
Write-Host 'AST_ARRAY_RUNTIME_OK=DynamicNonZeroReDim'
$applicationRuntimeFixtures = @(
    @{ Path = (Join-Path $Root 'samples/application-crypto.xps'); Marker = 'APPLICATION_CRYPTO_OK'; Name = 'Application.Crypto' },
    @{ Path = (Join-Path $Root 'samples/application-systemlog.xps'); Marker = 'APPLICATION-SYSTEMLOG=OK'; Name = 'Application.SystemLog' },
    @{ Path = (Join-Path $Root 'samples/application-registry-secrets.xps'); Marker = 'REGISTRY=XPscript-registry-ok'; Name = 'Application.Registry+Secrets' }
)
foreach ($runtimeFixture in $applicationRuntimeFixtures) {
    $runtimeOutput = @(& dotnet $CompilerPath run $runtimeFixture.Path --security=off 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or ($runtimeOutput -notmatch [regex]::Escape($runtimeFixture.Marker))) {
        throw "Application runtime fixture failed: $($runtimeFixture.Name)"
    }
    Write-Host "API_SURFACE_FIXTURE_RUNTIME_OK=$($runtimeFixture.Name)"
}
$sqlite = Join-Path $Root 'tests/ApiSurfaceAudit/xpdbsqlite-full-surface.xps'
$runOutput = @(& dotnet $CompilerPath run $sqlite --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($runOutput -notmatch 'XPDBSQLITE_FULL_SURFACE=OK')) {
    throw 'SQLite full-surface runtime verification failed.'
}
Write-Host 'API_SURFACE_FIXTURE_RUNTIME_OK=XPDBSQLite'
$httpOutput = @(& python $httpRuntime 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($httpOutput -notmatch 'HTTP_RUNTIME_FIXTURE_BATCH_OK')) { throw 'HTTP database full-surface runtime verification failed.' }
Write-Host 'API_SURFACE_FIXTURE_RUNTIME_OK=XPHttpDbSupabase+XPHttpDbDominoRest'
$appSession = Join-Path $Root 'tests/ApiSurfaceAudit/application-session-combined.xps'
& dotnet $CompilerPath compile $appSession --security=off
if ($LASTEXITCODE -ne 0) { throw 'Combined Application/Session fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_COMPILE_OK=Application+Session'
$appSessionRun = @(& dotnet $CompilerPath run $appSession --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($appSessionRun -notmatch 'APPLICATION_SESSION_COMBINED=OK')) { throw 'Combined Application/Session runtime fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_RUNTIME_OK=Application+Session'
$combined = Join-Path $Root 'tests/ApiSurfaceAudit/httpdb-domino-supabase-combined.xps'
& dotnet $CompilerPath compile $combined --security=off
if ($LASTEXITCODE -ne 0) { throw 'Combined Domino/Supabase fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_COMPILE_OK=Domino+Supabase+XPJson'
$uiCombined = Join-Path $Root 'tests/ApiSurfaceAudit/uiform-uijson-combined.xps'
& dotnet $CompilerPath compile $uiCombined --security=off
if ($LASTEXITCODE -ne 0) { throw 'Combined UIForm/UIListView/XPJson fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_COMPILE_OK=UIForm+UIListView+XPJson'
$uiCombinedRun = @(& dotnet $CompilerPath run $uiCombined --security=off 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or ($uiCombinedRun -notmatch 'UIFORM_UILISTVIEW_XPJSON_COMBINED=OK')) { throw 'Combined UIForm/UIListView/XPJson runtime fixture failed.' }
Write-Host 'API_SURFACE_FIXTURE_COMBINED_RUNTIME_OK=UIForm+UIListView+XPJson'
$unused = Join-Path $Root 'tests/ApiSurfaceAudit/unused-db-http-family.xps'
$unusedOutput = @(& dotnet $CompilerPath compile $unused --security=off --info 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'Unused-family fixture failed.' }
if (($unusedOutput -join "`n") -match 'SQLite|Npgsql|MySqlConnector|Microsoft.Data.Sqlite') {
    throw 'Unused database dependencies were staged for a source that uses no database objects.'
}
Write-Host 'API_SURFACE_UNUSED_DB_HTTP_DEPENDENCIES_PRUNED=OK'
$unusedUi = Join-Path $Root 'tests/ApiSurfaceAudit/unused-ui-family.xps'
& dotnet $CompilerPath compile $unusedUi --security=off --info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Unused UI fixture failed.' }
Write-Host 'API_SURFACE_UNUSED_UI_DEPENDENCIES_PRUNED=OK'
Write-Host 'API_SURFACE_FIXTURE_BATCH_OK'
