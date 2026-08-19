param(
    [ValidateSet('all','compiler','desktop-runtime','cgi','fastcgi','kestrel')]
    [string]$Package = 'all',
    [string]$Configuration = 'Release',
    [string]$Runtime = '',
    [switch]$SelfContained,
    [string]$OutputRoot = 'artifacts/distributions'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$OutputRoot = Join-Path $repoRoot $OutputRoot

function Publish-Project {
    param(
        [string]$Name,
        [string]$Project,
        [string]$SubFolder,
        [string[]]$ExtraArgs = @()
    )

    $output = Join-Path $OutputRoot $SubFolder
    if (Test-Path $output) { Remove-Item $output -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $output | Out-Null

    $args = @('publish', (Join-Path $repoRoot $Project), '-c', $Configuration, '-o', $output, '--nologo')
    if ($Runtime) { $args += @('-r', $Runtime) }
    if ($SelfContained) { $args += @('--self-contained', 'true') }
    elseif ($Runtime) { $args += @('--self-contained', 'false') }
    $args += $ExtraArgs

    Write-Host "Publishing $Name -> $output"
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Publishing $Name failed with exit code $LASTEXITCODE." }

    if (-not (Get-ChildItem -Path $output -File -ErrorAction SilentlyContinue)) {
        throw "Publishing $Name produced no files."
    }
}

$targets = @{
    'compiler' = @{ Name = 'XPscript Compiler'; Project = 'src/XPScript.Compiler/XPScript.Compiler.csproj'; Folder = 'compiler' }
    'desktop-runtime' = @{ Name = 'XPscript Desktop Runtime'; Project = 'src/XPScript.UI.Desktop/XPScript.UI.Desktop.csproj'; Folder = 'desktop-runtime' }
    'cgi' = @{ Name = 'XPscript CGI Host'; Project = 'src/XPScript.Web.Cgi/XPScript.Web.Cgi.csproj'; Folder = 'cgi' }
    'fastcgi' = @{ Name = 'XPscript FastCGI Host Bundle'; Project = 'src/XPScript.Cli/XPScript.Cli.csproj'; Folder = 'fastcgi' }
    'kestrel' = @{ Name = 'XPscript Kestrel Host Bundle'; Project = 'src/XPScript.Cli/XPScript.Cli.csproj'; Folder = 'kestrel' }
}

$selected = if ($Package -eq 'all') { @('compiler','desktop-runtime','cgi','fastcgi','kestrel') } else { @($Package) }
foreach ($key in $selected) {
    $target = $targets[$key]
    Publish-Project -Name $target.Name -Project $target.Project -SubFolder $target.Folder
}

Write-Host "Distribution output: $OutputRoot"
