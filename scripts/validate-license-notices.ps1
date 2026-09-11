$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$noticePath = Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md'
$catalogPath = Join-Path $repositoryRoot 'src/XPScript.Compiler/ApplicationDependencyCatalog.cs'

if (-not (Test-Path -LiteralPath $noticePath)) {
    throw "Missing THIRD-PARTY-NOTICES.md."
}
if (-not (Test-Path -LiteralPath $catalogPath)) {
    throw "Missing ApplicationDependencyCatalog.cs. Generated application dependencies cannot be validated."
}

$notice = Get-Content -LiteralPath $noticePath -Raw

# Licenses in this list have been reviewed for XPScript's distribution model.
# Adding a new identifier is an explicit legal/compliance decision, not merely a build fix.
$approvedLicenseIdentifiers = @(
    'Apache-2.0',
    'BSD-2-Clause',
    'BSD-3-Clause',
    'ISC',
    'MIT',
    'MS-PL',
    'PostgreSQL',
    'Unicode-3.0',
    'Unicode-DFS-2016',
    'Zlib'
)
$approvedLicenseSet = @{}
foreach ($identifier in $approvedLicenseIdentifiers) {
    $approvedLicenseSet[$identifier.ToLowerInvariant()] = $true
}

# NuGet packages that declare their license using a package file rather than an SPDX expression.
# Each entry must be reviewed independently before it is added here.
$approvedLicenseFilePackages = @{
    'avalonia.angle.windows.natives' = @('LICENSE', 'LICENSE.txt')
    'microsoft.data.sqlclient.sni.runtime' = @('LICENSE', 'LICENSE.txt')
}

function Get-StaticPackageNames {
    return @(
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter '*.csproj' |
            ForEach-Object {
                [xml]$project = Get-Content -LiteralPath $_.FullName -Raw
                $project.Project.ItemGroup.PackageReference |
                    ForEach-Object { [string]$_.Include }
            } |
            Where-Object { $_ } |
            Sort-Object -Unique
    )
}

function Get-GeneratedPackageReferences {
    $catalog = Get-Content -LiteralPath $catalogPath -Raw
    $versionConstants = @{}

    foreach ($match in [regex]::Matches($catalog, 'public\s+const\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"(?<value>[^"]+)"\s*;')) {
        $versionConstants[$match.Groups['name'].Value] = $match.Groups['value'].Value
    }

    $defaultsMatch = [regex]::Match(
        $catalog,
        'public\s+static\s+IReadOnlyList<ApplicationPackageReference>\s+Defaults\s*\{\s*get;\s*\}\s*=\s*\[(?<body>.*?)\]\s*;',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $defaultsMatch.Success) {
        throw 'Could not parse ApplicationDependencyCatalog.Defaults. Update the license validator when changing the catalog structure.'
    }

    $references = @()
    foreach ($match in [regex]::Matches($defaultsMatch.Groups['body'].Value, 'new\("(?<name>[^"]+)",\s*(?<version>"[^"]+"|[A-Za-z_][A-Za-z0-9_]*)\s*,')) {
        $packageName = $match.Groups['name'].Value
        $versionToken = $match.Groups['version'].Value
        if ($versionToken.StartsWith('"')) {
            $version = $versionToken.Trim('"')
        }
        elseif ($versionConstants.ContainsKey($versionToken)) {
            $version = [string]$versionConstants[$versionToken]
        }
        else {
            throw "Generated package '$packageName' uses unresolved version token '$versionToken'."
        }

        $references += [pscustomobject]@{
            Name = $packageName
            Version = $version
        }
    }

    if ($references.Count -eq 0) {
        throw 'ApplicationDependencyCatalog.Defaults did not contain any package references.'
    }

    $duplicates = @($references | Group-Object Name | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) {
        throw "ApplicationDependencyCatalog.Defaults contains duplicate package names: " + (($duplicates.Name | Sort-Object) -join ', ')
    }

    return $references
}

function Add-PackagesFromAssets {
    param(
        [Parameter(Mandatory = $true)][string]$AssetsPath,
        [Parameter(Mandatory = $true)][hashtable]$PackageTable
    )

    if (-not (Test-Path -LiteralPath $AssetsPath)) {
        throw "Expected NuGet assets file was not found: $AssetsPath"
    }

    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
    foreach ($libraryProperty in $assets.libraries.psobject.Properties) {
        if ([string]$libraryProperty.Value.type -ne 'package') { continue }
        $library = $libraryProperty.Name
        $parts = $library -split '/', 2
        if ($parts.Count -eq 2 -and $parts[1] -ne '0.0.0') {
            $PackageTable[$library] = $true
        }
    }
}

