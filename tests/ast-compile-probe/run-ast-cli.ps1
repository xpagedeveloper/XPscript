$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$output = Join-Path $root 'out/ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$invalidDefault = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-default-error.xps') -o (Join-Path $output 'invalid-default') 2>&1
if ($LASTEXITCODE -ne 2 -or ($invalidDefault -join "`n") -notmatch "Default value for Optional parameter 'value' is incompatible with its type") { throw 'AST invalid Optional default was not rejected by binding.' }
dotnet $compiler ast-compile $source -o $output
if ($LASTEXITCODE -ne 0) { throw 'AST CLI compilation failed.' }
$assembly = Join-Path $output 'Generated.dll'
$result = dotnet $assembly
if (($result -join "`n") -notmatch 'AST_OPTIONAL_CALL_OK') { throw 'AST omitted Optional arguments or ByRef default temporaries failed.' }
if (($result -join "`n") -notmatch 'AST_FUNCTION_RESULT_OK') { throw 'AST stored function result, fall-through or Exit Function failed.' }
if (($result -join "`n") -notmatch '(?m)^43\s*$') { throw 'AST procedure bodies or ByRef copy-back failed.' }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST_XPS_COMPILE_OK' -or ($result -join "`n") -notmatch 'AST_XPS_PRINT_OK' -or ($result -join "`n") -notmatch 'AST_XPS_IF_OK' -or ($result -join "`n") -notmatch '(?m)^1$' -or ($result -join "`n") -notmatch '(?m)^2$' -or ($result -join "`n") -notmatch '(?m)^3$' -or ($result -join "`n") -notmatch '(?m)^4$' -or ($result -join "`n") -notmatch 'AST_XPS_SELECT_OK' -or ($result -join "`n") -notmatch '(?m)^7$' -or ($result -join "`n") -notmatch '(?m)^8$') { throw 'AST CLI generated assembly did not run successfully.' }
Write-Host 'AST CLI compilation probe passed.'
