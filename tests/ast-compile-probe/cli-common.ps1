function Get-AstCliContext {
    param(
        [Parameter(Mandatory = $true)][string]$ProbeRoot,
        [Parameter(Mandatory = $true)][string]$ProbeScriptRoot,
        [string]$CompilerPath,
        [string]$OutputRoot
    )

    $root = (Resolve-Path (Join-Path $ProbeScriptRoot '../..')).Path
    $compiler = if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
        Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
    } else {
        [System.IO.Path]::GetFullPath($CompilerPath)
    }
    if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler DLL not found: $compiler" }

    [pscustomobject]@{
        Root = $root
        Compiler = $compiler
        Source = Join-Path $ProbeScriptRoot 'cli-main.xps'
        OutputRoot = if ($OutputRoot) { $OutputRoot } else { Join-Path $root 'out' }
        TemporaryRoot = $env:XPSCRIPT_TEMP_ROOT
    }
}
