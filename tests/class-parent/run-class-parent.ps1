$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-parent"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

function Compile-ExpectFailure([string] $sourceName, [string] $expectedText) {
    $source = Join-Path $root "samples/$sourceName"
    $output = Join-Path $outRoot ([System.IO.Path]::GetFileNameWithoutExtension($sourceName))
    $text = & dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) { throw "Expected compilation failure: $sourceName" }
    $joined = ($text -join [Environment]::NewLine)
    if ($joined -notmatch "XPS2009" -or $joined -notmatch $expectedText) {
        throw "Expected XPS2009 Parent diagnostic: $sourceName"
    }
}

$inheritanceSource = Join-Path $root "samples/class-inheritance-contract.xps"
$inheritanceOutput = Join-Path $outRoot "class-inheritance-contract"
& dotnet run --project $compiler -c Release --no-build -- $inheritanceSource -o $inheritanceOutput --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-inheritance-contract.xps" }
$inheritanceText = & $inheritanceOutput 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-inheritance-contract.xps" }
$joinedInheritance = ($inheritanceText -join [Environment]::NewLine)
if ($joinedInheritance -notmatch "INHERIT=base:touch\|base:touch\|base:touch:property") {
    throw "Parent Sub/Function/Property contract regression failed."
}

Compile-ExpectFailure "class-parent-private-error.xps" "Private base member"
Compile-ExpectFailure "class-parent-unknown-error.xps" "has no member"

Write-Host "CLASS_PARENT_FOCUSED=OK"
exit 0
