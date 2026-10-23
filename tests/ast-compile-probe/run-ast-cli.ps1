# Expected negative compiler cases intentionally write diagnostics to stderr.
param(
    [string]$CompilerPath
)

# Keep PowerShell from converting native stderr into terminating errors; every
# compiler/runtime invocation below asserts its exit code and output explicitly.
$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# Generated programs intentionally exercise repository-relative source paths.
# Normalize the process working directory so the suite behaves the same when
# invoked from CI, an IDE, or an arbitrary caller directory.
Set-Location -LiteralPath $root
# Keep generated compiler workspaces inside the repository during the probe so
# constrained Windows sandboxes cannot redirect them to an inaccessible profile.
$env:XPSCRIPT_TEMP_ROOT = Join-Path $root 'out/ast-test-temp'
$null = New-Item -ItemType Directory -Force -Path $env:XPSCRIPT_TEMP_ROOT
$compiler = if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
} else {
    [System.IO.Path]::GetFullPath($CompilerPath)
}
if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler DLL not found: $compiler" }
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$outputRoot = if ($env:XPSCRIPT_TEST_OUTPUT_ROOT) { $env:XPSCRIPT_TEST_OUTPUT_ROOT } else { Join-Path $root 'out' }
$output = Join-Path $outputRoot 'ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
function Invoke-ExpectedRuntimeFailure([string] $assemblyPath) {
    $info = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    [void]$info.ArgumentList.Add($assemblyPath)
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    if (-not $process.WaitForExit(10000)) {
        $process.Kill($true)
        $process.WaitForExit()
        return @{ TimedOut = $true; ExitCode = $null; Output = $process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd() }
    }
    return @{ TimedOut = $false; ExitCode = $process.ExitCode; Output = $process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd() }
}
$compileHelp = dotnet $compiler compile --help 2>&1
if ($LASTEXITCODE -ne 0 -or ($compileHelp -join "`n") -notmatch 'Usage:' -or ($compileHelp -join "`n") -notmatch '--target desktop\|console\|webiis' -or ($compileHelp -join "`n") -notmatch 'desktop\s+Create a desktop executable') { throw 'AST compile command help regression failed.' }
$astCompileHelp = dotnet $compiler ast-compile -h 2>&1
if ($LASTEXITCODE -ne 0 -or ($astCompileHelp -join "`n") -notmatch 'Usage:') { throw 'AST compile alias help regression failed.' }
Write-Host 'AST_COMMAND_HELP_OK'
$jsonFormatOutput = Join-Path $output 'cli-result-format-json'
$jsonFormatText = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $jsonFormatOutput --result-format json 2>&1
if ($LASTEXITCODE -ne 0) { throw 'AST JSON result-format compilation failed.' }
try { $jsonFormatResult = ($jsonFormatText -join "`n") | ConvertFrom-Json } catch { throw 'AST JSON result-format output is invalid JSON.' }
if ($jsonFormatResult.errors.Count -ne 0 -or $jsonFormatResult.output -notmatch 'Generated.dll$') { throw 'AST JSON result-format payload is incomplete.' }
$xmlFormatOutput = Join-Path $output 'cli-result-format-xml'
$xmlFormatText = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $xmlFormatOutput --result-format xml 2>&1
if ($LASTEXITCODE -ne 0) { throw 'AST XML result-format compilation failed.' }
try { $xmlFormatResult = [xml]($xmlFormatText -join "`n") } catch { throw 'AST XML result-format output is invalid XML.' }
if ($xmlFormatResult.compileResult.errors -ne $null -and $xmlFormatResult.compileResult.errors.ChildNodes.Count -ne 0) { throw 'AST XML result-format reported unexpected errors.' }
Write-Host 'AST_RESULT_FORMATS_OK'
$debugOutput = Join-Path $output 'cli-debug'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $debugOutput --debug --runtime false --single-file false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $debugOutput 'Generated.pdb'))) { throw 'AST --debug artifact regression failed.' }
$infoOutput = Join-Path $output 'cli-info'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $infoOutput --info --runtime false --single-file false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $infoOutput 'Generated.dll'))) { throw 'AST --info option regression failed.' }
Write-Host 'AST_INFO_OPTION_OK'
$dependencyInfoOutput = Join-Path $output 'cli-dependency-info'
$dependencyInfoText = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'mysql-native-next.xps') -o $dependencyInfoOutput --info --runtime false --single-file false 2>&1
$dependencyInfoJoined = $dependencyInfoText -join "`n"
if ($LASTEXITCODE -ne 0 -or $dependencyInfoJoined -notmatch 'MySqlConnector\s+2\.6\.2' -or
    $dependencyInfoJoined -notmatch 'already available locally|will be restored/downloaded') {
    throw 'AST dependency --info reporting regression failed.'
}
Write-Host 'AST_DEPENDENCY_INFO_OK'
$dependencyRunText = dotnet $compiler ast-run (Join-Path $PSScriptRoot 'mysql-native-next.xps') --info 2>&1
$dependencyRunJoined = $dependencyRunText -join "`n"
if ($LASTEXITCODE -ne 0 -or $dependencyRunJoined -notmatch 'MySqlConnector\s+2\.6\.2') {
    throw 'AST run dependency --info reporting regression failed.'
}
Write-Host 'AST_RUN_DEPENDENCY_INFO_OK'
$embedOutput = Join-Path $output 'cli-embed-assets'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $embedOutput --embed-assets --runtime false --single-file false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $embedOutput 'Generated.dll'))) { throw 'AST --embed-assets option regression failed.' }
Write-Host 'AST_EMBED_ASSETS_OPTION_OK'
$webIisRoot = Join-Path $output 'cli-webiis-source'
New-Item -ItemType Directory -Force -Path $webIisRoot | Out-Null
$webIisSource = @'
Sub Main()
    Print "AST_WEBIIS_TARGET_OK"
