param(
    [Parameter(Mandatory = $true)]
    [string]$Serial,
    [string]$Platform = "android-arm64",
    [string]$XPScript = ".\publish\xpscript\win-x64\xpscript.exe"
)

$ErrorActionPreference = "Stop"

$XPScript = [System.IO.Path]::GetFullPath($XPScript)
if (-not (Test-Path -LiteralPath $XPScript -PathType Leaf)) {
    throw "XPScript CLI was not found at $XPScript. Build/publish the CLI or pass -XPScript <path>."
}

if ([string]::IsNullOrWhiteSpace($Serial)) {
    throw "A physical Android device serial is required."
}

$runArgs = @(
    "android", "run",
    "samples/android-debug-print.xps",
    "--device", "physical",
    "--serial", $Serial,
    "--platform", $Platform,
    "--debug"
)

$output = & $XPScript @runArgs 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "Android physical-device smoke test failed. $output"
}

foreach ($expected in @(
    "Hello from XPScript on Android",
    "PLATFORM=Android",
    "XPSCRIPT-EXIT=0"
)) {
    if ($output -notmatch [regex]::Escape($expected)) {
        throw "Expected Android log output was not found: $expected. $output"
    }
}

Write-Host "ANDROID-PHYSICAL-DEVICE-INTEGRATION=OK"
