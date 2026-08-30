$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$cache = Get-Content (Join-Path $root 'src/XPScript.Compiler/NotesViewNavigatorCachePostProcessor.cs') -Raw
$history = Get-Content (Join-Path $root 'src/XPScript.Compiler/NotesViewNavigatorHistoryCapPostProcessor.cs') -Raw
$policy = Get-Content (Join-Path $root 'src/XPScript.Compiler/NotesViewNavigatorCachePolicyPostProcessor.cs') -Raw

$requiredCache = @(
    'CreateViewNav() => CreateViewNav(64)',
    'Math.Clamp(XPScriptRuntime.CInt(cacheSizeValue), 0, 512)',
    'Math.Clamp(value, 0, 512)',
    'requested = Math.Clamp(requested, 1, 512)',
    'if (_view.AutoUpdate)',
    '_view.NavigationGeneration'
)
foreach ($value in $requiredCache) {
    if (-not $cache.Contains($value)) { throw "Missing cache policy marker: $value" }
}
if (-not $history.Contains('MaxRetainedHistory')) { throw 'Missing retained-history cap.' }
if (-not $history.Contains('TrimHistory()')) { throw 'Missing retained-history trimming.' }
if (-not $policy.Contains('NavigationGeneration => 0')) { throw 'Missing stable navigator generation surface.' }
Write-Host 'NOTES-VIEW-NAVIGATOR-CACHE-POLICY=PASS'
