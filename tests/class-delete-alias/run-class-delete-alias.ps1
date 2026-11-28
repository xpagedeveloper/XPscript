$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-delete-alias"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-delete-alias-semantics.xps"
$output = Join-Path $outRoot "class-delete-alias-semantics"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-delete-alias-semantics.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-delete-alias-semantics.xps" }
$joined = ($text -join [Environment]::NewLine)

if (([regex]::Matches($joined, "(?m)^DELETE_CALLED=one\s*$")).Count -ne 1) { throw "Sub Delete must execute exactly once." }
if ($joined -notmatch "(?m)^FIRST_NOTHING=YES\s*$") { throw "Deleted reference was not cleared." }
if ($joined -notmatch "(?m)^SECOND_NOTHING=YES\s*$") { throw "Aliased reference did not observe deletion." }

Write-Host "CLASS_DELETE_ALIAS_FOCUSED=OK"
exit 0
