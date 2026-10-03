$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-fluent"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-fluent-chaining.xps"
$output = Join-Path $outRoot "class-fluent-chaining"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-fluent-chaining.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-fluent-chaining.xps" }
$joined = ($text -join [Environment]::NewLine)

if ($joined -notmatch "(?m)^CHAIN=ABC\s*$") { throw "Single-line fluent chain failed." }
if ($joined -notmatch "(?m)^CONTINUED=ABCDE\s*$") { throw "Line-continued fluent chain failed." }

Write-Host "CLASS_FLUENT_FOCUSED=OK"
exit 0
