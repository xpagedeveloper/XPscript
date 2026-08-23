$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$referenceFiles = @(
    (Join-Path $root 'docs/language-reference.md'),
    (Join-Path $root 'docs/file-io-reference.md'),
    (Join-Path $root 'docs/native-interop-reference.md'),
    (Join-Path $root 'docs/cli-reference.md'),
    (Join-Path $root 'docs/application-reference.md'),
    (Join-Path $root 'docs/desktop-ui-reference.md'),
    (Join-Path $root 'docs/database-ui-datasources.md'),
    (Join-Path $root 'docs/api-reference.md')
)
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($file in $referenceFiles) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        $errors.Add("Reference file is missing: $file")
        continue
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
            $errors.Add("Reference row does not contain the five required fields in ${file}: $row")
            continue
        }
        if ($row -notmatch '\[[^\]]+\.xps\]\((\.\./(?:samples|demo)/[^)]+\.xps)\)') {
            $errors.Add("Reference row is missing a complete .xps example link in ${file}: $row")
        }
    }

    $content = Get-Content -LiteralPath $file -Raw
    $links = [regex]::Matches($content, '\[[^\]]+\.xps\]\((\.\./(?:samples|demo)/[^)]+\.xps)\)')
    foreach ($match in $links) {
        $relative = $match.Groups[1].Value.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $file) $relative))
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            $errors.Add("Reference example does not exist: $relative (from $(Split-Path -Leaf $file))")
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
    'Platform', 'Shell'
)
foreach ($name in $requiredLanguage) {
    if ($language -notmatch [regex]::Escape($name)) {
        $errors.Add("Language reference is missing required command: $name")
    }
}

$fileIo = Get-Content -LiteralPath (Join-Path $root 'docs/file-io-reference.md') -Raw
$requiredFileIo = @(
    'FreeFile', 'Open', 'Charset', 'Close', 'Reset', 'Print #', 'Write #', 'Line Input #',
    'Input #', 'Input$', 'EOF', 'LOF', 'Seek', 'Loc', 'Put', 'Get', 'Lock', 'Unlock',
    'FileLen', 'FileDateTime', 'GetFileAttr', 'SetFileAttr', 'FileCopy', 'Kill',
    'Name', 'MkDir', 'RmDir', 'ChDir', 'ChDrive', 'Dir'
)
foreach ($name in $requiredFileIo) {
    if ($fileIo -notmatch [regex]::Escape($name)) {
        $errors.Add("File I/O reference is missing required command: $name")
    }
}

$interop = Get-Content -LiteralPath (Join-Path $root 'docs/native-interop-reference.md') -Raw
$requiredInterop = @(
    'Declare Function', 'Declare Sub', 'Alias',
    'WindowsLib', 'LinuxLib', 'MacOSLib',
    'WindowsX64Lib', 'WindowsArm64Lib', 'LinuxX64Lib', 'LinuxArm64Lib', 'MacOSX64Lib', 'MacOSArm64Lib',
    'WindowsAlias', 'LinuxAlias', 'MacOSAlias',
    'WindowsX64Alias', 'WindowsArm64Alias', 'LinuxX64Alias', 'LinuxArm64Alias', 'MacOSX64Alias', 'MacOSArm64Alias',
    'Reference', 'ReferenceNative', 'Runtime'
)
foreach ($name in $requiredInterop) {
    if ($interop -notmatch [regex]::Escape($name)) {
        $errors.Add("Interop reference is missing required selector/directive: $name")
    }
}

$cli = Get-Content -LiteralPath (Join-Path $root 'docs/cli-reference.md') -Raw
$requiredCli = @(
    'xpscriptc', 'run', '-o', '--runtime', '--framework-dependent', '--result-format',
    'xpscript web', '--root', '--default-document', '--address', '--bind', '--port', '--host', '--allowed-host',
    '--https-cert', '--https-cert-password-env', '--protocols', '--health', '--metrics', '--sessions',
    '--session-cookie', '--session-timeout-seconds', '--session-same-site', '--session-secure', '--operational-external',
    '--structured-log', '--static-files', '--static-max-bytes', '--config',
    'xpscript fastcgi', '--listen', '--unix-socket', 'xpscript compile', '--target webiis'
)
foreach ($name in $requiredCli) {
    if ($cli -notmatch [regex]::Escape($name)) {
        $errors.Add("CLI reference is missing required command/option: $name")
    }
}

$application = Get-Content -LiteralPath (Join-Path $root 'docs/application-reference.md') -Raw
foreach ($name in @('Application.ArgCount','Application.ExecutablePath','Application.ExecutableFileName','Application.TempFolder','Application.Path','Application.FileName','Application.State','Process.State','Session.State','Request.State')) {
    if ($application -notmatch [regex]::Escape($name)) {
        $errors.Add("Application reference is missing required member: $name")
    }
}

