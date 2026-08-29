param(
    [string]$FixturePath = 'samples/xpscript-notes-runtime-fixture.dxl'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $FixturePath -PathType Leaf)) {
    throw "Notes runtime fixture does not exist: $FixturePath"
}

[xml]$dxl = Get-Content -Raw -LiteralPath $FixturePath

$expectedNamespace = 'http://www.lotus.com/dxl'
if ($dxl.DocumentElement.LocalName -ne 'database') { throw "DXL root must be <database>, got <$($dxl.DocumentElement.LocalName)>" }
if ($dxl.DocumentElement.NamespaceURI -ne $expectedNamespace) { throw "Unexpected DXL namespace: $($dxl.DocumentElement.NamespaceURI)" }

$ns = [System.Xml.XmlNamespaceManager]::new($dxl.NameTable)
$ns.AddNamespace('dxl', $expectedNamespace)

function Require-SingleNode {
    param([Parameter(Mandatory)] [string]$XPath, [Parameter(Mandatory)] [string]$Label)
    $nodes = @($dxl.SelectNodes($XPath, $ns))
    if ($nodes.Count -ne 1) { throw "$Label must occur exactly once in the DXL fixture, found $($nodes.Count)." }
    return $nodes[0]
}

foreach ($formName in @('XPScriptTest', 'XPScriptCWFValid', 'XPScriptCWFErrors')) {
    [void](Require-SingleNode -XPath "/dxl:database/dxl:form[@name='$formName']" -Label "Form $formName")
}

[void](Require-SingleNode -XPath "/dxl:database/dxl:view[@name='XPScriptTestView']" -Label 'View XPScriptTestView')
[void](Require-SingleNode -XPath "/dxl:database/dxl:view[@name='XPScriptHierarchyView']" -Label 'View XPScriptHierarchyView')
[void](Require-SingleNode -XPath "/dxl:database/dxl:agent[@name='XPScriptTestAgent']" -Label 'Agent XPScriptTestAgent')

[void](Require-SingleNode -XPath "/dxl:database/dxl:form[@name='XPScriptTest']//dxl:field[@name='Subject']" -Label 'XPScriptTest.Subject field')
[void](Require-SingleNode -XPath "/dxl:database/dxl:form[@name='XPScriptTest']//dxl:field[@name='XPScriptMarker']" -Label 'XPScriptTest.XPScriptMarker field')
[void](Require-SingleNode -XPath "/dxl:database/dxl:form[@name='XPScriptTest']//dxl:field[@name='Body' and @type='richtext']" -Label 'XPScriptTest.Body rich-text field')

$defaultFormula = Require-SingleNode -XPath "/dxl:database/dxl:form[@name='XPScriptCWFValid']//dxl:field[@name='CWFDefault']/dxl:code[@event='defaultvalue']/dxl:formula" -Label 'CWFDefault default formula'
if ($defaultFormula.InnerText.Trim() -ne '"DEFAULT-VALUE"') { throw "CWFDefault formula must return DEFAULT-VALUE, got: $($defaultFormula.InnerText.Trim())" }

foreach ($fieldName in @('CWFErrorOne', 'CWFErrorTwo')) {
    $validation = Require-SingleNode -XPath "/dxl:database/dxl:form[@name='XPScriptCWFErrors']//dxl:field[@name='$fieldName']/dxl:code[@event='inputvalidation']/dxl:formula" -Label "$fieldName validation formula"
    $expectedFailure = '@Failure("' + $fieldName + ' failed")'
    if (-not $validation.InnerText.Contains($expectedFailure)) { throw "$fieldName validation formula does not contain the expected failure marker." }
}

foreach ($viewName in @('XPScriptTestView', 'XPScriptHierarchyView')) {
    $selection = Require-SingleNode -XPath "/dxl:database/dxl:view[@name='$viewName']/dxl:code[@event='selection']/dxl:formula" -Label "$viewName selection formula"
    if ($selection.InnerText.Trim() -ne 'Form = "XPScriptTest"') { throw "$viewName selection formula changed unexpectedly: $($selection.InnerText.Trim())" }
}

