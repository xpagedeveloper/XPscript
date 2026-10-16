$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$outputRoot = if ($env:XPSCRIPT_TEST_OUTPUT_ROOT) { $env:XPSCRIPT_TEST_OUTPUT_ROOT } else { Join-Path $root 'out' }
$output = Join-Path $outputRoot ('constructor-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('class-constructor-optional-reference', 'constructor-reference-optional', 'class-constructor-reference-byref', 'constructor-reference-byref-simple', 'constructor-reference-byref', 'constructor-reference-implicit', 'class-constructor-reference', 'constructor-reference-byval', 'class-constructor-reference-parity', 'constructor-optional-byref-zero', 'class-constructor-optional-byref', 'constructor-byref-optional', 'constructor-optional-byref', 'class-constructor-mixed', 'class-constructor-byref', 'class-constructor-byref-simple', 'constructor-byref', 'class-constructor-optional', 'class-constructor-optional-parity', 'constructor-optional', 'class-constructor-derived', 'class-constructor-derived-parity', 'constructor-parent-forwarding', 'class-constructor', 'class-constructor-parity')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST constructor compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = switch ($fixture) {
        'class-constructor-optional-reference' { 'OPTIONAL_REFERENCE_CONSTRUCTORS_OK' }
        'constructor-reference-optional' { 'CONSTRUCTOR_REFERENCE_OPTIONAL_OK' }
        'class-constructor-reference-byref' { 'BYREF_REFERENCE_CONSTRUCTORS_OK' }
        'constructor-reference-byref-simple' { "7`nREFERENCE_BYREF_SIMPLE_OK" }
        'constructor-reference-byref' { 'CONSTRUCTOR_REFERENCE_BYREF_OK' }
        'constructor-reference-implicit' { 'CONSTRUCTOR_REFERENCE_IMPLICIT_OK' }
        'class-constructor-reference' { 'REFERENCE_CONSTRUCTORS_OK' }
        'constructor-reference-byval' { 'CONSTRUCTOR_REFERENCE_BYVAL_OK' }
        'class-constructor-reference-parity' { "5`n5`nREFERENCE_CONSTRUCTOR_PARITY_OK" }
        'constructor-optional-byref-zero' { 'OPTIONAL_BYREF_ZERO_OK' }
        'class-constructor-optional-byref' { 'OPTIONAL_BYREF_CONSTRUCTORS_OK' }
        'constructor-byref-optional' { 'CONSTRUCTOR_BYREF_OPTIONAL_OK' }
        'constructor-optional-byref' { 'CONSTRUCTOR_OPTIONAL_BYREF_OK' }
        'class-constructor-mixed' { 'MIXED_CONSTRUCTORS_OK' }
        'class-constructor-byref' { 'BYREF_CONSTRUCTORS_OK' }
        'class-constructor-byref-simple' { "5`n5`nBYREF_CONSTRUCTOR_SIMPLE_OK" }
        'constructor-byref' { 'CONSTRUCTOR_BYREF_OK' }
        'class-constructor-optional' { 'OPTIONAL_CONSTRUCTORS_OK' }
        'class-constructor-optional-parity' { "5`ndefault`n9`nsupplied`nOPTIONAL_CONSTRUCTOR_PARITY_OK" }
        'constructor-optional' { 'CONSTRUCTOR_OPTIONAL_OK' }
        'class-constructor-derived' { 'CLASS_DERIVED_CONSTRUCTORS_OK' }
        'class-constructor-derived-parity' { "root`nchild`n5`n6`nDERIVED_CONSTRUCTOR_PARITY_OK" }
        'constructor-parent-forwarding' { 'PARENT_FORWARDING_OK' }
        'class-constructor' { 'CLASS_CONSTRUCTOR_OK' }
        'class-constructor-parity' { "5`nfirst`n9`nsecond`nCONSTRUCTOR_PARITY_OK" }
    }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST constructor execution failed: $fixture" }
}
$normal = Join-Path $output $(if ($IsWindows) { 'normal.exe' } else { 'normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-constructor-parity.xps') -o $normal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal constructor parity compilation failed.' }
$result = & $normal
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "5`nfirst`n9`nsecond`nCONSTRUCTOR_PARITY_OK") { throw 'Normal/AST constructor parity failed.' }
$normalDerived = Join-Path $output $(if ($IsWindows) { 'normal-derived.exe' } else { 'normal-derived' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-constructor-derived-parity.xps') -o $normalDerived --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal derived constructor parity compilation failed.' }
$result = & $normalDerived
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "root`nchild`n5`n6`nDERIVED_CONSTRUCTOR_PARITY_OK") { throw 'Normal/AST derived constructor parity failed.' }
$normalOptional = Join-Path $output $(if ($IsWindows) { 'normal-optional.exe' } else { 'normal-optional' })
# Normal compilation currently requires full constructor arity despite Optional.
# Omission semantics are exercised separately by class-constructor-optional.xps.
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-constructor-optional-parity.xps') -o $normalOptional --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal Optional constructor parity compilation failed.' }
$result = & $normalOptional
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "5`ndefault`n9`nsupplied`nOPTIONAL_CONSTRUCTOR_PARITY_OK") { throw 'Normal/AST Optional constructor parity failed.' }
# Normal compilation currently fails the simple ByRef constructor at runtime
# ('long' has no Value member); retain the existing valid ByVal parity above.
$normalReference = Join-Path $output $(if ($IsWindows) { 'normal-reference.exe' } else { 'normal-reference' })
# Concrete-class parameter mutation/snapshot parity; broader Object/default/error cases are AST capability tests.
# Reference ByRef's simple fixture remains AST capability: normal compilation runs but prints 0 instead of 7.
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-constructor-reference-parity.xps') -o $normalReference --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal reference constructor parity compilation failed.' }
$result = & $normalReference
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "5`n5`nREFERENCE_CONSTRUCTOR_PARITY_OK") { throw 'Normal/AST concrete-reference constructor parity failed.' }
foreach ($case in @(
    @('constructor-mixed-literal-error', 'XPS2001', 6),
    @('constructor-mixed-type-error', 'XPS2003', 7),
    @('constructor-mixed-default-error', 'XPS2008', 2),
    @('constructor-arity-error', 'XPS2004', 6),
    @('constructor-type-error', 'XPS2004', 6),
    @('constructor-private-error', 'XPS2009', 6),
    @('constructor-body-error', 'XPS2008', 3),
    @('constructor-byref-literal-error', 'XPS2001', 6),
    @('constructor-byref-type-error', 'XPS2003', 7),
    @('constructor-byref-property-error', 'XPS3001', 11),
    @('constructor-byref-indexed-property-error', 'XPS3001', 11),
    @('constructor-byref-module-storage-error', 'XPS3001', 7),
    @('constructor-byref-parent-literal-error', 'XPS2001', 7),
    @('constructor-byref-parent-type-error', 'XPS2003', 7),
    @('constructor-optional-byref-literal-error', 'XPS2001', 6),
    @('constructor-optional-byref-type-error', 'XPS2003', 7),
    @('constructor-optional-byref-default-error', 'XPS2008', 2),
    @('constructor-optional-byref-default-type-error', 'XPS2001', 2),
    @('constructor-optional-byref-parent-literal-error', 'XPS2001', 7),
    @('constructor-optional-order-error', 'XPS1012', 2),
    @('constructor-optional-marker-error', 'XPS1012', 2),
    @('constructor-optional-type-error', 'XPS2001', 2),
    @('constructor-optional-name-error', 'XPS2008', 2),
    @('constructor-optional-later-error', 'XPS2008', 2),
    @('constructor-optional-self-error', 'XPS2008', 2),
    @('constructor-optional-instance-error', 'XPS2008', 3),
    @('constructor-optional-required-error', 'XPS2004', 6),
    @('constructor-optional-slot-error', 'XPS1012', 6),
    @('constructor-optional-private-error', 'XPS2009', 6),
    @('constructor-refref-upcast-error', 'XPS2003', 11),
    @('constructor-refref-object-error', 'XPS2003', 9),
    @('constructor-refref-temporary-error', 'XPS2001', 8),
    @('constructor-refref-variant-error', 'XPS2003', 7),
    @('constructor-refref-property-error', 'XPS3001', 14),
    @('constructor-refref-array-deferred', 'XPS3001', 9),
    @('constructor-refref-list-deferred', 'XPS3001', 8),
    @('constructor-refref-forall-deferred', 'XPS3001', 9),
    @('constructor-refref-parent-error', 'XPS2003', 11),
    @('constructor-refref-optional-deferred', 'XPS3001', 4),
    @('constructor-refref-module-deferred', 'XPS3001', 8),
    @('constructor-optional-reference-type-error', 'XPS2001', 2),
    @('constructor-optional-reference-name-error', 'XPS2008', 4),
    @('constructor-reference-native-deferred', 'XPS3001', 2),
    @('constructor-reference-variant-deferred', 'XPS3001', 2),
    @('constructor-reference-list-deferred', 'XPS3001', 2),
    @('constructor-reference-unknown-deferred', 'XPS3001', 2),
    @('constructor-reference-scalar-error', 'XPS2004', 6),
    @('constructor-reference-class-error', 'XPS2004', 11),
    @('constructor-reference-downcast-error', 'XPS2004', 11),
    @('constructor-reference-method-deferred', 'XPS3001', 6),
    @('constructor-parent-late-error', 'XPS1012', 6),
    @('constructor-parent-duplicate-error', 'XPS1012', 6),
    @('constructor-parent-root-error', 'XPS1012', 3),
    @('constructor-parent-type-error', 'XPS2004', 7),
    @('constructor-parent-private-error', 'XPS2009', 7),
    @('constructor-parent-instance-error', 'XPS2008', 8),
    @('constructor-parent-nested-error', 'XPS1012', 6),
    @('constructor-private-base-field-error', 'XPS2008', 6),
    @('constructor-required-base-deferred', 'XPS2004', 5),
    @('constructor-overloads-deferred', 'XPS3001', 4)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Constructor diagnostic lost: $fixture`n$failure" }
}
Write-Host 'AST_CONSTRUCTORS_OK'
Write-Host 'AST_REFERENCE_CONSTRUCTORS_OK'
Write-Host 'AST_BYREF_REFERENCE_CONSTRUCTORS_OK'
Write-Host 'AST_OPTIONAL_REFERENCE_CONSTRUCTORS_OK'
Write-Host 'AST_MIXED_CONSTRUCTORS_OK'
Write-Host 'AST_OPTIONAL_BYREF_CONSTRUCTORS_OK'
Write-Host 'AST_DERIVED_CONSTRUCTORS_OK'
Write-Host 'AST_OPTIONAL_CONSTRUCTORS_OK'
Write-Host 'AST_BYREF_CONSTRUCTORS_OK'
