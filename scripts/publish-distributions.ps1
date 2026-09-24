param(
    [string]$Configuration = 'Release',
    [string]$Runtime = '',
    [switch]$SelfContained,
    [string]$OutputRoot = 'publish/xpscript'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repoRoot $OutputRoot
$project = Join-Path $repoRoot 'src/XPScript.Cli/XPScript.Cli.csproj'

Write-Host "Checking .NET WebAssembly build workload..."
$workloadList = (& dotnet workload list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to query installed .NET workloads."
}
if ($workloadList -notmatch '(?m)^\s*wasm-tools\s') {
    Write-Host "wasm-tools is not installed. Installing it for browser-wasm builds..."
    & dotnet workload install wasm-tools
    if ($LASTEXITCODE -ne 0) {
        throw "Installing the wasm-tools workload failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host "wasm-tools is already installed."
}

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $output | Out-Null

$args = @('publish', $project, '-c', $Configuration, '-o', $output, '--nologo', '-p:SkipUnifiedPublish=true')
if ($Runtime) {
    $args += @('-r', $Runtime)
}
if ($SelfContained) {
    $args += @('--self-contained', 'true')
}
elseif ($Runtime) {
    $args += @('--self-contained', 'false')
}

Write-Host "Publishing XPscript toolchain -> $output"
& dotnet @args
if ($LASTEXITCODE -ne 0) {
    throw "Publishing XPscript toolchain failed with exit code $LASTEXITCODE."
}

$entryPoint = Join-Path $output $(if ($IsWindows) { 'xpscript.exe' } else { 'xpscript' })
if (-not (Test-Path $entryPoint)) {
    $dllEntryPoint = Join-Path $output 'xpscript.dll'
    if (-not (Test-Path $dllEntryPoint)) {
        throw "Publishing XPscript toolchain did not produce xpscript entrypoint files."
    }
}

Write-Host "XPscript publish output: $output"
