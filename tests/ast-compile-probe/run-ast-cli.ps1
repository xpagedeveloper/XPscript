$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$output = Join-Path $root 'out/ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
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
$multiDimensional = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-array-error.xps') -o (Join-Path $output 'multidimensional-array-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($multiDimensional -join "`n") -notmatch 'AST arrays currently support one dimension only') { throw 'AST silently accepted a multidimensional array declaration.' }
$nonzeroBound = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nonzero-bound-array-error.xps') -o (Join-Path $output 'nonzero-bound-array-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nonzeroBound -join "`n") -notmatch 'AST arrays currently require a zero-based constant bound') { throw 'AST silently accepted a nonzero array lower bound.' }
$membershipOutput = Join-Path $output 'list-membership'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-membership.xps') -o $membershipOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List membership compilation failed.' }
$membershipResult = dotnet (Join-Path $membershipOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($membershipResult -join "`n") -ne "True`nFalse`nTAG_EVALUATED`nTrue`n3") { throw 'AST List membership read a missing element or evaluated its tag more than once.' }
$listInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-initializer-error.xps') -o (Join-Path $output 'list-initializer-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($listInvalid -join "`n") -notmatch 'AST List declarations with initializers are not implemented') { throw 'AST ignored an unsupported List initializer.' }
$listByRef = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-byref-error.xps') -o (Join-Path $output 'list-byref-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($listByRef -join "`n") -notmatch 'AST List element ByRef arguments are not implemented') { throw 'AST did not diagnose unsupported List ByRef copy-back.' }
$listOutput = Join-Path $output 'forall-list'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-alias.xps') -o $listOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ForAll List alias compilation failed.' }
$listResult = dotnet (Join-Path $listOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listResult -join "`n") -ne "a`nb`n11`n12`none:BLUE`ntwo:GREEN`na`na`nb`nb`n12`n13`n5`n1`n2`n2") { throw 'AST ForAll List write-through, ListTag, nested alias scope or snapshot semantics failed.' }
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
$unsupportedErrorHandling = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'on-error-error.xps') -o (Join-Path $output 'on-error-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedErrorHandling -join "`n") -notmatch 'AST On Error and Resume semantics are not implemented') { throw 'AST silently lowered unsupported error handling.' }
$unsupportedWith = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'with-error.xps') -o (Join-Path $output 'with-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedWith -join "`n") -notmatch 'AST With and implicit member access are not implemented') { throw 'AST silently lowered unsupported With/member access.' }
$unsupportedImage = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-error.xps') -o (Join-Path $output 'xpimage-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedImage -join "`n") -notmatch 'AST XPImage runtime integration is not implemented') { throw 'AST silently substituted XPImage with Object.' }
$unsupportedOptionBase = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'option-base-error.xps') -o (Join-Path $output 'option-base-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedOptionBase -join "`n") -notmatch 'AST Option Base semantics are not implemented') { throw 'AST silently assumed an unsupported Option Base.' }
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
$classRuntime = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-runtime-error.xps') -o (Join-Path $output 'class-runtime-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($classRuntime -join "`n") -notmatch 'AST class declarations and runtime object lifecycle are not implemented') { throw 'AST silently accepted unsupported class runtime placeholders.' }
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
$parserError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'parser-error.xps') -o (Join-Path $output 'parser-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($parserError -join "`n") -notmatch 'Expected CloseParenToken') { throw 'AST parser diagnostic regression failed.' }
$nestedOutput = Join-Path $output 'nested-goto'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nested-goto.xps') -o $nestedOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested GoTo compilation failed.' }
$nestedResult = dotnet (Join-Path $nestedOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedResult -join "`n") -ne "0`nNESTED_GOTO_OK") { throw 'AST nested GoTo branch/loop semantics failed.' }
$nestedLoopError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-for-error.xps') -o (Join-Path $output 'nested-for-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedLoopError -join "`n") -notmatch 'GoTo cannot enter a For or ForAll block') { throw 'AST did not diagnose a GoTo entering a nested loop clearly.' }
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
