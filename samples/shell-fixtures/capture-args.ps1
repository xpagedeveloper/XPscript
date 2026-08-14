param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$OutputPath,

    [Parameter(Position = 1)]
    [AllowEmptyString()]
    [string]$Arg0,

    [Parameter(Position = 2)]
    [AllowEmptyString()]
    [string]$Arg1,

    [Parameter(Position = 3)]
    [AllowEmptyString()]
    [string]$Arg2,

    [Parameter(Position = 4)]
    [AllowEmptyString()]
    [string]$Arg3
)

$values = @($Arg0, $Arg1, $Arg2, $Arg3)
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("COUNT=$($values.Count)")
for ($i = 0; $i -lt $values.Count; $i++) {
    $lines.Add("ARG${i}=$($values[$i])")
}

[System.IO.File]::WriteAllLines(
    [System.IO.Path]::GetFullPath($OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))
