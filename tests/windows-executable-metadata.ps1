$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $env:TEMP ('xpscript-metadata-' + [Guid]::NewGuid().ToString('N') + '.xps')
$output = Join-Path $env:TEMP ('xpscript-metadata-' + [Guid]::NewGuid().ToString('N') + '.exe')

@'
Option Declare

Sub Main()
    Application.Executable.FileDescription = "XPScript metadata regression test"
    Application.Executable.Comments = "Custom XPScript comment"
    Application.Executable.Product = "XPScript Metadata Test"
    Application.Executable.Company = "XPageDeveloper"
    Application.Executable.Version = "1.2.3.4"
    Application.Executable.Copyright = "Copyright 2026 XPageDeveloper"
    Print "metadata"
End Sub
'@ | Set-Content -LiteralPath $source -Encoding UTF8

try {
    Push-Location $repoRoot
    try {
        dotnet run --project src/XPScript.Compiler/XPScript.Compiler.csproj -- compile $source -o $output --platform win-x64
        if ($LASTEXITCODE -ne 0) { throw "XPScript compile failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $output)) { throw "Compiler did not produce $output" }

    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($output)
    $expected = [ordered]@{
        FileDescription = 'XPScript metadata regression test'
        Comments = 'Custom XPScript comment'
        CompanyName = 'XPageDeveloper'
        ProductName = 'XPScript Metadata Test'
        FileVersion = '1.2.3.4'
        ProductVersion = '1.2.3.4'
        LegalCopyright = 'Copyright 2026 XPageDeveloper'
    }

    $failures = @()
    foreach ($entry in $expected.GetEnumerator()) {
        $actual = $info.($entry.Key)
        if ($actual -ne $entry.Value) {
            $failures += "$($entry.Key): expected '$($entry.Value)', got '$actual'"
        }
    }

    if ($failures.Count -gt 0) {
        throw "Windows executable metadata regression:`n$($failures -join "`n")"
    }

    Write-Host 'Windows executable metadata regression test passed.'
}
finally {
    Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}
