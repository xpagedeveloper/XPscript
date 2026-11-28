$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-new"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-new-invokes-constructor.xps"
$output = Join-Path $outRoot "class-new-invokes-constructor"

& dotnet run --project $compiler -c Release --no-build -- $source -o $output --runtime=false
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: class-new-invokes-constructor.xps" }

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-new-invokes-constructor.xps" }
$joined = ($text -join [Environment]::NewLine)

$constructorCount = ([regex]::Matches($joined, "(?m)^CTOR=(?:first|second)\s*$")).Count
if ($constructorCount -ne 2) { throw "Sub New executed $constructorCount times; expected exactly twice." }
if (([regex]::Matches($joined, "(?m)^CTOR=first\s*$")).Count -ne 1) { throw "First New did not invoke its constructor exactly once." }
if (([regex]::Matches($joined, "(?m)^CTOR=second\s*$")).Count -ne 1) { throw "Second New did not invoke its constructor exactly once." }
if ($joined -notmatch "(?m)^FIRST=first\s*$") { throw "First object constructor state was not preserved." }
if ($joined -notmatch "(?m)^SECOND=second\s*$") { throw "Second object constructor state was not preserved." }

Write-Host "CLASS_NEW_FOCUSED=OK"
exit 0
