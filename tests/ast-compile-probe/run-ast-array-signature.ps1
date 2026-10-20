$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'

function Invoke-ExpectedCompilerDiagnostic([string] $sourcePath) {
    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $arguments = '"' + $compiler + '" ast-compile "' + $sourcePath + '"'
        $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -Wait -PassThru
        $output = @()
        if (Test-Path $stdoutPath) { $output += Get-Content -LiteralPath $stdoutPath }
        if (Test-Path $stderrPath) { $output += Get-Content -LiteralPath $stderrPath }
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = ($output -join "`n") }
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

foreach ($fixture in @('array-parameter-runtime-next', 'constructor-array-runtime-next', 'method-array-runtime-next', 'constructor-array-optional-next')) {
    $output = Join-Path $root ("out/array-signature-" + $fixture)
    if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    $result = dotnet $compiler ast-compile (Join-Path $PSScriptRoot ($fixture + '.xps')) -o $output 2>&1
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST compilation succeeded') { throw "Array signature compilation failed: $result" }
    $run = dotnet (Join-Path $output 'Generated.dll') 2>&1
    if ($LASTEXITCODE -ne 0 -or ($run -join "`n") -notmatch '41:-2:0|42:-1:1|43:-3:-1|nothing') { throw "Array signature runtime mismatch: $run" }
}
foreach ($case in @(
    @('array-storage-bounds-error', 'XPS1012', 2),
    @('constructor-array-rank-error', 'XPS1012', 2),
    @('constructor-array-list-error', 'XPS1012', 2)
)) {
    $fixture = $case[0]
    $diagnostic = Invoke-ExpectedCompilerDiagnostic (Join-Path $PSScriptRoot ($fixture + '.xps'))
    if ($diagnostic.ExitCode -ne 2 -or $diagnostic.Output -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") {
        throw "Array signature diagnostic mismatch: $($diagnostic.Output)"
    }
}
Write-Host 'AST_ARRAY_SIGNATURES_OK'
