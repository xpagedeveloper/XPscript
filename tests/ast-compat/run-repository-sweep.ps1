$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$harness = Join-Path $PSScriptRoot "HotCompilerSweep/HotCompilerSweep.csproj"

Write-Host "AST_REPOSITORY_HOT_COMPILER=CompilerDriver"
& dotnet run --project $harness -c Release -- $root
if ($LASTEXITCODE -ne 0) {
    throw "Hot compiler repository sweep failed with exit code $LASTEXITCODE."
}
