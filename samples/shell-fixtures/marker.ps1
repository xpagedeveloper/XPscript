param([string]$OutputPath)
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($OutputPath), "PS1=OK`n", [System.Text.UTF8Encoding]::new($false))
