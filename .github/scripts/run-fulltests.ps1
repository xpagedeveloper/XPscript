param(
  [ValidateSet('all','language','notes','runtime','platform','archive')]
  [string] $Suite = 'all'
)

$ErrorActionPreference = 'Stop'
$compilerDll = (Resolve-Path './src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll').Path
New-Item -ItemType Directory -Force -Path ./out/fulltest | Out-Null
$runtimeTimeoutMilliseconds = 50000
$compileTimeoutMilliseconds = if ($IsWindows) { 180000 } else { 60000 }

function Invoke-Bounded([string] $fileName, [string[]] $arguments, [int] $timeoutMilliseconds, [string] $label) {
  $si = [System.Diagnostics.ProcessStartInfo]::new(); $si.FileName = $fileName; $si.UseShellExecute = $false; $si.RedirectStandardOutput = $true; $si.RedirectStandardError = $true
  foreach ($argument in $arguments) { [void] $si.ArgumentList.Add([string] $argument) }
  $p = [System.Diagnostics.Process]::new(); $p.StartInfo = $si
  try {
    $startedAt = [DateTimeOffset]::UtcNow
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host "FULLTEST_PROCESS_START label=$label timeout_ms=$timeoutMilliseconds utc=$($startedAt.ToString('O'))"
    if (-not $p.Start()) { throw "Unable to start: $label" }
    Write-Host "FULLTEST_PROCESS_PID label=$label pid=$($p.Id)"
    $stdoutTask = $p.StandardOutput.ReadToEndAsync(); $stderrTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutMilliseconds)) {
      $timer.Stop()
      Write-Host "FULLTEST_PROCESS_TIMEOUT label=$label pid=$($p.Id) elapsed_ms=$($timer.ElapsedMilliseconds)"
      Write-Error "FULLTEST_TIMEOUT: $label (pid=$($p.Id))"
      try { $p.Kill($true) } catch { Write-Warning "Failed to kill ${label}: $_" }
      [void] $p.WaitForExit(10000)
      exit 124
    }
    $timer.Stop()
    Write-Host "FULLTEST_PROCESS_EXIT label=$label pid=$($p.Id) exit_code=$($p.ExitCode) elapsed_ms=$($timer.ElapsedMilliseconds)"
    $stdout = $stdoutTask.GetAwaiter().GetResult(); $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($stdout) { Write-Host $stdout.TrimEnd() }; if ($stderr) { Write-Host $stderr.TrimEnd() }
    return [pscustomobject]@{ ExitCode = $p.ExitCode; Output = $stdout + $stderr }
  } finally { $p.Dispose() }
}
function Compile-Xps([string] $source, [string] $name) { Write-Host "FULLTEST_COMPILE=$name"; $r = Invoke-Bounded 'dotnet' @($compilerDll,$source,'-o',"./out/fulltest/$name",'--runtime=false') $compileTimeoutMilliseconds "compile $name"; if ($r.ExitCode -ne 0) { if ($name -eq 'archive-compressed-tar') { Write-Host 'FULLTEST_RETRY_DEBUG=archive-compressed-tar'; $debug = Invoke-Bounded 'dotnet' @($compilerDll,$source,'-o',"./out/fulltest/$name-debug",'--runtime=false','--debug') $compileTimeoutMilliseconds "compile debug $name"; Write-Host $debug.Output }; exit $r.ExitCode } }
function Get-XpsExe([string] $name) { $plain = "./out/fulltest/$name"; $win = "$plain.exe"; if (Test-Path $win -PathType Leaf) { return (Resolve-Path $win).Path }; if (Test-Path $plain -PathType Leaf) { return (Resolve-Path $plain).Path }; throw "Executable not found: $name" }
function Run-Xps([string] $source, [string] $name, [string[]] $arguments = @()) { Compile-Xps $source $name; Write-Host "FULLTEST_RUN=$name"; $r = Invoke-Bounded (Get-XpsExe $name) $arguments $runtimeTimeoutMilliseconds "run $name"; if ($r.ExitCode -ne 0) { exit $r.ExitCode }; return $r }
function Expect-XpsFailure([string] $name, [string[]] $arguments, [string] $label) { $r = Invoke-Bounded (Get-XpsExe $name) $arguments $runtimeTimeoutMilliseconds "security $label"; if ($r.ExitCode -eq 0) { throw "Security probe unexpectedly succeeded: $label" }; if ([string]::IsNullOrWhiteSpace($r.Output)) { throw "Security probe returned no diagnostic: $label" }; if ($r.Output -match 'SharpCompress') { throw "Security diagnostic exposed implementation detail: $label" } }
function Should-Run([string] $name) { return $Suite -eq 'all' -or $Suite -eq $name }

