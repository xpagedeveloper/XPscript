$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$output = Join-Path $root 'out/ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
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
$multiDimensional = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-array-error.xps') -o (Join-Path $output 'multidimensional-array-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($multiDimensional -join "`n") -notmatch 'AST arrays currently support one dimension only') { throw 'AST silently accepted a multidimensional array declaration.' }
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
$nestedLoopBodyOutput = Join-Path $output 'goto-nested-loop-body'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-loop-body.xps') -o $nestedLoopBodyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested For/ForAll GoTo compilation failed.' }
$nestedLoopBodyResult = dotnet (Join-Path $nestedLoopBodyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedLoopBodyResult -join "`n") -ne '9') { throw 'AST nested For/ForAll GoTo semantics failed.' }
$nestedLoopError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-for-error.xps') -o (Join-Path $output 'nested-for-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedLoopError -join "`n") -notmatch 'GoTo cannot enter a For or ForAll block') { throw 'AST did not diagnose a GoTo entering a nested loop clearly.' }
$declarationJumpError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-error.xps') -o (Join-Path $output 'declaration-jump-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($declarationJumpError -join "`n") -notmatch 'GoTo cannot jump over local declaration') { throw 'AST did not diagnose a GoTo jumping over a local declaration clearly.' }
$declarationHoistOutput = Join-Path $output 'goto-declaration-hoist'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-hoist.xps') -o $declarationHoistOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GoTo declaration hoisting compilation failed.' }
$declarationHoistResult = dotnet (Join-Path $declarationHoistOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($declarationHoistResult -join "`n") -ne 'GOTO_DECLARATION_HOIST_OK') { throw 'AST GoTo declaration hoisting execution failed.' }
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
