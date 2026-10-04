$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/XPScript.Compiler.csproj"
$outRoot = Join-Path $root "out/class-fluent"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$source = Join-Path $root "samples/class-fluent-chaining.xps"
$output = Join-Path $outRoot "class-fluent-chaining"

& dotnet run --project $compiler -c Release -- $source -o $output --runtime=false --debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "=== Generated fluent-chain C# diagnostics ==="
    Get-ChildItem -Path $outRoot -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "--- $($_.FullName) ---"
        Select-String -Path $_.FullName -Pattern "Append|CHAIN|CONTINUED" -Context 2,2 | ForEach-Object {
            Write-Host $_.ToString()
        }
    }
    throw "Compilation failed: class-fluent-chaining.xps"
}

$text = & $output 2>&1
if ($LASTEXITCODE -ne 0) { throw "Program failed: class-fluent-chaining.xps" }
$joined = ($text -join [Environment]::NewLine)
Write-Host "=== Fluent runtime output ==="
Write-Host $joined
Write-Host "=== End fluent runtime output ==="

if ($joined -notmatch "(?m)^CHAIN=ABC\s*$") { throw "Single-line fluent chain failed." }
if ($joined -notmatch "(?m)^CONTINUED=ABCDE\s*$") { throw "Line-continued fluent chain failed." }

Write-Host "CLASS_FLUENT_FOCUSED=OK"
exit 0
