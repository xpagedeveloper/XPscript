$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/constructor-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('class-constructor', 'class-constructor-parity')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST constructor compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = if ($fixture -eq 'class-constructor') { 'CLASS_CONSTRUCTOR_OK' } else { "5`nfirst`n9`nsecond`nCONSTRUCTOR_PARITY_OK" }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST constructor execution failed: $fixture" }
}
$normal = Join-Path $output $(if ($IsWindows) { 'normal.exe' } else { 'normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-constructor-parity.xps') -o $normal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal constructor parity compilation failed.' }
$result = & $normal
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "5`nfirst`n9`nsecond`nCONSTRUCTOR_PARITY_OK") { throw 'Normal/AST constructor parity failed.' }
foreach ($case in @(
    @('constructor-arity-error', 'XPS2004', 6),
    @('constructor-type-error', 'XPS2004', 6),
    @('constructor-private-error', 'XPS2009', 6),
    @('constructor-body-error', 'XPS2008', 3),
    @('constructor-byref-deferred', 'XPS3001', 2),
    @('constructor-optional-deferred', 'XPS3001', 2),
    @('constructor-reference-deferred', 'XPS3001', 2),
    @('constructor-parent-deferred', 'XPS3001', 6),
    @('constructor-required-base-deferred', 'XPS3001', 5),
    @('constructor-overloads-deferred', 'XPS3001', 4)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Constructor diagnostic lost: $fixture`n$failure" }
}
Write-Host 'AST_CONSTRUCTORS_OK'
