$ErrorActionPreference = 'Stop'
$runtime = Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'src/XPScript.Compiler/NotesRuntimeSource.cs') -Raw
$cache = $runtime.IndexOf('NotesViewNavigatorCachePostProcessor.Apply(')
$policy = $runtime.IndexOf('NotesViewNavigatorCachePolicyPostProcessor.Apply(')
$history = $runtime.IndexOf('NotesViewNavigatorHistoryCapPostProcessor.Apply(')
if ($history -lt 0 -or $cache -lt 0 -or $policy -lt 0) { throw 'Missing NotesView cache postprocessor.' }
if (-not ($history -lt $cache -and $cache -lt $policy)) { throw 'NotesView cache postprocessor nesting order is invalid.' }
Write-Host 'NOTES-VIEW-CACHE-POSTPROCESSOR-ORDER=PASS'
