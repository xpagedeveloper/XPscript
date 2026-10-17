$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
foreach ($fixture in @('array-parameter-runtime-next', 'constructor-array-runtime-next')) {
    $output = Join-Path $root ("out/array-signature-" + $fixture)
    if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    $result = dotnet $compiler ast-compile (Join-Path $PSScriptRoot ($fixture + '.xps')) -o $output 2>&1
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST compilation succeeded') { throw "Array signature compilation failed: $result" }
    $run = dotnet (Join-Path $output 'Generated.dll') 2>&1
    if ($LASTEXITCODE -ne 0 -or ($run -join "`n") -notmatch '41:-2:0|42:-1:1') { throw "Array signature runtime mismatch: $run" }
}
foreach ($case in @(
    @('array-storage-bounds-error', 'XPS1012', 2),
    @('constructor-array-rank-error', 'XPS1012', 2),
    @('constructor-array-list-error', 'XPS1012', 2)
)) {
    $fixture = $case[0]
    $result = dotnet $compiler ast-compile (Join-Path $PSScriptRoot ($fixture + '.xps')) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($result -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") {
        throw "Array signature diagnostic mismatch: $result"
    }
}
Write-Host 'AST_ARRAY_SIGNATURES_OK'
