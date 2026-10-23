param(
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
} else {
    [System.IO.Path]::GetFullPath($CompilerPath)
}
if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler DLL not found: $compiler" }
$help = (dotnet $compiler --help | Out-String)
$required = @(
    'compile <source.xps>', 'ast-run <source.xps>', '--target webiis', '--platform RID', '--single-file', '--runtime',
    '--result-format', '--security', '--restricted', '--source-root', '--preprocessor',
    'run <source.xps>', '--Args'
)
foreach ($parameter in $required) {
    if ($help -notlike "*$parameter*") { throw "CLI help is missing parameter: $parameter" }
}

$describeOutput = dotnet $compiler describe XPFileInfoValue --ReSuLt-FoRmAt json | Out-String
if ($LASTEXITCODE -ne 0 -or $describeOutput -notlike '*"name": "XPFileInfoValue"*') {
    throw "Case-insensitive describe option regression: $describeOutput"
}
$symbolsOutput = dotnet $compiler symbols --SeArCh XPFile --ReSuLt-FoRmAt json | Out-String
if ($LASTEXITCODE -ne 0 -or $symbolsOutput -notlike '*XPFileInfoValue*') {
    throw "Case-insensitive symbols option regression: $symbolsOutput"
}
$validateInput = "Sub Main()`nEnd Sub"
$validateOutput = $validateInput | dotnet $compiler validate --StDiN --FiLeNaMe mixed.xps --ReSuLt-FoRmAt json | Out-String
if ($LASTEXITCODE -ne 0 -or $validateOutput -notlike '*"result": "ok"*') {
    throw "Case-insensitive validate option regression: $validateOutput"
}

$astHelp = (dotnet $compiler ast-compile 2>&1 | Out-String)
if ($LASTEXITCODE -ne 1 -or $astHelp -notlike '*ast-compile requires an .xps source file*') {
    throw 'AST command argument handling regression.'
}

$invalidRuntimeOutput = dotnet $compiler ast-compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --platform invalid-runtime --runtime=false --debug 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $invalidRuntimeOutput -notlike '*Unsupported runtime identifier*') {
    throw "AST invalid runtime validation regression: $invalidRuntimeOutput"
}

$invalidBooleanOutput = dotnet $compiler ast-compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --runtime=maybe --debug 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $invalidBooleanOutput -notlike '*--runtime must be true or false*') {
    throw "AST boolean option validation regression: $invalidBooleanOutput"
}

$conflictingDiagnosticsOutput = dotnet $compiler ast-compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --debug --info 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $conflictingDiagnosticsOutput -notlike '*AST compilation succeeded*') {
    throw "AST diagnostic-mode precedence regression: $conflictingDiagnosticsOutput"
}
$reversedConflictingDiagnosticsOutput = dotnet $compiler ast-compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --info --debug 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $reversedConflictingDiagnosticsOutput -notlike '*AST compilation succeeded*') {
    throw "AST reversed diagnostic-mode precedence regression: $reversedConflictingDiagnosticsOutput"
}
$mixedCaseAstOptionOutput = dotnet $compiler ast-compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --InFo --DeBuG 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $mixedCaseAstOptionOutput -notlike '*AST compilation succeeded*') {
    throw "AST case-insensitive option regression: $mixedCaseAstOptionOutput"
}
$mixedCaseDefaultCompileOutputPath = Join-Path $root 'tests/cli-compatibility/.tmp-mixed-case-compile'
if (Test-Path -LiteralPath $mixedCaseDefaultCompileOutputPath) { Remove-Item -LiteralPath $mixedCaseDefaultCompileOutputPath -Recurse -Force }
$mixedCaseDefaultCompileOutput = dotnet $compiler compile (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --InFo --DeBuG --OuTpUt $mixedCaseDefaultCompileOutputPath --RuNtImE=false 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $mixedCaseDefaultCompileOutput -notlike '*AST compilation succeeded*') {
    throw "Default compile case-insensitive option regression: $mixedCaseDefaultCompileOutput"
}