Write-Host "FULLTEST_SUITE=$Suite"

if (Should-Run 'language') {
  Write-Host '=== LANGUAGE FULLTEST ==='
  # Keep the focused inheritance contract near the front while class migration is active.
  $inheritance = Run-Xps ./samples/class-inheritance-contract.xps class-inheritance-contract
  if ($inheritance.Output -notmatch 'INHERIT=base:touch\|base:touch\|base:touch:property') { throw 'Class inheritance Me/Parent contract regression failed.' }
  if ($inheritance.Output -notmatch 'CHILD_DELETE[\s\S]*BASE_DELETE') { throw 'Class Delete inheritance order regression failed.' }

  # Keep the smallest regression for the latest compiler failure first.
  $isNothing = Run-Xps ./samples/is-nothing-unary-not-regression.xps is-nothing-unary-not-regression
  if ($isNothing.Output -notmatch 'IS-NOTHING=OK') { throw 'Is Nothing unary-Not rewrite regression failed.' }

  # Keep the smallest regression for the latest runtime/compiler interaction first.
  $functionNotByRef = Run-Xps ./samples/function-not-byref-regression.xps function-not-byref-regression
  if ($functionNotByRef.Output -notmatch 'FUNCTION-NOT-BYREF=OK') { throw 'Boolean default-ByRef function under Not regression failed.' }

  # Keep the smallest regression for the latest compiler failure first.
  $r = Invoke-Bounded 'dotnet' @($compilerDll,'./samples/function-result-name-conflict-error.xps','-o','./out/fulltest/function-result-name-conflict-error','--runtime=false') $compileTimeoutMilliseconds 'function result name conflict'
  if ($r.ExitCode -eq 0) { throw 'Function result name conflict unexpectedly compiled.' }
  if ($r.Output -notmatch 'XPS2014' -or $r.Output -notmatch 'conflicts with the function result name') { throw 'Function result name conflict did not produce XPS2014.' }

  # Keep the actively developed scope isolation regression early so CI surfaces failures immediately.
  $scope = Run-Xps ./samples/scope-isolation.xps scope-isolation
  foreach ($expected in @('LOCAL=40','STATIC=1','STATIC=2','GLOBAL=7','BYREF=4','ARRAY=22:3','LIST=kept:2')) { if ($scope.Output -notmatch [regex]::Escape($expected)) { throw "Scope isolation regression missing: $expected" } }
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
  # Keep the smallest regression for managed stack-trace suppression first.
  Compile-Xps ./samples/runtime-error-stacktrace-regression.xps runtime-error-stacktrace-regression
  $runtimeError = Invoke-Bounded (Get-XpsExe 'runtime-error-stacktrace-regression') @() $runtimeTimeoutMilliseconds 'runtime error stacktrace regression'
  if ($runtimeError.ExitCode -eq 0) { throw 'Runtime error stacktrace regression unexpectedly succeeded.' }
  if ($runtimeError.Output -match ' at Script\.|System\.[A-Za-z].*Exception') { throw 'Runtime error exposed managed C#/.NET stack details without debug.' }
  # Keep the most recently failing regression first so CI surfaces it immediately.
  Write-Host 'FULLTEST_CHECKPOINT=xpspreadsheet-invalid-format-first'
  Compile-Xps ./demo/spreadsheet/xpspreadsheet-invalid-format.xps xpspreadsheet-invalid-format
  $r = Invoke-Bounded (Get-XpsExe xpspreadsheet-invalid-format) @() $runtimeTimeoutMilliseconds 'unsupported spreadsheet format'
  if ($r.ExitCode -eq 0 -or $r.Output -notmatch 'supports only \.xlsx files') { throw 'XPSpreadsheet unsupported-format regression failed.' }
  Write-Host 'FULLTEST_CHECKPOINT=xpspreadsheet-invalid-format-passed'

  # Keep the main-branch spreadsheet styles regression early as well.
  Run-Xps ./demo/spreadsheet/xpspreadsheet-styles.xps xpspreadsheet-styles | Out-Null

  # Run the actively developed JSON Schema regression next.
  $jsonSchema = Run-Xps ./samples/xpjsonschema-runtime.xps xpjsonschema-runtime
  if ($jsonSchema.Output -notmatch 'XPJSONSCHEMA-RUNTIME=OK') { throw 'XPJsonSchema runtime regression did not complete.' }
  $jsonSchemaBoolean = Run-Xps ./samples/xpjsonschema-boolean-runtime.xps xpjsonschema-boolean-runtime
  if ($jsonSchemaBoolean.Output -notmatch 'XPJSONSCHEMA-BOOLEAN-RUNTIME=OK') { throw 'XPJsonSchema boolean-schema regression did not complete.' }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/SpreadsheetCapabilityProbe/SpreadsheetCapabilityProbe.csproj','-c','Release') $compileTimeoutMilliseconds 'Spreadsheet compiler probes'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/SpreadsheetSecurityFixtures/SpreadsheetSecurityFixtures.csproj','-c','Release','--','./out/spreadsheet-security-fixtures') $compileTimeoutMilliseconds 'Spreadsheet security fixtures'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  Compile-Xps ./samples/xpspreadsheet-security-reject.xps xpspreadsheet-security-reject
  foreach ($fixture in @('malformed.xlsx','missing-workbook.xlsx','too-many-parts.xlsx','oversized-part.xlsx')) { Expect-XpsFailure xpspreadsheet-security-reject @("../../out/spreadsheet-security-fixtures/$fixture") "XLSX $fixture" }
  foreach ($sample in @('xpspreadsheet-basic','xpspreadsheet-worksheets','xpspreadsheet-ranges','xpspreadsheet-formatting','xpspreadsheet-autofilter','xpspreadsheet-csv-interop')) { Run-Xps "./demo/spreadsheet/$sample.xps" $sample | Out-Null }
  if (-not $IsWindows) {
    $xlsx = Get-ChildItem -Path . -Filter 'xpspreadsheet-basic.xlsx' -File -Recurse | Select-Object -First 1; if ($null -eq $xlsx) { throw 'XPSpreadsheet round-trip did not create expected XLSX.' }
    Add-Type -AssemblyName System.IO.Compression; $zip = [System.IO.Compression.ZipFile]::OpenRead($xlsx.FullName)
    try { $marker = $zip.GetEntry('docProps/custom.xml'); if ($null -eq $marker) { throw 'XPSpreadsheet workbook marker is missing.' }; $reader = [System.IO.StreamReader]::new($marker.Open()); try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }; if ($text -notmatch 'XPScriptWorkbookVersion' -or $text -notmatch '>2<') { throw 'XPSpreadsheet workbook marker is invalid.' } } finally { $zip.Dispose() }
  }
  Run-Xps ./samples/native-csv-regression.xps native-csv-regression | Out-Null
  $csvJson = Run-Xps ./samples/csv-json-roundtrip.xps csv-json-roundtrip
  if ($csvJson.Output -notmatch 'CSV-JSON-ROUNDTRIP=OK') { throw 'CSV JSON round-trip regression did not complete.' }
  Run-Xps ./samples/native-xml-dom-regression.xps native-xml-dom-regression | Out-Null
  $r = Run-Xps ./samples/xpai-structured-output.xps xpai-structured
  if ($r.Output -notmatch 'XPAI-STRUCTURED-RUNTIME=OK') { throw 'XPAi structured output runtime regression did not complete.' }
  Write-Host 'XP_RUNTIME_FULLTEST: passed'
}

