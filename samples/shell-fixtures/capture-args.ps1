if ($args.Count -lt 1) {
    throw 'capture-args.ps1 requires an output path.'
}

$OutputPath = [string]$args[0]
$values = if ($args.Count -gt 1) { @($args[1..($args.Count - 1)]) } else { @() }

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("COUNT=$($values.Count)")
for ($i = 0; $i -lt $values.Count; $i++) {
    $lines.Add("ARG${i}=$($values[$i])")
}

[System.IO.File]::WriteAllLines(
    [System.IO.Path]::GetFullPath($OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))