$missingArgsOutput = dotnet $compiler ast-run (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --Args 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $missingArgsOutput -notlike '*--Args requires one quoted argument string*') {
    throw "AST run argument validation regression: $missingArgsOutput"
}

$runConflictingDiagnosticsOutput = dotnet $compiler ast-run (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --debug --info --Args 'ast-run-debug-precedence' 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $runConflictingDiagnosticsOutput -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $runConflictingDiagnosticsOutput -notlike '*APPLICATION_NEWLINE_END*') {
    throw "AST run diagnostic-mode precedence regression: $runConflictingDiagnosticsOutput"
}
$runReversedConflictingDiagnosticsOutput = dotnet $compiler ast-run (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --info --debug --Args 'ast-run-debug-precedence-reversed' 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $runReversedConflictingDiagnosticsOutput -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $runReversedConflictingDiagnosticsOutput -notlike '*APPLICATION_NEWLINE_END*') {
    throw "AST run reversed diagnostic-mode precedence regression: $runReversedConflictingDiagnosticsOutput"
}

$legacyCaseOutput = dotnet $compiler legacy-run (Join-Path $root 'tests/ast-compile-probe/application-newline.xps') --No-Daemon --InFo --ArGs 'legacy-case-insensitive' 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $legacyCaseOutput -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $legacyCaseOutput -notlike '*APPLICATION_NEWLINE_END*') {
    throw "Legacy run case-insensitive option regression: $legacyCaseOutput"
}

$navigationOutput = dotnet $compiler ast-run (Join-Path $root 'tests/ast-compile-probe/ast-run-navigation.xps') | Out-String
if ($LASTEXITCODE -ne 0 -or $navigationOutput -notlike '*AST_RUN_NAVIGATION_BEGIN*' -or $navigationOutput -notlike '*navigation.json*' -or $navigationOutput -notlike '*AST_RUN_NAVIGATION_END*') {
    throw "AST run navigation environment regression: $navigationOutput"
}

$astSource = Join-Path $root 'tests/ast-compile-probe/application-newline.xps'
$resultRoot = Join-Path $root 'tests/cli-compatibility/.tmp-result-format'
if (Test-Path -LiteralPath $resultRoot) {
    Remove-Item -LiteralPath $resultRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$jsonOutput = dotnet $compiler ast-compile $astSource -o (Join-Path $resultRoot 'json') --result-format json | Out-String
if ($LASTEXITCODE -ne 0) { throw "AST JSON result-format regression: $jsonOutput" }
$jsonResult = $jsonOutput | ConvertFrom-Json
if ($jsonResult.result -ne 'ok' -or $jsonResult.operation -ne 'ast-compile' -or [string]::IsNullOrWhiteSpace($jsonResult.output)) {
    throw 'AST JSON result-format contract regression.'
}

$xmlOutput = dotnet $compiler ast-compile $astSource -o (Join-Path $resultRoot 'xml') --result-format xml | Out-String
if ($LASTEXITCODE -ne 0) { throw "AST XML result-format regression: $xmlOutput" }
[xml]$xmlResult = $xmlOutput
if ($xmlResult.compileResult.result -ne 'ok' -or $xmlResult.compileResult.operation -ne 'ast-compile') {
    throw 'AST XML result-format contract regression.'
}

$aliasOutput = Join-Path $resultRoot 'singlefile-alias'
$aliasOutputText = dotnet $compiler ast-compile $astSource -o $aliasOutput --singlefile=false --runtime=false --info --embed-assets --security=strict --restricted --source-root $root --preprocessor identity | Out-String
if ($LASTEXITCODE -ne 0 -or $aliasOutputText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $aliasOutput 'Generated.dll'))) {
    throw "AST compile-option/security regression: $aliasOutputText"
}

$runtimeOutput = Join-Path $resultRoot 'runtime-true'
$runtimeOutputText = dotnet $compiler ast-compile $astSource -o $runtimeOutput --runtime=true --single-file=false --debug | Out-String
if ($LASTEXITCODE -ne 0 -or $runtimeOutputText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $runtimeOutput 'Generated.dll'))) {
    throw "AST self-contained runtime option regression: $runtimeOutputText"
}

