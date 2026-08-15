# Main branch protection

XPscript uses an always-running GitHub Actions check named `Required PR Gate` as the stable required status check for pull requests targeting `main`.

The repository ruleset should enforce the following policy on the default branch:

- changes must land through a pull request;
- unresolved review conversations block merge;
- `Required PR Gate` must pass;
- the pull request must be tested against the latest `main` state;
- force pushes are blocked;
- deletion of `main` is blocked;
- no default bypass actors are configured.

## Apply the ruleset

Use a fine-grained personal access token, GitHub App user token, or GitHub App installation token with repository `Administration: write` permission.

```powershell
$env:GITHUB_TOKEN = '<token>'
./tools/configure-main-ruleset.ps1
./tools/configure-main-ruleset.ps1 -Apply
```

The first command is a dry run. The second creates or updates the active `main-protection` repository ruleset.

The script validates that the active ruleset contains:

- `deletion`;
- `non_fast_forward`;
- `pull_request`;
- `required_status_checks` with `Required PR Gate`.

## Legacy blockupdate ruleset

The old `blockupdate` ruleset must not be enabled as-is. It contains generic `creation` and `update` restrictions which are broader than the intended merge policy.

After `main-protection` has been applied and validated, remove the legacy ruleset with:

```powershell
./tools/configure-main-ruleset.ps1 -Apply -RemoveLegacyBlockUpdate
```

The script refuses to leave the legacy ruleset active. Without `-RemoveLegacyBlockUpdate`, it verifies that `blockupdate` remains disabled.

## Validation

After applying the ruleset:

1. Open a harmless pull request against `main`.
2. Confirm `Required PR Gate` is required and must pass.
3. Create an unresolved review conversation or submit a change-request review from another authorized reviewer.
4. Confirm GitHub blocks merge until the review blocker is resolved.
5. Confirm a direct force push to `main` is rejected.
6. Confirm deletion of `main` is rejected.

Do not close issue #64 until the live GitHub ruleset is active and the blocking behavior has been verified on a test pull request.
