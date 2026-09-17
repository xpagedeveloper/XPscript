param(
  [ValidateSet('all','language','notes','runtime','platform')]
  [string] $Suite = 'all'
)

$ErrorActionPreference = 'Stop'
$compilerDll = (Resolve-Path './src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll').Path
New-Item -ItemType Directory -Force -Path ./out/fulltest | Out-Null
$runtimeTimeoutMilliseconds = 50000
$compileTimeoutMilliseconds = if ($IsWindows) { 120000 } else { 60000 }

function Invoke-Bounded([string] $fileName, [string[]] $arguments, [int] $timeoutMilliseconds, [string] $label) {
  $si = [System.Diagnostics.ProcessStartInfo]::new(); $si.FileName = $fileName; $si.UseShellExecute = $false; $si.RedirectStandardOutput = $true; $si.RedirectStandardError = $true
  foreach ($argument in $arguments) { [void] $si.ArgumentList.Add([string] $argument) }
  $p = [System.Diagnostics.Process]::new(); $p.StartInfo = $si
  try {
    if (-not $p.Start()) { throw "Unable to start: $label" }
    $stdoutTask = $p.StandardOutput.ReadToEndAsync(); $stderrTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutMilliseconds)) { Write-Error "FULLTEST_TIMEOUT: $label (pid=$($p.Id))"; try { $p.Kill($true) } catch { Write-Warning "Failed to kill ${label}: $_" }; [void] $p.WaitForExit(10000); exit 124 }
    $stdout = $stdoutTask.GetAwaiter().GetResult(); $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($stdout) { Write-Host $stdout.TrimEnd() }; if ($stderr) { Write-Host $stderr.TrimEnd() }
    return [pscustomobject]@{ ExitCode = $p.ExitCode; Output = $stdout + $stderr }
  } finally { $p.Dispose() }
}
function Compile-Xps([string] $source, [string] $name) { Write-Host "FULLTEST_COMPILE=$name"; $r = Invoke-Bounded 'dotnet' @($compilerDll,$source,'-o',"./out/fulltest/$name",'--runtime=false') $compileTimeoutMilliseconds "compile $name"; if ($r.ExitCode -ne 0) { exit $r.ExitCode } }
function Get-XpsExe([string] $name) { $plain = "./out/fulltest/$name"; $win = "$plain.exe"; if (Test-Path $win -PathType Leaf) { return (Resolve-Path $win).Path }; if (Test-Path $plain -PathType Leaf) { return (Resolve-Path $plain).Path }; throw "Executable not found: $name" }
function Run-Xps([string] $source, [string] $name, [string[]] $arguments = @()) { Compile-Xps $source $name; Write-Host "FULLTEST_RUN=$name"; $r = Invoke-Bounded (Get-XpsExe $name) $arguments $runtimeTimeoutMilliseconds "run $name"; if ($r.ExitCode -ne 0) { exit $r.ExitCode }; return $r }
function Expect-XpsFailure([string] $name, [string[]] $arguments, [string] $label) { $r = Invoke-Bounded (Get-XpsExe $name) $arguments $runtimeTimeoutMilliseconds "security $label"; if ($r.ExitCode -eq 0) { throw "Security probe unexpectedly succeeded: $label" }; if ([string]::IsNullOrWhiteSpace($r.Output)) { throw "Security probe returned no diagnostic: $label" }; if ($r.Output -match 'SharpCompress') { throw "Security diagnostic exposed implementation detail: $label" } }
function Should-Run([string] $name) { return $Suite -eq 'all' -or $Suite -eq $name }

Write-Host "FULLTEST_SUITE=$Suite"

if (Should-Run 'language') {
  Write-Host '=== LANGUAGE FULLTEST ==='
  Run-Xps ./samples/array-sort-regression.xps array-sort-regression | Out-Null
  Run-Xps ./samples/statement-layout-audit.xps statement-layout-audit | Out-Null
  Write-Host 'LANGUAGE_FULLTEST: passed'
}

if (Should-Run 'notes') {
  Write-Host '=== NOTES FULLTEST ==='
  foreach ($sample in @('notes-full-domino-runtime-test','notes-mail-surface','notes-mail-no-mime-surface')) { Compile-Xps "./samples/$sample.xps" $sample }
  $r = Invoke-Bounded 'dotnet' @($compilerDll,'./samples/notes-document-send-surface.xps','-o','./out/fulltest/notes-send','--runtime=false') $compileTimeoutMilliseconds 'NotesDocument.Send diagnostics'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  if ($r.Output -notmatch 'warning: NotesDocument\.Send attachForm=True is not supported') { throw 'Expected NotesDocument.Send attachForm compiler warning was not emitted.' }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/NotesRichTextDiagnostic/NotesRichTextDiagnostic.csproj','-c','Release','--','./samples/notes-database-query-access-runtime-test.xps','./out/fulltest/notes-query-access') $compileTimeoutMilliseconds 'Notes QueryAccess surface'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  if (-not $IsWindows -and $env:RUNNER_OS -eq 'Linux') { $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/NotesFullRuntimeSurfaceAudit/NotesFullRuntimeSurfaceAudit.csproj','-c','Release','--','NotesSession','NotesDocument','NotesDatabase','NotesDatabaseAccess') $compileTimeoutMilliseconds 'Notes surface audit'; if ($r.ExitCode -ne 0) { exit $r.ExitCode } }
  Write-Host 'NOTES_FULLTEST: passed'
}

