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

function Invoke-BoundedDotnetRun([string[]] $arguments, [int] $timeoutMilliseconds = 120000) {
    $info = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    if ($null -ne $info.ArgumentList) {
        foreach ($argument in $arguments) { [void]$info.ArgumentList.Add([string]$argument) }
    } else {
        $info.Arguments = (($arguments | ForEach-Object {
            $argument = [string]$_
            if ($argument -match '[\s"]') { '"' + $argument.Replace('\', '\\').Replace('"', '\"') + '"' } else { $argument }
        }) -join ' ')
    }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not $process.HasExited -and $stopwatch.ElapsedMilliseconds -lt $timeoutMilliseconds) {
            Start-Sleep -Milliseconds 100
        }
        if (-not $process.HasExited) {
            $process.Kill($true)
            [void]$process.WaitForExit(5000)
            $stdoutReady = $stdoutTask.Wait(5000)
            $stderrReady = $stderrTask.Wait(5000)
            return @{ TimedOut = $true; ExitCode = $null; Output = $(if ($stdoutReady) { $stdoutTask.Result } else { '' }) + $(if ($stderrReady) { $stderrTask.Result } else { '' }) }
        }
        $stdoutReady = $stdoutTask.Wait(5000)
        $stderrReady = $stderrTask.Wait(5000)
        return @{ TimedOut = $false; ExitCode = $process.ExitCode; Output = $(if ($stdoutReady) { $stdoutTask.Result } else { '' }) + $(if ($stderrReady) { $stderrTask.Result } else { '' }) }
    } finally {
        $process.Dispose()
    }
}