End Sub
'@
Set-Content -LiteralPath (Join-Path $webIisRoot 'main.xps') -Value $webIisSource
$webIisOutput = Join-Path $output 'cli-webiis'
if (Test-Path -LiteralPath $webIisOutput) { Remove-Item -LiteralPath $webIisOutput -Recurse -Force }
$webIisArchive = Join-Path $output 'cli-webiis.zip'
if (Test-Path -LiteralPath $webIisArchive) { Remove-Item -LiteralPath $webIisArchive -Force }
dotnet $compiler ast-compile (Join-Path $webIisRoot 'main.xps') -o $webIisOutput --target webiis --runtime false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $webIisArchive)) { throw 'AST --target webiis regression failed.' }
Write-Host 'AST_DEBUG_AND_WEBIIS_TARGET_OK'
$platformAliasOutput = Join-Path $output 'cli-platform-alias'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $platformAliasOutput --platform win-x64 --singlefile false --runtime false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $platformAliasOutput 'Generated.dll'))) { throw 'AST --platform/--singlefile alias regression failed.' }
$ridAliasOutput = Join-Path $output 'cli-rid-alias'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $ridAliasOutput --rid win-x64 --single-file false --runtime false
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $ridAliasOutput 'Generated.dll'))) { throw 'AST --rid/--single-file regression failed.' }
Write-Host 'AST_PLATFORM_ALIASES_OK'
foreach ($securityMode in @('off', 'warn', 'strict')) {
    $securityOutput = Join-Path $output "cli-security-$securityMode"
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $securityOutput --security $securityMode --runtime false --single-file false
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $securityOutput 'Generated.dll'))) { throw "AST --security=$securityMode regression failed." }
}
Write-Host 'AST_SECURITY_MODES_OK'
$restrictedIncludeOutput = Join-Path $output 'cli-restricted-include'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'include-use-next.xps') -o $restrictedIncludeOutput --restricted
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $restrictedIncludeOutput 'Generated.dll'))) { throw 'AST --restricted Include regression failed.' }
$sourceRootIncludeOutput = Join-Path $output 'cli-source-root-include'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'include-use-next.xps') -o $sourceRootIncludeOutput --source-root (Resolve-Path $PSScriptRoot).Path
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $sourceRootIncludeOutput 'Generated.dll'))) { throw 'AST --source-root Include regression failed.' }
Write-Host 'AST_INCLUDE_SECURITY_OPTIONS_OK'
$preprocessorOutput = Join-Path $output 'cli-preprocessors'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'cli-main.xps') -o $preprocessorOutput --preprocessor identity --preprocessor 'replace:Print=Print'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $preprocessorOutput 'Generated.dll'))) { throw 'AST repeated --preprocessor regression failed.' }
Write-Host 'AST_PREPROCESSOR_OPTIONS_OK'
$includeUseOutput = Join-Path $output 'include-use-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'include-use-next.xps') -o $includeUseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Include/Use compilation failed.' }
$includeUseResult = dotnet (Join-Path $includeUseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($includeUseResult -join "`n") -ne 'AST_INCLUDE_USE_OK') { throw 'AST Include/Use deduplication or relative resolution failed.' }
Write-Host 'AST_INCLUDE_USE_OK'
$inlineAssignmentOutput = Join-Path $output 'if-inline-assignment'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'if-inline-assignment.xps') -o $inlineAssignmentOutput
if ($LASTEXITCODE -ne 0) { throw 'AST inline If assignment compilation failed.' }
$inlineAssignmentResult = dotnet (Join-Path $inlineAssignmentOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inlineAssignmentResult -join "`n") -ne '1') { throw 'AST inline If assignment regression failed.' }
Write-Host 'AST_INLINE_IF_ASSIGNMENT_OK'
$inlineSetOutput = Join-Path $output 'if-inline-set'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'if-inline-set.xps') -o $inlineSetOutput
if ($LASTEXITCODE -ne 0) { throw 'AST inline If Set compilation failed.' }
$inlineSetResult = dotnet (Join-Path $inlineSetOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inlineSetResult -join "`n") -ne 'True') { throw 'AST inline If Set regression failed.' }
Write-Host 'AST_INLINE_IF_SET_OK'
$inlineExitOutput = Join-Path $output 'if-inline-exit'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'if-inline-exit.xps') -o $inlineExitOutput
if ($LASTEXITCODE -ne 0) { throw 'AST inline If Exit compilation failed.' }
$inlineExitResult = dotnet (Join-Path $inlineExitOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inlineExitResult -join "`n") -ne '') { throw 'AST inline If Exit regression failed.' }
Write-Host 'AST_INLINE_IF_EXIT_OK'
$operatorOutput = Join-Path $output 'operators-core'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'operators-core.xps') -o $operatorOutput
if ($LASTEXITCODE -ne 0) { throw 'AST core operator compilation failed.' }
$operatorResult = dotnet (Join-Path $operatorOutput 'Generated.dll')
$expectedOperators = @('POW=8', 'INTDIV=2', 'MOD=2', 'XORBOOL=True', 'EQVBOOL=True', 'IMPBOOL=False', 'ANDBITS=0', 'ORBITS=15', 'XORBITS=255', 'NOTBITS=-4')
if ($LASTEXITCODE -ne 0 -or (($operatorResult -join "`n") -cne ($expectedOperators -join "`n"))) { throw "AST core operator regression failed: $($operatorResult -join ', ')" }
Write-Host 'AST_CORE_OPERATORS_OK'
$incrementOutput = Join-Path $output 'increment-compound-operators'
dotnet $compiler ast-compile (Join-Path $root 'samples/increment-compound-operators.xps') -o $incrementOutput
if ($LASTEXITCODE -ne 0) { throw 'AST increment and compound assignment compilation failed.' }
$incrementResult = dotnet (Join-Path $incrementOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($incrementResult -join "`n") -notmatch 'INCREMENT-COMPOUND-OPERATORS=OK') { throw 'AST increment and compound assignment runtime regression failed.' }
Write-Host 'AST_INCREMENT_COMPOUND_OK'
$arrayIndexOutput = Join-Path $output 'operators-arrays'
dotnet $compiler ast-compile (Join-Path $root 'samples/operators-arrays.xps') -o $arrayIndexOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ArrayGetIndex regression compilation failed.' }
$arrayIndexResult = dotnet (Join-Path $arrayIndexOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayIndexResult -join "`n") -notmatch 'APPEND=1:6:One\|Two\|Three\|Four\|Five\|Six' -or ($arrayIndexResult -join "`n") -notmatch 'GETINDEX=5' -or ($arrayIndexResult -join "`n") -notmatch 'GETINDEX-MISS-NULL=OK' -or ($arrayIndexResult -join "`n") -notmatch 'EXPLODE=A\|a\|B\|b\|B' -or ($arrayIndexResult -join "`n") -notmatch 'UNIQUE=A\|B' -or ($arrayIndexResult -join "`n") -notmatch 'SLICE=a\|B\|b' -or ($arrayIndexResult -join "`n") -notmatch 'SPLICE-REMOVED=a\|B' -or ($arrayIndexResult -join "`n") -notmatch 'SPLICE-ARRAY=A\|X\|Y\|b\|B' -or ($arrayIndexResult -join "`n") -notmatch 'BRACE=Hello "Fredrik"' -or ($arrayIndexResult -join "`n") -notmatch 'BAR=Hello \| Fredrik') { throw 'AST operator-array compatibility regression failed.' }
Write-Host 'AST_ARRAY_GET_INDEX_NULL_OK'
$stringOverloadOutput = Join-Path $output 'native-http-resource-limits'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-http-resource-limits.xps') -o $stringOverloadOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $stringOverloadOutput 'Generated.dll'))) { throw 'AST String(count, value) overload regression failed.' }
Write-Host 'AST_STRING_OVERLOAD_OK'
$jsonRegressionOutput = Join-Path $output 'native-json-regression'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-json-regression.xps') -o $jsonRegressionOutput
if ($LASTEXITCODE -ne 0) { throw 'AST native JSON regression compilation failed.' }
$jsonRegressionResult = dotnet (Join-Path $jsonRegressionOutput 'Generated.dll')
$jsonRegressionText = $jsonRegressionResult -join "`n"
if ($LASTEXITCODE -ne 0 -or $jsonRegressionText -notmatch 'ALIASJSON=\{"api-key":"secret","end":"done"\}' -or $jsonRegressionText -notmatch 'ALIASREAD=incoming\|finished' -or $jsonRegressionText -notmatch 'ALIASROUNDTRIP=\{"api-key":"incoming","end":"finished"\}') { throw 'AST JsonName metadata regression failed.' }
Write-Host 'AST_JSON_NAME_OK'
$crossPlatformOutput = Join-Path $output 'file-exists-string-template'
dotnet $compiler ast-compile (Join-Path $root 'samples/file-exists-string-template.xps') -o $crossPlatformOutput
if ($LASTEXITCODE -ne 0) { throw 'AST cross-platform string-template regression compilation failed.' }
$crossPlatformResult = dotnet (Join-Path $crossPlatformOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($crossPlatformResult -join "`n") -notmatch 'Hello fredrik, you are 52 years old\. Escaped: \{0\} and \}\.') { throw 'AST StrTemplate regression failed.' }
Write-Host 'AST_STRTEMPLATE_OK'
$filePositionOutput = Join-Path $output 'file-position-reset'
dotnet $compiler ast-compile (Join-Path $root 'samples/file-position-reset.xps') -o $filePositionOutput
if ($LASTEXITCODE -ne 0) { throw 'AST file position/reset regression compilation failed.' }
$filePositionResult = dotnet (Join-Path $filePositionOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($filePositionResult -join "`n") -notmatch 'FILE-POSITION-RESET=OK') { throw 'AST file position/reset regression failed.' }
Write-Host 'AST_FILE_POSITION_RESET_OK'
$platformShellOutput = Join-Path $output 'platform-shell'
dotnet $compiler ast-compile (Join-Path $root 'samples/platform-shell.xps') -o $platformShellOutput
if ($LASTEXITCODE -ne 0) { throw 'AST platform shell regression compilation failed.' }
$platformShellResult = dotnet (Join-Path $platformShellOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($platformShellResult -join "`n") -notmatch 'PLATFORM=' -or ($platformShellResult -join "`n") -notmatch 'XPScript-Windows|XPScript-Linux|XPScript-MacOS') { throw 'AST Shell regression failed.' }
Write-Host 'AST_PLATFORM_SHELL_OK'
$shellProcessIdOutput = Join-Path $output 'shell-process-id-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'shell-process-id-next.xps') -o $shellProcessIdOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Shell process-ID regression compilation failed.' }
$shellProcessIdResult = dotnet (Join-Path $shellProcessIdOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($shellProcessIdResult -join "`n") -notmatch 'SHELL_PROCESS_IDS_OK') { throw 'AST Shell process-ID regression failed.' }
Write-Host 'AST_SHELL_PROCESS_IDS_OK'
$shellArgsOutput = Join-Path $output 'shell-structured-arguments'
dotnet $compiler ast-compile (Join-Path $root 'samples/shell-structured-arguments.xps') -o $shellArgsOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $shellArgsOutput 'Generated.dll'))) { throw 'AST ShellArgs regression failed.' }
Write-Host 'AST_SHELL_ARGS_OK'
$jsonSchemaOutput = Join-Path $output 'xpjsonschema-boolean-runtime'
dotnet $compiler ast-compile (Join-Path $root 'samples/xpjsonschema-boolean-runtime.xps') -o $jsonSchemaOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON schema boolean regression compilation failed.' }
$jsonSchemaResult = dotnet (Join-Path $jsonSchemaOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonSchemaResult -join "`n") -notmatch 'XPJSONSCHEMA-BOOLEAN-RUNTIME=OK') { throw 'AST JSON schema boolean regression failed.' }
Write-Host 'AST_JSON_SCHEMA_BOOLEAN_OK'
$nativeNullOutput = Join-Path $output 'native-null-abi'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-null-abi.xps') -o $nativeNullOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $nativeNullOutput 'Generated.dll'))) { throw 'AST scalar native declaration regression failed.' }
Write-Host 'AST_NATIVE_DECLARE_COMPILE_OK'
$nativeSubOutput = Join-Path $output 'native-sub-declaration'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-sub-declaration.xps') -o $nativeSubOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $nativeSubOutput 'Generated.dll'))) { throw 'AST native Sub declaration regression failed.' }
$nativeSubResult = dotnet (Join-Path $nativeSubOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nativeSubResult -join "`n") -notmatch 'NATIVE-SUB-COMPILE-OK') { throw 'AST native Sub declaration regression failed.' }
Write-Host 'AST_NATIVE_SUB_OK'
$nativeBuiltinCollisionOutput = Join-Path $output 'native-builtin-collision'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'native-builtin-collision-next.xps') -o $nativeBuiltinCollisionOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $nativeBuiltinCollisionOutput 'Generated.dll'))) { throw 'AST native declaration/built-in overload collision regression failed.' }
Write-Host 'AST_NATIVE_BUILTIN_COLLISION_OK'
$nativeByRefOutput = Join-Path $output 'native-byref-error-ast'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-byref-error-ast.xps') -o $nativeByRefOutput 2>&1 | Out-String | Tee-Object -Variable nativeByRefDiagnostics | Out-Null
if ($LASTEXITCODE -eq 0 -or $nativeByRefDiagnostics -notmatch 'XPS2013') { throw 'AST native ByRef diagnostic regression failed.' }
Write-Host 'AST_NATIVE_BYREF_DIAGNOSTIC_OK'
$nativeScalarOutput = Join-Path $output 'native-scalar-types'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-scalar-types.xps') -o $nativeScalarOutput
if ($LASTEXITCODE -ne 0) { throw 'AST native scalar type regression failed.' }
$nativeScalarResult = dotnet (Join-Path $nativeScalarOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nativeScalarResult -join "`n") -notmatch 'NATIVE-SCALAR-TYPES-COMPILE-OK') { throw 'AST native scalar type regression failed.' }
Write-Host 'AST_NATIVE_SCALAR_TYPES_OK'
$nativeUnicodeOutput = Join-Path $output 'native-unicode-declaration'
dotnet $compiler ast-compile (Join-Path $root 'samples/native-unicode-declaration.xps') -o $nativeUnicodeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST native Unicode declaration regression failed.' }
$nativeUnicodeResult = dotnet (Join-Path $nativeUnicodeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nativeUnicodeResult -join "`n") -notmatch 'NATIVE-UNICODE-COMPILE-OK') { throw 'AST native Unicode declaration regression failed.' }
Write-Host 'AST_NATIVE_UNICODE_OK'
$nativeMissingOutput = Join-Path $output 'native-missing-dependency'
$nativeMissingDiagnostics = dotnet $compiler ast-compile (Join-Path $root 'samples/native-target-mismatch-error.xps') -o $nativeMissingOutput 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $nativeMissingDiagnostics -notmatch 'native-probe.dll|dependency') { throw 'AST native missing dependency diagnostic regression failed.' }
Write-Host 'AST_NATIVE_MISSING_DEPENDENCY_OK'
# Exercise the previously failing Application metadata/build path before broad regressions.
& (Join-Path $PSScriptRoot 'run-ast-array-signature.ps1')
$missingPathOutput = Join-Path $output 'file-missing-path-errors'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-missing-path-errors.xps') -o $missingPathOutput
if ($LASTEXITCODE -ne 0) { throw 'AST missing-file/path error fixture compilation failed.' }
$missingPathResult = dotnet (Join-Path $missingPathOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($missingPathResult -join "`n") -ne 'FILE_MISSING_PATH_ERRORS_OK') { throw 'AST missing-file/path errors regression failed.' }
Write-Host 'AST_FILE_MISSING_PATH_ERRORS_OK'
$systemInventoryOutput = Join-Path $output 'systeminventory-native-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'systeminventory-native-next.xps') -o $systemInventoryOutput
if ($LASTEXITCODE -ne 0) { throw 'AST SystemInventory native-surface compilation failed.' }
$systemInventoryResult = dotnet (Join-Path $systemInventoryOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($systemInventoryResult -join "`n") -ne 'SYSTEMINVENTORY_AST_OK') { throw 'AST SystemInventory native-surface regression failed.' }
Write-Host 'AST_SYSTEMINVENTORY_NATIVE_SURFACE_OK'
$sqliteOutput = Join-Path $output 'sqlite-native-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'sqlite-native-next.xps') -o $sqliteOutput
if ($LASTEXITCODE -ne 0) { throw 'AST SQLite native-surface compilation failed.' }
if (Test-Path -LiteralPath (Join-Path $sqliteOutput 'MySqlConnector.dll')) {
    throw 'AST SQLite output incorrectly staged the unused MySqlConnector dependency.'
}
$sqliteResult = dotnet (Join-Path $sqliteOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($sqliteResult -join "`n") -ne 'SQLITE_AST_OK') { throw 'AST SQLite native-surface regression failed.' }
Write-Host 'AST_SQLITE_OK'
$legacySqliteOutput = Join-Path $output $(if ($IsWindows) { 'sqlite-native-legacy.exe' } else { 'sqlite-native-legacy' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'sqlite-native-next.xps') -o $legacySqliteOutput --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Legacy SQLite native-surface compilation failed.' }
$legacySqliteStdout = [System.IO.Path]::GetTempFileName()
try {
    $legacySqliteProcess = Start-Process -FilePath $legacySqliteOutput -RedirectStandardOutput $legacySqliteStdout -NoNewWindow -Wait -PassThru
    $legacySqliteResult = if (Test-Path $legacySqliteStdout) { Get-Content -LiteralPath $legacySqliteStdout } else { @() }
    if ($legacySqliteProcess.ExitCode -ne 0 -or ($legacySqliteResult -join "`n") -notmatch 'SQLITE_AST_OK') { throw 'Legacy SQLite native-surface regression failed.' }
} finally {
    Remove-Item -LiteralPath $legacySqliteStdout -Force -ErrorAction SilentlyContinue
}
Write-Host 'LEGACY_SQLITE_OK'
$mysqlOutput = Join-Path $output 'mysql-native-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'mysql-native-next.xps') -o $mysqlOutput
if ($LASTEXITCODE -ne 0) { throw 'AST MySQL native-surface compilation failed.' }
$mysqlConnectorAssembly = Join-Path $mysqlOutput 'MySqlConnector.dll'
$mysqlNotice = Join-Path $mysqlOutput 'Third-party-license.txt'
if (-not (Test-Path -LiteralPath $mysqlConnectorAssembly) -or -not (Test-Path -LiteralPath $mysqlNotice)) {
    throw 'AST MySQL published output did not stage its conditional runtime dependency and third-party notice.'
}
$runtimePackageCache = Join-Path $output 'mysql-runtime-empty-package-cache'
if (Test-Path -LiteralPath $runtimePackageCache) { Remove-Item -LiteralPath $runtimePackageCache -Recurse -Force }
New-Item -ItemType Directory -Path $runtimePackageCache -Force | Out-Null
$previousNugetPackages = $env:NUGET_PACKAGES
try {
    $env:NUGET_PACKAGES = $runtimePackageCache
    $mysqlRuntimeResult = dotnet (Join-Path $mysqlOutput 'Generated.dll')
    if ($LASTEXITCODE -ne 0 -or ($mysqlRuntimeResult -join "`n") -ne 'MYSQL_AST_OK') {
        throw 'AST MySQL compiled application did not run from its published output without a NuGet cache.'
    }
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
}
$mysqlResult = dotnet (Join-Path $mysqlOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($mysqlResult -join "`n") -ne 'MYSQL_AST_OK') { throw 'AST MySQL native-surface regression failed.' }
Write-Host 'AST_MYSQL_OK'
$legacyMysqlOutput = Join-Path $output $(if ($IsWindows) { 'mysql-native-legacy.exe' } else { 'mysql-native-legacy' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'mysql-native-next.xps') -o $legacyMysqlOutput --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Legacy MySQL native-surface compilation failed.' }
$legacyMysqlStdout = [System.IO.Path]::GetTempFileName()
try {
    $legacyMysqlProcess = Start-Process -FilePath $legacyMysqlOutput -RedirectStandardOutput $legacyMysqlStdout -NoNewWindow -Wait -PassThru
    $legacyMysqlResult = if (Test-Path $legacyMysqlStdout) { Get-Content -LiteralPath $legacyMysqlStdout } else { @() }
    if ($legacyMysqlProcess.ExitCode -ne 0 -or ($legacyMysqlResult -join "`n") -notmatch 'MYSQL_AST_OK') { throw 'Legacy MySQL native-surface regression failed.' }
} finally {
    Remove-Item -LiteralPath $legacyMysqlStdout -Force -ErrorAction SilentlyContinue
}
Write-Host 'LEGACY_MYSQL_OK'
$mssqlOutput = Join-Path $output 'mssql-native-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'mssql-native-next.xps') -o $mssqlOutput
if ($LASTEXITCODE -ne 0) { throw 'AST SQL Server native-surface compilation failed.' }
$mssqlResult = dotnet (Join-Path $mssqlOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($mssqlResult -join "`n") -ne 'MSSQL_AST_OK') { throw 'AST SQL Server native-surface regression failed.' }
Write-Host 'AST_MSSQL_OK'
$legacyMssqlOutput = Join-Path $output $(if ($IsWindows) { 'mssql-native-legacy.exe' } else { 'mssql-native-legacy' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'mssql-native-next.xps') -o $legacyMssqlOutput --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Legacy MSSQL native-surface compilation failed.' }
$legacyMssqlStdout = [System.IO.Path]::GetTempFileName()
try {
    $legacyMssqlProcess = Start-Process -FilePath $legacyMssqlOutput -RedirectStandardOutput $legacyMssqlStdout -NoNewWindow -Wait -PassThru
    $legacyMssqlResult = if (Test-Path $legacyMssqlStdout) { Get-Content -LiteralPath $legacyMssqlStdout } else { @() }
    if ($legacyMssqlProcess.ExitCode -ne 0 -or ($legacyMssqlResult -join "`n") -notmatch 'MSSQL_AST_OK') { throw 'Legacy MSSQL native-surface regression failed.' }
} finally {
    Remove-Item -LiteralPath $legacyMssqlStdout -Force -ErrorAction SilentlyContinue
}
Write-Host 'LEGACY_MSSQL_OK'
$legacySupabaseOutput = Join-Path $output $(if ($IsWindows) { 'supabase-native-legacy.exe' } else { 'supabase-native-legacy' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'supabase-native-next.xps') -o $legacySupabaseOutput --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Legacy Supabase native-surface compilation failed.' }
$legacySupabaseStdout = [System.IO.Path]::GetTempFileName()
try {
    $legacySupabaseProcess = Start-Process -FilePath $legacySupabaseOutput -RedirectStandardOutput $legacySupabaseStdout -NoNewWindow -Wait -PassThru
    $legacySupabaseResult = if (Test-Path $legacySupabaseStdout) { Get-Content -LiteralPath $legacySupabaseStdout } else { @() }
    if ($legacySupabaseProcess.ExitCode -ne 0 -or ($legacySupabaseResult -join "`n") -notmatch 'SUPABASE_AST_OK') { throw 'Legacy Supabase native-surface regression failed.' }
} finally {
    Remove-Item -LiteralPath $legacySupabaseStdout -Force -ErrorAction SilentlyContinue
}
Write-Host 'LEGACY_SUPABASE_OK'
$supabaseOutput = Join-Path $output 'supabase-native-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'supabase-native-next.xps') -o $supabaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Supabase native-surface compilation failed.' }
$supabaseResult = dotnet (Join-Path $supabaseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($supabaseResult -join "`n") -ne 'SUPABASE_AST_OK') { throw 'AST Supabase native-surface regression failed.' }
Write-Host 'AST_SUPABASE_OK'
$initializedStringOutput = Join-Path $output 'initialized-module-reference-string-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-string-next.xps') -o $initializedStringOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized module string-reference compilation failed.' }
$initializedStringResult = dotnet (Join-Path $initializedStringOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedStringResult -join "`n") -ne 'hello') { throw 'AST initialized module string-reference regression failed.' }
Write-Host 'AST_INITIALIZED_MODULE_STRING_REFERENCE_OK'
$initializedVariableOutput = Join-Path $output 'initialized-module-reference-variable-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-variable-next.xps') -o $initializedVariableOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized module variable-reference compilation failed.' }
$initializedVariableResult = dotnet (Join-Path $initializedVariableOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedVariableResult -join "`n") -ne '12') { throw 'AST initialized module variable-reference regression failed.' }
Write-Host 'AST_INITIALIZED_MODULE_VARIABLE_REFERENCE_OK'
$initializedNothingOutput = Join-Path $output 'initialized-module-reference-nothing-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-nothing-next.xps') -o $initializedNothingOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized module Nothing-reference compilation failed.' }
$initializedNothingResult = dotnet (Join-Path $initializedNothingOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedNothingResult -join "`n") -ne "True`nTrue") { throw 'AST initialized module Nothing/Boolean-reference regression failed.' }
Write-Host 'AST_INITIALIZED_MODULE_NOTHING_REFERENCE_OK'
$constOutput = Join-Path $output 'const-statements'
dotnet $compiler ast-compile (Join-Path $root 'samples/const-statements.xps') -o $constOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Const fixture compilation failed.' }
$constResult = dotnet (Join-Path $constOutput 'Generated.dll')
$expectedConstResult = @(
    'NOTES_RUNTIME=C:\Program Files\HCL\Notes',
    'TEXT_VALUE=hello',
    'INTEGER_VALUE=42',
    'LONG_VALUE=2147483648',
    'DOUBLE_VALUE=12,5',
    'BOOLEAN_VALUE=True',
    'INFERRED_INTEGER=7',
    'INFERRED_LONG=2147483648',
    'INFERRED_DOUBLE=3,25',
    'INFERRED_BOOLEAN=False',
    'CONST_STATEMENTS=OK'
)
if ($LASTEXITCODE -ne 0 -or ($constResult | Select-Object -Last 11) -join "`n" -ne ($expectedConstResult -join "`n")) { throw 'AST Const support regression failed.' }
Write-Host 'AST_CONST_SUPPORT_OK'
$moduleConstOutput = Join-Path $output 'module-const-syntax-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'module-const-syntax-next.xps') -o $moduleConstOutput
if ($LASTEXITCODE -ne 0) { throw 'AST module Const syntax compilation failed.' }
$moduleConstResult = dotnet (Join-Path $moduleConstOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleConstResult -join "`n") -ne "42`nhello`n7") { throw 'AST module/procedure Const syntax regression failed.' }
Write-Host 'AST_MODULE_CONST_SYNTAX_OK'
$runtimeGlobalsOutput = Join-Path $output 'runtime-globals-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'runtime-globals-next.xps') -o $runtimeGlobalsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST runtime globals compilation failed.' }
$runtimeGlobalsResult = dotnet (Join-Path $runtimeGlobalsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($runtimeGlobalsResult -join "`n") -ne "COMMAND_READ=True`nRUNTIME_GLOBALS_OK") { throw 'AST runtime globals regression failed.' }
Write-Host 'AST_RUNTIME_GLOBALS_OK'
$moduleArraysOutput = Join-Path $output 'module-arrays'
dotnet $compiler ast-compile (Join-Path $root 'samples/module-arrays.xps') -o $moduleArraysOutput
if ($LASTEXITCODE -ne 0) { throw 'AST module array compilation failed.' }
$moduleArraysResult = dotnet (Join-Path $moduleArraysOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleArraysResult -join "`n") -ne "MODULEARRAY=50`nMODULEBOUNDS=1:4`nMODULEFIXED=two") { throw 'AST module array storage regression failed.' }
Write-Host 'AST_MODULE_ARRAYS_OK'

$moduleArrayDefaultOutput = Join-Path $output 'module-array-default-private-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'module-array-default-private-next.xps') -o $moduleArrayDefaultOutput
if ($LASTEXITCODE -ne 0) { throw 'AST default-private module array compilation failed.' }
$moduleArrayDefaultResult = dotnet (Join-Path $moduleArrayDefaultOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleArrayDefaultResult -join "`n") -ne 'MODULE_DEFAULT_PRIVATE_ARRAY_OK') { throw 'AST default-private module array regression failed.' }
Write-Host 'AST_MODULE_DEFAULT_PRIVATE_ARRAY_OK'
$aiReservedParameterOutput = Join-Path $output 'ai-tool-reserved-parameter'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-tool-reserved-parameter.xps') -o $aiReservedParameterOutput
if ($LASTEXITCODE -ne 0) { throw 'AST AI tool reserved parameter compilation failed.' }
$aiReservedParameterResult = dotnet (Join-Path $aiReservedParameterOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiReservedParameterResult -join "`n") -ne 'AI_CALL_PARAMETER_OK') { throw 'AST AI tool reserved parameter regression failed.' }
Write-Host 'AST_AI_RESERVED_PARAMETER_OK'
$archiveSecurityOutput = Join-Path $output 'archive-security-fixtures'
dotnet $compiler ast-compile (Join-Path $root 'demo/archive/archive-security-fixtures.xps') -o $archiveSecurityOutput
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $archiveSecurityOutput 'Generated.dll'))) { throw 'AST archive security fixture compilation failed.' }
Write-Host 'AST_ARCHIVE_SECURITY_FIXTURE_OK'
$csvParseOutput = Join-Path $output 'csv-parse-demo'
dotnet $compiler ast-compile (Join-Path $root 'demo/csv/csv-parse.xps') -o $csvParseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST CSV parse demo compilation failed.' }
$csvParseResult = dotnet (Join-Path $csvParseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($csvParseResult -join "`n") -notmatch 'Rows=3' -or ($csvParseResult -join "`n") -notmatch '3 Item1 Linköping') { throw 'AST CSV parse demo regression failed.' }
Write-Host 'AST_CSV_PARSE_DEMO_OK'
$aiNativeOutput = Join-Path $output 'ai-native-metadata-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-native-metadata-next.xps') -o $aiNativeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST native AI metadata compilation failed.' }
$aiNativeResult = dotnet (Join-Path $aiNativeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiNativeResult -join "`n") -ne 'AI_NATIVE_METADATA_OK') { throw 'AST native AI metadata regression failed.' }
Write-Host 'AST_AI_NATIVE_METADATA_OK'
$aiStructuredOutput = Join-Path $output 'xpai-structured-output'
dotnet $compiler ast-compile (Join-Path $root 'samples/xpai-structured-output.xps') -o $aiStructuredOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAi structured-output compilation failed.' }
$aiStructuredResult = dotnet (Join-Path $aiStructuredOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiStructuredResult -join "`n") -ne 'XPAI-STRUCTURED-RUNTIME=OK') { throw 'AST XPAi structured-output regression failed.' }
Write-Host 'AST_XPAI_STRUCTURED_OK'
$aiToolPropertiesOutput = Join-Path $output 'ai-tool-properties-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-tool-properties-next.xps') -o $aiToolPropertiesOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAITool property compilation failed.' }
$aiToolPropertiesResult = dotnet (Join-Path $aiToolPropertiesOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiToolPropertiesResult -join "`n") -ne 'AI_TOOL_PROPERTIES_OK') { throw 'AST XPAITool property regression failed.' }
Write-Host 'AST_AI_TOOL_PROPERTIES_OK'
$aiToolFunctionOutput = Join-Path $output 'ai-tool-function-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-tool-function-next.xps') -o $aiToolFunctionOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAITool function compilation failed.' }
$aiToolFunctionResult = dotnet (Join-Path $aiToolFunctionOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiToolFunctionResult -join "`n") -ne 'AI_TOOL_FUNCTION_OK') { throw 'AST XPAITool function regression failed.' }
Write-Host 'AST_AI_TOOL_FUNCTION_OK'

