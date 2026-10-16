$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/destructor-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('class-destructor', 'class-destructor-parity')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST destructor compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = if ($fixture -eq 'class-destructor') { 'CLASS_DESTRUCTOR_OK' } else { "DESTRUCTOR_CALLED`nDESTRUCTOR_PARITY_OK" }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST destructor execution failed: $fixture" }
}
$normal = Join-Path $output $(if ($IsWindows) { 'normal.exe' } else { 'normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-destructor-parity.xps') -o $normal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal destructor parity compilation failed.' }
$result = & $normal
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne "DESTRUCTOR_CALLED`nDESTRUCTOR_PARITY_OK") { throw 'Normal/AST destructor parity failed.' }
foreach ($case in @(
    @('class-destructor-parameter-error', 'XPS1012', 2),
    @('class-destructor-duplicate-error', 'XPS1012', 4),
    @('class-destructor-function-error', 'XPS1012', 2),
    @('class-destructor-derived-deferred', 'XPS3001', 4),
    @('class-destructor-body-error', 'XPS2008', 3)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Destructor diagnostic lost: $fixture`n$failure" }
}
Write-Host 'AST_DESTRUCTORS_OK'
