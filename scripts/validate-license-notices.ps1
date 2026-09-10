$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$noticePath = Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md'
if (-not (Test-Path -LiteralPath $noticePath)) {
    throw "Missing THIRD-PARTY-NOTICES.md."
}

$notice = Get-Content -LiteralPath $noticePath -Raw
$packageNames = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter '*.csproj' |
    ForEach-Object {
        [xml]$project = Get-Content -LiteralPath $_.FullName -Raw
        $project.Project.ItemGroup.PackageReference |
            ForEach-Object { [string]$_.Include }
    } |
    Where-Object { $_ } |
    Sort-Object -Unique

$missing = @($packageNames | Where-Object {
    $notice.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0
})

if ($missing.Count -gt 0) {
    throw "The following direct NuGet packages are missing from THIRD-PARTY-NOTICES.md: " + ($missing -join ', ')
}

Write-Host ("License notice validation passed for {0} direct NuGet packages." -f $packageNames.Count)

$assetsFiles = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter 'project.assets.json'
if ($assetsFiles.Count -eq 0) {
    Write-Warning 'No restored project assets were found. Package license metadata was not checked.'
    exit 0
}

$globalPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
$packages = @{}
foreach ($assetsFile in $assetsFiles) {
    $assets = Get-Content -LiteralPath $assetsFile.FullName -Raw | ConvertFrom-Json
    foreach ($libraryProperty in $assets.libraries.psobject.Properties) {
        if ([string]$libraryProperty.Value.type -ne 'package') { continue }
        $library = $libraryProperty.Name
        $parts = $library -split '/', 2
        if ($parts.Count -eq 2 -and $parts[1] -ne '0.0.0') { $packages[$library] = $true }
    }
}

$licenseProblems = @()
foreach ($package in $packages.Keys) {
    $parts = $package -split '/', 2
    $packageDirectory = Join-Path (Join-Path $globalPackages $parts[0].ToLowerInvariant()) $parts[1]
    $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nuspec) {
        $licenseProblems += "$package has no NuGet metadata file"
        continue
    }

    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
    $license = $metadata.package.metadata.license
    $licenseText = if ($license) { [string]$license.InnerText } else { '' }
    $licenseType = if ($license) { [string]$license.type } else { '' }
    if ([string]::IsNullOrWhiteSpace($licenseText)) {
        $licenseProblems += "$package has no declared license"
        continue
    }

    $licenseValue = "$licenseType $licenseText".ToLowerInvariant()
    if ($licenseValue -match 'non.?commercial|noassertion|evaluation.?only|field.?of.?use|not.?for.?commercial') {
        $licenseProblems += "$package declares an incompatible or unclear commercial-use license: $licenseText"
    }
}

if ($licenseProblems.Count -gt 0) {
    throw "NuGet license validation failed:`n" + ($licenseProblems -join "`n")
}

Write-Host ("NuGet license metadata validation passed for {0} resolved packages." -f $packages.Count)