if (Should-Run 'runtime') {
  Write-Host '=== XP RUNTIME FULLTEST ==='
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/SpreadsheetCapabilityProbe/SpreadsheetCapabilityProbe.csproj','-c','Release') $compileTimeoutMilliseconds 'Spreadsheet compiler probes'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  foreach ($sample in @('xpspreadsheet-basic','xpspreadsheet-worksheets','xpspreadsheet-styles','xpspreadsheet-ranges','xpspreadsheet-formatting','xpspreadsheet-autofilter','xpspreadsheet-csv-interop')) { Run-Xps "./demo/spreadsheet/$sample.xps" $sample | Out-Null }
  if (-not $IsWindows) {
    $xlsx = Get-ChildItem -Path . -Filter 'xpspreadsheet-basic.xlsx' -File -Recurse | Select-Object -First 1; if ($null -eq $xlsx) { throw 'XPSpreadsheet round-trip did not create expected XLSX.' }
    Add-Type -AssemblyName System.IO.Compression; $zip = [System.IO.Compression.ZipFile]::OpenRead($xlsx.FullName)
    try { $marker = $zip.GetEntry('docProps/custom.xml'); if ($null -eq $marker) { throw 'XPSpreadsheet workbook marker is missing.' }; $reader = [System.IO.StreamReader]::new($marker.Open()); try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }; if ($text -notmatch 'XPScriptWorkbookVersion' -or $text -notmatch '>2<') { throw 'XPSpreadsheet workbook marker is invalid.' } } finally { $zip.Dispose() }
  }
  Compile-Xps ./demo/spreadsheet/xpspreadsheet-invalid-format.xps xpspreadsheet-invalid-format
  $r = Invoke-Bounded (Get-XpsExe xpspreadsheet-invalid-format) @() $runtimeTimeoutMilliseconds 'unsupported spreadsheet format'; if ($r.ExitCode -eq 0 -or $r.Output -notmatch 'supports only \.xlsx files') { throw 'XPSpreadsheet unsupported-format regression failed.' }
  Run-Xps ./samples/native-csv-regression.xps native-csv-regression | Out-Null
  Run-Xps ./samples/native-xml-dom-regression.xps native-xml-dom-regression | Out-Null
  $jsonSchema = Run-Xps ./samples/xpjsonschema-runtime.xps xpjsonschema-runtime
  if ($jsonSchema.Output -notmatch 'XPJSONSCHEMA-RUNTIME=OK') { throw 'XPJsonSchema runtime regression did not complete.' }
  $r = Run-Xps ./samples/xpai-structured-output.xps xpai-structured
  if ($r.Output -notmatch 'XPAI-STRUCTURED-RUNTIME=OK') { throw 'XPAi structured output runtime regression did not complete.' }
  Write-Host 'XP_RUNTIME_FULLTEST: passed'
}

if (Should-Run 'platform') {
  Write-Host '=== PLATFORM FULLTEST ==='
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/ArchiveCapabilityProbe/ArchiveCapabilityProbe.csproj','-c','Release') $compileTimeoutMilliseconds 'Archive compiler probes'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/ArchiveSecurityFixtures/ArchiveSecurityFixtures.csproj','-c','Release','--','./out/archive-security-fixtures') $compileTimeoutMilliseconds 'Archive security fixtures'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  foreach ($sample in @('archive-zip','archive-memory','archive-iterator','archive-edge-cases','archive-security-fixtures')) { Run-Xps "./demo/archive/$sample.xps" $sample | Out-Null }
  Compile-Xps ./demo/archive/archive-security-reject-zip.xps archive-security-reject-zip
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/traversal.zip','10000','2147483647','1000') 'path traversal'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/absolute-unix.zip','10000','2147483647','1000') 'absolute Unix path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/absolute-windows.zip','10000','2147483647','1000') 'absolute Windows path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/unc.zip','10000','2147483647','1000') 'UNC path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/symlink-entry.zip','10000','2147483647','1000') 'symbolic link entry'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/max-entries.zip','2','2147483647','1000') 'MaxEntries'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/max-size.zip','10000','100','1000') 'MaxExtractSize'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/compression-ratio.zip','10000','2147483647','10') 'MaxCompressionRatio'
  Compile-Xps ./demo/archive/archive-security-reject-extended.xps archive-security-reject-extended
  Expect-XpsFailure archive-security-reject-extended @('../../out/archive-security-fixtures/traversal.tar') 'extended TAR path traversal'
  Expect-XpsFailure archive-security-reject-extended @('../../out/archive-security-fixtures/absolute.tar') 'extended TAR absolute path'
  Expect-XpsFailure archive-security-reject-extended @('../../out/archive-security-fixtures/symlink-entry.tar') 'extended TAR symbolic link entry'
  if (Test-Path './out/archive-security-fixtures/extraction-symlink-ready.txt') { Compile-Xps ./demo/archive/archive-security-reject-extract.xps archive-security-reject-extract; Expect-XpsFailure archive-security-reject-extract @() 'destination symbolic link extraction'; if (Test-Path './out/archive-security-fixtures/outside/escape.txt') { throw 'Archive extraction escaped through destination symbolic link' } }
  foreach ($sample in @('archive-extended-memory','archive-extended-memory-write','archive-gzip','archive-rebuild','archive-targzip','archive-compressed-tar','archive-format-detection')) { Run-Xps "./demo/archive/$sample.xps" $sample | Out-Null }
  Run-Xps ./samples/application-runtime.xps application-runtime @('fulltest') | Out-Null
  foreach ($sample in @('networktools-native-local-smoke','networktools-phase2-local-smoke')) { Run-Xps "./samples/$sample.xps" $sample | Out-Null }
  Run-Xps ./samples/systeminventory-local-smoke.xps systeminventory-local-smoke | Out-Null
  Write-Host 'PLATFORM_FULLTEST: passed'
}

if ($Suite -eq 'all') { Write-Host 'ALL_FULLTESTS: passed' } else { Write-Host "FULLTEST_SUITE_PASSED=$Suite" }
