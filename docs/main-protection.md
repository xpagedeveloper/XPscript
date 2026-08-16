# Main branch protection

XPscript protects the default branch with the repository ruleset `main-protection`.

## Enforced policy

Changes to `main` must land through a pull request. Direct updates are blocked by the pull-request rule.

A pull request requires at least one approving review. New reviewable commits dismiss stale approvals. All review conversations must be resolved before merge. A submitted `Request changes` review therefore remains blocking until it is replaced by an approval or dismissed by a repository user with the required GitHub permission.

`Required PR Gate` is a required strict status check. The branch must be up to date with `main`, and the gate must pass before merge. The gate restores and builds `XPScriptCompiler.slnx` in Release mode, compiles the portable Evaluate smoke fixture, runs it, and verifies expected output.

Force pushes are blocked by the `non_fast_forward` rule. Deletion of `main` is blocked by the `deletion` rule.

No ruleset bypass actors are configured. Repository administration credentials are used only by the `Configure Main Protection` workflow to create, update, verify, or remove repository rulesets. They do not bypass pull-request review or CI requirements for normal code changes.

## Administration

The repository secret `MAIN_PROTECTION_TOKEN` must be a fine-grained GitHub token scoped to this repository with Administration write permission. The `Configure Main Protection` workflow runs when its workflow or ruleset script changes on `main`, and can also be started manually.

The implementation is `tools/configure-main-ruleset.ps1`. Normal automated application uses `-Apply` and removes the obsolete disabled `blockupdate` ruleset after `main-protection` has been created and validated.

The workflow fails if the active ruleset does not require an approval, resolved review conversations, stale-review dismissal, strict `Required PR Gate`, deletion protection, non-fast-forward protection, or if any bypass actor is present.

## Validation

After applying the policy, validate it with a harmless pull request whose CI is allowed to complete but which has no required approval. A merge attempt must be rejected by GitHub. Do not merge the validation pull request. Close it after the blocked merge has been proven.