$runtimeRidOutput = Join-Path $resultRoot 'runtime-rid'
$runtimeRidText = dotnet $compiler ast-compile $astSource -o $runtimeRidOutput --runtime=true --single-file=false --rid win-x64 | Out-String
if ($LASTEXITCODE -ne 0 -or $runtimeRidText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $runtimeRidOutput 'Generated.dll'))) {
    throw "AST self-contained RID option regression: $runtimeRidText"
}

$singleFileOutput = Join-Path $resultRoot 'single-file-true'
$singleFileText = dotnet $compiler ast-compile $astSource -o $singleFileOutput --runtime=true --single-file=true --rid win-x64 | Out-String
if ($LASTEXITCODE -ne 0 -or $singleFileText -notlike '*AST compilation succeeded*' -or
    -not (Test-Path (Join-Path $singleFileOutput 'Generated.exe')) -or
    (Test-Path (Join-Path $singleFileOutput 'Generated.dll'))) {
    throw "AST single-file output regression: $singleFileText"
}

$frameworkSingleFileOutput = Join-Path $resultRoot 'framework-single-file'
$frameworkSingleFileText = dotnet $compiler ast-compile $astSource -o $frameworkSingleFileOutput --runtime=false --single-file=true --rid win-x64 | Out-String
if ($LASTEXITCODE -ne 0 -or $frameworkSingleFileText -notlike '*AST compilation succeeded*' -or
    -not (Test-Path (Join-Path $frameworkSingleFileOutput 'Generated.exe'))) {
    throw "AST framework-dependent single-file regression: $frameworkSingleFileText"
}
$singleFileRunOutput = & (Join-Path $frameworkSingleFileOutput 'Generated.exe') | Out-String
if ($LASTEXITCODE -ne 0 -or $singleFileRunOutput -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $singleFileRunOutput -notlike '*APPLICATION_NEWLINE_END*') {
    throw "AST framework-dependent single-file execution regression: $singleFileRunOutput"
}

$platformAliasOutput = Join-Path $resultRoot 'platform-alias'
$platformAliasText = dotnet $compiler ast-compile $astSource -o $platformAliasOutput --platform win-x64 --runtime=false | Out-String
if ($LASTEXITCODE -ne 0 -or $platformAliasText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $platformAliasOutput 'Generated.dll'))) {
    throw "AST --platform alias regression: $platformAliasText"
}

$astRunText = dotnet $compiler ast-run $astSource --platform win-x64 --Args 'ast-run-check' | Out-String
if ($LASTEXITCODE -ne 0 -or $astRunText -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $astRunText -notlike '*APPLICATION_NEWLINE_END*') {
    throw "AST run regression: $astRunText"
}

$astRunCompatibilityText = dotnet $compiler ast-run $astSource --no-daemon --info --result-format text --Args 'ast-run-compatibility-check' | Out-String
if ($LASTEXITCODE -ne 0 -or $astRunCompatibilityText -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $astRunCompatibilityText -notlike '*APPLICATION_NEWLINE_END*') {
    throw "AST run legacy-option compatibility regression: $astRunCompatibilityText"
}

$daemonDependencyText = dotnet $compiler run (Join-Path $root 'tests/ast-compile-probe/mysql-native-next.xps') --info --Args 'daemon-dependency-check' 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $daemonDependencyText -notlike '*MySqlConnector 2.6.2*' -or $daemonDependencyText -notlike '*MYSQL_AST_OK*') {
    throw "Default daemon run dependency-info regression: $daemonDependencyText"
}

$defaultRunText = dotnet $compiler run $astSource --platform win-x64 --Args 'default-run-check' | Out-String
if ($LASTEXITCODE -ne 0 -or $defaultRunText -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $defaultRunText -notlike '*APPLICATION_NEWLINE_END*') {
    throw "Default AST run regression: $defaultRunText"
}

