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

Write-Host 'CLI_COMMAND_LINE_COMPATIBILITY_OK'
