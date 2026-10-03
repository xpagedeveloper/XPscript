$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-delete"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-inherited-delete.xps"
$output = Join-Path $outRoot "class-inherited-delete"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-inherited-delete.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-inherited-delete.xps" }
$joined = ($text -join [Environment]::NewLine)

$childCount = ([regex]::Matches($joined, "(?m)^CHILD_DELETE\s*$")).Count
$baseCount = ([regex]::Matches($joined, "(?m)^BASE_DELETE\s*$")).Count

if ($childCount -ne 1) { throw "Child Delete executed $childCount times; expected exactly once." }
if ($baseCount -ne 1) { throw "Base Delete executed $baseCount times; expected exactly once." }
if ($joined -notmatch "CHILD_DELETE[\s\S]*BASE_DELETE") { throw "Inherited Delete order must be child then base." }

Write-Host "CLASS_DELETE_FOCUSED=OK"
exit 0
