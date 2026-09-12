$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$legacy = @(
    '--framework' + '-dependent',
    '--runtime ' + 'RID',
    '--runtime ' + 'win-x64',
    '--runtime ' + 'win-arm64',
    '--runtime ' + 'linux-x64',
    '--runtime ' + 'linux-arm64',
    '--runtime ' + 'osx-x64',
    '--runtime ' + 'osx-arm64'
)
$extensions = @('.md','.cs','.ps1','.yml','.yaml','.html','.txt')
$failures = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $extensions -contains $_.Extension.ToLowerInvariant() -and
    $_.FullName -notmatch '[\\/](\.git|node_modules|bin|obj)[\\/]' -and
    $_.FullName -notlike '*validate-no-legacy-cli-options.ps1'
} | ForEach-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($pattern in $legacy) {
        if ($content.Contains($pattern, [StringComparison]::OrdinalIgnoreCase)) {
            $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
            $failures.Add("$relative contains removed syntax: $pattern")
        }
    }
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "ERROR: $_" }
    throw "Removed legacy CLI syntax remains in $($failures.Count) location(s)."
}
Write-Host 'LEGACY-CLI-SYNTAX=NONE'
