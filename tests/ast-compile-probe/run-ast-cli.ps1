$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')
$compiler = Join-Path $root 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll'
$source = Join-Path $PSScriptRoot 'cli-main.xps'
$output = Join-Path $root 'out/ast-cli'
New-Item -ItemType Directory -Force -Path $output | Out-Null
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-array.xps') -o (Join-Path $output 'static-array-early')
if ($LASTEXITCODE -ne 0) { throw 'AST Static array storage compilation failed.' }
$staticEarlyResult = dotnet (Join-Path $output 'static-array-early/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticEarlyResult -join "`n") -ne "1`n2") { throw 'AST Static array storage lifecycle failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-elements-runtime.xps') -o (Join-Path $output 'indexed-byref-elements-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST element ByRef index compilation failed.' }
$elementIndexResult = dotnet (Join-Path $output 'indexed-byref-elements-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($elementIndexResult -join "`n") -ne 'INDEXED_BYREF_ELEMENTS_OK') { throw 'AST element ByRef index alias/order/bounds/exception execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-elements-simple.xps') -o (Join-Path $output 'indexed-byref-elements-simple')
if ($LASTEXITCODE -ne 0) { throw 'AST ordinary/flat element ByRef index compilation failed.' }
$elementSimpleResult = dotnet (Join-Path $output 'indexed-byref-elements-simple/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($elementSimpleResult -join "`n") -ne 'INDEXED_BYREF_ELEMENTS_SIMPLE_OK') { throw 'AST ordinary/flat element alias execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-stable-storage.xps') -o (Join-Path $output 'list-stable-storage')
if ($LASTEXITCODE -ne 0) { throw 'AST shared List storage compilation failed.' }
$listStorageResult = dotnet (Join-Path $output 'list-stable-storage/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listStorageResult -join "`n") -ne 'LIST_STABLE_STORAGE_OK') { throw 'AST shared List storage failed.' }
$listStorageNormal = Join-Path $output $(if ($IsWindows) { 'list-stable-normal.exe' } else { 'list-stable-normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'list-stable-storage.xps') -o $listStorageNormal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal shared List storage compilation failed.' }
$listStorageNormalResult = & $listStorageNormal
if ($LASTEXITCODE -ne 0 -or ($listStorageNormalResult -join "`n") -ne 'LIST_STABLE_STORAGE_OK') { throw 'Normal/AST shared List storage parity failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-list-lifetime.xps') -o (Join-Path $output 'indexed-byref-list-lifetime')
if ($LASTEXITCODE -ne 0) { throw 'AST captured List reference compilation failed.' }
$listLifetimeResult = dotnet (Join-Path $output 'indexed-byref-list-lifetime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listLifetimeResult -join "`n") -ne 'INDEXED_BYREF_LIST_LIFETIME_OK') { throw 'AST captured List reference lifetime failed.' }
$moduleStorageFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-module-storage-error.xps') -o (Join-Path $output 'indexed-byref-module-storage-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($moduleStorageFailure -join "`n") -notmatch 'indexed-byref-module-storage-error.xps:9:') { throw 'Module header diagnostic source mapping lost.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-module-runtime.xps') -o (Join-Path $output 'indexed-byref-module-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST module ByRef index compilation failed.' }
$moduleIndexResult = dotnet (Join-Path $output 'indexed-byref-module-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($moduleIndexResult -join "`n") -ne 'INDEXED_BYREF_MODULE_OK') { throw 'AST module ByRef index storage/alias/exception execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'indexed-byref-runtime.xps') -o (Join-Path $output 'indexed-byref-runtime')
if ($LASTEXITCODE -ne 0) { throw 'AST ByRef index compilation failed.' }
$byRefIndexResult = dotnet (Join-Path $output 'indexed-byref-runtime/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($byRefIndexResult -join "`n") -ne 'INDEXED_BYREF_RUNTIME_OK') { throw 'AST ByRef index alias/exception execution failed.' }
foreach ($byRefInvalid in @('indexed-byref-module-storage-error', 'indexed-byref-module-type-error', 'indexed-byref-property-error', 'indexed-byref-literal-error', 'indexed-byref-type-error', 'indexed-byref-mode-error', 'indexed-byref-complex-error')) {
    $byRefFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$byRefInvalid.xps") -o (Join-Path $output $byRefInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($byRefFailure -join "`n") -notmatch "$byRefInvalid.xps:(5|8|9|16):") { throw "ByRef index diagnostic source mapping lost: $byRefInvalid" }
}
foreach ($indexedFixture in @('indexed-byref-module-simple', 'indexed-byref-field-capability', 'indexed-properties-runtime', 'indexed-get-parity')) {
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$indexedFixture.xps") -o (Join-Path $output $indexedFixture)
    if ($LASTEXITCODE -ne 0) { throw "AST indexed property compilation failed: $indexedFixture" }
    $indexedResult = dotnet (Join-Path $output "$indexedFixture/Generated.dll")
    $indexedExpected = if ($indexedFixture -eq 'indexed-get-parity') { '6' } elseif ($indexedFixture -eq 'indexed-byref-module-simple') { 'INDEXED_BYREF_MODULE_SIMPLE_OK' } elseif ($indexedFixture -eq 'indexed-byref-field-capability') { 'INDEXED_BYREF_FIELD_OK' } else { 'INDEXED_PROPERTIES_RUNTIME_OK' }
    if ($LASTEXITCODE -ne 0 -or ($indexedResult -join "`n") -ne $indexedExpected) { throw "AST indexed property execution failed: $indexedFixture" }
}
$indexedNormal = Join-Path $output $(if ($IsWindows) { 'indexed-get-normal.exe' } else { 'indexed-get-normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'indexed-get-parity.xps') -o $indexedNormal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal indexed getter compilation failed.' }
$indexedNormalResult = & $indexedNormal
if ($LASTEXITCODE -ne 0 -or ($indexedNormalResult -join "`n") -ne '6') { throw 'Normal/AST indexed getter parity failed.' }
dotnet $compiler ast-compile (Join-Path $root 'samples/indexed-properties.xps') -o (Join-Path $output 'indexed-existing-sample')
if ($LASTEXITCODE -ne 0) { throw 'Existing indexed scalar sample compilation failed.' }
$indexedSampleResult = dotnet (Join-Path $output 'indexed-existing-sample/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($indexedSampleResult -join "`n") -ne "INDEX1=first`nINDEX2=second") { throw 'Existing indexed scalar sample execution failed.' }
foreach ($indexedInvalid in @('indexed-signature-error', 'indexed-readonly-error', 'indexed-writeonly-error', 'indexed-arity-error', 'indexed-type-error', 'indexed-private-error')) {
    $indexedFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$indexedInvalid.xps") -o (Join-Path $output $indexedInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($indexedFailure -join "`n") -notmatch "$indexedInvalid.xps:(5|7|8):") { throw "Indexed diagnostic source mapping lost: $indexedInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-properties-capability.xps') -o (Join-Path $output 'class-properties-capability')
if ($LASTEXITCODE -ne 0) { throw 'AST explicit property setter/With compilation failed.' }
$propertyCapability = dotnet (Join-Path $output 'class-properties-capability/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($propertyCapability -join "`n") -ne 'CLASS_PROPERTIES_CAPABILITY_OK') { throw 'AST explicit property setter/With execution failed.' }
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-properties-native.xps') -o (Join-Path $output 'class-properties-native')
if ($LASTEXITCODE -ne 0) { throw 'AST property accessor compilation failed.' }
$propertyResult = dotnet (Join-Path $output 'class-properties-native/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($propertyResult -join "`n") -ne 'CLASS_PROPERTIES_NATIVE_OK') { throw 'AST property accessor execution failed.' }
$propertyNormal = Join-Path $output $(if ($IsWindows) { 'class-properties-normal.exe' } else { 'class-properties-normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-properties-native.xps') -o $propertyNormal --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal property accessor compilation failed.' }
$propertyNormalResult = & $propertyNormal
if ($LASTEXITCODE -ne 0 -or ($propertyNormalResult -join "`n") -ne ($propertyResult -join "`n")) { throw 'Normal/AST property parity failed.' }
foreach ($propertyInvalid in @('class-property-readonly-error', 'class-property-writeonly-error', 'class-property-private-error', 'class-property-body-error', 'class-property-type-error', 'class-property-indexed-deferred')) {
    $propertyFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$propertyInvalid.xps") -o (Join-Path $output $propertyInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($propertyFailure -join "`n") -notmatch "$propertyInvalid.xps:(2|3|5|8|9):") { throw "Property diagnostic source mapping lost: $propertyInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-methods-native.xps') -o (Join-Path $output 'class-methods-native')
if ($LASTEXITCODE -ne 0) { throw 'AST instance class method compilation failed.' }
$classMethodsResult = dotnet (Join-Path $output 'class-methods-native/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classMethodsResult -join "`n") -ne 'CLASS_METHODS_NATIVE_OK') { throw 'AST instance class method execution failed.' }
$classNormalPath = Join-Path $output $(if ($IsWindows) { 'class-methods-normal.exe' } else { 'class-methods-normal' })
dotnet $compiler compile (Join-Path $PSScriptRoot 'class-methods-native.xps') -o $classNormalPath --single-file=false --runtime=false
if ($LASTEXITCODE -ne 0) { throw 'Normal instance class method compilation failed.' }
$classNormalResult = & $classNormalPath
if ($LASTEXITCODE -ne 0 -or ($classNormalResult -join "`n") -ne ($classMethodsResult -join "`n")) { throw 'Normal/AST class method parity failed.' }
foreach ($classInvalid in @('class-method-scope-error', 'class-method-private-error', 'class-field-private-error', 'class-method-body-error')) {
    $classFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$classInvalid.xps") -o (Join-Path $output $classInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($classFailure -join "`n") -notmatch "$classInvalid.xps:(3|6|7):") { throw "Class diagnostic source span lost: $classInvalid" }
}
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-method-case.xps') -o (Join-Path $output 'class-method-case')
if ($LASTEXITCODE -ne 0) { throw 'AST case-insensitive class method compilation failed.' }
$classCaseResult = dotnet (Join-Path $output 'class-method-case/Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classCaseResult -join "`n") -ne '3') { throw 'AST case-insensitive class member execution failed.' }
dotnet run --project (Join-Path $root 'tests/AstApplicationRuntimeProbe') -c Release
if ($LASTEXITCODE -ne 0) { throw 'AST native Application runtime regression failed.' }
dotnet run --project (Join-Path $root 'tests/AstSpreadsheetRuntimeProbe') -c Release
if ($LASTEXITCODE -ne 0) { throw 'AST native spreadsheet runtime regression failed.' }
foreach ($sheetInvalid in @('spreadsheet-member-error', 'spreadsheet-constructor-error', 'spreadsheet-readonly-error', 'spreadsheet-private-setter-error', 'spreadsheet-arity-error')) {
    $sheetFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$sheetInvalid.xps") -o (Join-Path $output $sheetInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($sheetFailure -join "`n") -notmatch "$sheetInvalid.xps:(2|3):") { throw "Spreadsheet diagnostic or source span lost: $sheetInvalid" }
}
dotnet run --project (Join-Path $root 'tests/AstHttpRuntimeProbe') -c Release
if ($LASTEXITCODE -ne 0) { throw 'AST native HTTP/TLS runtime regression failed.' }
foreach ($httpInvalid in @('http-member-error', 'http-constructor-error', 'http-readonly-error', 'http-arity-error')) {
    $httpFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$httpInvalid.xps") -o (Join-Path $output $httpInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($httpFailure -join "`n") -notmatch "$httpInvalid.xps:(2|3):") { throw "HTTP diagnostic or source span lost: $httpInvalid" }
}
& (Join-Path $PSScriptRoot 'run-ast-xml.ps1')
$csvRoot = Join-Path $output ('csv-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $csvRoot | Out-Null
foreach ($csvFixture in @('csv-runtime', 'csv-reference', 'csv-parity', 'csv-unused', 'csv-escape-only')) {
    $csvSource = Join-Path $csvRoot "$csvFixture.xps"
    Copy-Item (Join-Path $PSScriptRoot "$csvFixture.xps") $csvSource
    $csvOutput = Join-Path $csvRoot $csvFixture
    dotnet $compiler ast-compile $csvSource -o $csvOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $csvFixture compilation failed." }
    Push-Location $csvRoot
    try { $csvResult = dotnet (Join-Path $csvOutput 'Generated.dll'); $csvExit = $LASTEXITCODE } finally { Pop-Location }
    $csvExpected = switch ($csvFixture) {
        'csv-runtime' { "2`n3`nname`nÅsa`nFredrik`nquoted `"value`"`nid`nname`nnote`n002`n001`n4`n001`nFredrik`nwindows-1252`n002`nFredrik`nutf-8-bom`nFredrik`nutf-16be`nFalse`nwindows-1252`nutf-16be`n3`n2`n5`nCSV column name 'missing' not found.`n9`nCSV row index out of range.`n5`nCSV header name 'ID' already exists.`n`"a;b`"`nCSV_AST_OK" }
        'csv-reference' { "shared`nreplacement`n2`n2`n1`n5`nordinary name`n7`nordinary row" }
        'csv-parity' { "utf-8-bom`nÅsa`nFalse`nwindows-1252`nutf-8-bom`n001`nCSV_PARITY_OK" }
        'csv-unused' { "XPCsvDocument CsvParse CsvEscape`nordinary row`n7" }
        'csv-escape-only' { '"a;b"' }
    }
    if ($csvExit -ne 0 -or ($csvResult -join "`n") -ne $csvExpected) { throw "AST $csvFixture output mismatch." }
    $csvAssembly = [System.Reflection.Assembly]::LoadFile((Join-Path $csvOutput 'Generated.dll'))
    if ($csvFixture -in @('csv-unused', 'csv-escape-only')) {
        if ($null -ne $csvAssembly.GetType('XPScriptCsvDocument', $false) -or $null -ne $csvAssembly.GetType('XPScriptCsvFileRuntime', $false)) { throw 'Unused CSV document/file runtime leaked into output.' }
        if ($csvFixture -eq 'csv-unused' -and $null -ne $csvAssembly.GetType('XPScriptNativeCsv', $false)) { throw 'Unused CSV helper runtime leaked into output.' }
    } elseif ($null -eq $csvAssembly.GetType('XPScriptCsvDocument', $false)) { throw 'CSV document unexpectedly used a fallback/placeholder.' }
    if ($csvAssembly.GetReferencedAssemblies().Name -match 'ClosedXML|Magick.NET|Microsoft.Data.Sqlite') { throw 'Pure CSV output acquired unrelated addon dependencies.' }
}
foreach ($csvSample in @('native-csv-regression', 'csv-json-roundtrip')) {
    $csvSampleOutput = Join-Path $csvRoot $csvSample
    dotnet $compiler ast-compile (Join-Path $root "samples/$csvSample.xps") -o $csvSampleOutput
    if ($LASTEXITCODE -ne 0) { throw "AST existing $csvSample compilation failed." }
    Push-Location $csvRoot
    try { $csvSampleResult = dotnet (Join-Path $csvSampleOutput 'Generated.dll'); $csvSampleExit = $LASTEXITCODE } finally { Pop-Location }
    if ($csvSampleExit -ne 0) { throw "AST existing $csvSample execution failed." }
    if ($csvSample -eq 'native-csv-regression' -and (($csvSampleResult -join "`n") -notmatch "MISSING_TEXT=CSV column name 'missing' not found\." -or ($csvSampleResult -join "`n") -notmatch 'FILE_OVERRIDE=Åsa:Malmö; Sweden')) { throw 'CSV runtime error text or file charset parity drifted.' }
    if ($csvSample -eq 'csv-json-roundtrip' -and ($csvSampleResult -join "`n") -ne 'CSV-JSON-ROUNDTRIP=OK') { throw 'CSV/JSON round trip drifted.' }
}
foreach ($csvInvalid in @('csv-member-error', 'csv-constructor-error', 'csv-readonly-error', 'csv-writefile-error', 'csv-load-arity-error')) {
    $csvFailure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$csvInvalid.xps") -o (Join-Path $csvRoot $csvInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($csvFailure -join "`n") -notmatch "$csvInvalid.xps:(2|3):") { throw "AST $csvInvalid lost its diagnostic or source position." }
}
$imageFixtureRoot = Join-Path $output ('xpimage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $imageFixtureRoot | Out-Null
$imageSource = Join-Path $imageFixtureRoot 'xpimage-runtime.xps'
Copy-Item (Join-Path $PSScriptRoot 'xpimage-runtime.xps') $imageSource
$imageOutput = Join-Path $imageFixtureRoot 'ast'
dotnet $compiler ast-compile $imageSource -o $imageOutput
if ($LASTEXITCODE -ne 0) { throw 'Native AST XPImage compilation failed.' }
$imageResult = dotnet (Join-Path $imageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageResult -join "`n") -ne "4`n3`n12`n0`n4`n2`n4`n3`n4`n0`n20`n2`n0`nTrue`n5`nTrue`n5`n4`nTrue`nTrue`nTrue`n2`n3`nXPIMAGE_AST_OK") { throw 'Native AST image operations, loading, ByVal/ByRef or cleanup regression failed.' }
$imageNotice = Get-Content (Join-Path $imageOutput 'Third-party-license.txt') -Raw
if ($imageNotice -notmatch 'Magick.NET-Q16-AnyCPU' -or $imageNotice -notmatch 'Magick.NET.Core' -or $imageNotice -match 'License text could not be resolved') { throw 'AST image addon notices are incomplete.' }
$imageVariantOutput = Join-Path $imageFixtureRoot 'variant-return'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-variant-return.xps') -o $imageVariantOutput
if ($LASTEXITCODE -ne 0) { throw 'AST image Variant return compilation failed.' }
$imageVariantResult = dotnet (Join-Path $imageVariantOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageVariantResult -join "`n") -ne "3`n4`nTrue") { throw 'AST image Variant return or replaced ByVal ownership failed.' }
$imageParitySource = Join-Path $imageFixtureRoot 'xpimage-parity.xps'
Copy-Item (Join-Path $PSScriptRoot 'xpimage-parity.xps') $imageParitySource
$imageParityOutput = Join-Path $imageFixtureRoot 'parity'
dotnet $compiler ast-compile $imageParitySource -o $imageParityOutput
if ($LASTEXITCODE -ne 0) { throw 'AST image parity compilation failed.' }
$imageParityResult = dotnet (Join-Path $imageParityOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($imageParityResult -join "`n") -ne "4`n6`n0`n0`n3`n2`nXPIMAGE_PARITY_OK") { throw 'AST image output differs from the shared normal compiler contract.' }
foreach ($imageInvalid in @('xpimage-constructor-error', 'xpimage-member-error', 'xpimage-readonly-error')) {
    $invalidResult = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$imageInvalid.xps") -o (Join-Path $imageFixtureRoot $imageInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($invalidResult -join "`n") -notmatch "$imageInvalid.xps:3:") { throw "AST $imageInvalid lost its source-mapped diagnostic." }
}
$runtimeOutput = Join-Path $output 'shared-runtime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'shared-runtime.xps') -o $runtimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST shared scalar runtime compilation failed.' }
$runtimeResult = dotnet (Join-Path $runtimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($runtimeResult -join "`n") -ne "INTEGER`nLONG`n17`nTrue`n2`nEMPTY=[]`nVariable is null`nNULL`n1`nFalse`nTrue`nbcd`nTrue`n3`n2`n1`nOVERFLOW=6`nCONTINUED") { throw 'AST scalar runtime conversion, Null/Empty, environment or overflow behavior drifted.' }
foreach ($jsonFixture in @('json-runtime', 'json-reference')) {
    $jsonOutput = Join-Path $output $jsonFixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$jsonFixture.xps") -o $jsonOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $jsonFixture compilation failed." }
    $jsonResult = dotnet (Join-Path $jsonOutput 'Generated.dll')
    $jsonExpected = if ($jsonFixture -eq 'json-runtime') {
        '{"name":"\u00C5sa","values":[7],"active":true}' + "`nÅsa`nObject`nTrue`n2`n7`n1`n7`n[null,false,3]`nInvalid JSON input.`nJSON array index out of range."
    } else { "4`n8`n2`n1`n2`ntrue`nnull`nnull`n19`nnames are ordinary variables" }
    if ($LASTEXITCODE -ne 0 -or ($jsonResult -join "`n") -ne $jsonExpected) { throw "AST $jsonFixture shared runtime output mismatch." }
}
$jsonInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'json-member-error.xps') -o (Join-Path $output 'json-member-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($jsonInvalid -join "`n") -notmatch "json-member-error.xps:3:16: XPS2009: Undefined member 'UnknownMember'") { throw 'AST JSON unknown-member diagnostic lost its code or source position.' }
$textObjectOutput = Join-Path $output 'object-name-text'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-name-text.xps') -o $textObjectOutput
if ($LASTEXITCODE -ne 0) { throw 'Object names inside strings/comments must not trigger legacy fallback.' }
$textObjectResult = dotnet (Join-Path $textObjectOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($textObjectResult -join "`n") -ne 'XPImage') { throw 'AST object-name text regression failed.' }
$continuationOutput = Join-Path $output 'line-continuation'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'line-continuation.xps') -o $continuationOutput
if ($LASTEXITCODE -ne 0) { throw 'AST line continuation compilation failed.' }
$continuationResult = dotnet (Join-Path $continuationOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($continuationResult -join "`n") -ne '3') { throw 'AST line continuation execution failed.' }
$aliasByRefOutput = Join-Path $output 'forall-list-byref'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-byref.xps') -o $aliasByRefOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ForAll List ByRef compilation failed.' }
$aliasByRefResult = dotnet (Join-Path $aliasByRefOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aliasByRefResult -join "`n") -ne "a:11`nb:12`n21`n22`n23`nhello!") { throw 'AST ForAll List ByRef copy-back or nested alias restoration failed.' }
$aliasMultipleOutput = Join-Path $output 'forall-list-byref-multiple'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-byref-multiple-error.xps') -o $aliasMultipleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multiple ForAll List ByRef compilation failed.' }
$aliasMultipleResult = dotnet (Join-Path $aliasMultipleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($aliasMultipleResult -join "`n") -ne '21') { throw 'AST multiple ForAll List ByRef copy-back failed.' }
$listCopybackOutput = Join-Path $output 'list-byref-copyback'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-byref-copyback.xps') -o $listCopybackOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List ByRef copy-back compilation failed.' }
$listCopybackResult = dotnet (Join-Path $listCopybackOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listCopybackResult -join "`n") -ne "TAG_EVALUATED_ONCE`n11`nhello!") { throw 'AST List ByRef must copy back typed values and evaluate the tag once.' }
$staticScopeOutput = Join-Path $output 'static-initializer-scope'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-initializer-scope.xps') -o $staticScopeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static initializer parameter/local binding failed.' }
$staticScopeResult = dotnet (Join-Path $staticScopeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticScopeResult -join "`n") -ne "BEFORE_CALLS`nAFTER_SKIPPED_DECLARATION`nSTATIC_INITIALIZED`n7`n3`n7`n3") { throw 'AST Static initializer must execute once at the first reached declaration with current parameter/local values.' }
$objectInitializerOutput = Join-Path $output 'object-initializer-lifetime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-initializer-lifetime.xps') -o $objectInitializerOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Object initializer compilation failed.' }
$objectInitializerResult = dotnet (Join-Path $objectInitializerOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectInitializerResult -join "`n") -ne "False`nFalse`nFalse`nTrue") { throw 'AST Dim Object initializer must rerun while a cleared Static Object initializer must not rerun.' }
$objectLifetimeOutput = Join-Path $output 'object-local-lifetime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'object-local-lifetime.xps') -o $objectLifetimeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST local Object lifetime compilation failed.' }
$objectLifetimeResult = dotnet (Join-Path $objectLifetimeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($objectLifetimeResult -join "`n") -ne "True`nTrue`nFalse`nTrue`nFalse`nFalse") { throw 'AST Dim Object must start as Nothing on each call while Static Object retains its reference.' }
$staticObjectOutput = Join-Path $output 'static-object'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-object.xps') -o $staticObjectOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static Object compilation failed.' }
$staticObjectResult = dotnet (Join-Path $staticObjectOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticObjectResult -join "`n") -ne "OBJECT_CREATED`nFalse`nFalse") { throw 'AST Static Object did not start as Nothing and retain the created instance.' }
$eraseOutput = Join-Path $output 'list-erase'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-erase.xps') -o $eraseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Erase compilation failed.' }
$eraseResult = dotnet (Join-Path $eraseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($eraseResult -join "`n") -ne "False`nTrue`nFalse`nfirst`n1`nFalse`nFalse`nLIST_ERASE_OK") { throw 'AST List tag removal, clearing or removal during alias iteration failed.' }
$arrayOutput = Join-Path $output 'array-storage'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'array-storage.xps') -o $arrayOutput
if ($LASTEXITCODE -ne 0) { throw 'AST array declaration compilation failed.' }
$arrayResult = dotnet (Join-Path $arrayOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($arrayResult -join "`n") -ne "1`n0`n1`nTrue`nTrue") { throw 'AST array bounds, string defaults or Erase semantics differ from published main.' }
$redimOutput = Join-Path $output 'redim-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'redim-array.xps') -o $redimOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic array ReDim compilation failed.' }
$redimResult = dotnet (Join-Path $redimOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($redimResult -join "`n") -ne "2`n9`n9") { throw 'AST ReDim type preservation or Preserve semantics differ from published main.' }
$redimError = Join-Path $output 'redim-array-error'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'redim-array-error.xps') -o $redimError
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic Erase regression did not compile.' }
$multiDimensionalRedimOutput = Join-Path $output 'multidimensional-redim'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-redim.xps') -o $multiDimensionalRedimOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic multidimensional ReDim compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalRedimOutput 'Generated.dll')) -join "`n") -ne "1`n4`n11`n22`n1`n5") { throw 'AST dynamic multidimensional ReDim/Preserve bounds or values mismatch.' }
$multiDimensionalRedimOptionBaseOutput = Join-Path $output 'multidimensional-redim-option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-redim-option-base.xps') -o $multiDimensionalRedimOptionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST dynamic multidimensional Option Base compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalRedimOptionBaseOutput 'Generated.dll')) -join "`n") -ne "7`n1`n1`n2`n3") { throw 'AST dynamic multidimensional Option Base bounds mismatch.' }
$multiDimensionalOutput = Join-Path $output 'multidimensional-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-array.xps') -o $multiDimensionalOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multidimensional array compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalOutput 'Generated.dll')) -join "`n") -ne "12`n0`n1`n0`n9`n1`n2`n3`n4`n1`n2`n0`n1`n3`n11`nTEXT_OK`n2.5`nTrue`n255`n`n0`nFalse`n13`n3`n4") { throw 'AST multidimensional array output mismatch.' }
$multiDimensionalOptionBaseOutput = Join-Path $output 'multidimensional-option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'multidimensional-option-base.xps') -o $multiDimensionalOptionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST multidimensional Option Base compilation failed.' }
if ((@(& dotnet (Join-Path $multiDimensionalOptionBaseOutput 'Generated.dll')) -join "`n") -ne "6`n1`n1") { throw 'AST multidimensional Option Base output mismatch.' }
$nonzeroBoundOutput = Join-Path $output 'nonzero-bound-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nonzero-array.xps') -o $nonzeroBoundOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nonzero array lower-bound compilation failed.' }
$nonzeroBoundResult = dotnet (Join-Path $nonzeroBoundOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nonzeroBoundResult -join "`n") -ne "10`n30") { throw 'AST nonzero array indexing returned the wrong elements.' }
$membershipOutput = Join-Path $output 'list-membership'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-membership.xps') -o $membershipOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List membership compilation failed.' }
$membershipResult = dotnet (Join-Path $membershipOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($membershipResult -join "`n") -ne "True`nFalse`nTAG_EVALUATED`nTrue`n3") { throw 'AST List membership read a missing element or evaluated its tag more than once.' }
$listInitializerOutput = Join-Path $output 'list-initializer'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-initializer-error.xps') -o $listInitializerOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List initializer compilation failed.' }
$listInitializerResult = dotnet (Join-Path $listInitializerOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listInitializerResult -join "`n") -ne '7') { throw 'AST List initializer did not preserve the source list.' }
$listByRef = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-byref-multiple-error.xps') -o (Join-Path $output 'list-byref-error') 2>&1
if ($LASTEXITCODE -ne 0) { throw 'AST multiple List ByRef copy-back compilation failed.' }
$listFunctionsOutput = Join-Path $output 'list-functions'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-functions.xps') -o $listFunctionsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Function ByRef/parameter/return compilation failed.' }
$listFunctionsResult = dotnet (Join-Path $listFunctionsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listFunctionsResult -join "`n") -ne "12`n11`n1`n23`n21`n50`nTrue`nFalse`n51") { throw 'AST List Function, alias, reference return, parameter replacement or exceptional copy-back failed.' }
$listOrderOutput = Join-Path $output 'list-function-order'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-function-order.xps') -o $listOrderOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List Function evaluation-order compilation failed.' }
$listOrderResult = dotnet (Join-Path $listOrderOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listOrderResult -join "`n") -ne "TAG_ONCE`n6`n6") { throw 'AST List Function did not evaluate the tag once and capture its value before later arguments.' }
$listOverloadsOutput = Join-Path $output 'list-parameter-overloads'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-parameter-overloads.xps') -o $listOverloadsOutput
if ($LASTEXITCODE -ne 0) { throw 'AST List parameter overload compilation failed.' }
$listOverloadsResult = dotnet (Join-Path $listOverloadsOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listOverloadsResult -join "`n") -ne "1`n10`n2`n20") { throw 'AST List parameter element types must preserve overload and Static storage isolation.' }
$isElementInvalid = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'list-iselement-invalid.xps') -o (Join-Path $output 'list-iselement-invalid') 2>&1
if ($LASTEXITCODE -ne 2 -or ($isElementInvalid -join "`n") -notmatch 'IsElement requires one keyed List access') { throw 'AST accepted an invalid IsElement argument.' }
foreach ($listInvalid in @('list-parameter-type-error', 'list-initializer-type-error')) {
    $invalidResult = dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$listInvalid.xps") -o (Join-Path $output $listInvalid) 2>&1
    if ($LASTEXITCODE -ne 2 -or ($invalidResult -join "`n") -notmatch 'No matching overload|Cannot assign') { throw "AST accepted incompatible List element types: $listInvalid" }
}
$listOutput = Join-Path $output 'forall-list'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'forall-list-alias.xps') -o $listOutput
if ($LASTEXITCODE -ne 0) { throw 'AST ForAll List alias compilation failed.' }
$listResult = dotnet (Join-Path $listOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($listResult -join "`n") -ne "a`nb`n11`n12`none:BLUE`ntwo:GREEN`na`na`nb`nb`n12`n13`n5`n1`n2`n2") { throw 'AST ForAll List write-through, ListTag, nested alias scope or snapshot semantics failed.' }
$staticInitializer = Join-Path $output 'static-initializer'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-initializer-error.xps') -o $staticInitializer
if ($LASTEXITCODE -ne 0) { throw 'AST Static scalar initializer compilation failed.' }
$staticInitializerResult = dotnet (Join-Path $staticInitializer 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticInitializerResult -join "`n") -ne '3') { throw 'AST Static scalar initializer produced the wrong result.' }
$staticOutput = Join-Path $output 'static-local'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-local-lifetime.xps') -o $staticOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Static local compilation failed.' }
$staticResult = dotnet (Join-Path $staticOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticResult -join "`n") -ne "1`n10`n2`n20`n1`n10`n2`n20`nSTATIC_DEFAULTS_OK`nSTATIC_RETAINED_OK`n1`n2") { throw 'AST Static lifetime, procedure/overload isolation, defaults or ByRef mutation failed.' }
$crossProcedure = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-cross-procedure-error.xps') -o (Join-Path $output 'cross-procedure') 2>&1
if ($LASTEXITCODE -ne 2 -or ($crossProcedure -join "`n") -notmatch "Unknown label 'outside'") { throw 'AST GoTo accepted a label from another procedure.' }
$gotoOutput = Join-Path $output 'goto-scope'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-procedure-scope.xps') -o $gotoOutput
if ($LASTEXITCODE -ne 0) { throw 'AST procedure-scoped GoTo compilation failed.' }
$gotoResult = dotnet (Join-Path $gotoOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($gotoResult -join "`n") -ne "7`nGOTO_PROCEDURE_SCOPE_OK") { throw 'AST procedure-scoped GoTo execution failed.' }
$unsupportedGoSub = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'unsupported-gosub.xps') -o (Join-Path $output 'unsupported-gosub') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedGoSub -join "`n") -notmatch 'GoSub is not implemented in the AST compiler') { throw 'AST GoSub was not explicitly rejected.' }
$resumeOutput = Join-Path $output 'resume-runtime'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'resume-runtime.xps') -o $resumeOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Resume runtime fixture failed to compile.' }
$resumeResult = dotnet (Join-Path $resumeOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($resumeResult -join "`n") -ne "AUTOMATIC_NEXT_OK`nRETRY_OK`n7001`ninner failure`nNEXT_OK`nLABEL_OK") { throw 'AST Resume retry, next, label or nested-call semantics failed.' }
foreach ($resumeFixture in @('resume-loops', 'resume-handler-error')) {
    $fixtureOutput = Join-Path $output $resumeFixture
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$resumeFixture.xps") -o $fixtureOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $resumeFixture compilation failed." }
    $fixtureResult = dotnet (Join-Path $fixtureOutput 'Generated.dll')
    $expected = if ($resumeFixture -eq 'resume-loops') { "3`n11`n12" } else { "7001`nPROPAGATION_OK" }
    if ($LASTEXITCODE -ne 0 -or ($fixtureResult -join "`n") -ne $expected) { throw "AST $resumeFixture execution failed." }
}
$resumeUnknown = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'resume-unknown-label.xps') -o (Join-Path $output 'resume-unknown-label') 2>&1
if ($LASTEXITCODE -ne 2 -or ($resumeUnknown -join "`n") -notmatch "Unknown label 'missing'") { throw 'AST On Error label validation failed.' }
foreach ($runtimeFailure in @('resume-disabled', 'resume-error')) {
    $failureOutput = Join-Path $output $runtimeFailure
    dotnet $compiler ast-compile (Join-Path $PSScriptRoot "$runtimeFailure.xps") -o $failureOutput
    if ($LASTEXITCODE -ne 0) { throw "AST $runtimeFailure compilation failed." }
    $failureResult = dotnet (Join-Path $failureOutput 'Generated.dll') 2>&1
    if ($LASTEXITCODE -eq 0 -or ($failureResult -join "`n") -notmatch 'DISABLED_ERROR|Resume without an active error handler') { throw "AST $runtimeFailure did not raise the expected runtime error." }
}
$withOutput = Join-Path $output 'with'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'with-error.xps') -o $withOutput
if ($LASTEXITCODE -ne 0) { throw 'AST With compilation failed.' }
$withResult = dotnet (Join-Path $withOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($withResult -join "`n") -ne "`n") { throw 'AST With receiver/member access execution failed.' }
$unsupportedImplicitMember = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'implicit-member-error.xps') -o (Join-Path $output 'implicit-member-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($unsupportedImplicitMember -join "`n") -notmatch "Implicit member 'Name' requires an active With scope") { throw 'AST accepted an implicit member outside a With scope.' }
$nativeImageOutput = Join-Path $output 'xpimage-error'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'xpimage-error.xps') -o $nativeImageOutput
if ($LASTEXITCODE -ne 0) { throw 'AST did not compile XPImage natively.' }
$nativeImageResult = dotnet (Join-Path $nativeImageOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nativeImageResult -join "`n") -ne '2') { throw 'AST XPImage construction failed.' }
$optionBaseOutput = Join-Path $output 'option-base'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'option-base.xps') -o $optionBaseOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Option Base compilation failed.' }
$optionBaseResult = dotnet (Join-Path $optionBaseOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionBaseResult -join "`n") -ne "1`n3`n1`n3`n10`n30") { throw 'AST Option Base bounds or indexing returned the wrong elements.' }
$byteOutput = Join-Path $output 'variant-byte-conversion'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'variant-byte-conversion.xps') -o $byteOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Variant-to-Byte conversion compilation failed.' }
$byteResult = dotnet (Join-Path $byteOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($byteResult -join "`n") -ne '7') { throw 'AST Variant-to-Byte conversion execution failed.' }
$isNothingOutput = Join-Path $output 'is-nothing'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'is-nothing.xps') -o $isNothingOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Is Nothing compilation failed.' }
$isNothingResult = dotnet (Join-Path $isNothingOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($isNothingResult -join "`n") -ne 'IS_NOTHING_OK') { throw 'AST Is Nothing did not preserve Variant Nothing semantics.' }
$classOutput = Join-Path $output 'class-field'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-field.xps') -o $classOutput
if ($LASTEXITCODE -ne 0) { throw 'AST simple class field compilation failed.' }
$classResult = dotnet (Join-Path $classOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classResult -join "`n") -ne '7') { throw 'AST simple class field access produced the wrong result.' }
$inheritanceOutput = Join-Path $output 'class-inheritance-fields'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-inheritance-fields.xps') -o $inheritanceOutput
if ($LASTEXITCODE -ne 0) { throw 'AST class inheritance compilation failed.' }
$inheritanceResult = dotnet (Join-Path $inheritanceOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($inheritanceResult -join "`n") -ne "4`n5") { throw 'AST inherited class field access produced the wrong result.' }
$staticClassOutput = Join-Path $output 'static-class-instance'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-class-instance.xps') -o $staticClassOutput
if ($LASTEXITCODE -ne 0) { throw 'AST static class instance compilation failed.' }
$staticClassResult = dotnet (Join-Path $staticClassOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticClassResult -join "`n") -ne "1`n2") { throw 'AST static class instance lifetime produced the wrong result.' }
$classLifecycleOutput = Join-Path $output 'class-lifecycle'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'class-lifecycle.xps') -o $classLifecycleOutput
if ($LASTEXITCODE -ne 0) { throw 'AST class lifecycle compilation failed.' }
$classLifecycleResult = dotnet (Join-Path $classLifecycleOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($classLifecycleResult -join "`n") -ne 'LIFECYCLE_OK') { throw 'AST class Delete lifecycle did not clear the reference.' }
$optionalOrder = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-order-error.xps') -o (Join-Path $output 'optional-order-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($optionalOrder -join "`n") -notmatch 'cannot follow an Optional parameter') { throw 'AST accepted a required parameter after Optional.' }
$optionalDefault = Join-Path $output 'optional-default-name'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-default-name.xps') -o $optionalDefault
if ($LASTEXITCODE -ne 0) { throw 'AST Optional default-name binding failed.' }
$optionalDefaultResult = dotnet (Join-Path $optionalDefault 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalDefaultResult -join "`n") -ne '5') { throw 'AST Optional default-name binding produced the wrong result.' }
$optionalSelection = Join-Path $output 'optional-overload-selection'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-overload-selection.xps') -o $optionalSelection
if ($LASTEXITCODE -ne 0) { throw 'AST Optional overload selection failed.' }
$optionalSelectionResult = dotnet (Join-Path $optionalSelection 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($optionalSelectionResult -join "`n") -ne '2') { throw 'AST did not prefer the explicit overload over Optional forwarding.' }
$staticArray = Join-Path $output 'static-array'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-array.xps') -o $staticArray
if ($LASTEXITCODE -ne 0) { throw 'AST Static array compilation failed.' }
$staticArrayResult = dotnet (Join-Path $staticArray 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticArrayResult -join "`n") -ne "1`n2") { throw 'AST Static array lifetime did not persist across calls.' }
$staticList = Join-Path $output 'static-list'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-list.xps') -o $staticList
if ($LASTEXITCODE -ne 0) { throw 'AST Static List compilation failed.' }
$staticListResult = dotnet (Join-Path $staticList 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticListResult -join "`n") -ne "1`n2") { throw 'AST Static List lifetime did not persist across calls.' }
$staticVariant = Join-Path $output 'static-variant'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'static-variant.xps') -o $staticVariant
if ($LASTEXITCODE -ne 0) { throw 'AST Static Variant compilation failed.' }
$staticVariantResult = dotnet (Join-Path $staticVariant 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($staticVariantResult -join "`n") -ne "1`n2") { throw 'AST Static Variant lifetime did not persist across calls.' }
$parserError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'parser-error.xps') -o (Join-Path $output 'parser-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($parserError -join "`n") -notmatch 'Expected CloseParenToken') { throw 'AST parser diagnostic regression failed.' }
$noEntry = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'no-entry-error.xps') -o (Join-Path $output 'no-entry-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($noEntry -join "`n") -notmatch 'requires a Sub declaration') { throw 'AST no-entry diagnostic regression failed.' }
$nestedOutput = Join-Path $output 'nested-goto'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'nested-goto.xps') -o $nestedOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested GoTo compilation failed.' }
$nestedResult = dotnet (Join-Path $nestedOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedResult -join "`n") -ne "0`nNESTED_GOTO_OK") { throw 'AST nested GoTo branch/loop semantics failed.' }
$loopBodyOutput = Join-Path $output 'goto-loop-body'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-loop-body.xps') -o $loopBodyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GoTo inside For/ForAll loop compilation failed.' }
$loopBodyResult = dotnet (Join-Path $loopBodyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($loopBodyResult -join "`n") -ne "6`n15") { throw 'AST GoTo inside For/ForAll loop semantics failed.' }
$exitForAllOutput = Join-Path $output 'exit-forall'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'exit-forall.xps') -o $exitForAllOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Exit ForAll compilation failed.' }
if ((@(& dotnet (Join-Path $exitForAllOutput 'Generated.dll')) -join "`n") -ne '1') { throw 'AST Exit ForAll did not leave only the current ForAll.' }
$nestedLoopBodyOutput = Join-Path $output 'goto-nested-loop-body'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-loop-body.xps') -o $nestedLoopBodyOutput
if ($LASTEXITCODE -ne 0) { throw 'AST nested For/ForAll GoTo compilation failed.' }
$nestedLoopBodyResult = dotnet (Join-Path $nestedLoopBodyOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($nestedLoopBodyResult -join "`n") -ne '9') { throw 'AST nested For/ForAll GoTo semantics failed.' }
$gotoOutError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-out-forall.xps') -o (Join-Path $output 'goto-out-forall') 2>&1
if ($LASTEXITCODE -ne 2 -or ($gotoOutError -join "`n") -notmatch 'same loop scope') { throw 'AST accepted GoTo out of ForAll.' }
$nestedForAllError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-forall.xps') -o (Join-Path $output 'goto-nested-forall') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedForAllError -join "`n") -notmatch 'same loop scope') { throw 'AST accepted GoTo between nested ForAll scopes.' }
$gotoIntoForAllError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-into-forall-error.xps') -o (Join-Path $output 'goto-into-forall-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($gotoIntoForAllError -join "`n") -notmatch 'same loop scope') { throw 'AST did not diagnose GoTo entering ForAll clearly.' }
$nestedLoopError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-nested-for-error.xps') -o (Join-Path $output 'nested-for-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($nestedLoopError -join "`n") -notmatch 'same loop scope') { throw 'AST did not diagnose a GoTo entering a nested loop clearly.' }
$declarationJumpError = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-error.xps') -o (Join-Path $output 'declaration-jump-error') 2>&1
if ($LASTEXITCODE -ne 2 -or ($declarationJumpError -join "`n") -notmatch 'GoTo cannot jump over local declaration') { throw 'AST did not diagnose a GoTo jumping over a local declaration clearly.' }
$declarationHoistOutput = Join-Path $output 'goto-declaration-hoist'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'goto-declaration-hoist.xps') -o $declarationHoistOutput
if ($LASTEXITCODE -ne 0) { throw 'AST GoTo declaration hoisting compilation failed.' }
$declarationHoistResult = dotnet (Join-Path $declarationHoistOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($declarationHoistResult -join "`n") -ne 'GOTO_DECLARATION_HOIST_OK') { throw 'AST GoTo declaration hoisting execution failed.' }
$applicationNewLineOutput = Join-Path $output 'application-newline'
dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'application-newline.xps') -o $applicationNewLineOutput
if ($LASTEXITCODE -ne 0) { throw 'AST Application.NewLine compilation failed.' }
$applicationNewLineResult = dotnet (Join-Path $applicationNewLineOutput 'Generated.dll')
if ($LASTEXITCODE -ne 0 -or ($applicationNewLineResult -join "`n") -notmatch 'APPLICATION_NEWLINE_BEGIN' -or ($applicationNewLineResult -join "`n") -notmatch 'APPLICATION_NEWLINE_END') { throw 'AST Application.NewLine runtime regression failed.' }
$invalidDefault = dotnet $compiler ast-compile (Join-Path $PSScriptRoot 'optional-default-error.xps') -o (Join-Path $output 'invalid-default') 2>&1
if ($LASTEXITCODE -ne 2 -or ($invalidDefault -join "`n") -notmatch "Default value for Optional parameter 'value' is incompatible with its type") { throw 'AST invalid Optional default was not rejected by binding.' }
dotnet $compiler ast-compile $source -o $output
if ($LASTEXITCODE -ne 0) { throw 'AST CLI compilation failed.' }
$assembly = Join-Path $output 'Generated.dll'
$result = dotnet $assembly
if (($result -join "`n") -notmatch 'AST_OPTIONAL_CALL_OK') { throw 'AST omitted Optional arguments or ByRef default temporaries failed.' }
if (($result -join "`n") -notmatch 'AST_FUNCTION_RESULT_OK') { throw 'AST stored function result, fall-through or Exit Function failed.' }
if (($result -join "`n") -notmatch '(?m)^43\s*$') { throw 'AST procedure bodies or ByRef copy-back failed.' }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'AST_XPS_COMPILE_OK' -or ($result -join "`n") -notmatch 'AST_XPS_PRINT_OK' -or ($result -join "`n") -notmatch 'AST_XPS_IF_OK' -or ($result -join "`n") -notmatch '(?m)^1$' -or ($result -join "`n") -notmatch '(?m)^2$' -or ($result -join "`n") -notmatch '(?m)^3$' -or ($result -join "`n") -notmatch '(?m)^4$' -or ($result -join "`n") -notmatch 'AST_XPS_SELECT_OK' -or ($result -join "`n") -notmatch '(?m)^7$' -or ($result -join "`n") -notmatch '(?m)^8$') { throw 'AST CLI generated assembly did not run successfully.' }
Write-Host 'AST CLI compilation probe passed.'