$aiToolCallbackErrorOutput = Join-Path $output 'ai-tool-callback-error-next'
$aiToolCallbackErrorText = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-tool-callback-error-next.xps') -o $aiToolCallbackErrorOutput 2>&1
if ($LASTEXITCODE -ne 2 -or ($aiToolCallbackErrorText -join "`n") -notmatch 'MissingCallback') { throw 'AST missing XPAITool callback diagnostic failed.' }
Write-Host 'AST_AI_TOOL_CALLBACK_DIAGNOSTIC_OK'
$aiToolLifecycleOutput = Join-Path $output 'ai-tool-lifecycle-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'ai-tool-lifecycle-next.xps') -o $aiToolLifecycleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAITool lifecycle compilation failed.' }
$aiToolLifecycleResult = dotnet (Join-Path $aiToolLifecycleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiToolLifecycleResult -join "`n") -ne 'AI_TOOL_LIFECYCLE_OK') { throw 'AST XPAITool lifecycle regression failed.' }
Write-Host 'AST_AI_TOOL_LIFECYCLE_OK'
$xpaiRequestErrorOutput = Join-Path $output 'xpai-request-error-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpai-request-error-next.xps') -o $xpaiRequestErrorOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAi request-error compilation failed.' }
$xpaiRequestErrorResult = dotnet (Join-Path $xpaiRequestErrorOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($xpaiRequestErrorResult -join "`n") -ne 'XPAI_REQUEST_ERROR_OK') { throw 'AST XPAi request-error regression failed.' }
Write-Host 'AST_XPAI_REQUEST_ERROR_OK'
$notesSearchOutput = Join-Path $output 'notes-search-constants-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'notes-search-constants-next.xps') -o $notesSearchOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Notes search constants compilation failed.' }
$notesSearchResult = dotnet (Join-Path $notesSearchOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($notesSearchResult -join "`n") -ne 'NOTES_SEARCH_CONSTANTS_OK') { throw 'AST Notes search constants regression failed.' }
Write-Host 'AST_NOTES_SEARCH_CONSTANTS_OK'
$dateBuiltinsOutput = Join-Path $output 'date-builtins-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'date-builtins-next.xps') -o $dateBuiltinsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST date built-ins compilation failed.' }
$dateBuiltinsResult = dotnet (Join-Path $dateBuiltinsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($dateBuiltinsResult -join "`n") -ne 'DATE_BUILTINS_OK') { throw 'AST date built-ins regression failed.' }
Write-Host 'AST_DATE_BUILTINS_OK'
$datePartsOutput = Join-Path $output 'date-parts-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'date-parts-next.xps') -o $datePartsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST date parts compilation failed.' }
$datePartsResult = dotnet (Join-Path $datePartsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($datePartsResult -join "`n") -ne 'DATE_PARTS_OK') { throw 'AST date parts regression failed.' }
Write-Host 'AST_DATE_PARTS_OK'
$dateValuesOutput = Join-Path $output 'date-values-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'date-values-next.xps') -o $dateValuesOutput
if ($LASTEXITCODE -ne 0) { throw 'AST date values compilation failed.' }
$dateValuesResult = dotnet (Join-Path $dateValuesOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($dateValuesResult -join "`n") -ne 'DATE_VALUES_OK') { throw 'AST date values regression failed.' }
Write-Host 'AST_DATE_VALUES_OK'
$timePartsOutput = Join-Path $output 'time-parts-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'time-parts-next.xps') -o $timePartsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST time parts compilation failed.' }
$timePartsResult = dotnet (Join-Path $timePartsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($timePartsResult -join "`n") -ne 'TIME_PARTS_OK') { throw 'AST time parts regression failed.' }
Write-Host 'AST_TIME_PARTS_OK'
$shellIdOutput = Join-Path $output 'shell-id-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'shell-id-next.xps') -o $shellIdOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ShellId compilation failed.' }
$shellIdResult = dotnet (Join-Path $shellIdOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($shellIdResult -join "`n") -ne 'SHELL_ID_GUARD_OK') { throw 'AST ShellId regression failed.' }
Write-Host 'AST_SHELL_ID_OK'

$msgBoxOutput = Join-Path $output 'msgbox-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'msgbox-next.xps') -o $msgBoxOutput
if ($LASTEXITCODE -ne 0) { throw 'AST MsgBox compilation failed.' }
$msgBoxResult = dotnet (Join-Path $msgBoxOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($msgBoxResult -join "`n") -ne "AST MsgBox`nMSGBOX_RUNTIME_OK") { throw 'AST MsgBox runtime regression failed.' }
Write-Host 'AST_MSGBOX_OK'

$fileRuntimeOutput = Join-Path $output 'file-runtime-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-runtime-next.xps') -o $fileRuntimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST file runtime compilation failed.' }
$fileRuntimeResult = dotnet (Join-Path $fileRuntimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileRuntimeResult -join "`n") -ne 'FILE_RUNTIME_OK') { throw 'AST file runtime regression failed.' }
Write-Host 'AST_FILE_RUNTIME_OK'

$fileGetOutput = Join-Path $output 'file-get-next'
Set-Content -LiteralPath (Join-Path $root 'ast-file-get-next-regression.bin') -Value 'seed' -NoNewline
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-get-next.xps') -o $fileGetOutput
if ($LASTEXITCODE -ne 0) { throw 'AST file Get compilation failed.' }
$fileGetResult = dotnet (Join-Path $fileGetOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileGetResult -join "`n") -ne 'FILE_GET_OK') { throw 'AST file Get regression failed.' }
Write-Host 'AST_FILE_GET_OK'

$fileTextOutput = Join-Path $output 'file-text-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-text-next.xps') -o $fileTextOutput
if ($LASTEXITCODE -ne 0) { throw 'AST file text compilation failed.' }
$fileTextResult = dotnet (Join-Path $fileTextOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileTextResult -join "`n") -ne 'FILE_TEXT_OK') { throw 'AST file text regression failed.' }
Write-Host 'AST_FILE_TEXT_OK'

$fileInputCharsOutput = Join-Path $output 'file-input-chars-next'
Set-Content -LiteralPath (Join-Path $root 'ast-file-input-chars-next-v3.bin') -Value 'seed' -NoNewline
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-input-chars-next.xps') -o $fileInputCharsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Input$ compilation failed.' }
$fileInputCharsResult = dotnet (Join-Path $fileInputCharsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileInputCharsResult -join "`n") -ne 'FILE_INPUT_CHARS_OK') { throw 'AST Input$ regression failed.' }
Write-Host 'AST_FILE_INPUT_CHARS_OK'

$fileInputRecordOutput = Join-Path $output 'file-input-record-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-input-record-next.xps') -o $fileInputRecordOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Input record compilation failed.' }
$fileInputRecordResult = dotnet (Join-Path $fileInputRecordOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileInputRecordResult -join "`n") -ne 'FILE_INPUT_RECORD_OK') { throw 'AST Input record regression failed.' }
Write-Host 'AST_FILE_INPUT_RECORD_OK'

$fileLockOutput = Join-Path $output 'file-lock-next'
Set-Content -LiteralPath (Join-Path $root 'ast-file-lock-next.bin') -Value 'seed' -NoNewline
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-lock-next.xps') -o $fileLockOutput
if ($LASTEXITCODE -ne 0) { throw 'AST file lock compilation failed.' }
$fileLockResult = dotnet (Join-Path $fileLockOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileLockResult -join "`n") -ne 'FILE_LOCK_OK') { throw 'AST file lock regression failed.' }
Write-Host 'AST_FILE_LOCK_OK'

$isDirDriveOutput = Join-Path $output 'isdir-drive-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'isdir-drive-next.xps') -o $isDirDriveOutput
if ($LASTEXITCODE -ne 0) { throw 'AST IsDir drive compilation failed.' }
$isDirDriveResult = dotnet (Join-Path $isDirDriveOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($isDirDriveResult -join "`n") -ne 'ISDIR_DRIVE_OK') { throw 'AST IsDir drive regression failed.' }
Write-Host 'AST_ISDIR_DRIVE_OK'

$fileDateTimeOutput = Join-Path $output 'file-date-time-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-date-time-next.xps') -o $fileDateTimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST FileDateTime compilation failed.' }
$fileDateTimeResult = dotnet (Join-Path $fileDateTimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileDateTimeResult -join "`n") -ne 'FILE_DATETIME_OK') { throw 'AST FileDateTime regression failed.' }
Write-Host 'AST_FILE_DATETIME_OK'
$dirOutput = Join-Path $output 'dir-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'dir-next.xps') -o $dirOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Dir compilation failed.' }
$dirResult = dotnet (Join-Path $dirOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($dirResult -join "`n") -ne 'DIR_AST_OK') { throw 'AST Dir regression failed.' }
Write-Host 'AST_DIR_OK'
$fileAttributesOutput = Join-Path $output 'file-attributes-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-attributes-next.xps') -o $fileAttributesOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GetFileAttr compilation failed.' }
$fileAttributesResult = dotnet (Join-Path $fileAttributesOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileAttributesResult -join "`n") -ne 'FILE_ATTRIBUTES_AST_OK') { throw 'AST GetFileAttr regression failed.' }
Write-Host 'AST_FILE_ATTRIBUTES_OK'
$fileCopyNameOutput = Join-Path $output 'file-copy-name-next'
New-Item -ItemType Directory -Force -Path 'out/ast-filecopy-test' | Out-Null
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-copy-name-next.xps') -o $fileCopyNameOutput
if ($LASTEXITCODE -ne 0) { throw 'AST FileCopy/Name compilation failed.' }
$fileCopyNameResult = dotnet (Join-Path $fileCopyNameOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileCopyNameResult -join "`n") -ne 'FILE_COPY_NAME_AST_OK') { throw 'AST FileCopy/Name regression failed.' }
Write-Host 'AST_FILE_COPY_NAME_OK'
$chDirOutput = Join-Path $output 'chdir-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'chdir-next.xps') -o $chDirOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ChDir compilation failed.' }
$chDirResult = dotnet (Join-Path $chDirOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($chDirResult -join "`n") -ne 'CHDIR_AST_OK') { throw 'AST ChDir regression failed.' }
Write-Host 'AST_CHDIR_OK'
$fileExistsAliasesOutput = Join-Path $output 'file-exists-aliases-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-exists-aliases-next.xps') -o $fileExistsAliasesOutput
if ($LASTEXITCODE -ne 0) { throw 'AST FileExists/DirExists compilation failed.' }
$fileExistsAliasesResult = dotnet (Join-Path $fileExistsAliasesOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($fileExistsAliasesResult -join "`n") -ne 'FILE_EXISTS_ALIASES_AST_OK') { throw 'AST FileExists/DirExists regression failed.' }
Write-Host 'AST_FILE_EXISTS_ALIASES_OK'

$pathObjectOutput = Join-Path $output 'path-object-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'path-object-next.xps') -o $pathObjectOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Path object compilation failed.' }
$pathObjectResult = dotnet (Join-Path $pathObjectOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($pathObjectResult -join "`n") -ne 'PATH_OBJECT_AST_OK') { throw 'AST Path object regression failed.' }
Write-Host 'AST_PATH_OBJECT_OK'
$fileInfoOutput = Join-Path $output 'file-info-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'file-info-next.xps') -o $fileInfoOutput
if ($LASTEXITCODE -ne 0) { throw 'AST FileInfo compilation failed.' }
$fileInfoResult = dotnet (Join-Path $fileInfoOutput 'Generated.dll')
$expectedFileInfoResult = @(
    'file-exists-aliases-next.xps', '.xps', 'True', 'False', 'True', 'True',
    'ast-compile-probe', 'True', 'False', 'True', 'FILE_INFO_AST_OK'
)
if ($LASTEXITCODE -ne 0 -or ($fileInfoResult -join "`n") -ne ($expectedFileInfoResult -join "`n")) { throw 'AST FileInfo regression failed.' }
Write-Host 'AST_FILE_INFO_OK'

$inputBoxOutput = Join-Path $output 'inputbox-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'inputbox-next.xps') -o $inputBoxOutput
if ($LASTEXITCODE -ne 0) { throw 'AST InputBox compilation failed.' }
$inputBoxResult = 'INPUTBOX_OK' | dotnet (Join-Path $inputBoxOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inputBoxResult -join "`n") -notmatch 'INPUTBOX_RUNTIME_OK') { throw 'AST InputBox runtime regression failed.' }
Write-Host 'AST_INPUTBOX_OK'

$chDriveOutput = Join-Path $output 'chdrive-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'chdrive-next.xps') -o $chDriveOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ChDrive compilation failed.' }
$chDriveResult = dotnet (Join-Path $chDriveOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($chDriveResult -join "`n") -ne 'CHDRIVE_GUARD_OK') { throw 'AST ChDrive guard regression failed.' }
Write-Host 'AST_CHDRIVE_OK'

