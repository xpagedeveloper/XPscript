$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$output = Join-Path $root 'out/ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$staticInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-initializer-error.xps') -o (Join-Path $output 'static-initializer-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($staticInvalid -join "`n") -notmatch 'AST Static currently requires a scalar declaration without an initializer') { throw 'AST unsupported Static initializer was not explicitly rejected.' }
$staticOutput = Join-Path $output 'static-local'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-local-lifetime.xps') -o $staticOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static local compilation failed.' }
$staticResult = dotnet (Join-Path $staticOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticResult -join "`n") -ne "1`n10`n2`n20`n1`n10`n2`n20`nSTATIC_DEFAULTS_OK`nSTATIC_RETAINED_OK`n1`n2") { throw 'AST Static lifetime, procedure/overload isolation, defaults or ByRef mutation failed.' }
$crossProcedure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-cross-procedure-error.xps') -o (Join-Path $output 'cross-procedure') 2>&1
if ($LASTEXITCODE -ne 2 -or ($crossProcedure -join "`n") -notmatch "Unknown label 'outside'") { throw 'AST GoTo accepted a label from another procedure.' }
$gotoOutput = Join-Path $output 'goto-scope'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-procedure-scope.xps') -o $gotoOutput
if ($LASTEXITCODE -ne 0) { throw 'AST procedure-scoped GoTo compilation failed.' }
$gotoResult = dotnet (Join-Path $gotoOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($gotoResult -join "`n") -ne "7`nGOTO_PROCEDURE_SCOPE_OK") { throw 'AST procedure-scoped GoTo execution failed.' }
$unsupportedGoSub = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'unsupported-gosub.xps') -o (Join-Path $output 'unsupported-gosub') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedGoSub -join "`n") -notmatch 'GoSub is not implemented in the AST compiler') { throw 'AST GoSub was not explicitly rejected.' }
$nestedOutput = Join-Path $output 'nested-goto'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nested-goto.xps') -o $nestedOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested GoTo compilation failed.' }
$nestedResult = dotnet (Join-Path $nestedOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedResult -join "`n") -ne "0`nNESTED_GOTO_OK") { throw 'AST nested GoTo branch/loop semantics failed.' }
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
