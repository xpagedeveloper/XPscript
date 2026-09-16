$ErrorActionPreference = 'Stop'
$compilerDll = (Resolve-Path './src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll').Path
New-Item -ItemType Directory -Force -Path ./out/fulltest | Out-Null
$output = './out/fulltest/xpjsonschema-runtime'
$exe = if ($IsWindows) { "$output.exe" } else { $output }

function Invoke-Bounded([string] $fileName, [string[]] $arguments, [int] $timeoutMilliseconds, [string] $label) {
  $si = [System.Diagnostics.ProcessStartInfo]::new()
  $si.FileName = $fileName
  $si.UseShellExecute = $false
  $si.RedirectStandardOutput = $true
  $si.RedirectStandardError = $true
  foreach ($argument in $arguments) { [void] $si.ArgumentList.Add([string] $argument) }
  $p = [System.Diagnostics.Process]::new(); $p.StartInfo = $si
  try {
    if (-not $p.Start()) { throw "Unable to start: $label" }
    $stdoutTask = $p.StandardOutput.ReadToEndAsync(); $stderrTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutMilliseconds)) {
      try { $p.Kill($true) } catch { Write-Warning "Failed to kill ${label}: $_" }
      [void] $p.WaitForExit(10000)
      throw "XPJSONSCHEMA_FULLTEST_TIMEOUT: $label"
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult(); $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($stdout) { Write-Host $stdout.TrimEnd() }
    if ($stderr) { Write-Host $stderr.TrimEnd() }
    if ($p.ExitCode -ne 0) { throw "$label failed with exit code $($p.ExitCode)" }
    return $stdout + $stderr
  } finally { $p.Dispose() }
}

Invoke-Bounded 'dotnet' @($compilerDll,'./samples/xpjsonschema-runtime.xps','-o',$output,'--runtime=false') 120000 'compile XPJsonSchema runtime' | Out-Null
$result = Invoke-Bounded (Resolve-Path $exe).Path @() 50000 'run XPJsonSchema runtime'
if ($result -notmatch 'XPJSONSCHEMA_RUNTIME: passed') { throw 'XPJsonSchema runtime did not report success.' }
Write-Host 'XPJSONSCHEMA_FULLTEST: passed'
