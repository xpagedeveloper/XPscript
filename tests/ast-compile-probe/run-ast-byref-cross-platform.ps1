param([string]$CompilerPath)

. (Join-Path $PSScriptRoot 'cli-common.ps1')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
$compiler = if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
} else {
    [System.IO.Path]::GetFullPath($CompilerPath)
}
if (-not (Test-Path -LiteralPath $compiler)) { throw "Compiler DLL not found: $compiler" }
$output = Join-Path $root ('out/ast-byref-cross-platform/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null

function Invoke-Fixture([string]$name, [string]$expected, [switch]$Debug) {
    $fixture = Join-Path $PSScriptRoot "$name.xps"
    $target = Join-Path $output $name
    Write-Host "AST_BYREF_FIXTURE_START=$fixture"
    $args = @($compiler, 'ast-compile', $fixture, '-o', $target)
    if ($Debug) { $args += '--debug' }
    $compile = dotnet @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "AST ByRef compile failed: $fixture`n$($compile -join "`n")" }
    $run = Invoke-BoundedDotnetRun @((Join-Path $target 'Generated.dll')) 120000
    Write-Host "AST_BYREF_RUNTIME_STATUS=$($run.TimedOut):$($run.ExitCode)"
    Write-Host "AST_BYREF_RUNTIME_OUTPUT=$($run.Output.Trim())"
    if ($run.TimedOut -or $run.ExitCode -ne 0 -or $run.Output.Trim().Replace("`r`n", "`n") -cne $expected) {
        throw "AST ByRef runtime failed: $fixture`n$($run.Output)"
    }
    Write-Host "AST_BYREF_FIXTURE_OK=$fixture"
}

Write-Host 'AST_CLI_CHECKPOINT=byref-cross-platform-start'
Invoke-Fixture 'class-method-sqlite-next' "True`nAST_SQLITE_CLASS_METHOD_BYREF_OK"
Invoke-Fixture 'byref-property-next' 'AST_BYREF_PROPERTY_COPYBACK_OK' -Debug
foreach ($name in @('byref-readonly-property-error')) {
    $fixture = Join-Path $PSScriptRoot "$name.xps"
    Write-Host "AST_BYREF_FIXTURE_START=$fixture"
    $result = dotnet $compiler ast-compile $fixture -o (Join-Path $output $name) --debug 2>&1
    $expectedLine = if ($name -eq 'byref-readonly-property-error') { 13 } else { 19 }
    if ($LASTEXITCODE -ne 2 -or ($result -join "`n") -notmatch [regex]::Escape("${name}.xps:${expectedLine}:") + '.*XPS2004: No matching overload') {
        throw "AST ByRef rejection diagnostic failed: $fixture`n$($result -join "`n")"
    }
    Write-Host "AST_BYREF_FIXTURE_OK=$fixture"
}
Invoke-Fixture 'class-method-database-next' "True`nTrue`nTrue`nTrue`nTrue`nAST_DATABASE_CLASS_METHOD_BYREF_OK" -Debug
Invoke-Fixture 'byref-indexed-property-next' 'AST_BYREF_INDEXED_PROPERTY_OK' -Debug
Invoke-Fixture 'class-method-native-reference-runtime-next' 'AST_NATIVE_CLASS_METHOD_REFERENCE_RUNTIME_OK'
Invoke-Fixture 'class-method-archive-next' 'AST_ARCHIVE_CLASS_METHOD_OK'
Write-Host 'AST_CLI_CHECKPOINT=byref-cross-platform-complete'