$subjectColumn = Require-SingleNode -XPath "/dxl:database/dxl:view[@name='XPScriptTestView']/dxl:column[@itemname='Subject']" -Label 'XPScriptTestView Subject column'
if ($subjectColumn.GetAttribute('sort') -ne 'ascending') { throw 'XPScriptTestView Subject column must remain ascending for deterministic key tests.' }

$groupColumn = Require-SingleNode -XPath "/dxl:database/dxl:view[@name='XPScriptHierarchyView']/dxl:column[@itemname='XPScriptGroup']" -Label 'XPScriptHierarchyView group column'
if ($groupColumn.GetAttribute('categorized') -ne 'true' -or $groupColumn.GetAttribute('sort') -ne 'ascending') { throw 'XPScriptHierarchyView group column must remain categorized and ascending.' }

$agentScript = Require-SingleNode -XPath "/dxl:database/dxl:agent[@name='XPScriptTestAgent']/dxl:code[@event='action']/dxl:lotusscript" -Label 'XPScriptTestAgent LotusScript body'
if ($agentScript.InnerText -notmatch 'XPSCRIPT-AGENT=OK') { throw 'XPScriptTestAgent is missing the no-context success marker.' }
if ($agentScript.InnerText -notmatch 'XPSCRIPT-AGENT-DOC=') { throw 'XPScriptTestAgent is missing the document-context marker.' }

$expectedDocuments = @(
    @{ Subject = 'Alpha'; Marker = 'fixture-alpha'; Group = 'Group A'; TextList = @('one', 'two', 'three'); Number = '12.5' },
    @{ Subject = 'Beta'; Marker = 'fixture-beta'; Group = 'Group A'; TextList = @('red', 'green', 'blue'); Number = '99' },
    @{ Subject = 'Gamma'; Marker = 'fixture-gamma'; Group = 'Group B'; TextList = @('north', 'south'); Number = '7' }
)

foreach ($expected in $expectedDocuments) {
    $subject = $expected.Subject
    $document = Require-SingleNode -XPath "/dxl:database/dxl:document[@form='XPScriptTest'][dxl:item[@name='Subject']/dxl:text='$subject']" -Label "Fixture document $subject"
    $markerNode = $document.SelectSingleNode("dxl:item[@name='XPScriptMarker']/dxl:text", $ns)
    if ($null -eq $markerNode -or $markerNode.InnerText -ne $expected.Marker) { throw "Fixture document $subject has the wrong XPScriptMarker." }
    $groupNode = $document.SelectSingleNode("dxl:item[@name='XPScriptGroup']/dxl:text", $ns)
    if ($null -eq $groupNode -or $groupNode.InnerText -ne $expected.Group) { throw "Fixture document $subject has the wrong XPScriptGroup." }
    $numberNode = $document.SelectSingleNode("dxl:item[@name='NumberValue']/dxl:number", $ns)
    if ($null -eq $numberNode -or $numberNode.InnerText -ne $expected.Number) { throw "Fixture document $subject has the wrong NumberValue." }
    $textNodes = @($document.SelectNodes("dxl:item[@name='TextList']/dxl:textlist/dxl:text", $ns))
    $actualTextList = @($textNodes | ForEach-Object { $_.InnerText })
    if ($actualTextList.Count -ne $expected.TextList.Count) { throw "Fixture document $subject has the wrong TextList length." }
    for ($i = 0; $i -lt $expected.TextList.Count; $i++) {
        if ($actualTextList[$i] -ne $expected.TextList[$i]) { throw "Fixture document $subject has an unexpected TextList value at index $i." }
    }
    $richText = $document.SelectSingleNode("dxl:item[@name='Body']/dxl:richtext", $ns)
    if ($null -eq $richText) { throw "Fixture document $subject must contain a rich-text Body item." }
}

$fixtureDocuments = @($dxl.SelectNodes("/dxl:database/dxl:document[@form='XPScriptTest']", $ns))
if ($fixtureDocuments.Count -ne $expectedDocuments.Count) { throw "Expected exactly $($expectedDocuments.Count) XPScriptTest fixture documents, found $($fixtureDocuments.Count)." }

Write-Host "NOTES-RUNTIME-FIXTURE=OK ($FixturePath)"