$desktop = Get-Content -LiteralPath (Join-Path $root 'docs/desktop-ui-reference.md') -Raw
foreach ($name in @('MsgBox','ShowDialog','OpenFileDialog','LoadFileDialog','SaveFileDialog')) {
    if ($desktop -notmatch [regex]::Escape($name)) {
        $errors.Add("Desktop UI reference is missing required command: $name")
    }
}

$databaseUi = Get-Content -LiteralPath (Join-Path $root 'docs/database-ui-datasources.md') -Raw
$requiredDatabaseUi = @(
    'XPDBSQLite.QueryArray', 'XPDBSQLite.GetRow', 'XPDBSQLite.SaveRow', 'XPDBSQLite.Attachments',
    'XPDbMsSql.QueryArray', 'XPDbMsSql.GetRow', 'XPDbMsSql.SaveRow', 'XPDbMsSql.Attachments',
    'HTTPDBSupabase.QueryArray', 'HTTPDBSupabase.GetRow', 'HTTPDBSupabase.SaveRow', 'HTTPDBSupabase.SetAttachmentBucket', 'HTTPDBSupabase.Attachments',
    'HTTPDBDominoRest.GetViewArray', 'HTTPDBDominoRest.QueryArray', 'HTTPDBDominoRest.GetRow', 'HTTPDBDominoRest.SaveRow', 'HTTPDBDominoRest.Attachments',
    'AttachmentCollection.List', 'AttachmentCollection.GetMetadata', 'AttachmentCollection.FindByName',
    'AttachmentCollection.Save', 'AttachmentCollection.SaveAs', 'AttachmentCollection.Get', 'AttachmentCollection.GetAll', 'AttachmentCollection.Delete',
    'createdBy', 'immutable', 'new `attachmentId`',
    'UIListView.BindData', 'UIForm.BindData', 'native database columns/items', '64 MiB'
)
foreach ($name in $requiredDatabaseUi) {
    if ($databaseUi -notmatch [regex]::Escape($name)) {
        $errors.Add("Database UI data-source reference is missing required member/contract: $name")
    }
}

$api = Get-Content -LiteralPath (Join-Path $root 'docs/api-reference.md') -Raw
$requiredApi = @(
    'HttpClient.Get', 'HttpClient.Put', 'HttpClient.Patch', 'HttpClient.Delete',
    'JsonObject.Set', 'JsonArray.Add', 'XPDBSQLite.Execute', 'XPDbMsSql.Query',
    'HTTPDBSupabase', 'HTTPDBDominoRest',
    '`Complete`', 'AutoExecuteTools', 'MaxToolIterations', 'NewRequest',
    '`AddFunction`', '`AddParameter`', 'AIToolCall',
    'UIForm', 'AddLookupField', 'UIListView', 'AddRowButton',
    'FromBody', 'Response.Created', 'Session.Authenticate', 'RequestScope.Add'
)
foreach ($name in $requiredApi) {
    if ($api -notmatch [regex]::Escape($name)) {
        $errors.Add("Runtime API reference is missing required member: $name")
    }
}

$demoRoot = Join-Path $root 'demo'
$demoReadmePath = Join-Path $demoRoot 'README.md'
if (-not (Test-Path -LiteralPath $demoReadmePath -PathType Leaf)) {
    $errors.Add('demo/README.md is missing.')
} else {
    $demoReadme = Get-Content -LiteralPath $demoReadmePath -Raw
    $demoFiles = Get-ChildItem -LiteralPath $demoRoot -Recurse -File -Filter '*.xps'
    if ($demoFiles.Count -eq 0) {
        $errors.Add('No demo .xps files were found.')
    }
    foreach ($demo in $demoFiles) {
        $relative = [IO.Path]::GetRelativePath($demoRoot, $demo.FullName).Replace('\', '/')
        if ($demoReadme -notmatch [regex]::Escape($relative)) {
            $errors.Add("Demo is not listed in demo/README.md: $relative")
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "DOCS-DEMO-VALIDATION-ERRORS=$($errors.Count)"
    foreach ($errorMessage in $errors) {
        Write-Host "ERROR: $errorMessage"
    }
    throw "Documentation/demo validation failed with $($errors.Count) error(s)."
}

Write-Host "DOC-REFERENCE-FILES=$($referenceFiles.Count)"
Write-Host "DEMO-XPS-FILES=$($demoFiles.Count)"
Write-Host 'DOCS-DEMO-VALIDATION=OK'
