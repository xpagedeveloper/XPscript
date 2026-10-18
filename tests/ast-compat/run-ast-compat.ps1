$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$compiler = Join-Path $root "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll"
$outRoot = Join-Path $root "out/ast-compat"

$tests = @(
    @{ Name = "print"; File = "01-print.xps"; Expected = @("AST_PRINT_OK") },
    @{ Name = "arithmetic"; File = "02-arithmetic.xps"; Expected = @("14", "20") },
    @{ Name = "if-single-line"; File = "03-if-single-line.xps"; Expected = @("SINGLE_TRUE") },
    @{ Name = "if-block"; File = "04-if-block.xps"; Expected = @("BLOCK_TRUE") },
    @{ Name = "if-elseif-else"; File = "05-if-elseif-else.xps"; Expected = @("TWO") },
    @{ Name = "if-boolean"; File = "06-if-boolean.xps"; Expected = @("BOOLEAN_OK") },
    @{ Name = "if-nested"; File = "07-if-nested.xps"; Expected = @("NESTED_OK") },
    @{ Name = "function-return"; File = "08-function-return.xps"; Expected = @("42") },
    @{ Name = "function-parameters"; File = "09-function-parameters.xps"; Expected = @("42") },
    @{ Name = "nested-function-calls"; File = "10-nested-function-calls.xps"; Expected = @("42") },
    @{ Name = "if-function-call"; File = "11-if-function-call.xps"; Expected = @("CALL_IF_OK") },
    @{ Name = "comments-and-rem"; File = "12-comments-and-rem.xps"; Expected = @("COMMENTS_OK") },
    @{ Name = "line-continuation"; File = "13-line-continuation.xps"; Expected = @("3") },
    @{ Name = "application-newline"; File = "14-application-newline.xps"; Expected = @("NEWLINE_BEGIN", "A", "B", "NEWLINE_END") }
)

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

foreach ($test in $tests) {
    Write-Host "=== AST compatibility baseline: $($test.Name) ==="
    $source = Join-Path $PSScriptRoot $test.File
    $output = Join-Path $outRoot $test.Name
    if ($IsWindows) { $output += ".exe" }
    $legacyOutput = Join-Path $outRoot ($test.Name + '-legacy')
    if ($IsWindows) { $legacyOutput += ".exe" }
    dotnet run --project (Join-Path $root 'src/XPScript.Compiler/XPScript.Compiler.csproj') -c Release --no-build -- $source -o $legacyOutput --runtime=false
    $legacyCompileCode = $LASTEXITCODE
    if ($legacyCompileCode -ne 0) { throw "Legacy compilation failed: $($test.Name) (exit code $legacyCompileCode)" }
    if (-not (Test-Path $legacyOutput)) { throw "Legacy compiler produced no executable: $($test.Name)" }
    $legacyLines = @(& $legacyOutput)
    $legacyRunCode = $LASTEXITCODE
    if ($legacyRunCode -ne 0) { throw "Legacy program failed: $($test.Name), exit code $legacyRunCode" }
    dotnet $compiler ast-compile $source -o $output
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $($test.Name)" }

    $generated = Join-Path $output "Generated.dll"
    if (-not (Test-Path $generated)) { throw "AST compilation produced no executable: $($test.Name)" }
    $lines = @(& dotnet $generated)
    if ($LASTEXITCODE -ne 0) { throw "Program failed: $($test.Name), exit code $LASTEXITCODE" }
    $actual = @($lines | ForEach-Object { "$_".TrimEnd() })
    $legacyActual = @($legacyLines | ForEach-Object { "$_".TrimEnd() })
    $expected = @($test.Expected)
    if (($actual -join "\n") -cne ($expected -join "\n")) {
        throw "Output mismatch for $($test.Name). Expected [$($expected -join ', ')] but got [$($actual -join ', ')]"
    }
    if (($legacyActual -join "\n") -cne ($actual -join "\n")) {
        throw "Legacy/AST runtime output mismatch for $($test.Name). Legacy [$($legacyActual -join ', ')] AST [$($actual -join ', ')]"
    }
}

Write-Host "AST compatibility baseline passed: $($tests.Count) deterministic programs with legacy/AST compile and runtime parity."