if (Should-Run 'archive') {
  Write-Host '=== ARCHIVE FOCUSED TEST ==='
  # Keep compressed TAR regression first here: it is the current focused Archive failure.
  Run-Xps ./demo/archive/archive-compressed-tar.xps archive-compressed-tar | Out-Null
  Compile-Xps ./demo/archive/archive-read-only-format-regression.xps archive-read-only-format-regression
  $readOnlyFixtureRoot = './out/fulltest/archive-read-only-fixtures'
  Remove-Item -Recurse -Force $readOnlyFixtureRoot -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force $readOnlyFixtureRoot | Out-Null
  Set-Content -NoNewline -Path (Join-Path $readOnlyFixtureRoot 'payload.txt') -Value 'archive-read-only-format'
  if (-not (Get-Command 'bzip2' -ErrorAction SilentlyContinue)) { throw 'bzip2 is required for the focused Archive regression.' }
  $bzip2Fixture = Join-Path $readOnlyFixtureRoot 'payload.txt.bz2'
  $bzip2Process = Invoke-Bounded 'bzip2' @('-k','-f',(Resolve-Path (Join-Path $readOnlyFixtureRoot 'payload.txt')).Path) $runtimeTimeoutMilliseconds 'create BZip2 Archive read-only fixture'
  if ($bzip2Process.ExitCode -ne 0 -or -not (Test-Path $bzip2Fixture -PathType Leaf)) { throw 'Unable to create BZip2 Archive read-only fixture.' }
  $bzip2Extract = Join-Path $readOnlyFixtureRoot 'bzip2-extracted.txt'
  $readOnlyRun = Run-Xps ./demo/archive/archive-read-only-format-regression.xps archive-read-only-format-regression @("../../out/fulltest/archive-read-only-fixtures/payload.txt.bz2","../../out/fulltest/archive-read-only-fixtures/bzip2-extracted.txt")
  if ($readOnlyRun.Output -notmatch 'ARCHIVE_READ_ONLY_FORMAT=OK') { throw 'Archive BZip2 read-only regression did not complete.' }
  if ((Get-Content -Raw $bzip2Extract) -ne 'archive-read-only-format') { throw 'Archive BZip2 extraction payload mismatch.' }
}

