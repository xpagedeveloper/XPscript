param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(ValueFromRemainingArguments = $true)]
    [AllowEmptyString()]
    [string[]]$Rest
)

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("COUNT=$($Rest.Count)")
for ($i = 0; $i -lt $Rest.Count; $i++) {
    $lines.Add("ARG${i}=$($Rest[$i])")
}

[System.IO.File]::WriteAllLines(
    [System.IO.Path]::GetFullPath($OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))
