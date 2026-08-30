$ErrorActionPreference = 'Stop'
$cap = 2048
$cacheSize = 512
$rows = New-Object System.Collections.Generic.List[int]
$current = -1
for ($absolute = 0; $absolute -lt 32000; $absolute++) {
    if ($current + 1 -ge $rows.Count) {
        for ($i = 0; $i -lt $cacheSize -and ($absolute + $i) -lt 32000; $i++) { $rows.Add($absolute + $i) }
    }
    $current++
    if ($current -gt $cap) {
        $remove = $current - $cap
        $rows.RemoveRange(0, $remove)
        $current -= $remove
    }
    if ($rows.Count -gt ($cap + $cacheSize)) { throw "History cache exceeded bound: $($rows.Count)" }
}
Write-Host "NOTES-VIEW-CACHE-HISTORY-MODEL=PASS MAX=$($cap + $cacheSize)"
