$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/destructor-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('class-destructor-derived', 'class-destructor-derived-parity', 'class-destructor-derived-signature', 'class-destructor', 'class-destructor-parity')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST destructor compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = switch ($fixture) {
        'class-destructor-derived' { 'CLASS_DERIVED_DESTRUCTORS_OK' }
        'class-destructor-derived-parity' { "LEAF`nMIDDLE`nROOT`nDERIVED_DESTRUCTOR_PARITY_OK" }
        'class-destructor-derived-signature' { 'DERIVED_DESTRUCTOR_SIGNATURE_OK' }
        'class-destructor' { 'CLASS_DESTRUCTOR_OK' }
        'class-destructor-parity' { "DESTRUCTOR_CALLED`nDESTRUCTOR_PARITY_OK" }
    }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST destructor execution failed: $fixture" }
}
$derivedMethodTarget = Join-Path $output 'class-destructor-derived-method-deferred'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-destructor-derived-method-deferred.xps') -o $derivedMethodTarget
if ($LASTEXITCODE -ne 0) { throw 'AST derived method destructor fixture compilation failed.' }
Write-Host 'AST_DERIVED_METHOD_DESTRUCTOR_OK'
foreach ($fixture in @('class-destructor-derived-parity', 'class-destructor-parity')) {
    $normal = Join-Path $output ($fixture + $(if ($IsWindows) { '-normal.exe' } else { '-normal' }))
    dotnet $compiler legacy-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $normal --single-file=false --runtime=false
    if ($LASTEXITCODE -ne 0) { throw "Normal destructor parity compilation failed: $fixture" }
    $result = & $normal
    $expected = if ($fixture -eq 'class-destructor-parity') { "DESTRUCTOR_CALLED`nDESTRUCTOR_PARITY_OK" } else { "LEAF`nMIDDLE`nROOT`nDERIVED_DESTRUCTOR_PARITY_OK" }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "Normal/AST destructor parity failed: $fixture" }
}
foreach ($case in @(
    @('class-destructor-parameter-error', 'XPS1012', 2),
    @('class-destructor-duplicate-error', 'XPS1012', 4),
    @('class-destructor-function-error', 'XPS1012', 2),
    @('class-destructor-private-base-error', 'XPS2008', 6),
    @('class-destructor-private-member-error', 'XPS2009', 6),
    @('class-destructor-private-method-error', 'XPS2009', 7),
    @('class-destructor-derived-body-error', 'XPS2008', 5),
    @('class-destructor-body-error', 'XPS2008', 3)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Destructor diagnostic lost: $fixture`n$failure" }
}
Write-Host 'AST_DESTRUCTORS_OK'
Write-Host 'AST_DERIVED_DESTRUCTORS_OK'
