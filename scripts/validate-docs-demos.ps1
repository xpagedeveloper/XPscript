$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$referenceFiles = @(
    (Join-Path $root 'docs/language-reference.md'),
    (Join-Path $root 'docs/api-reference.md')
)

foreach ($file in $referenceFiles) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Reference file is missing: $file"
    }

    $lines = Get-Content -LiteralPath $file
    $tableRows = $lines | Where-Object {
        $_ -match '^\| ' -and
        $_ -notmatch '^\|---' -and
        $_ -notmatch '^\| (Command|Member|Rule/member|Command/option) \|'
    }

    foreach ($row in $tableRows) {
        $pipeCount = ([regex]::Matches($row, '\|')).Count
        if ($pipeCount -lt 6) {
            throw "Reference row does not contain the five required fields in ${file}: $row"
        }
        if ($row -notmatch '\[[^\]]+\.xps\]\((\.\./(?:samples|demo)/[^)]+\.xps)\)') {
            throw "Reference row is missing a complete .xps example link in ${file}: $row"
        }
    }

    $content = Get-Content -LiteralPath $file -Raw
    $links = [regex]::Matches($content, '\[[^\]]+\.xps\]\((\.\./(?:samples|demo)/[^)]+\.xps)\)')
    foreach ($match in $links) {
        $relative = $match.Groups[1].Value.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $file) $relative))
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "Reference example does not exist: $relative (from $(Split-Path -Leaf $file))"
        }
    }
}

$language = Get-Content -LiteralPath (Join-Path $root 'docs/language-reference.md') -Raw
$requiredLanguage = @(
    'Option Declare', 'Dim', 'Sub', 'Function', 'Exit Sub', 'Exit Function',
    'If', 'Select Case', 'ForAll', 'Do While', 'Do Until', 'While', 'Wend',
    'On Error', 'Resume', 'Err', 'Error$', 'Erl', 'With',
    'ReDim Preserve', 'LBound', 'UBound', 'ArraySplice',
    'CType', 'CVDate', 'IsScalar', 'IsUnknown',
    'LenB', 'InstrB', 'StrCompare', 'StrConv', 'StrToken', 'UChr', 'Uni',
    'RegexValidate', 'RegexMatch', 'Base64DecodeBinary', 'UrlEncode',
    'Date.Adjust', 'Date.Difference', 'Date.OSDateFormatting',
    'Input$', 'Lock', 'Unlock', 'ChDrive', 'Platform', 'Shell',
    'Declare Function', 'ReferenceNative', '--runtime', '--result-format'
)
foreach ($name in $requiredLanguage) {
    if ($language -notmatch [regex]::Escape($name)) {
        throw "Language reference is missing required command: $name"
    }
}

$api = Get-Content -LiteralPath (Join-Path $root 'docs/api-reference.md') -Raw
$requiredApi = @(
    'HttpClient.Get', 'HttpClient.Put', 'HttpClient.Patch', 'HttpClient.Delete',
    'JsonObject.Set', 'JsonArray.Add', 'XPDBSQLite.Execute', 'XPDbMsSql.Query',
    'HTTPDBSupabase', 'HTTPDBDominoRest',
    'XPAi.Complete', 'AutoExecuteTools', 'MaxToolIterations', 'NewRequest',
    'AITool.AddFunction', 'AITool.AddParameter', 'AIToolCall',
    'UIForm', 'AddLookupField', 'UIListView', 'AddRowButton',
    'FromBody', 'Response.Created', 'Session.Authenticate', 'RequestScope.Add'
)
foreach ($name in $requiredApi) {
    if ($api -notmatch [regex]::Escape($name)) {
        throw "Runtime API reference is missing required member: $name"
    }
}

$demoRoot = Join-Path $root 'demo'
$demoReadmePath = Join-Path $demoRoot 'README.md'
if (-not (Test-Path -LiteralPath $demoReadmePath -PathType Leaf)) {
    throw 'demo/README.md is missing.'
}
$demoReadme = Get-Content -LiteralPath $demoReadmePath -Raw
$demoFiles = Get-ChildItem -LiteralPath $demoRoot -Recurse -File -Filter '*.xps'
if ($demoFiles.Count -eq 0) {
    throw 'No demo .xps files were found.'
}
foreach ($demo in $demoFiles) {
    $relative = [IO.Path]::GetRelativePath($demoRoot, $demo.FullName).Replace('\', '/')
    if ($demoReadme -notmatch [regex]::Escape($relative)) {
        throw "Demo is not listed in demo/README.md: $relative"
    }
}

Write-Host "DOC-REFERENCE-FILES=$($referenceFiles.Count)"
Write-Host "DEMO-XPS-FILES=$($demoFiles.Count)"
Write-Host 'DOCS-DEMO-VALIDATION=OK'
