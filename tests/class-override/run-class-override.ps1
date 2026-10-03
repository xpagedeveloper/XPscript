$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-override"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

function Compile-ExpectFailure([string] $sourceName) {
    $source = Join-Path $root "samples/$sourceName"
    $output = Join-Path $outRoot ([System.IO.Path]::GetFileNameWithoutExtension($sourceName))
    $text = & dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) { throw "Expected compilation failure: $sourceName" }
    $joined = ($text -join [Environment]::NewLine)
    if ($joined -notmatch "XPS2007" -or $joined -notmatch "incompatible override") {
        throw "Expected XPS2007 incompatible override diagnostic: $sourceName"
    }
}

function Compile-And-Run([string] $sourceName, [string] $expectedPattern) {
    $source = Join-Path $root "samples/$sourceName"
    $output = Join-Path $outRoot ([System.IO.Path]::GetFileNameWithoutExtension($sourceName))
    & dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $sourceName" }
    $text = & $output 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Program failed: $sourceName" }
    if (($text -join [Environment]::NewLine) -notmatch $expectedPattern) {
        throw "Unexpected runtime output: $sourceName"
    }
}

Compile-ExpectFailure "class-override-signature-error.xps"
Compile-And-Run "class-override-signature-ok.xps" "(?m)^7\s*$"
Compile-ExpectFailure "class-property-override-signature-error.xps"
Compile-And-Run "class-property-override-signature-ok.xps" "(?m)^base:child\s*$"
Compile-ExpectFailure "class-transitive-override-signature-error.xps"

Write-Host "CLASS_OVERRIDE_FOCUSED=OK"
exit 0
