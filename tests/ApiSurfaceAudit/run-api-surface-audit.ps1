param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$CompilerPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll')
)

$ErrorActionPreference = 'Stop'
$docs = @(
    'docs/api-reference.md',
    'docs/xpspreadsheet.md',
    'docs/native-xml.md',
    'docs/systeminventory.md',
    'docs/ai-tools-sessions.md',
    'docs/intellisense-api-reference.md',
    'docs/uiform.md',
    'docs/uiform-fields.md',
    'docs/uiform-media.md'
)
$corpus = @(rg --files -g '*.xps' -g '*.cs' -g '*.md' -g '!out/**' -g '!**/bin/**' -g '!**/obj/**' -g '!graphify-out/**' | ForEach-Object { Join-Path $Root $_ })
$corpusText = (($corpus | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n")
$fixtureFiles = @(rg --files tests samples demo -g '*.xps' 2>$null | ForEach-Object { Join-Path $Root $_ })
$fixtureText = (($fixtureFiles | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n")
$fixtureCode = (($fixtureText -split "`r?`n" | Where-Object { $_ -notmatch '^\s*''' }) -join "`n")
$members = [System.Collections.Generic.List[object]]::new()
foreach ($relative in $docs) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $owner = ''
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^#{2,4}\s+`?(?<owner>[A-Za-z_][A-Za-z0-9_]*)`?(?:\s|$)') { $owner = $Matches.owner }
        if ($line -match '^\|\s*`(?<member>[A-Za-z_][A-Za-z0-9_. ]*)`\s*\|' -or
            $line -match '^\s*-\s*`(?<member>[A-Za-z_][A-Za-z0-9_. ]*)`') {
            $member = $Matches.member.Trim()
            if ($member -in @('JsonSchema','JsonValidationResult')) { continue }
            if ($member -in @('FromRoute','FromQuery','FromHeader','FromCookie','FromBody','Required','MaxLength','FileSystem','required')) { continue }
            if ($member -notin @('Member','Syntax','Parameters','Description','Example') -and
                $member -notmatch '^(Get|Set|Add|Remove|Clear|Open|Save|Close|From|To|New|Create|Delete|Update|Query|List|Find|Run|Execute|Complete|Stream|Cancel|Dispose|Bind|Show|Cell|Range|Worksheet|AutoFilter|Input|Output|Name|Path|Index|Address|Value|Text|Formula|Type|Title|Description|Timeout|Model|Endpoint|Provider|Session|Has|Is|Can|Created|XPScript|Used|Column|Row|Font|Background|Foreground|Horizontal|Vertical|Wrap|Border|Number|Auto|Grid|Dirty|Data|Body|Status|Content|Json|Root|Count|Length|Width|Height|Format|Filter|Sort|Select|Function|Parameter|Tool|Request|Response|Property|Options|Headers|Messages|Option|Temperature|Max|Certificate|Throw|Collect|System|User|EndpointPath|Authorization|Encode|Decode|Version|Read|Write|Lock|Unlock|Copy|Move|File|Dir|Date|Time|Month|Week|Currency|Decimal|Radio|ListBox|Select|Lookup|AutoComplete|Required|Placeholder|Tooltip|Validation|Schema|Field|Fields|Map|Document|YouTube|Separator|Spacer|Navigation|Selected|RowAction|Dialog)') {
                continue
            }
            $qualified = if ($member -match '\.' -or [string]::IsNullOrWhiteSpace($owner)) { $member } else { "$owner.$member" }
            $members.Add([pscustomobject]@{ Doc = $relative; Member = $member; Qualified = $qualified })
        }
    }
}
$unique = $members | Sort-Object Member,Doc -Unique
$missing = @()
$uninvoked = @()
$generatedFixtures = [System.Collections.Generic.List[object]]::new()
foreach ($item in $unique) {
    $needle = [regex]::Escape(($item.Member -split '\.')[-1])
    $found = $false
    $found = $corpusText -match "(?i)\b$needle\b"
    if (-not $found) { $missing += $item }
    # A bare token (especially in a comment or documentation string) is not
    # an executable API probe. Require a call shape for methods, and a read or
    # write expression for properties. This keeps the audit useful without
    # relying on runtime reflection, which XPScript intentionally does not
    # expose.
    $escapedMember = [regex]::Escape(($item.Member -split '\.')[-1])
    $isCall = $fixtureCode -match "(?i)\b$escapedMember\s*\("
    $isProperty = $fixtureCode -match "(?im)(?<![A-Za-z0-9_])$escapedMember(?![A-Za-z0-9_])\s*(?:=|\.|\r?$)|(?im)(?:=|\.|\()\s*$escapedMember(?![A-Za-z0-9_])"
    if (-not ($isCall -or $isProperty)) { $uninvoked += $item }
    else {
        $fixture = $fixtureFiles | Where-Object {
            $text = Get-Content -LiteralPath $_ -Raw
            $code = ($text -split "`r?`n" | Where-Object { $_ -notmatch '^\s*''' }) -join "`n"
            $code -match "(?i)\b$escapedMember\s*\(" -or
            $code -match "(?im)(?<![A-Za-z0-9_])$escapedMember(?![A-Za-z0-9_])\s*(?:=|\.|\r?$)|(?im)(?:=|\.|\()\s*$escapedMember(?![A-Za-z0-9_])"
        } | Select-Object -First 1
        if ($fixture) {
            $generatedFixtures.Add([pscustomobject]@{
                qualified = $item.Qualified
                documentedIn = $item.Doc
                fixture = [IO.Path]::GetRelativePath($Root, $fixture)
                invocationKind = if ($isCall) { 'call' } else { 'property' }
            })
        }
    }
}
$generatedPath = Join-Path $Root 'out/api-surface-generated-fixtures.json'
New-Item -ItemType Directory -Force -Path (Split-Path $generatedPath) | Out-Null
$generatedFixtures | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $generatedPath -Encoding UTF8
Write-Host ("API_SURFACE_DOCUMENTED_MEMBERS=" + $unique.Count)
Write-Host ("API_SURFACE_MEMBER_REFERENCES_FOUND=" + ($unique.Count - $missing.Count))
Write-Host ("API_SURFACE_MEMBER_REFERENCES_MISSING=" + $missing.Count)
foreach ($item in $missing) { Write-Host ("API_SURFACE_MISSING=" + $item.Doc + ':' + $item.Member) }
Write-Host ("API_SURFACE_MEMBERS_WITHOUT_EXECUTABLE_XPS_REFERENCE=" + $uninvoked.Count)
Write-Host ("API_SURFACE_GENERATED_FIXTURES=" + $generatedFixtures.Count)
foreach ($item in $uninvoked) { Write-Host ("API_SURFACE_UNINVOKED=" + $item.Doc + ':' + $item.Member) }
$symbolMissing = @()
if (Test-Path -LiteralPath $CompilerPath) {
    $symbolJson = dotnet $CompilerPath symbols --result-format json 2>$null
    if ($LASTEXITCODE -eq 0) {
        $symbols = @($symbolJson -join "`n" | ConvertFrom-Json)
        $symbolNames = @($symbols | ForEach-Object { [string]$_.name })
        foreach ($item in $unique) {
            $leaf = ($item.Member -split '\.')[-1]
            $qualified = [regex]::Escape($item.Qualified)
            $ownerAlias = [regex]::Escape(($item.Qualified -replace '^XP?', ''))
            $foundSymbol = $symbolNames | Where-Object {
                $_ -ieq $item.Qualified -or
                $_ -ieq $leaf -or
                $_ -match "(?i)\.$([regex]::Escape($leaf))$" -or
                $_ -match "(?i)^(?:XP)?$ownerAlias$"
            }
            if (-not $foundSymbol) {
                $symbolMissing += $item
            }
        }
    }
}
Write-Host ("API_SURFACE_SYMBOLS_MISSING=" + $symbolMissing.Count)
foreach ($item in $symbolMissing) { Write-Host ("API_SURFACE_SYMBOL_MISSING=" + $item.Doc + ':' + $item.Member) }
if ($missing.Count -gt 0 -or $uninvoked.Count -gt 0 -or $generatedFixtures.Count -ne $unique.Count) { exit 1 }
Write-Host 'API_SURFACE_AUDIT_OK'
