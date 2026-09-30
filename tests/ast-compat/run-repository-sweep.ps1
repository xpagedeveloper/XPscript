$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compilerDll = (Resolve-Path (Join-Path $root "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll")).Path
$outRoot = Join-Path $root "out/ast-repository-sweep"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

# These names describe fixtures whose purpose is to provoke a compiler diagnostic.
$negativePatterns = @(
    '*-error.xps',
    '*-invalid.xps',
    '*-ambiguous.xps',
    '*-duplicate.xps',
    '*-no-match.xps'
)

function Test-IsNegativeFixture([System.IO.FileInfo] $file) {
    foreach ($pattern in $negativePatterns) {
        if ($file.Name -like $pattern) { return $true }
    }
    return $false
}

$files = @(
    Get-ChildItem (Join-Path $root "samples") -Filter *.xps -File -Recurse
    Get-ChildItem (Join-Path $root "demo") -Filter *.xps -File -Recurse
) | Sort-Object FullName -Unique

$positive = @($files | Where-Object { -not (Test-IsNegativeFixture $_) })
$negative = @($files | Where-Object { Test-IsNegativeFixture $_ })

Write-Host "AST_REPOSITORY_SWEEP total=$($files.Count) positive=$($positive.Count) negative=$($negative.Count)"

$index = 0
foreach ($file in $positive) {
    $index++
    $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName)
    $safeName = ($relative -replace '[^A-Za-z0-9._-]', '_') -replace '\.xps$', ''
    $output = Join-Path $outRoot ("{0:D4}-{1}" -f $index, $safeName)
    Write-Host "AST_REPOSITORY_COMPILE=$relative"
    & dotnet $compilerDll $file.FullName -o $output --runtime=false
    if ($LASTEXITCODE -ne 0) {
        throw "Repository AST compatibility compile failed: $relative"
    }
}

Write-Host "AST_REPOSITORY_NEGATIVE_FIXTURES"
foreach ($file in $negative) {
    Write-Host ([System.IO.Path]::GetRelativePath($root, $file.FullName))
}

Write-Host "AST repository compile sweep passed: $($positive.Count) positive scripts compiled; $($negative.Count) named negative fixtures classified separately."