$legacyRunText = dotnet $compiler legacy-run $astSource --platform win-x64 --no-daemon --Args 'legacy-run-check' | Out-String
if ($LASTEXITCODE -ne 0 -or $legacyRunText -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $legacyRunText -notlike '*APPLICATION_NEWLINE_END*') {
    throw "Legacy run alias regression: $legacyRunText"
}

$runV1Text = dotnet $compiler runv1 $astSource --platform win-x64 --no-daemon --Args 'runv1-check' | Out-String
if ($LASTEXITCODE -ne 0 -or $runV1Text -notlike '*APPLICATION_NEWLINE_BEGIN*' -or $runV1Text -notlike '*APPLICATION_NEWLINE_END*') {
    throw "runv1 legacy alias regression: $runV1Text"
}

$legacyOutput = Join-Path $resultRoot 'legacy-compile'
$legacyOutputText = dotnet $compiler legacy-compile $astSource -o $legacyOutput --runtime=false --single-file=false --debug | Out-String
if ($LASTEXITCODE -ne 0 -or $legacyOutputText -notlike '*result: ok*' -or -not (Test-Path $legacyOutput)) {
    throw "Legacy compile option compatibility regression: $legacyOutputText"
}

$compileV1Output = Join-Path $resultRoot 'compilev1'
$compileV1Text = dotnet $compiler compilev1 $astSource -o $compileV1Output --runtime=false --single-file=false | Out-String
if ($LASTEXITCODE -ne 0 -or $compileV1Text -notlike '*result: ok*' -or -not (Test-Path $compileV1Output)) {
    throw "compilev1 legacy alias regression: $compileV1Text"
}

foreach ($legacyTarget in @('desktop', 'console')) {
    $legacyTargetText = dotnet $compiler legacy-compile $astSource --target $legacyTarget --runtime=false --single-file=false 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $legacyTargetText -notlike '*--target currently supports webiis*') {
        throw "Legacy --target $legacyTarget validation regression: $legacyTargetText"
    }
}

$legacyWebIisText = dotnet $compiler legacy-compile $astSource --target webiis --runtime=false --single-file=false 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $legacyWebIisText -notlike '*webiis target requires main.xps*') {
    throw "Legacy webiis entry-point validation regression: $legacyWebIisText"
}

$defaultCompileOutput = Join-Path $resultRoot 'default-ast-compile'
$defaultCompileText = dotnet $compiler compile $astSource -o $defaultCompileOutput --runtime=false --single-file=false | Out-String
if ($LASTEXITCODE -ne 0 -or $defaultCompileText -notlike '*AST compilation succeeded*') {
    throw "Default compile AST regression: $defaultCompileText"
}

foreach ($target in @('desktop', 'console')) {
    $targetOutputPath = Join-Path $resultRoot "target-$target"
    $targetText = dotnet $compiler ast-compile $astSource -o $targetOutputPath --target $target --runtime=false | Out-String
    if ($LASTEXITCODE -ne 0 -or $targetText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $targetOutputPath 'Generated.dll'))) {
        throw "AST --target $target compatibility regression: $targetText"
    }
}

$invalidTargetText = dotnet $compiler ast-compile $astSource --target unsupported-target 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) {
    throw "AST invalid --target validation regression: $invalidTargetText"
}

$webSource = Join-Path $root 'demo/webiis/main.xps'
$webOutput = Join-Path $resultRoot 'webiis'
$targetOutput = dotnet $compiler ast-compile $webSource --target webiis --runtime=false --output $webOutput --result-format json | Out-String
if ($LASTEXITCODE -ne 0) { throw "AST webiis target regression: $targetOutput" }
$targetResult = $targetOutput | ConvertFrom-Json
if ($targetResult.result -ne 'ok' -or $targetResult.operation -ne 'ast-compile' -or -not (Test-Path ($webOutput + '.zip'))) {
    throw 'AST webiis target did not produce a deployable package.'
}

Write-Host 'CLI_COMMAND_LINE_COMPATIBILITY_OK'
