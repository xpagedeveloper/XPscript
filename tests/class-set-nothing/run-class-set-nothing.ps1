$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-set-nothing"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-set-nothing-semantics.xps"
$output = Join-Path $outRoot "class-set-nothing-semantics"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-set-nothing-semantics.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-set-nothing-semantics.xps" }
$joined = ($text -join [Environment]::NewLine)

if ($joined -match "(?m)^DELETE_CALLED\s*$") { throw "Set object = Nothing must not invoke Sub Delete." }
if ($joined -notmatch "(?m)^PRIMARY_NOTHING=YES\s*$") { throw "Set object = Nothing did not clear the target reference." }
if ($joined -notmatch "(?m)^ALIAS_NAME=alive\s*$") { throw "Clearing one reference incorrectly cleared its alias." }
if ($joined -match "(?m)^ALIAS_NOTHING=YES\s*$") { throw "Alias became Nothing after clearing another reference." }

Write-Host "CLASS_SET_NOTHING_FOCUSED=OK"
exit 0
