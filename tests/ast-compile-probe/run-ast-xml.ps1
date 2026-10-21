$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$output = Join-Path $root ('out/ast-cli/xml-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
# Keep the smallest discovered Parent/With failures first.
foreach ($fixture in @('xml-parent', 'xml-with-call', 'xml-runtime', 'xml-reference', 'xml-security', 'xml-parity', 'xml-unused', 'xml-escape-only')) {
    $directory = Join-Path $output $fixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$fixture.xps") -o $directory
    if ($LASTEXITCODE -ne 0) { throw "AST $fixture compilation failed." }
    $result = dotnet (Join-Path $directory 'Generated.dll')
    $expected = switch ($fixture) {
        'xml-parent' { "root`nParent Me" }
        'xml-with-call' { "42`nok" }
        'xml-runtime' { 'XML_AST_OK' }
        'xml-reference' { 'XML_REFERENCE_OK' }
        'xml-security' { "Invalid XML input.`nInvalid XML input.`nInvalid XML input.`nXML input exceeds the 8 MiB parse limit.`nExternal DTD identifiers and external entities are not supported.`nDTD input exceeds the 1 MiB limit.`nXML operation requires an element node.`nXML_SECURITY_OK" }
        'xml-parity' { "root`nÅsa & Anna`n43`nFalse`n<root id=`"43`"><child>Åsa &amp; Anna</child></root>`nreplaced`n5 &lt; 10 &amp; 20 &gt; 10`nXML_PARITY_OK" }
        'xml-unused' { 'XPXmlDocument XmlParse XmlEscape' }
        'xml-escape-only' { '&lt;&amp;&gt;' }
    }
    if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -ne $expected) { throw "AST $fixture output mismatch: $($result -join '`n')" }
    $assembly = [System.Reflection.Assembly]::LoadFile((Join-Path $directory 'Generated.dll'))
    if ($fixture -in @('xml-unused', 'xml-escape-only')) {
        if ($null -ne $assembly.GetType('XPScriptXmlDocument', $false)) { throw 'Unused XML DOM runtime leaked into output.' }
        if ($fixture -eq 'xml-unused' -and $null -ne $assembly.GetType('XPScriptNativeXml', $false)) { throw 'Unused XML helper runtime leaked into output.' }
    } elseif ($null -eq $assembly.GetType('XPScriptXmlDocument', $false)) { throw 'XML used a legacy fallback or placeholder.' }
    if ($assembly.GetReferencedAssemblies().Name -match 'ClosedXML|Magick.NET|Microsoft.Data.Sqlite|SharpCompress') { throw 'XML output acquired unrelated addon references.' }
}
foreach ($fixture in @(
    @('xml-constructor-error', 'XPS1009', 2), @('xml-element-constructor-error', 'XPS1008', 2),
    @('xml-member-error', 'XPS2009', 3), @('xml-readonly-error', 'XPS2001', 3),
    @('xml-index-readonly-error', 'XPS2001', 3), @('xml-arity-error', 'XPS2004', 3), @('xml-static-error', 'XPS2009', 2))) {
    $result = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$($fixture[0]).xps") -o (Join-Path $output $fixture[0]) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($result -join "`n") -notmatch "$($fixture[0]).xps:$($fixture[2]):\d+: $($fixture[1]):") {
        throw "XML diagnostic lost its code or source position: $($fixture[0])"
    }
}
# Compare real compilation and execution, not validation-only acceptance.
foreach ($fixture in @('xml-parent', 'xml-parity', 'native-xml-dom-regression', 'native-xml-regression')) {
    $source = if ($fixture.StartsWith('native-')) { Join-Path $root "samples/$fixture.xps" } else { Join-Path $PSScriptRoot "$fixture.xps" }
    $astDirectory = Join-Path $output "$fixture-ast-parity"
    dotnet $compiler ast-compile $source -o $astDirectory
    if ($LASTEXITCODE -ne 0) { throw "AST parity compilation failed: $fixture" }
    $astResult = dotnet (Join-Path $astDirectory 'Generated.dll')
    if ($LASTEXITCODE -ne 0) { throw "AST parity execution failed: $fixture" }
    $normalExe = Join-Path $output "$fixture-normal"
    if ($IsWindows) { $normalExe += '.exe' }
    dotnet $compiler legacy-compile $source -o $normalExe --single-file=false --runtime=false
    if ($LASTEXITCODE -ne 0) { throw "Normal XML parity compilation failed: $fixture" }
    $normalResult = & $normalExe
    if ($LASTEXITCODE -ne 0 -or ($normalResult -join "`n") -ne ($astResult -join "`n")) { throw "Normal/AST XML execution differs: $fixture" }
}
Write-Host 'AST native XML compilation probe passed.'
