# Dependency security and package patching

XPScript can inspect the NuGet dependency graph used by an application, report known package vulnerabilities, and maintain version-scoped compatible package patches. The important rule is that application security checks are based on the dependencies the generated application actually uses. A vulnerability in an unused database or UI package does not create a warning for an application that does not include that package.

## Application dependency inspection

Use `dependencies` to inspect the resolved dependency graph for an XPScript source file:

```text
xpscript dependencies app.xps
xpscript dependencies app.xps --platform win-x64
xpscript dependencies app.xps --json
```

XPScript first compiles/transpiles the source far enough to determine which optional application features are present. It then creates a temporary NuGet project containing only the required direct package references, restores it, and asks NuGet for the resolved top-level and transitive dependency graph.

For example, an application that does not use SQL Server does not receive `Microsoft.Data.SqlClient` merely because XPScript supports SQL Server.

## Vulnerability inspection

Use `security` to check the resolved application dependencies against NuGet vulnerability information:

```text
xpscript security app.xps
xpscript security app.xps --platform linux-x64
xpscript security app.xps --json
```

Exit codes are:

| Exit code | Meaning |
|---|---|
| `0` | The check completed and no known vulnerable dependency was found. |
| `2` | One or more vulnerable dependencies were found. |
| `3` | Vulnerability information was unavailable, so XPScript cannot claim that the dependency graph is clean. |

NuGet advisory data is obtained at check time. A newly published advisory can therefore be detected without requiring a new XPScript release, provided the configured NuGet audit source is reachable and supplies vulnerability information.

`no known vulnerabilities` means exactly that: no vulnerability was reported by the available advisory data. It is not a guarantee that a package contains no undiscovered vulnerability.

## Automatic checks during compile and run

Normal execution keeps dependency security checking off to preserve the fast path:

```text
xpscript run app.xps
```

`--info` and `--debug` automatically enable warning mode:

```text
xpscript run app.xps --info
xpscript run app.xps --debug
xpscript compile app.xps --debug
```

The behavior can be selected explicitly:

```text
--security=off
--security=warn
--security=strict
```

`off` disables the application dependency audit. `warn` reports vulnerability and audit-availability warnings without intentionally blocking the build. `strict` also treats unavailable vulnerability information as a failure and blocks on high/critical application dependency findings.

An explicit `--security=off` overrides the automatic security check enabled by `--info` or `--debug`.

## Compatible package patching

XPScript can use newer official NuGet patch releases without moving an application to a different major or minor dependency line.

If the XPScript baseline is `7.0.2`, compatible candidates include:

```text
7.0.3   allowed
7.0.9   allowed
7.1.0   not allowed
8.0.0   not allowed
7.0.3-preview.1   not allowed
```

A candidate must be stable, newer than the baseline, and have the same major and minor version. This deliberately conservative policy reduces the risk of introducing behavioral or binary incompatibilities through automatic dependency updates.

## Patch commands

Check what is available without changing the local patch state:

```text
xpscript patch all --check
xpscript patch Microsoft.Data.SqlClient --check
```

Apply compatible patches:

```text
xpscript patch all
xpscript patch Microsoft.Data.SqlClient
```

Inspect or remove the current XPScript version's patch state:

```text
xpscript patch status
xpscript patch list
xpscript patch remove all
```

Downloaded package files are hashed with SHA-256 and recorded in the patch manifest.

## Security-only patching

Use `--security-only` when maintenance updates should be ignored:

```text
xpscript patch all --check --security-only
xpscript patch all --security-only
xpscript patch Microsoft.Data.SqlClient --security-only
```

XPScript classifies a candidate using current vulnerability metadata:

- `security patch`: the baseline is reported vulnerable and the compatible candidate is not reported vulnerable.
- `maintenance patch`: a newer compatible patch exists but the baseline is not currently reported vulnerable.
- `no known vulnerabilities`: no known vulnerability is reported and no applicable security action is required.
- `known vulnerability remains`: both baseline and candidate are reported vulnerable.
- `known vulnerability; no compatible patch`: the baseline is vulnerable but there is no eligible same-major/minor patch release.
- `security status unavailable`: the vulnerability metadata required for the classification could not be obtained.

`--security-only` applies only candidates classified as security patches. It does not automatically apply maintenance patches, and it does not apply a package when the security status cannot be verified.

## Dependency groups

Some package families must remain synchronized. Avalonia packages are treated as an atomic compatibility group rather than patched independently. XPScript looks for a stable version in the same version line that is available for every package in the group. If no common compatible version exists, the group is left unchanged.

This prevents an update from creating a mixed Avalonia dependency set merely because one package published a patch release before the others.

## Patches are scoped to the XPScript version

Patch state is isolated by the exact XPScript release line. Conceptually the cache is organized as:

```text
XPScript/
  package-patches/
    0.9.3-beta/
      manifest.json
      packages/
```

A patch selected while running XPScript `0.9.3-beta` is not automatically used by XPScript `0.9.4`. Old patch data may remain on disk for rollback or cleanup, but a different XPScript release uses a different patch namespace.

This makes the XPScript release plus its selected patch manifest a reproducible dependency baseline and avoids silently carrying old compatibility decisions into a new XPScript release.

## Application-specific behavior

Patch availability is global to the dependency baseline, but generated applications still include only the package families they use. For example:

```text
XPScript baseline: Microsoft.Data.SqlClient 7.0.2
Selected patch:    Microsoft.Data.SqlClient 7.0.3
```

An application using SQL Server resolves the selected `7.0.3` version. An application that does not use SQL Server does not gain the package because a patch exists.

The dependency inspector and security audit operate on the resolved application graph, so their output reflects the version actually selected for the application rather than only the original XPScript baseline.

## Recommended workflows

For development, use `xpscript run app.xps --info` to get the normal informational output plus dependency security warnings. Before release, run `xpscript security app.xps` for each target RID that matters to the deployment. Use `xpscript patch all --check` to review compatible maintenance and security patches before applying them.

For a security-focused maintenance operation, use `xpscript patch all --check --security-only` first, review the classifications, and then run `xpscript patch all --security-only` if the proposed security patches are acceptable. Rebuild and test the application after changing its dependency patch state.