function Test-LicenseExpression {
    param([Parameter(Mandatory = $true)][string]$Expression)

    $tokens = [regex]::Matches($Expression, '[A-Za-z0-9][A-Za-z0-9.\-+]*') |
        ForEach-Object { $_.Value }

    if ($tokens.Count -eq 0) {
        return @("license expression '$Expression' contains no recognizable SPDX identifiers")
    }

    $problems = @()
    foreach ($token in $tokens) {
        if ($token -in @('AND', 'OR', 'WITH')) { continue }
        if (-not $approvedLicenseSet.ContainsKey($token.ToLowerInvariant())) {
            $problems += "unapproved license identifier '$token' in expression '$Expression'"
        }
    }
    return $problems
}

$staticPackageNames = Get-StaticPackageNames
$generatedPackageReferences = Get-GeneratedPackageReferences
$generatedPackageNames = @($generatedPackageReferences | ForEach-Object Name)
$directPackageNames = @($staticPackageNames + $generatedPackageNames | Sort-Object -Unique)

$missing = @($directPackageNames | Where-Object {
    $notice.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0
})
if ($missing.Count -gt 0) {
    throw "The following XPScript or generated-application NuGet packages are missing from THIRD-PARTY-NOTICES.md: " + ($missing -join ', ')
}

Write-Host ("License notice validation passed for {0} direct NuGet packages, including {1} generated-application packages." -f $directPackageNames.Count, $generatedPackageNames.Count)

$packages = @{}
$assetsFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -Filter 'project.assets.json')
if ($assetsFiles.Count -eq 0) {
    throw 'No restored project assets were found. Run dotnet restore before license validation.'
}
foreach ($assetsFile in $assetsFiles) {
    Add-PackagesFromAssets -AssetsPath $assetsFile.FullName -PackageTable $packages
}

# Restore every package that XPScript may add to a generated application. This makes
# generated-only dependencies and all of their transitive dependencies visible to CI.
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("xpscript-license-validation-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    $generatedProjectPath = Join-Path $tempRoot 'GeneratedDependencies.csproj'
    $packageReferenceLines = foreach ($reference in $generatedPackageReferences) {
        $name = [System.Security.SecurityElement]::Escape([string]$reference.Name)
        $version = [System.Security.SecurityElement]::Escape([string]$reference.Version)
        "    <PackageReference Include=\"$name\" Version=\"$version\" />"
    }

    $generatedProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
$($packageReferenceLines -join "`n")
  </ItemGroup>
</Project>
"@
    Set-Content -LiteralPath $generatedProjectPath -Value $generatedProject -Encoding utf8

    & dotnet restore $generatedProjectPath --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for generated application dependencies with exit code $LASTEXITCODE."
    }

    Add-PackagesFromAssets -AssetsPath (Join-Path $tempRoot 'obj/project.assets.json') -PackageTable $packages
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$globalPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
$licenseProblems = @()
foreach ($package in ($packages.Keys | Sort-Object)) {
    $parts = $package -split '/', 2
    $packageName = $parts[0]
    $packageDirectory = Join-Path (Join-Path $globalPackages $packageName.ToLowerInvariant()) $parts[1]
    $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nuspec) {
        $licenseProblems += "$package has no NuGet metadata file. Manual license review required."
        continue
    }

    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
    $license = $metadata.package.metadata.license
    $licenseText = if ($license) { [string]$license.InnerText } else { '' }
    $licenseType = if ($license) { [string]$license.type } else { '' }
    if ([string]::IsNullOrWhiteSpace($licenseText)) {
        $licenseProblems += "$package has no declared NuGet license. Manual license review required."
        continue
    }

    if ($licenseType.Equals('expression', [StringComparison]::OrdinalIgnoreCase)) {
        $expressionProblems = @(Test-LicenseExpression -Expression $licenseText)
        foreach ($problem in $expressionProblems) {
            $licenseProblems += "$package declares $problem. Manual license review required."
        }
        continue
    }

    if ($licenseType.Equals('file', [StringComparison]::OrdinalIgnoreCase)) {
        $packageKey = $packageName.ToLowerInvariant()
        if (-not $approvedLicenseFilePackages.ContainsKey($packageKey)) {
            $licenseProblems += "$package uses license file '$licenseText' but the package has not been explicitly approved. Manual license review required."
            continue
        }

        $approvedFiles = @($approvedLicenseFilePackages[$packageKey])
        if ($approvedFiles -notcontains $licenseText) {
            $licenseProblems += "$package uses unexpected license file '$licenseText'. Approved file names: $($approvedFiles -join ', '). Manual license review required."
            continue
        }

        $licenseFilePath = Join-Path $packageDirectory $licenseText
        if (-not (Test-Path -LiteralPath $licenseFilePath)) {
            $licenseProblems += "$package declares license file '$licenseText' but the file is missing from the restored package."
        }
        continue
    }

    $licenseProblems += "$package declares unsupported NuGet license type '$licenseType' with value '$licenseText'. Manual license review required."
}

if ($licenseProblems.Count -gt 0) {
    throw "NuGet license validation failed:`n - " + ($licenseProblems -join "`n - ")
}

Write-Host ("NuGet license policy validation passed for {0} resolved packages." -f $packages.Count)
Write-Host ("Approved SPDX identifiers: {0}" -f ($approvedLicenseIdentifiers -join ', '))
