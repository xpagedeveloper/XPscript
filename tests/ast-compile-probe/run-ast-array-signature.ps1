$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
foreach ($case in @(
    @('array-storage-bounds-error', 'XPS1012', 2),
    @('constructor-array-deferred', 'XPS3001', 2),
    @('procedure-array-deferred', 'XPS3001', 1),
    @('method-array-deferred', 'XPS3001', 2),
    @('constructor-array-rank-error', 'XPS1012', 2),
    @('constructor-array-list-error', 'XPS1012', 2)
)) {
    $fixture = $case[0]
    $result = dotnet $compiler ast-compile (Join-Path $PSScriptRoot ($fixture + '.xps')) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($result -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") {
        throw "Array signature diagnostic mismatch: $result"
    }
}
Write-Host 'AST_ARRAY_SIGNATURE_GUARDS_OK'
