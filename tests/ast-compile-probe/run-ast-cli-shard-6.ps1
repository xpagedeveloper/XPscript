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

Write-AstCliCheckpoint 'shard-6-json-schema-runtime'
$schemaOutput = Join-Path $output 'json-schema-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'json-schema-transitive-runtime-next.xps') -o $schemaOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 JSON-schema transitive runtime compilation failed.' }
$schemaResult = @(dotnet (Join-Path $schemaOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($schemaResult -join "`n") -notmatch 'AST_JSON_SCHEMA_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 JSON-schema transitive runtime execution regression failed.'
}
Write-Host 'AST_JSON_SCHEMA_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-http-json-runtime'
$httpOutput = Join-Path $output 'http-json-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'http-json-transitive-runtime-next.xps') -o $httpOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 HTTP/JSON transitive runtime compilation failed.' }
$httpResult = @(dotnet (Join-Path $httpOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($httpResult -join "`n") -notmatch 'AST_HTTP_JSON_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 HTTP/JSON transitive runtime execution regression failed.'
}
Write-Host 'AST_HTTP_JSON_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-filesystem-runtime'
$filesystemOutput = Join-Path $output 'filesystem-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'filesystem-transitive-runtime-next.xps') -o $filesystemOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 filesystem transitive runtime compilation failed.' }
$filesystemResult = @(dotnet (Join-Path $filesystemOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($filesystemResult -join "`n") -notmatch 'AST_FILESYSTEM_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 filesystem transitive runtime execution regression failed.'
}
Write-Host 'AST_FILESYSTEM_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-database-runtime'
$databaseOutput = Join-Path $output 'database-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'database-transitive-runtime-next.xps') -o $databaseOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 database transitive runtime compilation failed.' }
$databaseResult = @(dotnet (Join-Path $databaseOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($databaseResult -join "`n") -notmatch 'AST_DATABASE_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 database transitive runtime execution regression failed.'
}
Write-Host 'AST_DATABASE_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-csv-runtime'
$csvOutput = Join-Path $output 'csv-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'csv-transitive-runtime-next.xps') -o $csvOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 CSV transitive runtime compilation failed.' }
$csvResult = @(dotnet (Join-Path $csvOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($csvResult -join "`n") -notmatch 'AST_CSV_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 CSV transitive runtime execution regression failed.'
}
Write-Host 'AST_CSV_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-xml-runtime'
$xmlOutput = Join-Path $output 'xml-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'xml-transitive-runtime-next.xps') -o $xmlOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 XML transitive runtime compilation failed.' }
$xmlResult = @(dotnet (Join-Path $xmlOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($xmlResult -join "`n") -notmatch 'AST_XML_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 XML transitive runtime execution regression failed.'
}
Write-Host 'AST_XML_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-image-runtime'
$imageOutput = Join-Path $output 'image-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'image-transitive-runtime-next.xps') -o $imageOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 image transitive runtime compilation failed.' }
$imageResult = @(dotnet (Join-Path $imageOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($imageResult -join "`n") -notmatch 'AST_IMAGE_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 image transitive runtime execution regression failed.'
}
Write-Host 'AST_IMAGE_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-spreadsheet-runtime'
$spreadsheetOutput = Join-Path $output 'spreadsheet-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'spreadsheet-transitive-runtime-next.xps') -o $spreadsheetOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 spreadsheet transitive runtime compilation failed.' }
$spreadsheetResult = @(dotnet (Join-Path $spreadsheetOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($spreadsheetResult -join "`n") -notmatch 'AST_SPREADSHEET_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 spreadsheet transitive runtime execution regression failed.'
}
Write-Host 'AST_SPREADSHEET_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-platform-runtime'
$platformOutput = Join-Path $output 'platform-runtime'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'platform-transitive-runtime-next.xps') -o $platformOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 platform transitive runtime compilation failed.' }
$env:XPSCRIPT_PLATFORM_FIXTURE = 'ok'
$platformResult = @(dotnet (Join-Path $platformOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($platformResult -join "`n") -notmatch 'AST_PLATFORM_TRANSITIVE_RUNTIME_OK') {
    throw 'Shard 6 platform transitive runtime execution regression failed.'
}
Write-Host 'AST_PLATFORM_TRANSITIVE_RUNTIME_OK'

Write-AstCliCheckpoint 'shard-6-uiform-ast-route'
$uiFormOutput = Join-Path $output 'uiform-ast-route'
$uiFormCompile = @(dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'uiform-ast-route-next.xps') -o $uiFormOutput --debug 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Shard 6 UIForm AST route compilation failed: $($uiFormCompile -join "`n")" }
$uiFormResult = @(dotnet (Join-Path $uiFormOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($uiFormResult -join "`n") -notmatch 'AST_UIFORM_ROUTE_OK') {
    throw 'Shard 6 UIForm AST route execution regression failed.'
}
Write-Host 'AST_UIFORM_ROUTE_OK'

Write-AstCliCheckpoint 'shard-6-uiform-schema-free'
$uiFormSchemaFreeOutput = Join-Path $output 'uiform-schema-free'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'uiform-schema-free-next.xps') -o $uiFormSchemaFreeOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 schema-free UIForm compilation failed.' }
$schemaFreeAssembly = Join-Path $uiFormSchemaFreeOutput 'Generated.dll'
$schemaFreeTypeCheck = & pwsh -NoProfile -Command '& { $assembly = [System.Reflection.Assembly]::LoadFrom($args[0]); if ($null -ne $assembly.GetType($args[1], $false)) { exit 0 }; exit 1 }' $schemaFreeAssembly 'XPScriptJsonSchema'
$schemaFreeHasSchema = $LASTEXITCODE -eq 0
$schemaFreeTypeCheck = & pwsh -NoProfile -Command '& { $assembly = [System.Reflection.Assembly]::LoadFrom($args[0]); if ($null -ne $assembly.GetType($args[1], $false)) { exit 0 }; exit 1 }' $schemaFreeAssembly 'XPScriptJsonValidationResult'
$schemaFreeHasValidationResult = $LASTEXITCODE -eq 0
if ($schemaFreeHasSchema -or $schemaFreeHasValidationResult) {
    throw 'Schema-free UIForm output included JSON-schema runtime providers.'
}
$uiFormSchemaFreeResult = @(dotnet (Join-Path $uiFormSchemaFreeOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($uiFormSchemaFreeResult -join "`n") -notmatch 'AST_UIFORM_SCHEMA_FREE_OK') {
    throw 'Shard 6 schema-free UIForm execution regression failed.'
}
Write-Host 'AST_UIFORM_SCHEMA_FREE_OK'

Write-AstCliCheckpoint 'shard-6-uiform-schema-enabled'
$uiFormSchemaEnabledOutput = Join-Path $output 'uiform-schema-enabled'
dotnet $context.Compiler ast-compile (Join-Path $PSScriptRoot 'uiform-schema-enabled-next.xps') -o $uiFormSchemaEnabledOutput
if ($LASTEXITCODE -ne 0) { throw 'Shard 6 schema-enabled UIForm compilation failed.' }
$schemaEnabledAssembly = Join-Path $uiFormSchemaEnabledOutput 'Generated.dll'
$schemaEnabledTypeCheck = & pwsh -NoProfile -Command '& { $assembly = [System.Reflection.Assembly]::LoadFrom($args[0]); if ($null -ne $assembly.GetType($args[1], $false)) { exit 0 }; exit 1 }' $schemaEnabledAssembly 'XPScriptJsonSchema'
$schemaEnabledHasSchema = $LASTEXITCODE -eq 0
$schemaEnabledTypeCheck = & pwsh -NoProfile -Command '& { $assembly = [System.Reflection.Assembly]::LoadFrom($args[0]); if ($null -ne $assembly.GetType($args[1], $false)) { exit 0 }; exit 1 }' $schemaEnabledAssembly 'XPScriptJsonValidationResult'
$schemaEnabledHasValidationResult = $LASTEXITCODE -eq 0
if (-not $schemaEnabledHasSchema -or -not $schemaEnabledHasValidationResult) {
    throw 'Schema-enabled UIForm output omitted JSON-schema runtime providers.'
}
$uiFormSchemaEnabledResult = @(dotnet (Join-Path $uiFormSchemaEnabledOutput 'Generated.dll'))
if ($LASTEXITCODE -ne 0 -or ($uiFormSchemaEnabledResult -join "`n") -notmatch 'AST_UIFORM_SCHEMA_ENABLED_OK') {
    throw 'Shard 6 schema-enabled UIForm execution regression failed.'
}
Write-Host 'AST_UIFORM_SCHEMA_ENABLED_OK'

Write-AstCliCheckpoint 'shard-6-complete'
