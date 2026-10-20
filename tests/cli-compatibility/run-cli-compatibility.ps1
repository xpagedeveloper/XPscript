$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$help = (dotnet $compiler --help | Out-String)
$required = @(
    'compile <source.xps>', '--target webiis', '--platform RID', '--single-file', '--runtime',
    '--result-format', '--security', '--restricted', '--source-root', '--preprocessor',
    'run <source.xps>', '--Args'
)
foreach ($parameter in $required) {
    if ($help -notlike "*$parameter*") { throw "CLI help is missing parameter: $parameter" }
}

$astHelp = (dotnet $compiler ast-compile 2>&1 | Out-String)
if ($LASTEXITCODE -ne 1 -or $astHelp -notlike '*ast-compile requires an .xps source file*') {
    throw 'AST command argument handling regression.'
}

$astSource = Join-Path $root 'tests/ast-compile-probe/application-newline.xps'
$resultRoot = Join-Path $root 'tests/cli-compatibility/.tmp-result-format'
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
$aliasOutputText = dotnet $compiler ast-compile $astSource -o $aliasOutput --singlefile=false --runtime=false --security=strict --restricted --source-root $root --preprocessor identity | Out-String
if ($LASTEXITCODE -ne 0 -or $aliasOutputText -notlike '*AST compilation succeeded*' -or -not (Test-Path (Join-Path $aliasOutput 'Generated.dll'))) {
    throw "AST compile-option/security regression: $aliasOutputText"
}

Write-Host 'CLI_COMMAND_LINE_COMPATIBILITY_OK'
