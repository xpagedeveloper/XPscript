param(
    [string]$AvdName = "",
    [string]$ApkPath = "",
    [int]$BootTimeoutSeconds = 180,
    [int]$LogTimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

function Resolve-AndroidTool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME, "$env:LOCALAPPDATA\Android\Sdk") |
        Where-Object { $_ -and (Test-Path $_) } |
        Select-Object -Unique

    foreach ($root in $roots) {
        $candidates = switch ($Name) {
            "adb" { @("$root\platform-tools\adb.exe") }
            "emulator" { @("$root\emulator\emulator.exe") }
            default { @() }
        }
        foreach ($candidate in $candidates) {
            if (Test-Path $candidate) { return $candidate }
        }
    }
    throw "Unable to locate Android tool '$Name'. Set ANDROID_SDK_ROOT/ANDROID_HOME or install Android Studio SDK tools."
}

$adb = Resolve-AndroidTool "adb"

$ready = @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '^emulator-\d+\s+device$' })
$emulator = $null

if ($ready.Count -eq 0) {
    $emulator = Resolve-AndroidTool "emulator"
    if (-not $AvdName) {
        $avds = @(& $emulator -list-avds | Where-Object { $_.Trim() })
        if ($avds.Count -eq 0) { throw "No Android Virtual Device is configured." }
        if ($avds.Count -gt 1) {
            throw "Multiple AVDs found. Specify -AvdName. Available: $($avds -join ', ')"
        }
        $AvdName = $avds[0]
    }

    Write-Host "Starting Android emulator '$AvdName'..."
    Start-Process -FilePath $emulator -ArgumentList @("-avd", $AvdName, "-no-boot-anim") | Out-Null
}

& $adb wait-for-device
$deadline = (Get-Date).AddSeconds($BootTimeoutSeconds)
do {
    $booted = (& $adb shell getprop sys.boot_completed 2>$null).Trim()
    if ($booted -eq "1") { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)
if ($booted -ne "1") { throw "Android emulator did not finish booting within $BootTimeoutSeconds seconds." }

if (-not $ApkPath) {
    $candidate = Get-ChildItem ./out -Recurse -Filter *.apk -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'emulator|x64' } |
        Select-Object -First 1
    if (-not $candidate) { throw "No x64 emulator APK found under ./out. Pass -ApkPath explicitly." }
    $ApkPath = $candidate.FullName
}
$ApkPath = (Resolve-Path $ApkPath).Path

Write-Host "Installing $ApkPath"
& $adb install -r $ApkPath
if ($LASTEXITCODE -ne 0) { throw "adb install failed with exit code $LASTEXITCODE." }

& $adb logcat -c
& $adb shell monkey -p com.xpscript.debugapp -c android.intent.category.LAUNCHER 1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Android application launch failed with exit code $LASTEXITCODE." }

$deadline = (Get-Date).AddSeconds($LogTimeoutSeconds)
$matched = $false
do {
    $log = (& $adb logcat -d -s "XPScript:I" "*:S" 2>&1) -join [Environment]::NewLine
    if ($log.Contains("Hello from XPScript on Android")) {
        $matched = $true
        break
    }
    Start-Sleep -Seconds 1
} while ((Get-Date) -lt $deadline)

Write-Host $log
if (-not $matched) { throw "Expected XPScript Android log output was not observed." }
Write-Host "Android emulator smoke test passed."
