$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/constructor-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('class-constructor-derived', 'class-constructor-derived-parity', 'constructor-parent-forwarding', 'class-constructor', 'class-constructor-parity')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST constructor compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = switch ($fixture) {
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
foreach ($case in @(
    @('constructor-arity-error', 'XPS2004', 6),
    @('constructor-type-error', 'XPS2004', 6),
    @('constructor-private-error', 'XPS2009', 6),
    @('constructor-body-error', 'XPS2008', 3),
    @('constructor-byref-deferred', 'XPS3001', 2),
    @('constructor-optional-deferred', 'XPS3001', 2),
    @('constructor-reference-deferred', 'XPS3001', 2),
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
Write-Host 'AST_DERIVED_CONSTRUCTORS_OK'
