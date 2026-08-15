param(
    [Parameter(Mandatory = $false)]
    [string]$Owner = "xpagedeveloper",

    [Parameter(Mandatory = $false)]
    [string]$Repository = "XPscript",

    [Parameter(Mandatory = $false)]
    [string]$Token = $env:GITHUB_TOKEN,

    [Parameter(Mandatory = $false)]
    [switch]$Apply,

    [Parameter(Mandatory = $false)]
    [switch]$RemoveLegacyBlockUpdate
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "A GitHub token is required. Pass -Token or set GITHUB_TOKEN. The token needs Administration: write for the repository."
}

$headers = @{
    Accept = "application/vnd.github+json"
    Authorization = "Bearer $Token"
    "X-GitHub-Api-Version" = "2026-03-10"
}

$apiBase = "https://api.github.com/repos/$Owner/$Repository"
$rulesetName = "main-protection"
$legacyRulesetName = "blockupdate"

$ruleset = @{
    name = $rulesetName
    target = "branch"
    enforcement = "active"
    conditions = @{
        ref_name = @{
            include = @("~DEFAULT_BRANCH")
            exclude = @()
        }
    }
    rules = @(
        @{
            type = "deletion"
        },
        @{
            type = "non_fast_forward"
        },
        @{
            type = "pull_request"
            parameters = @{
                allowed_merge_methods = @("merge", "squash", "rebase")
                dismiss_stale_reviews_on_push = $true
                require_code_owner_review = $false
                require_last_push_approval = $false
                required_approving_review_count = 0
                required_review_thread_resolution = $true
            }
        },
        @{
            type = "required_status_checks"
            parameters = @{
                do_not_enforce_on_create = $true
                required_status_checks = @(
                    @{
                        context = "Required PR Gate"
                    }
                )
                strict_required_status_checks_policy = $true
            }
        }
    )
    bypass_actors = @()
}

$payload = $ruleset | ConvertTo-Json -Depth 12

Write-Host "Target repository: $Owner/$Repository"
Write-Host "Ruleset: $rulesetName"
Write-Host "Mode: $(if ($Apply) { 'APPLY' } else { 'DRY-RUN' })"
Write-Host $payload

if (-not $Apply) {
    Write-Host "Dry-run only. Re-run with -Apply to create or update the ruleset."
    exit 0
}

$existing = Invoke-RestMethod -Method Get -Uri "$apiBase/rulesets" -Headers $headers
$current = $existing | Where-Object { $_.name -eq $rulesetName } | Select-Object -First 1

if ($null -eq $current) {
    Write-Host "Creating ruleset '$rulesetName'."
    $created = Invoke-RestMethod -Method Post -Uri "$apiBase/rulesets" -Headers $headers -ContentType "application/json" -Body $payload
    Write-Host "Created ruleset id=$($created.id)."
}
else {
    Write-Host "Updating ruleset '$rulesetName' id=$($current.id)."
    $updated = Invoke-RestMethod -Method Put -Uri "$apiBase/rulesets/$($current.id)" -Headers $headers -ContentType "application/json" -Body $payload
    Write-Host "Updated ruleset id=$($updated.id)."
}

$finalRulesets = Invoke-RestMethod -Method Get -Uri "$apiBase/rulesets" -Headers $headers
$final = $finalRulesets | Where-Object { $_.name -eq $rulesetName } | Select-Object -First 1
if ($null -eq $final) {
    throw "The main-protection ruleset was not found after apply."
}

$finalDetails = Invoke-RestMethod -Method Get -Uri "$apiBase/rulesets/$($final.id)" -Headers $headers
if ($finalDetails.enforcement -ne "active") {
    throw "The main-protection ruleset is not active after apply."
}

$ruleTypes = @($finalDetails.rules | ForEach-Object { $_.type })
foreach ($requiredType in @("deletion", "non_fast_forward", "pull_request", "required_status_checks")) {
    if ($ruleTypes -notcontains $requiredType) {
        throw "The applied ruleset is missing required rule '$requiredType'."
    }
}

$statusRule = $finalDetails.rules | Where-Object { $_.type -eq "required_status_checks" } | Select-Object -First 1
$contexts = @($statusRule.parameters.required_status_checks | ForEach-Object { $_.context })
if ($contexts -notcontains "Required PR Gate") {
    throw "The applied ruleset does not require 'Required PR Gate'."
}

$legacy = $finalRulesets | Where-Object { $_.name -eq $legacyRulesetName } | Select-Object -First 1
if ($null -ne $legacy) {
    if ($RemoveLegacyBlockUpdate) {
        Write-Host "Deleting legacy ruleset '$legacyRulesetName' id=$($legacy.id)."
        Invoke-RestMethod -Method Delete -Uri "$apiBase/rulesets/$($legacy.id)" -Headers $headers
    }
    else {
        $legacyDetails = Invoke-RestMethod -Method Get -Uri "$apiBase/rulesets/$($legacy.id)" -Headers $headers
        if ($legacyDetails.enforcement -ne "disabled") {
            throw "Legacy ruleset '$legacyRulesetName' is not disabled. Refusing to leave conflicting enforcement active."
        }
        Write-Host "Legacy ruleset '$legacyRulesetName' remains disabled. Use -RemoveLegacyBlockUpdate to remove it after validating main-protection."
    }
}

Write-Host "MAIN-RULESET-CONFIGURATION=OK"
