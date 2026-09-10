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
