$ErrorActionPreference = 'Stop'

$payload = [ordered]@{
    count = $args.Count
    values = @($args)
}

$payload | ConvertTo-Json -Compress | Set-Content -LiteralPath './shell-args-result.json' -Encoding utf8
