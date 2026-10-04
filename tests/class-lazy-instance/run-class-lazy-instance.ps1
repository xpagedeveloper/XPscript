$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-lazy-instance"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-lazy-instance.xps"
$output = Join-Path $outRoot "class-lazy-instance"

& dotnet run --project $compiler -c Release -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-lazy-instance.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-lazy-instance.xps" }
$joined = ($text -join [Environment]::NewLine)
Write-Host $joined

if ($joined -notmatch "(?m)^BEFORE=0\s*$") { throw "Lazy instance was constructed before first access." }
if ($joined -notmatch "(?m)^AFTER-FIRST=1\s*$") { throw "First access did not construct exactly one instance." }
if ($joined -notmatch "(?m)^AFTER-SECOND=1\s*$") { throw "Second access constructed another instance." }
if ($joined -notmatch "(?m)^FIRST=ready\s*$") { throw "First lazy instance is invalid." }
if ($joined -notmatch "(?m)^SECOND=ready\s*$") { throw "Reused lazy instance is invalid." }

Write-Host "CLASS_LAZY_INSTANCE_FOCUSED=OK"
exit 0
