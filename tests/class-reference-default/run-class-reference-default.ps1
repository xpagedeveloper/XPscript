$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-reference-default"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-reference-default-nothing.xps"
$output = Join-Path $outRoot "class-reference-default-nothing"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-reference-default-nothing.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-reference-default-nothing.xps" }
$joined = ($text -join [Environment]::NewLine)

if ($joined -notmatch "(?m)^LOCAL_NOTHING=OK\s*$") { throw "Uninitialized local object reference was not Nothing." }
if ($joined -notmatch "(?m)^FIELD_NOTHING=OK\s*$") { throw "Uninitialized class field object reference was not Nothing." }

Write-Host "CLASS_REFERENCE_DEFAULT_FOCUSED=OK"
exit 0
