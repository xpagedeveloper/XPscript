$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/image-get-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('image-property-get', 'image-property-signature-only')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST image getter compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = if ($fixture -eq 'image-property-get') { 'IMAGE_PROPERTY_GET_OK' } else { 'IMAGE_PROPERTY_SIGNATURE_ONLY_OK' }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST image getter lifetime regression failed: $fixture" }
    $notice = Get-Content (Join-Path $target 'Third-party-license.txt') -Raw
    if ($notice -notmatch 'Magick.NET-Q16-AnyCPU' -or $notice -notmatch 'Magick.NET.Core' -or $notice -match 'License text could not be resolved') { throw 'Image getter addon notices are incomplete.' }
}
foreach ($case in @(
    @('native-property-image-deferred', 'XPS3001', 2),
    @('image-property-set-byref-deferred', 'XPS3001', 2),
    @('image-property-index-deferred', 'XPS3001', 2),
    @('image-property-readonly-error', 'XPS2001', 7),
    @('image-property-private-error', 'XPS2009', 7),
    @('image-property-body-error', 'XPS2008', 3)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Image getter/accessor diagnostic lost: $fixture" }
    if ($fixture -in @('native-property-image-deferred', 'image-property-set-byref-deferred') -and ($failure -join "`n") -notlike '*XPImage Property Set ownership is not implemented yet*') { throw 'Image setter ownership deferral lost its stable explanation.' }
}
Write-Host 'AST_IMAGE_GETTERS_OK'