$enumOutput = Join-Path $output 'language-extensions'
dotnet $compiler ast-compile (Join-Path $root 'samples/language-extensions.xps') -o $enumOutput
if ($LASTEXITCODE -ne 0) { throw 'AST enum/Type language extensions fixture compilation failed.' }
$enumResult = dotnet (Join-Path $enumOutput 'Generated.dll')
$expectedEnumResult = @(
    'ENUM=11', 'ENUMQUAL=20', 'TYPE=Fredrik:40', 'OPTIONAL=Hello Fredrik',
    'OPTIONAL2=Hi Fredrik', 'PROPERTYLET=PropertyLet', 'STRCONV=hello world',
    'CTYPE=42', 'VALUE=7', 'COUNT=8'
)
if ($LASTEXITCODE -ne 0 -or ($enumResult -join "`n") -ne ($expectedEnumResult -join "`n")) { throw 'AST enum/Type language extensions regression failed.' }
Write-Host 'AST_ENUM_LANGUAGE_EXTENSIONS_OK'
$spanOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-span-error.xps') -o (Join-Path $output 'preprocessor-span-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($spanOutput -join "`n") -notmatch 'preprocessor-span-error\.xps:5:') { throw 'AST preprocessor source span mapping regression failed.' }
Write-Host 'AST_PREPROCESSOR_SPAN_MAPPING_OK'
$optionBaseSpanOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-span-option-base-error.xps') -o (Join-Path $output 'preprocessor-span-option-base-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($optionBaseSpanOutput -join "`n") -notmatch 'preprocessor-span-option-base-error\.xps:3:') { throw 'AST Option Base source span mapping regression failed.' }
Write-Host 'AST_OPTION_BASE_SPAN_MAPPING_OK'
$routeAttributeSpanOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-span-route-attribute-error.xps') -o (Join-Path $output 'preprocessor-span-route-attribute-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($routeAttributeSpanOutput -join "`n") -notmatch 'preprocessor-span-route-attribute-error\.xps:3:') { throw 'AST route attribute source span mapping regression failed.' }
Write-Host 'AST_ROUTE_ATTRIBUTE_SPAN_MAPPING_OK'
$sleepColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-sleep-error.xps') -o (Join-Path $output 'preprocessor-column-sleep-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($sleepColumnOutput -join "`n") -notmatch 'preprocessor-column-sleep-error\.xps:2:11:') { throw 'AST Sleep source column mapping regression failed.' }
Write-Host 'AST_SLEEP_COLUMN_MAPPING_OK'
$mkDirColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-mkdir-error.xps') -o (Join-Path $output 'preprocessor-column-mkdir-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($mkDirColumnOutput -join "`n") -notmatch 'preprocessor-column-mkdir-error\.xps:2:11:') { throw 'AST MkDir source column mapping regression failed.' }
Write-Host 'AST_MKDIR_COLUMN_MAPPING_OK'
$putColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-put-error.xps') -o (Join-Path $output 'preprocessor-column-put-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($putColumnOutput -join "`n") -notmatch 'preprocessor-column-put-error\.xps:2:10:') { throw 'AST Put source column mapping regression failed.' }
Write-Host 'AST_PUT_COLUMN_MAPPING_OK'
$openColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-open-error.xps') -o (Join-Path $output 'preprocessor-column-open-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($openColumnOutput -join "`n") -notmatch 'preprocessor-column-open-error\.xps:2:10:') { throw 'AST Open source column mapping regression failed.' }
Write-Host 'AST_OPEN_COLUMN_MAPPING_OK'
$closeColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-close-error.xps') -o (Join-Path $output 'preprocessor-column-close-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($closeColumnOutput -join "`n") -notmatch 'preprocessor-column-close-error\.xps:2:12:') { throw 'AST Close source column mapping regression failed.' }
Write-Host 'AST_CLOSE_COLUMN_MAPPING_OK'
$lockColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-lock-error.xps') -o (Join-Path $output 'preprocessor-column-lock-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($lockColumnOutput -join "`n") -notmatch 'preprocessor-column-lock-error\.xps:2:11:') { throw 'AST Lock source column mapping regression failed.' }
Write-Host 'AST_LOCK_COLUMN_MAPPING_OK'
$unlockColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-unlock-error.xps') -o (Join-Path $output 'preprocessor-column-unlock-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($unlockColumnOutput -join "`n") -notmatch 'preprocessor-column-unlock-error\.xps:2:13:') { throw 'AST Unlock source column mapping regression failed.' }
Write-Host 'AST_UNLOCK_COLUMN_MAPPING_OK'
$redimColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-redim-error.xps') -o (Join-Path $output 'preprocessor-column-redim-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($redimColumnOutput -join "`n") -notmatch 'preprocessor-column-redim-error\.xps:3:18:') { throw 'AST ReDim source column mapping regression failed.' }
Write-Host 'AST_REDIM_COLUMN_MAPPING_OK'
$printColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-print-error.xps') -o (Join-Path $output 'preprocessor-column-print-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($printColumnOutput -join "`n") -notmatch 'preprocessor-column-print-error\.xps:3:15:') { throw 'AST Print file source column mapping regression failed.' }
Write-Host 'AST_PRINT_FILE_COLUMN_MAPPING_OK'
$killColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-kill-error.xps') -o (Join-Path $output 'preprocessor-column-kill-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($killColumnOutput -join "`n") -notmatch 'preprocessor-column-kill-error\.xps:2:10:') { throw 'AST Kill source column mapping regression failed.' }
Write-Host 'AST_KILL_COLUMN_MAPPING_OK'
$inputColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-input-error.xps') -o (Join-Path $output 'preprocessor-column-input-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($inputColumnOutput -join "`n") -notmatch 'preprocessor-column-input-error\.xps:3:18:') { throw 'AST Input source column mapping regression failed.' }
Write-Host 'AST_INPUT_COLUMN_MAPPING_OK'
$lineInputColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-line-input-error.xps') -o (Join-Path $output 'preprocessor-column-line-input-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($lineInputColumnOutput -join "`n") -notmatch 'preprocessor-column-line-input-error\.xps:2:30:') { throw 'AST Line Input source column mapping regression failed.' }
Write-Host 'AST_LINE_INPUT_COLUMN_MAPPING_OK'
$errorValueColumnOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-column-error-value.xps') -o (Join-Path $output 'preprocessor-column-error-value') 2>&1
if ($LASTEXITCODE -eq 0 -or ($errorValueColumnOutput -join "`n") -notmatch 'preprocessor-column-error-value\.xps:2:20:') { throw 'AST Error$ source column mapping regression failed.' }
Write-Host 'AST_ERROR_VALUE_COLUMN_MAPPING_OK'
$imageParameterOutput = Join-Path $output 'xpimage-parameter-runtime-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-parameter-runtime-next.xps') -o $imageParameterOutput
if ($LASTEXITCODE -ne 0) { throw 'AST scalar XPImage procedure/method compilation failed.' }
$imageParameterResult = dotnet (Join-Path $imageParameterOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageParameterResult -join "`n") -ne "6`n6") { throw 'AST scalar XPImage procedure/method execution failed.' }
Write-Host 'AST_XPIMAGE_PROCEDURE_PARAMETER_OK'
$sqliteClassMethodOutput = Join-Path $output 'class-method-sqlite-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-sqlite-next.xps') -o $sqliteClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST SQLite class-method parameter compilation failed.' }
$sqliteClassMethodResult = dotnet (Join-Path $sqliteClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($sqliteClassMethodResult -join "`n") -ne 'True') { throw 'AST SQLite class-method parameter execution failed.' }
Write-Host 'AST_SQLITE_CLASS_METHOD_PARAMETER_OK'
$databaseClassMethodOutput = Join-Path $output 'class-method-database-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-database-next.xps') -o $databaseClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST database class-method parameter compilation failed.' }
$databaseClassMethodResult = dotnet (Join-Path $databaseClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($databaseClassMethodResult -join "`n") -ne "True`nTrue`nTrue`nTrue`nTrue") { throw 'AST database class-method parameter execution failed.' }
Write-Host 'AST_DATABASE_CLASS_METHOD_PARAMETERS_OK'
$databaseDisposeOutput = Join-Path $output 'database-dispose-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'database-dispose-next.xps') -o $databaseDisposeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST database Dispose compilation failed.' }
$databaseDisposeResult = dotnet (Join-Path $databaseDisposeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($databaseDisposeResult -join "`n") -ne 'DATABASE_DISPOSE_OK') { throw 'AST database Dispose regression failed.' }
Write-Host 'AST_DATABASE_DISPOSE_OK'
$databaseUiDataSourceOutput = Join-Path $output 'database-ui-data-source-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'database-ui-data-source-next.xps') -o $databaseUiDataSourceOutput
if ($LASTEXITCODE -ne 0) { throw 'AST database UI data-source compilation failed.' }
$databaseUiDataSourceResult = dotnet (Join-Path $databaseUiDataSourceOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($databaseUiDataSourceResult -join "`n") -ne 'DATABASE_UI_DATA_SOURCE_OK') { throw 'AST database UI data-source regression failed.' }
Write-Host 'AST_DATABASE_UI_DATA_SOURCE_OK'
$databaseAttachmentsOutput = Join-Path $output 'database-attachments-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'database-attachments-next.xps') -o $databaseAttachmentsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST database attachments compilation failed.' }
$databaseAttachmentsResult = dotnet (Join-Path $databaseAttachmentsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($databaseAttachmentsResult -join "`n") -ne 'DATABASE_ATTACHMENTS_OK') { throw 'AST database attachments regression failed.' }
Write-Host 'AST_DATABASE_ATTACHMENTS_OK'
$attachmentClassMethodOutput = Join-Path $output 'class-method-attachments-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-attachments-next.xps') -o $attachmentClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST attachment class-method parameter compilation failed.' }
$attachmentClassMethodResult = dotnet (Join-Path $attachmentClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($attachmentClassMethodResult -join "`n") -ne 'AST_ATTACHMENT_CLASS_METHOD_OK') { throw 'AST attachment class-method parameter regression failed.' }
Write-Host 'AST_ATTACHMENT_CLASS_METHOD_OK'
$inventoryClassMethodOutput = Join-Path $output 'class-method-system-inventory-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-system-inventory-next.xps') -o $inventoryClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST SystemInventory class-method parameter compilation failed.' }
$inventoryClassMethodResult = dotnet (Join-Path $inventoryClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inventoryClassMethodResult -join "`n") -ne "True`nTrue`nTrue`nTrue") { throw 'AST SystemInventory class-method parameter execution failed.' }
Write-Host 'AST_SYSTEM_INVENTORY_CLASS_METHOD_PARAMETERS_OK'
$jsonSchemaClassMethodOutput = Join-Path $output 'class-method-json-schema-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-schema-next.xps') -o $jsonSchemaClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPJsonSchema class-method parameter compilation failed.' }
$jsonSchemaClassMethodResult = dotnet (Join-Path $jsonSchemaClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonSchemaClassMethodResult -join "`n") -ne 'True') { throw 'AST XPJsonSchema class-method parameter execution failed.' }
Write-Host 'AST_JSON_SCHEMA_CLASS_METHOD_PARAMETER_OK'
$aiClassMethodOutput = Join-Path $output 'class-method-ai-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-ai-next.xps') -o $aiClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST AI class-method parameter compilation failed.' }
$aiClassMethodResult = dotnet (Join-Path $aiClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aiClassMethodResult -join "`n") -ne "True`nTrue`nTrue`nTrue`nTrue") { throw 'AST AI class-method parameter execution failed.' }
Write-Host 'AST_AI_CLASS_METHOD_PARAMETERS_OK'
$optionalClassMethodOutput = Join-Path $output 'class-method-optional-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-next.xps') -o $optionalClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional scalar class-method compilation failed.' }
$optionalClassMethodResult = dotnet (Join-Path $optionalClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalClassMethodResult -join "`n") -ne "5`n7") { throw 'AST optional scalar class-method execution failed.' }
Write-Host 'AST_OPTIONAL_CLASS_METHOD_OK'
$optionalReferenceClassMethodOutput = Join-Path $output 'class-method-optional-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-reference-next.xps') -o $optionalReferenceClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional Object class-method compilation failed.' }
$optionalReferenceClassMethodResult = dotnet (Join-Path $optionalReferenceClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalReferenceClassMethodResult -join "`n") -ne "0`n1") { throw 'AST optional Object class-method execution failed.' }
Write-Host 'AST_OPTIONAL_REFERENCE_CLASS_METHOD_OK'
$optionalUserReferenceClassMethodOutput = Join-Path $output 'class-method-optional-user-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-user-reference-next.xps') -o $optionalUserReferenceClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional user-reference class-method compilation failed.' }
$optionalUserReferenceClassMethodResult = dotnet (Join-Path $optionalUserReferenceClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalUserReferenceClassMethodResult -join "`n") -ne "0`n14") { throw 'AST optional user-reference class-method execution failed.' }
Write-Host 'AST_OPTIONAL_USER_REFERENCE_CLASS_METHOD_OK'
$optionalByRefReferenceClassMethodOutput = Join-Path $output 'class-method-optional-byref-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-byref-reference-next.xps') -o $optionalByRefReferenceClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional ByRef Object class-method compilation failed.' }
$optionalByRefReferenceClassMethodResult = dotnet (Join-Path $optionalByRefReferenceClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalByRefReferenceClassMethodResult -join "`n") -ne "0`n1") { throw 'AST optional ByRef Object class-method execution failed.' }
Write-Host 'AST_OPTIONAL_BYREF_REFERENCE_CLASS_METHOD_OK'
$optionalByRefUserReferenceClassMethodOutput = Join-Path $output 'class-method-optional-byref-user-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-byref-user-reference-next.xps') -o $optionalByRefUserReferenceClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional ByRef user-reference class-method compilation failed.' }
$optionalByRefUserReferenceClassMethodResult = dotnet (Join-Path $optionalByRefUserReferenceClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalByRefUserReferenceClassMethodResult -join "`n") -ne "0`n15") { throw 'AST optional ByRef user-reference class-method execution failed.' }
Write-Host 'AST_OPTIONAL_BYREF_USER_REFERENCE_CLASS_METHOD_OK'
$staticClassMethodOutput = Join-Path $output 'class-method-static-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-static-next.xps') -o $staticClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST scalar Static class-method compilation failed.' }
$staticClassMethodResult = dotnet (Join-Path $staticClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticClassMethodResult -join "`n") -ne "1`n2`n3") { throw 'AST scalar Static class-method execution failed.' }
Write-Host 'AST_STATIC_CLASS_METHOD_OK'
$derivedClassMethodOutput = Join-Path $output 'class-method-derived-next'
$derivedConstructorFailureOutput = Join-Path $output 'class-constructor-derived-failure-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-constructor-derived-failure-next.xps') -o $derivedConstructorFailureOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived constructor failure compilation failed.' }
$derivedConstructorFailureResult = dotnet (Join-Path $derivedConstructorFailureOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedConstructorFailureResult -join "`n") -ne 'CLASS_DERIVED_CONSTRUCTOR_FAILURE_OK') { throw 'AST derived constructor failure cleanup failed.' }
Write-Host 'AST_DERIVED_CONSTRUCTOR_FAILURE_OK'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-derived-next.xps') -o $derivedClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived scalar class-method compilation failed.' }
$derivedClassMethodResult = dotnet (Join-Path $derivedClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedClassMethodResult -join "`n") -ne '8') { throw 'AST derived scalar class-method execution failed.' }
Write-Host 'AST_DERIVED_CLASS_METHOD_OK'
$parentClassMethodOutput = Join-Path $output 'class-method-parent-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-parent-next.xps') -o $parentClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Parent.member class-method compilation failed.' }
$parentClassMethodResult = dotnet (Join-Path $parentClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($parentClassMethodResult -join "`n") -ne '7') { throw 'AST Parent.member class-method execution failed.' }
Write-Host 'AST_PARENT_CLASS_METHOD_OK'
$overrideClassMethodOutput = Join-Path $output 'class-method-override-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-override-next.xps') -o $overrideClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST class-method override compilation failed.' }
$overrideClassMethodResult = dotnet (Join-Path $overrideClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($overrideClassMethodResult -join "`n") -ne '9') { throw 'AST class-method override execution failed.' }
Write-Host 'AST_CLASS_METHOD_OVERRIDE_OK'
$referenceClassMethodOutput = Join-Path $output 'class-method-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-reference-next.xps') -o $referenceClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST concrete reference class-method compilation failed.' }
$referenceClassMethodResult = dotnet (Join-Path $referenceClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($referenceClassMethodResult -join "`n") -ne '7') { throw 'AST concrete reference class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_REFERENCE_OK'
$listClassMethodOutput = Join-Path $output 'class-method-list-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-list-next.xps') -o $listClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List class-method compilation failed.' }
$listClassMethodResult = dotnet (Join-Path $listClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listClassMethodResult -join "`n") -ne "8`n8") { throw 'AST List class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_LIST_OK'
$byRefListClassMethodOutput = Join-Path $output 'class-method-list-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-list-byref-next.xps') -o $byRefListClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ByRef List class-method compilation failed.' }
$byRefListClassMethodResult = dotnet (Join-Path $byRefListClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($byRefListClassMethodResult -join "`n") -ne '21') { throw 'AST ByRef List class-method execution failed.' }
Write-Host 'AST_BYREF_LIST_CLASS_METHOD_OK'
$listElementByRefMethodOutput = Join-Path $output 'class-method-list-element-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-list-element-byref-next.xps') -o $listElementByRefMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List element ByRef class-method compilation failed.' }
$listElementByRefMethodResult = dotnet (Join-Path $listElementByRefMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listElementByRefMethodResult -join "`n") -ne "11`n11") { throw 'AST List element ByRef class-method copy-back failed.' }
Write-Host 'AST_LIST_ELEMENT_BYREF_CLASS_METHOD_OK'
$optionalListClassMethodOutput = Join-Path $output 'class-method-optional-list-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-list-next.xps') -o $optionalListClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional List class-method compilation failed.' }
$optionalListClassMethodResult = dotnet (Join-Path $optionalListClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalListClassMethodResult -join "`n") -ne "0`n12`n12") { throw 'AST optional List class-method execution failed.' }
Write-Host 'AST_OPTIONAL_LIST_CLASS_METHOD_OK'
$optionalByRefListClassMethodOutput = Join-Path $output 'class-method-optional-byref-list-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-byref-list-next.xps') -o $optionalByRefListClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional ByRef List class-method compilation failed.' }
$optionalByRefListClassMethodResult = dotnet (Join-Path $optionalByRefListClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalByRefListClassMethodResult -join "`n") -ne "0`n12`n12") { throw 'AST optional ByRef List class-method execution failed.' }
Write-Host 'AST_OPTIONAL_BYREF_LIST_CLASS_METHOD_OK'
$optionalArrayClassMethodOutput = Join-Path $output 'class-method-optional-array-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-array-next.xps') -o $optionalArrayClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional array class-method compilation failed.' }
$optionalArrayClassMethodResult = dotnet (Join-Path $optionalArrayClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalArrayClassMethodResult -join "`n") -ne "0`n7`n7") { throw 'AST optional array class-method execution failed.' }
Write-Host 'AST_OPTIONAL_ARRAY_CLASS_METHOD_OK'
$optionalByRefArrayClassMethodOutput = Join-Path $output 'class-method-optional-byref-array-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-byref-array-next.xps') -o $optionalByRefArrayClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional ByRef array class-method compilation failed.' }
$optionalByRefArrayClassMethodResult = dotnet (Join-Path $optionalByRefArrayClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalByRefArrayClassMethodResult -join "`n") -ne "9`n9`n9") { throw 'AST optional ByRef array class-method execution failed.' }
Write-Host 'AST_OPTIONAL_BYREF_ARRAY_CLASS_METHOD_OK'
$staticListClassMethodOutput = Join-Path $output 'class-method-static-list-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-static-list-next.xps') -o $staticListClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static List class-method compilation failed.' }
$staticListClassMethodResult = dotnet (Join-Path $staticListClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticListClassMethodResult -join "`n") -ne "1`n2") { throw 'AST Static List class-method execution failed.' }
Write-Host 'AST_STATIC_LIST_CLASS_METHOD_OK'
$staticArrayClassMethodOutput = Join-Path $output 'class-method-static-array-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-static-array-next.xps') -o $staticArrayClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static array class-method compilation failed.' }
$staticArrayClassMethodResult = dotnet (Join-Path $staticArrayClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticArrayClassMethodResult -join "`n") -ne "1`n2") { throw 'AST Static array class-method execution failed.' }
Write-Host 'AST_STATIC_ARRAY_CLASS_METHOD_OK'
$arrayClassMethodOutput = Join-Path $output 'method-array-runtime-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'method-array-runtime-next.xps') -o $arrayClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST array class-method compilation failed.' }
$arrayClassMethodResult = dotnet (Join-Path $arrayClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayClassMethodResult -join "`n") -ne '43:-3:-1') { throw 'AST array class-method execution failed.' }
Write-Host 'AST_ARRAY_CLASS_METHOD_OK'
$objectClassMethodOutput = Join-Path $output 'class-method-object-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-object-next.xps') -o $objectClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Object class-method compilation failed.' }
$objectClassMethodResult = dotnet (Join-Path $objectClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectClassMethodResult -join "`n") -ne "0`n1") { throw 'AST Object class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_OBJECT_OK'
$variantClassMethodOutput = Join-Path $output 'class-method-variant-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-variant-next.xps') -o $variantClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Variant class-method compilation failed.' }
$variantClassMethodResult = dotnet (Join-Path $variantClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($variantClassMethodResult -join "`n") -ne '5') { throw 'AST Variant class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_VARIANT_OK'
$jsonClassMethodOutput = Join-Path $output 'class-method-json-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-next.xps') -o $jsonClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON class-method compilation failed.' }
$jsonClassMethodResult = dotnet (Join-Path $jsonClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonClassMethodResult -join "`n") -ne '5') { throw 'AST JSON class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_JSON_OK'
$jsonArrayClassMethodOutput = Join-Path $output 'class-method-json-array-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-array-next.xps') -o $jsonArrayClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON array class-method compilation failed.' }
$jsonArrayClassMethodResult = dotnet (Join-Path $jsonArrayClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonArrayClassMethodResult -join "`n") -ne '2') { throw 'AST JSON array class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_JSON_ARRAY_OK'
$jsonDocumentClassMethodOutput = Join-Path $output 'class-method-json-document-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-document-next.xps') -o $jsonDocumentClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON document class-method compilation failed.' }
$jsonDocumentClassMethodResult = dotnet (Join-Path $jsonDocumentClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonDocumentClassMethodResult -join "`n") -ne 'Array') { throw 'AST JSON document class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_JSON_DOCUMENT_OK'
$jsonElementClassMethodOutput = Join-Path $output 'class-method-json-element-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-element-next.xps') -o $jsonElementClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON element class-method compilation failed.' }
$jsonElementClassMethodResult = dotnet (Join-Path $jsonElementClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($jsonElementClassMethodResult -join "`n") -ne 'Boolean') { throw 'AST JSON element class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_JSON_ELEMENT_OK'
$xmlClassMethodOutput = Join-Path $output 'class-method-xml-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-xml-next.xps') -o $xmlClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XML class-method compilation failed.' }
$xmlClassMethodResult = dotnet (Join-Path $xmlClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($xmlClassMethodResult -join "`n") -ne 'root') { throw 'AST XML class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_XML_OK'
$xmlDocumentClassMethodOutput = Join-Path $output 'class-method-xml-document-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-xml-document-next.xps') -o $xmlDocumentClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XML document class-method compilation failed.' }
$xmlDocumentClassMethodResult = dotnet (Join-Path $xmlDocumentClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($xmlDocumentClassMethodResult -join "`n") -ne 'root') { throw 'AST XML document class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_XML_DOCUMENT_OK'
$xmlElementClassMethodOutput = Join-Path $output 'class-method-xml-element-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-xml-element-next.xps') -o $xmlElementClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XML element class-method compilation failed.' }
$xmlElementClassMethodResult = dotnet (Join-Path $xmlElementClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($xmlElementClassMethodResult -join "`n") -ne 'root') { throw 'AST XML element class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_XML_ELEMENT_OK'
$csvClassMethodOutput = Join-Path $output 'class-method-csv-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-csv-next.xps') -o $csvClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST CSV class-method compilation failed.' }
$csvClassMethodResult = dotnet (Join-Path $csvClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($csvClassMethodResult -join "`n") -ne ';') { throw 'AST CSV class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_CSV_OK'
$spreadsheetClassMethodOutput = Join-Path $output 'class-method-spreadsheet-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-spreadsheet-next.xps') -o $spreadsheetClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST spreadsheet class-method compilation failed.' }
$spreadsheetClassMethodResult = dotnet (Join-Path $spreadsheetClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($spreadsheetClassMethodResult -join "`n") -ne '1') { throw 'AST spreadsheet class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_SPREADSHEET_OK'
$httpClientClassMethodOutput = Join-Path $output 'class-method-http-client-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-client-next.xps') -o $httpClientClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP client class-method compilation failed.' }
$httpClientClassMethodResult = dotnet (Join-Path $httpClientClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpClientClassMethodResult -join "`n") -ne '30') { throw 'AST HTTP client class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_CLIENT_OK'
$notesHttpClassMethodOutput = Join-Path $output 'class-method-notes-http-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-notes-http-next.xps') -o $notesHttpClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Notes HTTP class-method compilation failed.' }
$notesHttpClassMethodResult = dotnet (Join-Path $notesHttpClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($notesHttpClassMethodResult -join "`n") -ne 'AST_NOTES_HTTP_CLASS_METHOD_OK') { throw 'AST Notes HTTP class-method execution failed.' }
Write-Host 'AST_NOTES_HTTP_CLASS_METHOD_OK'
$httpRequestClassMethodOutput = Join-Path $output 'class-method-http-request-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-request-next.xps') -o $httpRequestClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP request class-method compilation failed.' }
$httpRequestClassMethodResult = dotnet (Join-Path $httpRequestClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpRequestClassMethodResult -join "`n") -ne 'PATCH') { throw 'AST HTTP request class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_REQUEST_OK'
$httpResponseClassMethodOutput = Join-Path $output 'class-method-http-response-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-response-next.xps') -o $httpResponseClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP response class-method compilation failed.' }
$httpResponseClassMethodResult = dotnet (Join-Path $httpResponseClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpResponseClassMethodResult -join "`n") -ne '1') { throw 'AST HTTP response class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_RESPONSE_OK'
$httpPartsClassMethodOutput = Join-Path $output 'class-method-http-parts-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-parts-next.xps') -o $httpPartsClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP part/file class-method compilation failed.' }
$httpPartsClassMethodResult = dotnet (Join-Path $httpPartsClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpPartsClassMethodResult -join "`n") -ne '1') { throw 'AST HTTP part/file class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_PARTS_OK'
$httpCollectionsClassMethodOutput = Join-Path $output 'class-method-http-collections-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-collections-next.xps') -o $httpCollectionsClassMethodOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP part/file collection class-method compilation failed.' }
$httpCollectionsClassMethodResult = dotnet (Join-Path $httpCollectionsClassMethodOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpCollectionsClassMethodResult -join "`n") -ne '1') { throw 'AST HTTP part/file collection class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_COLLECTIONS_OK'
$httpResponseReturnOutput = Join-Path $output 'class-method-http-response-return-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-response-return-next.xps') -o $httpResponseReturnOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP response class-method return compilation failed.' }
$httpResponseReturnResult = dotnet (Join-Path $httpResponseReturnOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpResponseReturnResult -join "`n") -ne '1') { throw 'AST HTTP response class-method return execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_RESPONSE_RETURN_OK'
$httpPartsReturnOutput = Join-Path $output 'class-method-http-parts-return-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-parts-return-next.xps') -o $httpPartsReturnOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP part/file class-method return compilation failed.' }
$httpPartsReturnResult = dotnet (Join-Path $httpPartsReturnOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpPartsReturnResult -join "`n") -ne '1') { throw 'AST HTTP part/file class-method return execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_PARTS_RETURN_OK'
$httpCollectionsReturnOutput = Join-Path $output 'class-method-http-collections-return-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-collections-return-next.xps') -o $httpCollectionsReturnOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP part/file collection class-method return compilation failed.' }
$httpCollectionsReturnResult = dotnet (Join-Path $httpCollectionsReturnOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpCollectionsReturnResult -join "`n") -ne '1') { throw 'AST HTTP part/file collection class-method return execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_COLLECTIONS_RETURN_OK'
$classMethodNetworkToolsOutput = Join-Path $output 'class-method-networktools-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-networktools-next.xps') -o $classMethodNetworkToolsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST NetworkTools class-method compilation failed.' }
$classMethodNetworkToolsResult = dotnet (Join-Path $classMethodNetworkToolsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodNetworkToolsResult -join "`n") -ne 'CLASS_METHOD_NETWORKTOOLS_OK') { throw 'AST NetworkTools class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_NETWORKTOOLS_OK'
$classMethodPathOutput = Join-Path $output 'class-method-path-values-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-path-values-next.xps') -o $classMethodPathOutput
if ($LASTEXITCODE -ne 0) { throw 'AST path class-method compilation failed.' }
$classMethodPathResult = dotnet (Join-Path $classMethodPathOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodPathResult -join "`n") -ne 'CLASS_METHOD_PATH_VALUE_OK') { throw 'AST path class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_PATH_VALUE_OK'
$classMethodHttpDatabaseOutput = Join-Path $output 'class-method-http-database-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-http-database-next.xps') -o $classMethodHttpDatabaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP database class-method compilation failed.' }
$classMethodHttpDatabaseResult = dotnet (Join-Path $classMethodHttpDatabaseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodHttpDatabaseResult -join "`n") -ne 'CLASS_METHOD_HTTP_DATABASE_OK') { throw 'AST HTTP database class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_HTTP_DATABASE_OK'
$classMethodMssqlAliasOutput = Join-Path $output 'class-method-mssql-alias-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-mssql-alias-next.xps') -o $classMethodMssqlAliasOutput
if ($LASTEXITCODE -ne 0) { throw 'AST MSSQL alias class-method compilation failed.' }
$classMethodMssqlAliasResult = dotnet (Join-Path $classMethodMssqlAliasOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodMssqlAliasResult -join "`n") -ne 'CLASS_METHOD_MSSQL_ALIAS_OK') { throw 'AST MSSQL alias class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_MSSQL_ALIAS_OK'
$classMethodShellFileInfoOutput = Join-Path $output 'class-method-shell-fileinfo-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-shell-fileinfo-next.xps') -o $classMethodShellFileInfoOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ShellResult/FileInfo class-method compilation failed.' }
$classMethodShellFileInfoResult = dotnet (Join-Path $classMethodShellFileInfoOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodShellFileInfoResult -join "`n") -ne 'CLASS_METHOD_SHELL_FILEINFO_OK') { throw 'AST ShellResult/FileInfo class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_SHELL_FILEINFO_OK'
$classMethodJsonValidationOutput = Join-Path $output 'class-method-json-validation-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-json-validation-next.xps') -o $classMethodJsonValidationOutput
if ($LASTEXITCODE -ne 0) { throw 'AST JSON validation class-method compilation failed.' }
$classMethodJsonValidationResult = dotnet (Join-Path $classMethodJsonValidationOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodJsonValidationResult -join "`n") -ne 'CLASS_METHOD_JSON_VALIDATION_OK') { throw 'AST JSON validation class-method execution failed.' }
Write-Host 'AST_CLASS_METHOD_JSON_VALIDATION_OK'
$derivedPropertyOutput = Join-Path $output 'class-property-derived-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-derived-next.xps') -o $derivedPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived property compilation failed.' }
$derivedPropertyResult = dotnet (Join-Path $derivedPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedPropertyResult -join "`n") -ne '8') { throw 'AST derived property execution failed.' }
Write-Host 'AST_DERIVED_PROPERTY_OK'
$derivedPropertySetOutput = Join-Path $output 'class-property-derived-set-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-derived-set-next.xps') -o $derivedPropertySetOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived property setter compilation failed.' }
$derivedPropertySetResult = dotnet (Join-Path $derivedPropertySetOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedPropertySetResult -join "`n") -ne '9') { throw 'AST derived property setter execution failed.' }
Write-Host 'AST_DERIVED_PROPERTY_SET_OK'
$derivedIndexedPropertyOutput = Join-Path $output 'class-property-derived-index-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-derived-index-next.xps') -o $derivedIndexedPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived indexed property compilation failed.' }
$derivedIndexedPropertyResult = dotnet (Join-Path $derivedIndexedPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedIndexedPropertyResult -join "`n") -ne '7') { throw 'AST derived indexed property execution failed.' }
Write-Host 'AST_DERIVED_INDEXED_PROPERTY_OK'
$derivedIndexedPropertySetOutput = Join-Path $output 'class-property-derived-index-set-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-derived-index-set-next.xps') -o $derivedIndexedPropertySetOutput
if ($LASTEXITCODE -ne 0) { throw 'AST derived indexed property setter compilation failed.' }
$derivedIndexedPropertySetResult = dotnet (Join-Path $derivedIndexedPropertySetOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($derivedIndexedPropertySetResult -join "`n") -ne '13') { throw 'AST derived indexed property setter execution failed.' }
Write-Host 'AST_DERIVED_INDEXED_PROPERTY_SET_OK'
$overridePropertyOutput = Join-Path $output 'class-property-override-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-override-next.xps') -o $overridePropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST property override compilation failed.' }
$overridePropertyResult = dotnet (Join-Path $overridePropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($overridePropertyResult -join "`n") -ne '9') { throw 'AST property override execution failed.' }
Write-Host 'AST_PROPERTY_OVERRIDE_OK'
$indexedOverridePropertyOutput = Join-Path $output 'class-property-indexed-override-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-indexed-override-next.xps') -o $indexedOverridePropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST indexed property override compilation failed.' }
$indexedOverridePropertyResult = dotnet (Join-Path $indexedOverridePropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($indexedOverridePropertyResult -join "`n") -ne '12') { throw 'AST indexed property override execution failed.' }
Write-Host 'AST_INDEXED_PROPERTY_OVERRIDE_OK'
$parentPropertyOutput = Join-Path $output 'class-property-parent-next'
$emptySlotPropertyOutput = Join-Path $output 'class-property-empty-slot-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-empty-slot-next.xps') -o $emptySlotPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST empty indexed-property slot compilation failed.' }
$emptySlotPropertyResult = dotnet (Join-Path $emptySlotPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($emptySlotPropertyResult -join "`n") -ne "1`n1") { throw 'AST empty indexed-property slot execution failed.' }
Write-Host 'AST_EMPTY_INDEXED_PROPERTY_SLOT_OK'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-parent-next.xps') -o $parentPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Parent property compilation failed.' }
$parentPropertyResult = dotnet (Join-Path $parentPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($parentPropertyResult -join "`n") -ne '7') { throw 'AST Parent property execution failed.' }
Write-Host 'AST_PARENT_PROPERTY_OK'
$objectOverridePropertyOutput = Join-Path $output 'class-property-object-override-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-object-override-next.xps') -o $objectOverridePropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST object property override compilation failed.' }
$objectOverridePropertyResult = dotnet (Join-Path $objectOverridePropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectOverridePropertyResult -join "`n") -ne '12') { throw 'AST object property override execution failed.' }
Write-Host 'AST_OBJECT_PROPERTY_OVERRIDE_OK'
$objectParentPropertyOutput = Join-Path $output 'class-property-object-parent-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-object-parent-next.xps') -o $objectParentPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Parent typed-object property compilation failed.' }
$objectParentPropertyResult = dotnet (Join-Path $objectParentPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectParentPropertyResult -join "`n") -ne '9') { throw 'AST Parent typed-object property execution failed.' }
Write-Host 'AST_PARENT_OBJECT_PROPERTY_OK'
$indexedObjectPropertyOutput = Join-Path $output 'class-property-indexed-object-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-indexed-object-next.xps') -o $indexedObjectPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST indexed object property compilation failed.' }
$indexedObjectPropertyResult = dotnet (Join-Path $indexedObjectPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($indexedObjectPropertyResult -join "`n") -ne 'CLASS_INDEXED_PROPERTY_OBJECT_OK') { throw 'AST indexed object property execution failed.' }
Write-Host 'AST_INDEXED_OBJECT_PROPERTY_OK'
$initializedModuleOutput = Join-Path $output 'initialized-module-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-byref-next.xps') -o $initializedModuleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized module storage compilation failed.' }
$initializedModuleResult = dotnet (Join-Path $initializedModuleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedModuleResult -join "`n") -ne '8') { throw 'AST initialized module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_MODULE_STORAGE_OK'
$initializedBooleanOutput = Join-Path $output 'initialized-module-boolean-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-boolean-next.xps') -o $initializedBooleanOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized Boolean module storage compilation failed.' }
$initializedBooleanResult = dotnet (Join-Path $initializedBooleanOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedBooleanResult -join "`n") -ne 'False') { throw 'AST initialized Boolean module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_BOOLEAN_MODULE_STORAGE_OK'
$initializedStringOutput = Join-Path $output 'initialized-module-string-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-string-next.xps') -o $initializedStringOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized String module storage compilation failed.' }
$initializedStringResult = dotnet (Join-Path $initializedStringOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedStringResult -join "`n") -ne 'ready!') { throw 'AST initialized String module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_STRING_MODULE_STORAGE_OK'
$initializedReferenceOutput = Join-Path $output 'initialized-module-reference-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-next.xps') -o $initializedReferenceOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized reference module storage compilation failed.' }
$initializedReferenceResult = dotnet (Join-Path $initializedReferenceOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedReferenceResult -join "`n") -ne '9') { throw 'AST initialized reference module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_REFERENCE_MODULE_STORAGE_OK'
$initializedReferenceNewOutput = Join-Path $output 'initialized-module-reference-new-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-new-next.xps') -o $initializedReferenceNewOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized New reference module storage compilation failed.' }
$initializedReferenceNewResult = dotnet (Join-Path $initializedReferenceNewOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedReferenceNewResult -join "`n") -ne '12') { throw 'AST initialized New reference module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_REFERENCE_NEW_MODULE_STORAGE_OK'
$initializedReferenceArgsOutput = Join-Path $output 'initialized-module-reference-args-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-reference-args-next.xps') -o $initializedReferenceArgsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized reference-argument module storage compilation failed.' }
$initializedReferenceArgsResult = dotnet (Join-Path $initializedReferenceArgsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedReferenceArgsResult -join "`n") -ne '13') { throw 'AST initialized reference-argument module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_REFERENCE_ARGS_MODULE_STORAGE_OK'
$initializedDateOutput = Join-Path $output 'initialized-module-date-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-date-next.xps') -o $initializedDateOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized Date module storage compilation failed.' }
$initializedDateResult = dotnet (Join-Path $initializedDateOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedDateResult -join "`n") -notmatch '2026') { throw 'AST initialized Date module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_DATE_MODULE_STORAGE_OK'
$initializedXpImageOutput = Join-Path $output 'initialized-module-xpimage-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-xpimage-byref-next.xps') -o $initializedXpImageOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized XPImage module ByRef storage compilation failed.' }
$initializedXpImageResult = dotnet (Join-Path $initializedXpImageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedXpImageResult -join "`n") -ne 'INITIALIZED_MODULE_XPIMAGE_BYREF_OK') { throw 'AST initialized XPImage module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_XPIMAGE_MODULE_OK'
$initializedJsonOutput = Join-Path $output 'initialized-module-json-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-json-byref-next.xps') -o $initializedJsonOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized JSON module ByRef storage compilation failed.' }
$initializedJsonResult = dotnet (Join-Path $initializedJsonOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedJsonResult -join "`n") -ne 'INITIALIZED_MODULE_JSON_BYREF_OK') { throw 'AST initialized JSON module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_JSON_MODULE_OK'
$initializedJsonCollectionsOutput = Join-Path $output 'initialized-module-json-collections-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-json-collections-byref-next.xps') -o $initializedJsonCollectionsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized JSON collection module ByRef storage compilation failed.' }
$initializedJsonCollectionsResult = dotnet (Join-Path $initializedJsonCollectionsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedJsonCollectionsResult -join "`n") -ne 'INITIALIZED_MODULE_JSON_COLLECTIONS_BYREF_OK') { throw 'AST initialized JSON collection module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_JSON_COLLECTIONS_MODULE_OK'
$initializedXmlOutput = Join-Path $output 'initialized-module-xml-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-xml-byref-next.xps') -o $initializedXmlOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized XML module ByRef storage compilation failed.' }
$initializedXmlResult = dotnet (Join-Path $initializedXmlOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedXmlResult -join "`n") -ne 'INITIALIZED_MODULE_XML_BYREF_OK') { throw 'AST initialized XML module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_XML_MODULE_OK'
$initializedCsvOutput = Join-Path $output 'initialized-module-csv-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-csv-byref-next.xps') -o $initializedCsvOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized CSV module ByRef storage compilation failed.' }
$initializedCsvResult = dotnet (Join-Path $initializedCsvOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedCsvResult -join "`n") -ne 'INITIALIZED_MODULE_CSV_BYREF_OK') { throw 'AST initialized CSV module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_CSV_MODULE_OK'
$initializedSpreadsheetOutput = Join-Path $output 'initialized-module-spreadsheet-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-spreadsheet-byref-next.xps') -o $initializedSpreadsheetOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized spreadsheet module ByRef storage compilation failed.' }
$initializedSpreadsheetResult = dotnet (Join-Path $initializedSpreadsheetOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedSpreadsheetResult -join "`n") -ne 'INITIALIZED_MODULE_SPREADSHEET_BYREF_OK') { throw 'AST initialized spreadsheet module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_SPREADSHEET_MODULE_OK'
$initializedHttpOutput = Join-Path $output 'initialized-module-http-byref-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'initialized-module-http-byref-next.xps') -o $initializedHttpOutput
if ($LASTEXITCODE -ne 0) { throw 'AST initialized HTTP module ByRef storage compilation failed.' }
$initializedHttpResult = dotnet (Join-Path $initializedHttpOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($initializedHttpResult -join "`n") -ne 'INITIALIZED_MODULE_HTTP_BYREF_OK') { throw 'AST initialized HTTP module ByRef storage failed.' }
Write-Host 'AST_INITIALIZED_HTTP_MODULE_OK'
$virtualDispatchOutput = Join-Path $output 'class-method-virtual-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-virtual-dispatch-next.xps') -o $virtualDispatchOutput
if ($LASTEXITCODE -ne 0) { throw 'AST virtual derived method dispatch compilation failed.' }
$virtualDispatchResult = dotnet (Join-Path $virtualDispatchOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($virtualDispatchResult -join "`n") -ne 'CLASS_METHOD_VIRTUAL_DISPATCH_OK') { throw 'AST virtual derived method dispatch failed.' }
Write-Host 'AST_CLASS_METHOD_VIRTUAL_DISPATCH_OK'
$virtualPropertyOutput = Join-Path $output 'class-property-virtual-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-virtual-dispatch-next.xps') -o $virtualPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST virtual derived property dispatch compilation failed.' }
$virtualPropertyResult = dotnet (Join-Path $virtualPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($virtualPropertyResult -join "`n") -ne 'CLASS_PROPERTY_VIRTUAL_DISPATCH_OK') { throw 'AST virtual derived property dispatch failed.' }
Write-Host 'AST_CLASS_PROPERTY_VIRTUAL_DISPATCH_OK'
$virtualIndexedPropertyOutput = Join-Path $output 'class-property-indexed-virtual-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-indexed-virtual-dispatch-next.xps') -o $virtualIndexedPropertyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST virtual indexed property dispatch compilation failed.' }
$virtualIndexedPropertyResult = dotnet (Join-Path $virtualIndexedPropertyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($virtualIndexedPropertyResult -join "`n") -ne 'CLASS_INDEXED_PROPERTY_VIRTUAL_DISPATCH_OK') { throw 'AST virtual indexed property dispatch failed.' }
Write-Host 'AST_CLASS_INDEXED_PROPERTY_VIRTUAL_DISPATCH_OK'
$indexedSetterOutput = Join-Path $output 'class-property-indexed-setter-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-indexed-setter-dispatch-next.xps') -o $indexedSetterOutput
if ($LASTEXITCODE -ne 0) { throw 'AST indexed property setter dispatch compilation failed.' }
$indexedSetterResult = dotnet (Join-Path $indexedSetterOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($indexedSetterResult -join "`n") -ne 'CLASS_INDEXED_PROPERTY_SETTER_DISPATCH_OK') { throw 'AST indexed property setter dispatch failed.' }
Write-Host 'AST_CLASS_INDEXED_PROPERTY_SETTER_DISPATCH_OK'
$scalarSetterOutput = Join-Path $output 'class-property-setter-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-property-setter-dispatch-next.xps') -o $scalarSetterOutput
if ($LASTEXITCODE -ne 0) { throw 'AST scalar property setter dispatch compilation failed.' }
$scalarSetterResult = dotnet (Join-Path $scalarSetterOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($scalarSetterResult -join "`n") -ne 'CLASS_PROPERTY_SETTER_DISPATCH_OK') { throw 'AST scalar property setter dispatch failed.' }
Write-Host 'AST_CLASS_PROPERTY_SETTER_DISPATCH_OK'
$optionalVirtualDispatchOutput = Join-Path $output 'class-method-optional-virtual-dispatch-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-optional-virtual-dispatch-next.xps') -o $optionalVirtualDispatchOutput
if ($LASTEXITCODE -ne 0) { throw 'AST optional virtual method dispatch compilation failed.' }
$optionalVirtualDispatchResult = dotnet (Join-Path $optionalVirtualDispatchOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalVirtualDispatchResult -join "`n") -ne 'CLASS_OPTIONAL_METHOD_VIRTUAL_DISPATCH_OK') { throw 'AST optional virtual method dispatch failed.' }
Write-Host 'AST_OPTIONAL_METHOD_VIRTUAL_DISPATCH_OK'
$arrayBoundsOutput = Join-Path $output 'array-storage-bounds'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'array-storage-bounds.xps') -o $arrayBoundsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST physical array bounds compilation failed.' }
$arrayBoundsResult = dotnet (Join-Path $arrayBoundsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayBoundsResult -join "`n") -ne 'ARRAY_STORAGE_BOUNDS_OK') { throw 'AST shared array storage/bounds regression failed.' }
Write-Host 'AST_ARRAY_STORAGE_BOUNDS_OK'
$arrayParityOutput = Join-Path $output 'array-storage-parity'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'array-storage-parity.xps') -o $arrayParityOutput
if ($LASTEXITCODE -ne 0) { throw 'AST array parity compilation failed.' }
$arrayParityResult = dotnet (Join-Path $arrayParityOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayParityResult -join "`n") -ne "-3`n-1`n7`nARRAY_STORAGE_PARITY_OK") { throw 'AST array parity execution failed.' }
$normalArray = Join-Path $output $(if ($IsWindows) { 'normal-array.exe' } else { 'normal-array' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'array-storage-parity.xps') -o $normalArray --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal array parity compilation failed.' }
$normalStdout = [System.IO.Path]::GetTempFileName()
try {
    $normalProcess = Start-Process -FilePath $normalArray -RedirectStandardOutput $normalStdout -NoNewWindow -Wait -PassThru
    $normalArrayResult = if (Test-Path $normalStdout) { Get-Content -LiteralPath $normalStdout } else { @() }
    if ($normalProcess.ExitCode -ne 0 -or ($normalArrayResult -join "`n") -ne ($arrayParityResult -join "`n")) { throw 'Normal/AST array storage parity failed.' }
}
finally {
    Remove-Item -LiteralPath $normalStdout -Force -ErrorAction SilentlyContinue
}
$applicationProbeProject = Join-Path $root 'tests/AstApplicationRuntimeProbe/AstApplicationRuntimeProbe.csproj'
dotnet build $applicationProbeProject -c Release --no-restore -p:UseAppHost=false -p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) { throw 'AST native Application runtime probe build failed.' }
$applicationProbeDll = Join-Path $root 'tests/AstApplicationRuntimeProbe/bin/Release/net10.0/AstApplicationRuntimeProbe.dll'
dotnet $applicationProbeDll
if ($LASTEXITCODE -ne 0) { throw 'AST native Application runtime regression failed.' }
& (Join-Path $PSScriptRoot 'run-ast-constructor.ps1')
& (Join-Path $PSScriptRoot 'run-ast-destructor.ps1')
& (Join-Path $PSScriptRoot 'run-ast-image-get.ps1')
dotnet run --project (Join-Path $root 'tests/AstHttpRuntimeProbe') -c Release -- $root --accessors-only
if ($LASTEXITCODE -ne 0) { throw 'AST HTTP accessor reference/security regression failed.' }
foreach ($fixture in @('http-property-signature-only', 'tabular-reference-properties', 'tabular-reference-signatures', 'native-reference-properties', 'native-reference-signatures')) {
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture)
    if ($LASTEXITCODE -ne 0) { throw "AST native reference accessor compilation failed: $fixture" }
    $result = dotnet (Join-Path $output "$fixture/Generated.dll")
    $expected = switch ($fixture) {
        'http-property-signature-only' { 'HTTP_PROPERTY_SIGNATURE_ONLY_OK' }
        'tabular-reference-properties' { 'TABULAR_REFERENCE_PROPERTIES_OK' }
        'tabular-reference-signatures' { 'TABULAR_REFERENCE_SIGNATURES_OK' }
        'native-reference-properties' { 'NATIVE_REFERENCE_PROPERTIES_OK' }
        'native-reference-signatures' { 'NATIVE_REFERENCE_SIGNATURES_OK' }
    }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST native reference accessor execution failed: $fixture" }
}
$httpIndexedOutput = Join-Path $output 'http-property-indexed'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'http-property-index-deferred.xps') -o $httpIndexedOutput
if ($LASTEXITCODE -ne 0) { throw 'AST indexed HTTP accessor compilation failed.' }
$httpIndexedResult = dotnet (Join-Path $httpIndexedOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($httpIndexedResult -join "`n") -ne 'HTTP_INDEXED_PROPERTY_OK') { throw 'AST indexed HTTP accessor regression failed.' }
Write-Host 'AST_HTTP_INDEXED_PROPERTY_OK'
foreach ($case in @(
    @('http-property-type-error', 'XPS2001', 8),
    @('http-property-mode-error', 'XPS2001', 8),
    @('http-property-readonly-error', 'XPS2001', 7),
    @('http-property-file-type-error', 'XPS2001', 8),
    @('tabular-property-type-error', 'XPS2001', 8),
    @('tabular-property-mode-error', 'XPS2001', 8),
    @('tabular-property-frombytes-error', 'XPS1012', 7),
    @('tabular-property-readonly-error', 'XPS2001', 7),
    @('native-property-type-error', 'XPS2001', 8),
    @('native-property-mode-error', 'XPS2001', 8),
    @('image-property-set-mode-error', 'XPS2001', 8)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Native accessor diagnostic lost: $fixture" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'inherited-reference-runtime.xps') -o (Join-Path $output 'inherited-reference-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST inherited reference compilation failed.' }
$inheritedReferenceResult = dotnet (Join-Path $output 'inherited-reference-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inheritedReferenceResult -join "`n") -ne 'INHERITED_REFERENCE_RUNTIME_OK') { throw 'AST inherited reference execution failed.' }
foreach ($case in @(
    @('inherited-reference-downcast-error', 'XPS2001', 11),
    @('inherited-reference-sibling-error', 'XPS2001', 13),
    @('inherited-reference-isolation-error', 'XPS2001', 11),
    @('inheritance-cycle-error', 'XPS1012', 1),
    @('inheritance-self-cycle-error', 'XPS1012', 1),
    @('inheritance-unknown-base-error', 'XPS2008', 1)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Inherited reference diagnostic lost: $fixture" }
}
foreach ($fixture in @('indexed-object-runtime', 'indexed-object-properties')) {
    $fixtureSource = if ($fixture -eq 'indexed-object-properties') { Join-Path $root 'samples/indexed-object-properties.xps' } else { Join-Path $PSScriptRoot "$fixture.xps" }
    dotnet $compiler ast-compile $fixtureSource -o (Join-Path $output $fixture)
    if ($LASTEXITCODE -ne 0) { throw "AST typed/indexed object compilation failed: $fixture" }
    $result = dotnet (Join-Path $output "$fixture/Generated.dll")
    $expected = if ($fixture -eq 'indexed-object-runtime') { 'INDEXED_OBJECT_RUNTIME_OK' } else { "OBJECT1=first-object`nOBJECT2=second-object" }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST typed/indexed object execution failed: $fixture" }
}
foreach ($fixture in @('object-property-member-mode-error', 'object-property-declaration-error', 'object-property-mode-error', 'object-property-scalar-error', 'object-property-type-error', 'object-property-let-error', 'object-property-call-target-error')) {
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:(2|4|7|11):\d+: XPS2001:") { throw "Object accessor diagnostic lost: $fixture" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-property-set.xps') -o (Join-Path $output 'object-property-set')
if ($LASTEXITCODE -ne 0) { throw 'AST object Property Set compilation failed.' }
$objectPropertyResult = dotnet (Join-Path $output 'object-property-set/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectPropertyResult -join "`n") -ne 'OBJECT_PROPERTY_SET_OK') { throw 'AST object Property Set execution failed.' }
foreach ($fixture in @('indexed-optional-simple', 'indexed-optional-runtime')) {
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture)
    if ($LASTEXITCODE -ne 0) { throw "AST Optional index compilation failed: $fixture" }
    $result = dotnet (Join-Path $output "$fixture/Generated.dll")
    $expected = if ($fixture -eq 'indexed-optional-simple') { 'INDEXED_OPTIONAL_SIMPLE_OK' } else { 'INDEXED_OPTIONAL_RUNTIME_OK' }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST Optional index execution failed: $fixture" }
}
foreach ($fixture in @('indexed-optional-order-error', 'indexed-optional-default-error', 'indexed-optional-name-error', 'indexed-optional-later-error', 'indexed-optional-signature-error', 'indexed-optional-value-error', 'indexed-optional-required-error', 'indexed-optional-marker-error')) {
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:(2|5|8):") { throw "Optional index diagnostic lost: $fixture" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-array.xps') -o (Join-Path $output 'static-array-early')
if ($LASTEXITCODE -ne 0) { throw 'AST Static array storage compilation failed.' }
$staticEarlyResult = dotnet (Join-Path $output 'static-array-early/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticEarlyResult -join "`n") -ne "1`n2") { throw 'AST Static array storage lifecycle failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-elements-runtime.xps') -o (Join-Path $output 'indexed-byref-elements-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST element ByRef index compilation failed.' }
$elementIndexResult = dotnet (Join-Path $output 'indexed-byref-elements-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($elementIndexResult -join "`n") -ne 'INDEXED_BYREF_ELEMENTS_OK') { throw 'AST element ByRef index alias/order/bounds/exception execution failed.' }
$listElementCallOutput = Join-Path $output 'byref-list-element-call-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'byref-list-element-call-next.xps') -o $listElementCallOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ByRef List-element call compilation failed.' }
$listElementCallResult = dotnet (Join-Path $listElementCallOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listElementCallResult -join "`n") -ne "7`n7") { throw 'AST ByRef List-element call execution failed.' }
Write-Host 'AST_BYREF_LIST_ELEMENT_CALL_OK'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-elements-simple.xps') -o (Join-Path $output 'indexed-byref-elements-simple')
if ($LASTEXITCODE -ne 0) { throw 'AST ordinary/flat element ByRef index compilation failed.' }
$elementSimpleResult = dotnet (Join-Path $output 'indexed-byref-elements-simple/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($elementSimpleResult -join "`n") -ne 'INDEXED_BYREF_ELEMENTS_SIMPLE_OK') { throw 'AST ordinary/flat element alias execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-stable-storage.xps') -o (Join-Path $output 'list-stable-storage')
if ($LASTEXITCODE -ne 0) { throw 'AST shared List storage compilation failed.' }
$listStorageResult = dotnet (Join-Path $output 'list-stable-storage/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listStorageResult -join "`n") -ne 'LIST_STABLE_STORAGE_OK') { throw 'AST shared List storage failed.' }
$listStorageNormal = Join-Path $output $(if ($IsWindows) { 'list-stable-normal.exe' } else { 'list-stable-normal' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'list-stable-storage.xps') -o $listStorageNormal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal shared List storage compilation failed.' }
$listStorageNormalResult = & $listStorageNormal
if ($LASTEXITCODE -ne 0 -or ($listStorageNormalResult -join "`n") -ne 'LIST_STABLE_STORAGE_OK') { throw 'Normal/AST shared List storage parity failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-list-lifetime.xps') -o (Join-Path $output 'indexed-byref-list-lifetime')
if ($LASTEXITCODE -ne 0) { throw 'AST captured List reference compilation failed.' }
$listLifetimeResult = dotnet (Join-Path $output 'indexed-byref-list-lifetime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listLifetimeResult -join "`n") -ne 'INDEXED_BYREF_LIST_LIFETIME_OK') { throw 'AST captured List reference lifetime failed.' }
$moduleStorageOutput = Join-Path $output 'indexed-byref-module-storage-error'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-module-storage-error.xps') -o $moduleStorageOutput
if ($LASTEXITCODE -ne 0) { throw 'AST module ByRef storage compilation failed.' }
$moduleStorageResult = dotnet (Join-Path $moduleStorageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleStorageResult -join "`n") -ne '1') { throw 'AST module ByRef storage execution failed.' }
Write-Host 'AST_INDEXED_BYREF_MODULE_STORAGE_OK'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-module-runtime.xps') -o (Join-Path $output 'indexed-byref-module-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST module ByRef index compilation failed.' }
$moduleIndexResult = dotnet (Join-Path $output 'indexed-byref-module-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleIndexResult -join "`n") -ne 'INDEXED_BYREF_MODULE_OK') { throw 'AST module ByRef index storage/alias/exception execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-runtime.xps') -o (Join-Path $output 'indexed-byref-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST ByRef index compilation failed.' }
$byRefIndexResult = dotnet (Join-Path $output 'indexed-byref-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($byRefIndexResult -join "`n") -ne 'INDEXED_BYREF_RUNTIME_OK') { throw 'AST ByRef index alias/exception execution failed.' }
foreach ($byRefInvalid in @('indexed-byref-module-type-error', 'indexed-byref-property-error', 'indexed-byref-literal-error', 'indexed-byref-type-error', 'indexed-byref-mode-error', 'indexed-byref-complex-error')) {
    $byRefFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$byRefInvalid.xps") -o (Join-Path $output $byRefInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($byRefFailure -join "`n") -notmatch "$byRefInvalid.xps:(5|8|9|16):") { throw "ByRef index diagnostic source mapping lost: $byRefInvalid" }
}
foreach ($indexedFixture in @('indexed-byref-module-simple', 'indexed-byref-field-capability', 'indexed-properties-runtime', 'indexed-get-parity')) {
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$indexedFixture.xps") -o (Join-Path $output $indexedFixture)
    if ($LASTEXITCODE -ne 0) { throw "AST indexed property compilation failed: $indexedFixture" }
    $indexedResult = dotnet (Join-Path $output "$indexedFixture/Generated.dll")
    $indexedExpected = if ($indexedFixture -eq 'indexed-get-parity') { '6' } elseif ($indexedFixture -eq 'indexed-byref-module-simple') { 'INDEXED_BYREF_MODULE_SIMPLE_OK' } elseif ($indexedFixture -eq 'indexed-byref-field-capability') { 'INDEXED_BYREF_FIELD_OK' } else { 'INDEXED_PROPERTIES_RUNTIME_OK' }
    if ($LASTEXITCODE -ne 0 -or ($indexedResult -join "`n") -ne $indexedExpected) { throw "AST indexed property execution failed: $indexedFixture" }
}
$indexedNormal = Join-Path $output $(if ($IsWindows) { 'indexed-get-normal.exe' } else { 'indexed-get-normal' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'indexed-get-parity.xps') -o $indexedNormal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal indexed getter compilation failed.' }
$indexedNormalResult = & $indexedNormal
if ($LASTEXITCODE -ne 0 -or ($indexedNormalResult -join "`n") -ne '6') { throw 'Normal/AST indexed getter parity failed.' }
dotnet $compiler ast-compile (Join-Path $root 'samples/indexed-properties.xps') -o (Join-Path $output 'indexed-existing-sample')
if ($LASTEXITCODE -ne 0) { throw 'Existing indexed scalar sample compilation failed.' }
$indexedSampleResult = dotnet (Join-Path $output 'indexed-existing-sample/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($indexedSampleResult -join "`n") -ne "INDEX1=first`nINDEX2=second") { throw 'Existing indexed scalar sample execution failed.' }
foreach ($indexedInvalid in @('indexed-signature-error', 'indexed-readonly-error', 'indexed-writeonly-error', 'indexed-arity-error', 'indexed-type-error', 'indexed-private-error')) {
    $indexedFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$indexedInvalid.xps") -o (Join-Path $output $indexedInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($indexedFailure -join "`n") -notmatch "$indexedInvalid.xps:(5|7|8):") { throw "Indexed diagnostic source mapping lost: $indexedInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-properties-capability.xps') -o (Join-Path $output 'class-properties-capability')
if ($LASTEXITCODE -ne 0) { throw 'AST explicit property setter/With compilation failed.' }
$propertyCapability = dotnet (Join-Path $output 'class-properties-capability/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($propertyCapability -join "`n") -ne 'CLASS_PROPERTIES_CAPABILITY_OK') { throw 'AST explicit property setter/With execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-properties-native.xps') -o (Join-Path $output 'class-properties-native')
if ($LASTEXITCODE -ne 0) { throw 'AST property accessor compilation failed.' }
$propertyResult = dotnet (Join-Path $output 'class-properties-native/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($propertyResult -join "`n") -ne 'CLASS_PROPERTIES_NATIVE_OK') { throw 'AST property accessor execution failed.' }
# This fixture intentionally covers AST-only native property signatures. Legacy
# parity is retained by the scalar/object property fixtures below; requiring
# legacy support here would reject an explicitly supported AST extension.
foreach ($propertyInvalid in @('class-property-readonly-error', 'class-property-writeonly-error', 'class-property-private-error', 'class-property-body-error', 'class-property-type-error')) {
    $propertyFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$propertyInvalid.xps") -o (Join-Path $output $propertyInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($propertyFailure -join "`n") -notmatch "$propertyInvalid.xps:(2|3|5|8|9):") { throw "Property diagnostic source mapping lost: $propertyInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-methods-native.xps') -o (Join-Path $output 'class-methods-native')
if ($LASTEXITCODE -ne 0) { throw 'AST instance class method compilation failed.' }
$classMethodsResult = dotnet (Join-Path $output 'class-methods-native/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodsResult -join "`n") -ne 'CLASS_METHODS_NATIVE_OK') { throw 'AST instance class method execution failed.' }
$classNormalPath = Join-Path $output $(if ($IsWindows) { 'class-methods-normal.exe' } else { 'class-methods-normal' })
dotnet $compiler legacy-compile (Join-Path $PSScriptRoot 'class-methods-native.xps') -o $classNormalPath --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal instance class method compilation failed.' }
$classNormalResult = & $classNormalPath
if ($LASTEXITCODE -ne 0 -or ($classNormalResult -join "`n") -ne ($classMethodsResult -join "`n")) { throw 'Normal/AST class method parity failed.' }
foreach ($classInvalid in @('class-method-scope-error', 'class-method-private-error', 'class-field-private-error', 'class-method-body-error')) {
    $classFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$classInvalid.xps") -o (Join-Path $output $classInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($classFailure -join "`n") -notmatch "$classInvalid.xps:(3|6|7):") { throw "Class diagnostic source span lost: $classInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-case.xps') -o (Join-Path $output 'class-method-case')
if ($LASTEXITCODE -ne 0) { throw 'AST case-insensitive class method compilation failed.' }
$classCaseResult = dotnet (Join-Path $output 'class-method-case/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classCaseResult -join "`n") -ne '3') { throw 'AST case-insensitive class member execution failed.' }
dotnet build (Join-Path $root 'tests/AstSpreadsheetRuntimeProbe/AstSpreadsheetRuntimeProbe.csproj') -c Release --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1
if ($LASTEXITCODE -ne 0) { throw 'AST native spreadsheet runtime probe build failed.' }
dotnet (Join-Path $root 'tests/AstSpreadsheetRuntimeProbe/bin/Release/net10.0/AstSpreadsheetRuntimeProbe.dll') $root
if ($LASTEXITCODE -ne 0) { throw 'AST native spreadsheet runtime regression failed.' }
foreach ($sheetInvalid in @('spreadsheet-member-error', 'spreadsheet-constructor-error', 'spreadsheet-readonly-error', 'spreadsheet-private-setter-error', 'spreadsheet-arity-error')) {
    $sheetFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$sheetInvalid.xps") -o (Join-Path $output $sheetInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($sheetFailure -join "`n") -notmatch "$sheetInvalid.xps:(2|3):") { throw "Spreadsheet diagnostic or source span lost: $sheetInvalid" }
}
dotnet run --project (Join-Path $root 'tests/AstHttpRuntimeProbe') -c Release
if ($LASTEXITCODE -ne 0) { throw 'AST native HTTP/TLS runtime regression failed.' }
foreach ($httpInvalid in @('http-member-error', 'http-constructor-error', 'http-readonly-error', 'http-arity-error')) {
    $httpFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$httpInvalid.xps") -o (Join-Path $output $httpInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($httpFailure -join "`n") -notmatch "$httpInvalid.xps:(2|3):") { throw "HTTP diagnostic or source span lost: $httpInvalid" }
}
& (Join-Path $PSScriptRoot 'run-ast-xml.ps1')
$csvRoot = Join-Path $output ('csv-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $csvRoot | Out-Null
foreach ($csvFixture in @('csv-runtime', 'csv-reference', 'csv-parity', 'csv-unused', 'csv-escape-only')) {
    $csvSource = Join-Path $csvRoot "$csvFixture.xps"
    Copy-Item (Join-Path $PSScriptRoot "$csvFixture.xps") $csvSource
    $csvOutput = Join-Path $csvRoot $csvFixture
    dotnet $compiler ast-compile $csvSource -o $csvOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $csvFixture compilation failed." }
    Push-Location $csvRoot
    try { $csvResult = dotnet (Join-Path $csvOutput 'Generated.dll'); $csvExit = $LASTEXITCODE } finally { Pop-Location }
    $csvExpected = switch ($csvFixture) {
        'csv-runtime' { "2`n3`nname`nÅsa`nFredrik`nquoted `"value`"`nid`nname`nnote`n002`n001`n4`n001`nFredrik`nwindows-1252`n002`nFredrik`nutf-8-bom`nFredrik`nutf-16be`nFalse`nwindows-1252`nutf-16be`n3`n2`n5`nCSV column name 'missing' not found.`n9`nCSV row index out of range.`n5`nCSV header name 'ID' already exists.`n`"a;b`"`nCSV_AST_OK" }
        'csv-reference' { "shared`nreplacement`n2`n2`n1`n5`nordinary name`n7`nordinary row" }
        'csv-parity' { "utf-8-bom`nÅsa`nFalse`nwindows-1252`nutf-8-bom`n001`nCSV_PARITY_OK" }
        'csv-unused' { "XPCsvDocument CsvParse CsvEscape`nordinary row`n7" }
        'csv-escape-only' { '"a;b"' }
    }
    if ($csvExit -ne 0 -or ($csvResult -join "`n") -ne $csvExpected) { throw "AST $csvFixture output mismatch." }
    $csvAssembly = [System.Reflection.Assembly]::LoadFile((Join-Path $csvOutput 'Generated.dll'))
    if ($csvFixture -in @('csv-unused', 'csv-escape-only')) {
        if ($null -ne $csvAssembly.GetType('XPScriptCsvDocument', $false) -or $null -ne $csvAssembly.GetType('XPScriptCsvFileRuntime', $false)) { throw 'Unused CSV document/file runtime leaked into output.' }
        if ($csvFixture -eq 'csv-unused' -and $null -ne $csvAssembly.GetType('XPScriptNativeCsv', $false)) { throw 'Unused CSV helper runtime leaked into output.' }
    } elseif ($null -eq $csvAssembly.GetType('XPScriptCsvDocument', $false)) { throw 'CSV document unexpectedly used a fallback/placeholder.' }
    if ($csvAssembly.GetReferencedAssemblies().Name -match 'ClosedXML|Magick.NET|Microsoft.Data.Sqlite') { throw 'Pure CSV output acquired unrelated addon dependencies.' }
}
foreach ($csvSample in @('native-csv-regression', 'csv-json-roundtrip')) {
    $csvSampleOutput = Join-Path $csvRoot $csvSample
    dotnet $compiler ast-compile (Join-Path $root "samples/$csvSample.xps") -o $csvSampleOutput
    if ($LASTEXITCODE -ne 0) { throw "AST existing $csvSample compilation failed." }
    Push-Location $csvRoot
    try { $csvSampleResult = dotnet (Join-Path $csvSampleOutput 'Generated.dll'); $csvSampleExit = $LASTEXITCODE } finally { Pop-Location }
    if ($csvSampleExit -ne 0) { throw "AST existing $csvSample execution failed." }
    if ($csvSample -eq 'native-csv-regression' -and (($csvSampleResult -join "`n") -notmatch "MISSING_TEXT=CSV column name 'missing' not found\." -or ($csvSampleResult -join "`n") -notmatch 'FILE_OVERRIDE=Åsa:Malmö; Sweden')) { throw 'CSV runtime error text or file charset parity drifted.' }
    if ($csvSample -eq 'csv-json-roundtrip' -and ($csvSampleResult -join "`n") -ne 'CSV-JSON-ROUNDTRIP=OK') { throw 'CSV/JSON round trip drifted.' }
}
foreach ($csvInvalid in @('csv-member-error', 'csv-constructor-error', 'csv-readonly-error', 'csv-writefile-error', 'csv-load-arity-error')) {
    $csvFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$csvInvalid.xps") -o (Join-Path $csvRoot $csvInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($csvFailure -join "`n") -notmatch "$csvInvalid.xps:(2|3):") { throw "AST $csvInvalid lost its diagnostic or source position." }
}
$imageFixtureRoot = Join-Path $output ('xpimage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $imageFixtureRoot | Out-Null
$imageSource = Join-Path $imageFixtureRoot 'xpimage-runtime.xps'
Copy-Item (Join-Path $PSScriptRoot 'xpimage-runtime.xps') $imageSource
$imageOutput = Join-Path $imageFixtureRoot 'ast'
dotnet $compiler ast-compile $imageSource -o $imageOutput
if ($LASTEXITCODE -ne 0) { throw 'Native AST XPImage compilation failed.' }
$imageResult = dotnet (Join-Path $imageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageResult -join "`n") -ne "4`n3`n12`n0`n4`n2`n4`n3`n4`n0`n20`n2`n0`nTrue`n5`nTrue`n5`n4`nTrue`nTrue`nTrue`n2`n3`nXPIMAGE_AST_OK") { throw 'Native AST image operations, loading, ByVal/ByRef or cleanup regression failed.' }
$imageNotice = Get-Content (Join-Path $imageOutput 'Third-party-license.txt') -Raw
if ($imageNotice -notmatch 'Magick.NET-Q16-AnyCPU' -or $imageNotice -notmatch 'Magick.NET.Core' -or $imageNotice -match 'License text could not be resolved') { throw 'AST image addon notices are incomplete.' }
$imageVariantOutput = Join-Path $imageFixtureRoot 'variant-return'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-variant-return.xps') -o $imageVariantOutput
if ($LASTEXITCODE -ne 0) { throw 'AST image Variant return compilation failed.' }
$imageVariantResult = dotnet (Join-Path $imageVariantOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageVariantResult -join "`n") -ne "3`n4`nTrue") { throw 'AST image Variant return or replaced ByVal ownership failed.' }
$imageParitySource = Join-Path $imageFixtureRoot 'xpimage-parity.xps'
Copy-Item (Join-Path $PSScriptRoot 'xpimage-parity.xps') $imageParitySource
$imageParityOutput = Join-Path $imageFixtureRoot 'parity'
dotnet $compiler ast-compile $imageParitySource -o $imageParityOutput
if ($LASTEXITCODE -ne 0) { throw 'AST image parity compilation failed.' }
$imageParityResult = dotnet (Join-Path $imageParityOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageParityResult -join "`n") -ne "4`n6`n0`n0`n3`n2`nXPIMAGE_PARITY_OK") { throw 'AST image output differs from the shared normal compiler contract.' }
foreach ($imageInvalid in @('xpimage-constructor-error', 'xpimage-member-error', 'xpimage-readonly-error')) {
    $invalidResult = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$imageInvalid.xps") -o (Join-Path $imageFixtureRoot $imageInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($invalidResult -join "`n") -notmatch "$imageInvalid.xps:3:") { throw "AST $imageInvalid lost its source-mapped diagnostic." }
}
$runtimeOutput = Join-Path $output 'shared-runtime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'shared-runtime.xps') -o $runtimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST shared scalar runtime compilation failed.' }
$runtimeResult = dotnet (Join-Path $runtimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($runtimeResult -join "`n") -ne "INTEGER`nLONG`n17`nTrue`n2`nEMPTY=[]`nVariable is null`nNULL`n1`nFalse`nTrue`nbcd`nTrue`n3`n2`n1`nOVERFLOW=6`nCONTINUED") { throw 'AST scalar runtime conversion, Null/Empty, environment or overflow behavior drifted.' }
foreach ($jsonFixture in @('json-runtime', 'json-reference')) {
    $jsonOutput = Join-Path $output $jsonFixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$jsonFixture.xps") -o $jsonOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $jsonFixture compilation failed." }
    $jsonResult = dotnet (Join-Path $jsonOutput 'Generated.dll')
    $jsonExpected = if ($jsonFixture -eq 'json-runtime') {
        '{"name":"\u00C5sa","values":[7],"active":true}' + "`nÅsa`nObject`nTrue`n2`n7`n1`n7`n[null,false,3]`nInvalid JSON input.`nJSON array index out of range."
    } else { "4`n8`n2`n1`n2`ntrue`nnull`nnull`n19`nnames are ordinary variables" }
    if ($LASTEXITCODE -ne 0 -or ($jsonResult -join "`n") -ne $jsonExpected) { throw "AST $jsonFixture shared runtime output mismatch." }
}
$notesHttpOutput = Join-Path $output 'notes-http-native'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'notes-http-native-next.xps') -o $notesHttpOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Notes HTTP native compilation failed.' }
$notesHttpResult = dotnet (Join-Path $notesHttpOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($notesHttpResult -join [Environment]::NewLine) -ne 'NOTES_HTTP_NATIVE_OK') { throw 'AST Notes HTTP native runtime failed.' }

$xpNotesHttpAliasOutput = Join-Path $output 'xpnotes-http-request-alias-next'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpnotes-http-request-alias-next.xps') -o $xpNotesHttpAliasOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPNotesHTTPRequest alias compilation failed.' }
$xpNotesHttpAliasResult = dotnet (Join-Path $xpNotesHttpAliasOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($xpNotesHttpAliasResult -join [Environment]::NewLine) -ne 'XP_NOTES_HTTP_REQUEST_ALIAS_OK') { throw 'AST XPNotesHTTPRequest alias regression failed.' }
$jsonInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'json-member-error.xps') -o (Join-Path $output 'json-member-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($jsonInvalid -join "`n") -notmatch "json-member-error.xps:3:16: XPS2009: Undefined member 'UnknownMember'") { throw 'AST JSON unknown-member diagnostic lost its code or source position.' }
$textObjectOutput = Join-Path $output 'object-name-text'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-name-text.xps') -o $textObjectOutput
if ($LASTEXITCODE -ne 0) { throw 'Object names inside strings/comments must not trigger legacy fallback.' }
$textObjectResult = dotnet (Join-Path $textObjectOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($textObjectResult -join "`n") -ne 'XPImage') { throw 'AST object-name text regression failed.' }
$continuationOutput = Join-Path $output 'line-continuation'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'line-continuation.xps') -o $continuationOutput
if ($LASTEXITCODE -ne 0) { throw 'AST line continuation compilation failed.' }
$continuationResult = dotnet (Join-Path $continuationOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($continuationResult -join "`n") -ne '3') { throw 'AST line continuation execution failed.' }
$aliasByRefOutput = Join-Path $output 'forall-list-byref'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-byref.xps') -o $aliasByRefOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ForAll List ByRef compilation failed.' }
$aliasByRefResult = dotnet (Join-Path $aliasByRefOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aliasByRefResult -join "`n") -ne "a:11`nb:12`n21`n22`n23`nhello!") { throw 'AST ForAll List ByRef copy-back or nested alias restoration failed.' }
$aliasMultipleOutput = Join-Path $output 'forall-list-byref-multiple'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-byref-multiple-error.xps') -o $aliasMultipleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multiple ForAll List ByRef compilation failed.' }
$aliasMultipleResult = dotnet (Join-Path $aliasMultipleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aliasMultipleResult -join "`n") -ne '21') { throw 'AST multiple ForAll List ByRef copy-back failed.' }
$listCopybackOutput = Join-Path $output 'list-byref-copyback'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-byref-copyback.xps') -o $listCopybackOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List ByRef copy-back compilation failed.' }
$listCopybackResult = dotnet (Join-Path $listCopybackOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listCopybackResult -join "`n") -ne "TAG_EVALUATED_ONCE`n11`nhello!") { throw 'AST List ByRef must copy back typed values and evaluate the tag once.' }
$staticScopeOutput = Join-Path $output 'static-initializer-scope'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-initializer-scope.xps') -o $staticScopeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static initializer parameter/local binding failed.' }
$staticScopeResult = dotnet (Join-Path $staticScopeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticScopeResult -join "`n") -ne "BEFORE_CALLS`nAFTER_SKIPPED_DECLARATION`nSTATIC_INITIALIZED`n7`n3`n7`n3") { throw 'AST Static initializer must execute once at the first reached declaration with current parameter/local values.' }
$objectInitializerOutput = Join-Path $output 'object-initializer-lifetime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-initializer-lifetime.xps') -o $objectInitializerOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Object initializer compilation failed.' }
$objectInitializerResult = dotnet (Join-Path $objectInitializerOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectInitializerResult -join "`n") -ne "False`nFalse`nFalse`nTrue") { throw 'AST Dim Object initializer must rerun while a cleared Static Object initializer must not rerun.' }
$objectLifetimeOutput = Join-Path $output 'object-local-lifetime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-local-lifetime.xps') -o $objectLifetimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST local Object lifetime compilation failed.' }
$objectLifetimeResult = dotnet (Join-Path $objectLifetimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectLifetimeResult -join "`n") -ne "True`nTrue`nFalse`nTrue`nFalse`nFalse") { throw 'AST Dim Object must start as Nothing on each call while Static Object retains its reference.' }
$staticObjectOutput = Join-Path $output 'static-object'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-object.xps') -o $staticObjectOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static Object compilation failed.' }
$staticObjectResult = dotnet (Join-Path $staticObjectOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticObjectResult -join "`n") -ne "OBJECT_CREATED`nFalse`nFalse") { throw 'AST Static Object did not start as Nothing and retain the created instance.' }
$eraseOutput = Join-Path $output 'list-erase'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-erase.xps') -o $eraseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Erase compilation failed.' }
$eraseResult = dotnet (Join-Path $eraseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($eraseResult -join "`n") -ne "False`nTrue`nFalse`nfirst`n1`nFalse`nFalse`nLIST_ERASE_OK") { throw 'AST List tag removal, clearing or removal during alias iteration failed.' }
$arrayOutput = Join-Path $output 'array-storage'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'array-storage.xps') -o $arrayOutput
if ($LASTEXITCODE -ne 0) { throw 'AST array declaration compilation failed.' }
$arrayResult = dotnet (Join-Path $arrayOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayResult -join "`n") -ne "1`n0`n1`nTrue`nTrue") { throw 'AST array bounds, string defaults or Erase semantics differ from published main.' }
$redimOutput = Join-Path $output 'redim-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'redim-array.xps') -o $redimOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic array ReDim compilation failed.' }
$redimResult = dotnet (Join-Path $redimOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($redimResult -join "`n") -ne "2`n9`n9") { throw 'AST ReDim type preservation or Preserve semantics differ from published main.' }
$redimError = Join-Path $output 'redim-array-error'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'redim-array-error.xps') -o $redimError
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic Erase regression did not compile.' }
$multiDimensionalRedimOutput = Join-Path $output 'multidimensional-redim'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-redim.xps') -o $multiDimensionalRedimOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic multidimensional ReDim compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalRedimOutput 'Generated.dll')) -join "`n") -ne "1`n4`n11`n22`n1`n5") { throw 'AST dynamic multidimensional ReDim/Preserve bounds or values mismatch.' }
$multiDimensionalRedimOptionBaseOutput = Join-Path $output 'multidimensional-redim-option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-redim-option-base.xps') -o $multiDimensionalRedimOptionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic multidimensional Option Base compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalRedimOptionBaseOutput 'Generated.dll')) -join "`n") -ne "7`n1`n1`n2`n3") { throw 'AST dynamic multidimensional Option Base bounds mismatch.' }
$multiDimensionalOutput = Join-Path $output 'multidimensional-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-array.xps') -o $multiDimensionalOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multidimensional array compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalOutput 'Generated.dll')) -join "`n") -ne "12`n0`n1`n0`n9`n1`n2`n3`n4`n1`n2`n0`n1`n3`n11`nTEXT_OK`n2.5`nTrue`n255`n`n0`nFalse`n13`n3`n4") { throw 'AST multidimensional array output mismatch.' }
$multiDimensionalOptionBaseOutput = Join-Path $output 'multidimensional-option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-option-base.xps') -o $multiDimensionalOptionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multidimensional Option Base compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalOptionBaseOutput 'Generated.dll')) -join "`n") -ne "6`n1`n1") { throw 'AST multidimensional Option Base output mismatch.' }
$nonzeroBoundOutput = Join-Path $output 'nonzero-bound-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nonzero-array.xps') -o $nonzeroBoundOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nonzero array lower-bound compilation failed.' }
$nonzeroBoundResult = dotnet (Join-Path $nonzeroBoundOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nonzeroBoundResult -join "`n") -ne "10`n30") { throw 'AST nonzero array indexing returned the wrong elements.' }
$membershipOutput = Join-Path $output 'list-membership'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-membership.xps') -o $membershipOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List membership compilation failed.' }
$membershipResult = dotnet (Join-Path $membershipOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($membershipResult -join "`n") -ne "True`nFalse`nTAG_EVALUATED`nTrue`n3") { throw 'AST List membership read a missing element or evaluated its tag more than once.' }
$listInitializerOutput = Join-Path $output 'list-initializer'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-initializer-error.xps') -o $listInitializerOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List initializer compilation failed.' }
$listInitializerResult = dotnet (Join-Path $listInitializerOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listInitializerResult -join "`n") -ne '7') { throw 'AST List initializer did not preserve the source list.' }
$listByRef = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-byref-multiple-error.xps') -o (Join-Path $output 'list-byref-error') 2>&1
if ($LASTEXITCODE -ne 0) { throw 'AST multiple List ByRef copy-back compilation failed.' }
$listFunctionsOutput = Join-Path $output 'list-functions'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-functions.xps') -o $listFunctionsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Function ByRef/parameter/return compilation failed.' }
$listFunctionsResult = dotnet (Join-Path $listFunctionsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listFunctionsResult -join "`n") -ne "12`n11`n1`n23`n21`n50`nTrue`nFalse`n51") { throw 'AST List Function, alias, reference return, parameter replacement or exceptional copy-back failed.' }
$listOrderOutput = Join-Path $output 'list-function-order'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-function-order.xps') -o $listOrderOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Function evaluation-order compilation failed.' }
$listOrderResult = dotnet (Join-Path $listOrderOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listOrderResult -join "`n") -ne "TAG_ONCE`n6`n6") { throw 'AST List Function did not evaluate the tag once and capture its value before later arguments.' }
$listOverloadsOutput = Join-Path $output 'list-parameter-overloads'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-parameter-overloads.xps') -o $listOverloadsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List parameter overload compilation failed.' }
$listOverloadsResult = dotnet (Join-Path $listOverloadsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listOverloadsResult -join "`n") -ne "1`n10`n2`n20") { throw 'AST List parameter element types must preserve overload and Static storage isolation.' }
$isElementInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-iselement-invalid.xps') -o (Join-Path $output 'list-iselement-invalid') 2>&1
if ($LASTEXITCODE -ne 2 -or ($isElementInvalid -join "`n") -notmatch 'IsElement requires one keyed List access') { throw 'AST accepted an invalid IsElement argument.' }
foreach ($listInvalid in @('list-parameter-type-error', 'list-initializer-type-error')) {
    $invalidResult = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$listInvalid.xps") -o (Join-Path $output $listInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($invalidResult -join "`n") -notmatch 'No matching overload|Cannot assign') { throw "AST accepted incompatible List element types: $listInvalid" }
}
$listOutput = Join-Path $output 'forall-list'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-alias.xps') -o $listOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ForAll List alias compilation failed.' }
$listResult = dotnet (Join-Path $listOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listResult -join "`n") -ne "a`nb`n11`n12`none:BLUE`ntwo:GREEN`na`na`nb`nb`n12`n13`n5`n1`n2`n2") { throw 'AST ForAll List write-through, ListTag, nested alias scope or snapshot semantics failed.' }
$staticInitializer = Join-Path $output 'static-initializer'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-initializer-error.xps') -o $staticInitializer
if ($LASTEXITCODE -ne 0) { throw 'AST Static scalar initializer compilation failed.' }
$staticInitializerResult = dotnet (Join-Path $staticInitializer 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticInitializerResult -join "`n") -ne '3') { throw 'AST Static scalar initializer produced the wrong result.' }
$staticOutput = Join-Path $output 'static-local'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-local-lifetime.xps') -o $staticOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static local compilation failed.' }
$staticResult = dotnet (Join-Path $staticOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticResult -join "`n") -ne "1`n10`n2`n20`n1`n10`n2`n20`nSTATIC_DEFAULTS_OK`nSTATIC_RETAINED_OK`n1`n2") { throw 'AST Static lifetime, procedure/overload isolation, defaults or ByRef mutation failed.' }
$moduleStaticOutput = Join-Path $output 'static-module-function'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-module-function-next.xps') -o $moduleStaticOutput
if ($LASTEXITCODE -ne 0) { throw 'AST module-level Static Function compilation failed.' }
$moduleStaticResult = dotnet (Join-Path $moduleStaticOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleStaticResult -join "`n") -ne 'STATIC_MODULE_FUNCTION_OK') { throw 'AST module-level Static Function execution failed.' }
$xpaiOverloadOutput = Join-Path $output 'xpai-overloads'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpai-overloads-next.xps') -o $xpaiOverloadOutput
if ($LASTEXITCODE -ne 0) { throw 'AST XPAi overload compilation failed.' }
Write-Host 'AST_XPAI_OVERLOADS_OK'
$crossProcedure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-cross-procedure-error.xps') -o (Join-Path $output 'cross-procedure') 2>&1
if ($LASTEXITCODE -ne 2 -or ($crossProcedure -join "`n") -notmatch "goto-cross-procedure-error\.xps:7:5: XPS1012: Unknown label 'outside'") { throw 'AST GoTo source mapping or cross-procedure label validation failed.' }
$gotoOutput = Join-Path $output 'goto-scope'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-procedure-scope.xps') -o $gotoOutput
if ($LASTEXITCODE -ne 0) { throw 'AST procedure-scoped GoTo compilation failed.' }
$gotoResult = dotnet (Join-Path $gotoOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($gotoResult -join "`n") -ne "7`nGOTO_PROCEDURE_SCOPE_OK") { throw 'AST procedure-scoped GoTo execution failed.' }
$unsupportedGoSub = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'unsupported-gosub.xps') -o (Join-Path $output 'unsupported-gosub') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedGoSub -join "`n") -notmatch 'GoSub is not implemented in the AST compiler') { throw 'AST GoSub was not explicitly rejected.' }
$resumeOutput = Join-Path $output 'resume-runtime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'resume-runtime.xps') -o $resumeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Resume runtime fixture failed to compile.' }
$resumeResult = dotnet (Join-Path $resumeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($resumeResult -join "`n") -ne "AUTOMATIC_NEXT_OK`nRETRY_OK`n7001`ninner failure`nNEXT_OK`nLABEL_OK") { throw 'AST Resume retry, next, label or nested-call semantics failed.' }
foreach ($resumeFixture in @('resume-loops', 'resume-handler-error')) {
    $fixtureOutput = Join-Path $output $resumeFixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$resumeFixture.xps") -o $fixtureOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $resumeFixture compilation failed." }
    $fixtureResult = dotnet (Join-Path $fixtureOutput 'Generated.dll')
    $expected = if ($resumeFixture -eq 'resume-loops') { "3`n11`n12" } else { "7001`nPROPAGATION_OK" }
    if ($LASTEXITCODE -ne 0 -or ($fixtureResult -join "`n") -ne $expected) { throw "AST $resumeFixture execution failed." }
}
$resumeUnknown = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'resume-unknown-label.xps') -o (Join-Path $output 'resume-unknown-label') 2>&1
if ($LASTEXITCODE -ne 2 -or ($resumeUnknown -join "`n") -notmatch "resume-unknown-label\.xps:2:5: XPS1012: Unknown label 'missing'") { throw 'AST On Error label source mapping or validation failed.' }
foreach ($runtimeFailure in @('resume-disabled', 'resume-error')) {
    $failureOutput = Join-Path $output $runtimeFailure
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$runtimeFailure.xps") -o $failureOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $runtimeFailure compilation failed." }
    $failureResult = Invoke-ExpectedRuntimeFailure (Join-Path $failureOutput 'Generated.dll')
    if (($failureResult.ExitCode -eq 0 -and -not $failureResult.TimedOut) -or $failureResult.Output -notmatch 'DISABLED_ERROR|Resume without an active error handler') { throw "AST $runtimeFailure did not raise the expected runtime error." }
}
$withOutput = Join-Path $output 'with'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'with-error.xps') -o $withOutput
if ($LASTEXITCODE -ne 0) { throw 'AST With compilation failed.' }
$withResult = dotnet (Join-Path $withOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($withResult -join "`n") -ne "`n") { throw 'AST With receiver/member access execution failed.' }
$unsupportedImplicitMember = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'implicit-member-error.xps') -o (Join-Path $output 'implicit-member-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedImplicitMember -join "`n") -notmatch "Implicit member 'Name' requires an active With scope") { throw 'AST accepted an implicit member outside a With scope.' }
$nativeImageOutput = Join-Path $output 'xpimage-error'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-error.xps') -o $nativeImageOutput
if ($LASTEXITCODE -ne 0) { throw 'AST did not compile XPImage natively.' }
$nativeImageResult = dotnet (Join-Path $nativeImageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nativeImageResult -join "`n") -ne '2') { throw 'AST XPImage construction failed.' }
$optionBaseOutput = Join-Path $output 'option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'option-base.xps') -o $optionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Option Base compilation failed.' }
$optionBaseResult = dotnet (Join-Path $optionBaseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionBaseResult -join "`n") -ne "1`n3`n1`n3`n10`n30") { throw 'AST Option Base bounds or indexing returned the wrong elements.' }
$byteOutput = Join-Path $output 'variant-byte-conversion'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'variant-byte-conversion.xps') -o $byteOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Variant-to-Byte conversion compilation failed.' }
$byteResult = dotnet (Join-Path $byteOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($byteResult -join "`n") -ne '7') { throw 'AST Variant-to-Byte conversion execution failed.' }
$isNothingOutput = Join-Path $output 'is-nothing'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'is-nothing.xps') -o $isNothingOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Is Nothing compilation failed.' }
$isNothingResult = dotnet (Join-Path $isNothingOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($isNothingResult -join "`n") -ne 'IS_NOTHING_OK') { throw 'AST Is Nothing did not preserve Variant Nothing semantics.' }
$classOutput = Join-Path $output 'class-field'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-field.xps') -o $classOutput
if ($LASTEXITCODE -ne 0) { throw 'AST simple class field compilation failed.' }
$classResult = dotnet (Join-Path $classOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classResult -join "`n") -ne '7') { throw 'AST simple class field access produced the wrong result.' }
$inheritanceOutput = Join-Path $output 'class-inheritance-fields'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-inheritance-fields.xps') -o $inheritanceOutput
if ($LASTEXITCODE -ne 0) { throw 'AST class inheritance compilation failed.' }
$inheritanceResult = dotnet (Join-Path $inheritanceOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inheritanceResult -join "`n") -ne "4`n5") { throw 'AST inherited class field access produced the wrong result.' }
$staticClassOutput = Join-Path $output 'static-class-instance'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-class-instance.xps') -o $staticClassOutput
if ($LASTEXITCODE -ne 0) { throw 'AST static class instance compilation failed.' }
$staticClassResult = dotnet (Join-Path $staticClassOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticClassResult -join "`n") -ne "1`n2") { throw 'AST static class instance lifetime produced the wrong result.' }
$classLifecycleOutput = Join-Path $output 'class-lifecycle'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-lifecycle.xps') -o $classLifecycleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST class lifecycle compilation failed.' }
$classLifecycleResult = dotnet (Join-Path $classLifecycleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classLifecycleResult -join "`n") -ne 'LIFECYCLE_OK') { throw 'AST class Delete lifecycle did not clear the reference.' }
$optionalOrder = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-order-error.xps') -o (Join-Path $output 'optional-order-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($optionalOrder -join "`n") -notmatch 'cannot follow an Optional parameter') { throw 'AST accepted a required parameter after Optional.' }
$optionalDefault = Join-Path $output 'optional-default-name'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-default-name.xps') -o $optionalDefault
if ($LASTEXITCODE -ne 0) { throw 'AST Optional default-name binding failed.' }
$optionalDefaultResult = dotnet (Join-Path $optionalDefault 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalDefaultResult -join "`n") -ne '5') { throw 'AST Optional default-name binding produced the wrong result.' }
$optionalSelection = Join-Path $output 'optional-overload-selection'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-overload-selection.xps') -o $optionalSelection
if ($LASTEXITCODE -ne 0) { throw 'AST Optional overload selection failed.' }
$optionalSelectionResult = dotnet (Join-Path $optionalSelection 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalSelectionResult -join "`n") -ne '2') { throw 'AST did not prefer the explicit overload over Optional forwarding.' }
$staticArray = Join-Path $output 'static-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-array.xps') -o $staticArray
if ($LASTEXITCODE -ne 0) { throw 'AST Static array compilation failed.' }
$staticArrayResult = dotnet (Join-Path $staticArray 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticArrayResult -join "`n") -ne "1`n2") { throw 'AST Static array lifetime did not persist across calls.' }
$staticList = Join-Path $output 'static-list'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-list.xps') -o $staticList
if ($LASTEXITCODE -ne 0) { throw 'AST Static List compilation failed.' }
$staticListResult = dotnet (Join-Path $staticList 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticListResult -join "`n") -ne "1`n2") { throw 'AST Static List lifetime did not persist across calls.' }
$staticVariant = Join-Path $output 'static-variant'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-variant.xps') -o $staticVariant
if ($LASTEXITCODE -ne 0) { throw 'AST Static Variant compilation failed.' }
$staticVariantResult = dotnet (Join-Path $staticVariant 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticVariantResult -join "`n") -ne "1`n2") { throw 'AST Static Variant lifetime did not persist across calls.' }
$parserError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'parser-error.xps') -o (Join-Path $output 'parser-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($parserError -join "`n") -notmatch 'Expected CloseParenToken') { throw 'AST parser diagnostic regression failed.' }
$preprocessorParserError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'preprocessor-parser-error.xps') -o (Join-Path $output 'preprocessor-parser-error') 2>&1
if ($LASTEXITCODE -eq 0 -or ($preprocessorParserError -join "`n") -notmatch 'preprocessor-parser-error\.xps:2:') { throw 'AST preprocessor parser source mapping regression failed.' }
Write-Host 'AST_PREPROCESSOR_PARSER_MAPPING_OK'
$matrixScaffoldingOutput = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'sample-scaffolding-matrix-error.xps') -o (Join-Path $output 'sample-scaffolding-matrix-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($matrixScaffoldingOutput -join "`n") -notmatch 'sample-scaffolding-matrix-error\.xps:2:\d+: XPS2008: Undefined variable') { throw 'AST sample-specific matrix scaffolding regression failed.' }
Write-Host 'AST_MATRIX_SCAFFOLDING_REMOVED_OK'
$noEntry = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'no-entry-error.xps') -o (Join-Path $output 'no-entry-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($noEntry -join "`n") -notmatch 'requires a Sub declaration') { throw 'AST no-entry diagnostic regression failed.' }
$nestedOutput = Join-Path $output 'nested-goto'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nested-goto.xps') -o $nestedOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested GoTo compilation failed.' }
$nestedResult = dotnet (Join-Path $nestedOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedResult -join "`n") -ne "0`nNESTED_GOTO_OK") { throw 'AST nested GoTo branch/loop semantics failed.' }
$loopBodyOutput = Join-Path $output 'goto-loop-body'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-loop-body.xps') -o $loopBodyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GoTo inside For/ForAll loop compilation failed.' }
$loopBodyResult = dotnet (Join-Path $loopBodyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($loopBodyResult -join "`n") -ne "6`n15") { throw 'AST GoTo inside For/ForAll loop semantics failed.' }
$exitForAllOutput = Join-Path $output 'exit-forall'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'exit-forall.xps') -o $exitForAllOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Exit ForAll compilation failed.' }
if ((@(& dotnet (Join-Path $exitForAllOutput 'Generated.dll')) -join "`n") -ne '1') { throw 'AST Exit ForAll did not leave only the current ForAll.' }
$nestedLoopBodyOutput = Join-Path $output 'goto-nested-loop-body'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-loop-body.xps') -o $nestedLoopBodyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested For/ForAll GoTo compilation failed.' }
$nestedLoopBodyResult = dotnet (Join-Path $nestedLoopBodyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedLoopBodyResult -join "`n") -ne '9') { throw 'AST nested For/ForAll GoTo semantics failed.' }
$gotoOutError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-out-forall.xps') -o (Join-Path $output 'goto-out-forall') 2>&1
if ($LASTEXITCODE -ne 2 -or ($gotoOutError -join "`n") -notmatch 'same loop scope') { throw 'AST accepted GoTo out of ForAll.' }
$nestedForAllError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-forall.xps') -o (Join-Path $output 'goto-nested-forall') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedForAllError -join "`n") -notmatch 'same loop scope') { throw 'AST accepted GoTo between nested ForAll scopes.' }
$gotoIntoForAllError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-into-forall-error.xps') -o (Join-Path $output 'goto-into-forall-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($gotoIntoForAllError -join "`n") -notmatch 'same loop scope') { throw 'AST did not diagnose GoTo entering ForAll clearly.' }
$nestedLoopError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-for-error.xps') -o (Join-Path $output 'nested-for-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedLoopError -join "`n") -notmatch 'same loop scope') { throw 'AST did not diagnose a GoTo entering a nested loop clearly.' }
$declarationJumpError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-error.xps') -o (Join-Path $output 'declaration-jump-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($declarationJumpError -join "`n") -notmatch 'GoTo cannot jump over local declaration') { throw 'AST did not diagnose a GoTo jumping over a local declaration clearly.' }
$declarationHoistOutput = Join-Path $output 'goto-declaration-hoist'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-hoist.xps') -o $declarationHoistOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GoTo declaration hoisting compilation failed.' }
$declarationHoistResult = dotnet (Join-Path $declarationHoistOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($declarationHoistResult -join "`n") -ne 'GOTO_DECLARATION_HOIST_OK') { throw 'AST GoTo declaration hoisting execution failed.' }
$applicationNewLineOutput = Join-Path $output 'application-newline'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'application-newline.xps') -o $applicationNewLineOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Application.NewLine compilation failed.' }
$applicationNewLineResult = dotnet (Join-Path $applicationNewLineOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($applicationNewLineResult -join "`n") -notmatch 'APPLICATION_NEWLINE_BEGIN' -or ($applicationNewLineResult -join "`n") -notmatch 'APPLICATION_NEWLINE_END') { throw 'AST Application.NewLine runtime regression failed.' }
$invalidDefault = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-default-error.xps') -o (Join-Path $output 'invalid-default') 2>&1
if ($LASTEXITCODE -ne 2 -or ($invalidDefault -join "`n") -notmatch "Default value for Optional parameter 'value' is incompatible with its type") { throw 'AST invalid Optional default was not rejected by binding.' }
$evaluateRemovedError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'evaluate-removed-error.xps') -o (Join-Path $output 'evaluate-removed-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($evaluateRemovedError -join "`n") -notmatch "The name 'Evaluate' does not exist in the current context") { throw 'AST removed Evaluate function was not rejected.' }
$erlOutput = Join-Path $output 'erl-physical-source-line'
dotnet $compiler ast-compile (Join-Path $root 'samples/erl-physical-source-line.xps') -o $erlOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Erl/Format/Error compatibility fixture failed to compile.' }
$erlRun = dotnet (Join-Path $erlOutput 'Generated.dll') 2>&1
if ($LASTEXITCODE -ne 0 -or ($erlRun -join "`n") -notmatch 'Erl=5') { throw 'AST Erl/Format/Error compatibility fixture failed at runtime.' }
dotnet $compiler ast-compile $source -o $output
if ($LASTEXITCODE -ne 0) { throw 'AST CLI compilation failed.' }
$assembly = Join-Path $output 'Generated.dll'
$result = dotnet $assembly
if (($result -join "`n") -notmatch 'AST_OPTIONAL_CALL_OK') { throw 'AST omitted Optional arguments or ByRef default temporaries failed.' }
if (($result -join "`n") -notmatch 'AST_FUNCTION_RESULT_OK') { throw 'AST stored function result, fall-through or Exit Function failed.' }
if (($result -join "`n") -notmatch '(?m)^43\s*$') { throw 'AST procedure bodies or ByRef copy-back failed.' }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST_XPS_COMPILE_OK' -or ($result -join "`n") -notmatch 'AST_XPS_PRINT_OK' -or ($result -join "`n") -notmatch 'AST_XPS_IF_OK' -or ($result -join "`n") -notmatch '(?m)^1$' -or ($result -join "`n") -notmatch '(?m)^2$' -or ($result -join "`n") -notmatch '(?m)^3$' -or ($result -join "`n") -notmatch '(?m)^4$' -or ($result -join "`n") -notmatch 'AST_XPS_SELECT_OK' -or ($result -join "`n") -notmatch '(?m)^7$' -or ($result -join "`n") -notmatch '(?m)^8$') { throw 'AST CLI generated assembly did not run successfully.' }
Write-Host 'AST CLI compilation probe passed.'
