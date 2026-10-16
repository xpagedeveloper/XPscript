$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/image-get-' + [Guid]::NewGuid().ToString('N'))
foreach ($fixture in @('image-constructor-derived', 'image-constructor', 'image-destructor-derived', 'image-destructor', 'image-field-delete', 'image-property-set', 'image-property-set-byval-signature', 'image-property-set-byref-signature', 'image-property-get', 'image-property-signature-only')) {
    $target = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $target
    if ($LASTEXITCODE -ne 0) { throw "AST image getter compilation failed: $fixture" }
    $result = dotnet (Join-Path $target 'Generated.dll')
    $expected = switch ($fixture) {
        'image-constructor-derived' { 'IMAGE_DERIVED_CONSTRUCTORS_OK' }
        'image-constructor' { 'IMAGE_CONSTRUCTOR_OK' }
        'image-destructor-derived' { 'IMAGE_DERIVED_DESTRUCTORS_OK' }
        'image-destructor' { 'IMAGE_DESTRUCTOR_OK' }
        'image-field-delete' { 'IMAGE_FIELD_DELETE_OK' }
        'image-property-set' { 'IMAGE_PROPERTY_SET_OK' }
        'image-property-set-byval-signature' { 'IMAGE_SET_BYVAL_SIGNATURE_OK' }
        'image-property-set-byref-signature' { 'IMAGE_SET_BYREF_SIGNATURE_OK' }
        'image-property-get' { 'IMAGE_PROPERTY_GET_OK' }
        'image-property-signature-only' { 'IMAGE_PROPERTY_SIGNATURE_ONLY_OK' }
    }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST image getter lifetime regression failed: $fixture" }
    $notice = Get-Content (Join-Path $target 'Third-party-license.txt') -Raw
    if ($notice -notmatch 'Magick.NET-Q16-AnyCPU' -or $notice -notmatch 'Magick.NET.Core' -or $notice -match 'License text could not be resolved') { throw 'Image getter addon notices are incomplete.' }
}
foreach ($case in @(
    @('image-property-set-mode-error', 'XPS2001', 8),
    @('image-property-set-type-error', 'XPS2001', 8),
    @('image-property-index-deferred', 'XPS3001', 2),
    @('image-property-readonly-error', 'XPS2001', 7),
    @('image-property-private-error', 'XPS2009', 7),
    @('image-property-body-error', 'XPS2008', 3)
)) {
    $fixture = $case[0]
    $failure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o (Join-Path $output $fixture) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($failure -join "`n") -notmatch "$fixture.xps:$($case[2]):\d+: $($case[1]):") { throw "Image getter/accessor diagnostic lost: $fixture" }
}
Write-Host 'AST_IMAGE_SETTERS_OK'
Write-Host 'AST_IMAGE_GETTERS_OK'
Write-Host 'AST_IMAGE_FIELD_DELETE_OK'
Write-Host 'AST_IMAGE_DESTRUCTORS_OK'
Write-Host 'AST_IMAGE_DERIVED_DESTRUCTORS_OK'
Write-Host 'AST_IMAGE_CONSTRUCTORS_OK'
Write-Host 'AST_IMAGE_DERIVED_CONSTRUCTORS_OK'