if (Should-Run 'platform') {
  Write-Host '=== PLATFORM FULLTEST ==='
  # Android setup performs real installations when run; compile it only in CI.
  Compile-Xps ./Android/setup-android-dev.xps android-setup-dev-compile
  # Process execution is a core platform primitive. Keep its smallest regression first.
  $shellExecuteMissing = Run-Xps ./samples/shellexecute-missing.xps shellexecute-missing
  if ($shellExecuteMissing.Output -notmatch 'SHELLEXECUTE-MISSING=OK') { throw 'ShellExecute missing executable regression did not complete.' }
  $shellExecuteArgvSmoke = Run-Xps ./samples/shellexecute-argv-smoke.xps shellexecute-argv-smoke
  if ($shellExecuteArgvSmoke.Output -notmatch 'SHELLEXECUTE-ARGV-SMOKE=OK') { throw 'ShellExecute argv smoke regression did not complete.' }
  $shellExecute = Run-Xps ./samples/shellexecute-basic.xps shellexecute-basic
  if ($shellExecute.Output -notmatch 'SHELLEXECUTE-BASIC=OK') { throw 'ShellExecute basic regression did not complete.' }
  $shellExecuteArgs = Run-Xps ./samples/shellexecute-arguments.xps shellexecute-arguments
  if ($shellExecuteArgs.Output -notmatch 'SHELLEXECUTE-ARGS=OK') { throw 'ShellExecute structured argument regression did not complete.' }
  $shellExecuteTimeout = Run-Xps ./samples/shellexecute-timeout.xps shellexecute-timeout
  if ($shellExecuteTimeout.Output -notmatch 'SHELLEXECUTE-TIMEOUT=OK') { throw 'ShellExecute timeout regression did not complete.' }
  $shellExecutePressure = Run-Xps ./samples/shellexecute-pressure.xps shellexecute-pressure
  if ($shellExecutePressure.Output -notmatch 'SHELLEXECUTE-PRESSURE=OK') { throw 'ShellExecute stdout/stderr pressure regression did not complete.' }
  # Focused first regression: keep the most recently failing corrupt ZIP behavior at the front of the platform suite.
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/ArchiveSecurityFixtures/ArchiveSecurityFixtures.csproj','-c','Release','--','./out/archive-security-fixtures') $compileTimeoutMilliseconds 'Archive corrupt stream fixture first'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  Compile-Xps ./demo/archive/archive-corrupt-stream-regression.xps archive-corrupt-stream-regression
  Expect-XpsFailure archive-corrupt-stream-regression @('../../out/archive-security-fixtures/corrupt-stream.zip') 'corrupt compressed stream focused regression'
  Expect-XpsFailure archive-corrupt-stream-regression @('../../out/archive-security-fixtures/incorrect-size-metadata.zip') 'incorrect archive size metadata focused regression'
  # Focused replacement-failure regression: a failed save must leave the original ZIP byte-for-byte unchanged.
  if ($IsWindows) {
    $replacementPath = './out/fulltest/archive-replacement-failure.zip'
    if (Test-Path $replacementPath) { Remove-Item -Force $replacementPath }
    Compress-Archive -Path './README.md' -DestinationPath $replacementPath
    $replacementHashBefore = (Get-FileHash $replacementPath -Algorithm SHA256).Hash
    Compile-Xps ./demo/archive/archive-replacement-failure-regression.xps archive-replacement-failure-regression
    try {
      (Get-Item $replacementPath).IsReadOnly = $true
      Expect-XpsFailure archive-replacement-failure-regression @('../../archive-replacement-failure.zip') 'archive replacement failure preserves original'
      $replacementHashAfter = (Get-FileHash $replacementPath -Algorithm SHA256).Hash
      if ($replacementHashAfter -ne $replacementHashBefore) { throw 'Failed Archive replacement changed the original archive.' }
    } finally {
      if (Test-Path $replacementPath) { (Get-Item $replacementPath).IsReadOnly = $false }
    }
  }
  # Focused extraction-commit rollback regression: a failed commit must restore the destination.
  $rollbackArchive = './out/fulltest/archive-extraction-rollback.zip'
  $rollbackSource = './out/fulltest/archive-extraction-rollback-source'
  $rollbackTarget = './out/fulltest/archive-extraction-rollback-target'
  Remove-Item -Recurse -Force $rollbackSource,$rollbackTarget -ErrorAction SilentlyContinue
  Remove-Item -Force $rollbackArchive -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force $rollbackSource,$rollbackTarget | Out-Null
  Set-Content -NoNewline -Path (Join-Path $rollbackSource 'a-new.txt') -Value 'new'
  Set-Content -NoNewline -Path (Join-Path $rollbackSource 'b-existing.txt') -Value 'replacement'
  Set-Content -NoNewline -Path (Join-Path $rollbackSource 'z-blocked.txt') -Value 'blocked'
  Compress-Archive -Path (Join-Path $rollbackSource '*') -DestinationPath $rollbackArchive
  Set-Content -NoNewline -Path (Join-Path $rollbackTarget 'b-existing.txt') -Value 'original'
  New-Item -ItemType Directory -Force (Join-Path $rollbackTarget 'z-blocked.txt') | Out-Null
  Compile-Xps ./demo/archive/archive-extraction-rollback-regression.xps archive-extraction-rollback-regression
  Expect-XpsFailure archive-extraction-rollback-regression @('../../archive-extraction-rollback.zip','../../archive-extraction-rollback-target') 'archive extraction commit rollback'
  if (Test-Path (Join-Path $rollbackTarget 'a-new.txt')) { throw 'Failed Archive extraction left a newly committed file behind.' }
  if ((Get-Content -Raw (Join-Path $rollbackTarget 'b-existing.txt')) -ne 'original') { throw 'Failed Archive extraction did not restore an overwritten file.' }
  if (-not (Test-Path (Join-Path $rollbackTarget 'z-blocked.txt') -PathType Container)) { throw 'Failed Archive extraction changed the blocking destination directory.' }
  # Practical large-file Archive regression: round-trip a 16 MiB file and verify it byte-for-byte.
  $largeSource = './out/fulltest/archive-large-source.bin'
  $largeExtract = './out/fulltest/archive-large-extract'
  Remove-Item -Recurse -Force $largeExtract -ErrorAction SilentlyContinue
  $largeBytes = New-Object byte[] (16MB)
  for ($i = 0; $i -lt $largeBytes.Length; $i += 4096) { $largeBytes[$i] = [byte](($i / 4096) % 251) }
  [System.IO.File]::WriteAllBytes($largeSource, $largeBytes)
  $largeHashBefore = (Get-FileHash $largeSource -Algorithm SHA256).Hash
  Compile-Xps ./demo/archive/archive-large-file-regression.xps archive-large-file-regression
  $largeRun = Run-Xps ./demo/archive/archive-large-file-regression.xps archive-large-file-regression @('../../out/fulltest/archive-large-source.bin','../../out/fulltest/archive-large-extract')
  if ($largeRun.Output -notmatch 'ARCHIVE_LARGE_FILE=OK') { throw 'Archive large-file regression did not complete.' }
  $largeHashAfter = (Get-FileHash (Join-Path $largeExtract 'large.bin') -Algorithm SHA256).Hash
  if ($largeHashAfter -ne $largeHashBefore) { throw 'Archive large-file round-trip changed file contents.' }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/CompilerMachineInterfaceProbe/CompilerMachineInterfaceProbe.csproj','-c','Release','--','.') $compileTimeoutMilliseconds 'Compiler machine interface probe'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  # Keep writable extended in-memory format coverage focused and early when it regresses.
  Compile-Xps ./demo/archive/archive-extended-memory-write.xps archive-extended-memory-write-focused
  $archiveWriteFocused = Run-Xps ./demo/archive/archive-extended-memory-write.xps archive-extended-memory-write-focused
  if ($archiveWriteFocused.Output -notmatch 'ARCHIVE_EXTENDED_MEMORY_WRITE=OK') { throw 'Archive writable format round-trip regression did not complete.' }

  # Compile the focused read-only format probe early and run it against a deterministic
  # read-only fixture produced by the platform tools available in CI.
  Compile-Xps ./demo/archive/archive-read-only-format-regression.xps archive-read-only-format-regression
  $readOnlyFixtureRoot = './out/fulltest/archive-read-only-fixtures'
  Remove-Item -Recurse -Force $readOnlyFixtureRoot -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force $readOnlyFixtureRoot | Out-Null
  Set-Content -NoNewline -Path (Join-Path $readOnlyFixtureRoot 'payload.txt') -Value 'archive-read-only-format'
  if (Get-Command 'bzip2' -ErrorAction SilentlyContinue) {
    $bzip2Fixture = Join-Path $readOnlyFixtureRoot 'payload.txt.bz2'
    $bzip2Process = Invoke-Bounded 'bzip2' @('-k','-f',(Resolve-Path (Join-Path $readOnlyFixtureRoot 'payload.txt')).Path) $runtimeTimeoutMilliseconds 'create BZip2 Archive read-only fixture'
    if ($bzip2Process.ExitCode -ne 0 -or -not (Test-Path $bzip2Fixture -PathType Leaf)) { throw 'Unable to create BZip2 Archive read-only fixture.' }
    $bzip2Extract = Join-Path $readOnlyFixtureRoot 'bzip2-extracted.txt'
    $readOnlyRun = Run-Xps ./demo/archive/archive-read-only-format-regression.xps archive-read-only-format-regression @("../../out/fulltest/archive-read-only-fixtures/payload.txt.bz2","../../out/fulltest/archive-read-only-fixtures/bzip2-extracted.txt")
    if ($readOnlyRun.Output -notmatch 'ARCHIVE_READ_ONLY_FORMAT=OK') { throw 'Archive BZip2 read-only regression did not complete.' }
    if ((Get-Content -Raw $bzip2Extract) -ne 'archive-read-only-format') { throw 'Archive BZip2 extraction payload mismatch.' }
  }


  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/ArchiveCapabilityProbe/ArchiveCapabilityProbe.csproj','-c','Release') $compileTimeoutMilliseconds 'Archive compiler probes'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  $r = Invoke-Bounded 'dotnet' @('run','--project','./tests/ArchiveSecurityFixtures/ArchiveSecurityFixtures.csproj','-c','Release','--','./out/archive-security-fixtures') $compileTimeoutMilliseconds 'Archive security fixtures'; if ($r.ExitCode -ne 0) { exit $r.ExitCode }
  foreach ($sample in @('archive-zip','archive-memory','archive-iterator','archive-edge-cases','archive-security-fixtures','archive-case-sensitivity-regression')) { Run-Xps "./demo/archive/$sample.xps" $sample | Out-Null }
  Compile-Xps ./demo/archive/archive-security-reject-zip.xps archive-security-reject-zip
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/traversal.zip','10000','2147483647','1000') 'path traversal'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/absolute-unix.zip','10000','2147483647','1000') 'absolute Unix path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/absolute-windows.zip','10000','2147483647','1000') 'absolute Windows path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/unc.zip','10000','2147483647','1000') 'UNC path'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/mixed-separator-traversal.zip','10000','2147483647','1000') 'mixed separator traversal'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/corrupt-stream.zip','10000','2147483647','1000','read') 'corrupt compressed stream'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/incorrect-size-metadata.zip','10000','2147483647','1000','read') 'incorrect archive size metadata'
  Expect-XpsFailure archive-security-reject-zip @('../../out/archive-security-fixtures/malformed.zip','10000','2147483647','1000') 'malformed archive'
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
