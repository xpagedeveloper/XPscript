param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
$docs = @(
    'docs/api-reference.md',
    'docs/xpspreadsheet.md',
    'docs/native-xml.md',
    'docs/systeminventory.md',
    'docs/ai-tools-sessions.md',
    'docs/uiform.md',
    'docs/uiform-fields.md',
    'docs/uiform-media.md'
)
$corpus = @(rg --files -g '*.xps' -g '*.cs' -g '*.md' -g '!out/**' -g '!**/bin/**' -g '!**/obj/**' -g '!graphify-out/**' | ForEach-Object { Join-Path $Root $_ })
$corpusText = (($corpus | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n")
$fixtureFiles = @(rg --files tests samples demo -g '*.xps' 2>$null | ForEach-Object { Join-Path $Root $_ })
$fixtureText = (($fixtureFiles | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n")
$members = [System.Collections.Generic.List[object]]::new()
foreach ($relative in $docs) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\|\s*`(?<member>[A-Za-z_][A-Za-z0-9_. ]*)`\s*\|' -or
            $line -match '^\s*-\s*`(?<member>[A-Za-z_][A-Za-z0-9_. ]*)`') {
            $member = $Matches.member.Trim()
            if ($member -in @('FromRoute','FromQuery','FromHeader','FromCookie')) { continue }
            if ($member -notin @('Member','Syntax','Parameters','Description','Example') -and
                $member -notmatch '^(Get|Set|Add|Remove|Clear|Open|Save|Close|From|To|New|Create|Delete|Update|Query|List|Find|Run|Execute|Complete|Stream|Cancel|Dispose|Bind|Show|Cell|Range|Worksheet|AutoFilter|Input|Output|Name|Path|Index|Address|Value|Text|Formula|Type|Title|Description|Timeout|Model|Endpoint|Provider|Session|Has|Is|Can|Created|XPScript|Used|Column|Row|Font|Background|Foreground|Horizontal|Vertical|Wrap|Border|Number|Auto|Grid|Dirty|Data|Body|Status|Content|Json|Root|Count|Length|Width|Height|Format|Filter|Sort|Select|Function|Parameter|Tool|Request|Response|Property|Options|Headers|Messages|Option|Temperature|Max|Certificate|Throw|Collect|System|User|EndpointPath|Authorization|Encode|Decode|Version|Read|Write|Lock|Unlock|Copy|Move|File|Dir|Date|Time|Month|Week|Currency|Decimal|Radio|ListBox|Select|Lookup|AutoComplete|Required|Placeholder|Tooltip|Validation|Schema|Field|Fields|Map|Document|YouTube|Separator|Spacer|Navigation|Selected|RowAction|Dialog)') {
                continue
            }
            $members.Add([pscustomobject]@{ Doc = $relative; Member = $member })
        }
    }
}
$unique = $members | Sort-Object Member,Doc -Unique
$missing = @()
$uninvoked = @()
foreach ($item in $unique) {
    $needle = [regex]::Escape(($item.Member -split '\.')[-1])
    $found = $false
    $found = $corpusText -match "(?i)\b$needle\b"
    if (-not $found) { $missing += $item }
    if ($fixtureText -notmatch "(?i)\b$needle\b") { $uninvoked += $item }
}
Write-Host ("API_SURFACE_DOCUMENTED_MEMBERS=" + $unique.Count)
Write-Host ("API_SURFACE_MEMBER_REFERENCES_FOUND=" + ($unique.Count - $missing.Count))
Write-Host ("API_SURFACE_MEMBER_REFERENCES_MISSING=" + $missing.Count)
foreach ($item in $missing) { Write-Host ("API_SURFACE_MISSING=" + $item.Doc + ':' + $item.Member) }
Write-Host ("API_SURFACE_MEMBERS_WITHOUT_XPS_FIXTURE_REFERENCE=" + $uninvoked.Count)
foreach ($item in $uninvoked) { Write-Host ("API_SURFACE_UNINVOKED=" + $item.Doc + ':' + $item.Member) }
if ($missing.Count -gt 0 -or $uninvoked.Count -gt 0) { exit 1 }
Write-Host 'API_SURFACE_AUDIT_OK'
