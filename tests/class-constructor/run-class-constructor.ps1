$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-constructor"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-inherited-constructor.xps"
$output = Join-Path $outRoot "class-inherited-constructor"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-inherited-constructor.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-inherited-constructor.xps" }
$joined = ($text -join [Environment]::NewLine)

if ($joined -notmatch "BASE_CTOR=base:ok") { throw "Base constructor was not invoked." }
if ($joined -notmatch "CHILD_CTOR=child:ok") { throw "Child constructor was not invoked." }
if ($joined -notmatch "BASE_CTOR=base:ok[\s\S]*CHILD_CTOR=child:ok") { throw "Base constructor did not execute before child constructor body." }

Write-Host "CLASS_CONSTRUCTOR_FOCUSED=OK"
exit 0
