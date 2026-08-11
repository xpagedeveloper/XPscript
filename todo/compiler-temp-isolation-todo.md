# XPScript compiler temp/build isolation TODO

(c) xpagedeveloper.com 2026

Security and concurrency checklist for compiler-generated temporary files, build directories and final output paths.

## Goals

- [ ] every compiler invocation gets a unique build workspace
- [ ] concurrent compiler processes cannot read or overwrite another invocation's temporary source, project, obj, bin or publish files
- [ ] generated source and project files are never written to predictable shared filenames without a unique parent directory
- [ ] cleanup removes only the workspace created by the current invocation
- [ ] failed builds leave no writable shared state that a later build can accidentally reuse

## Workspace creation

- [ ] use a cryptographically unpredictable or GUID-based invocation identifier
- [ ] create workspaces beneath a dedicated XPScript compiler temp root
- [ ] use `Path.GetTempPath()` only as the root; never use a fixed direct child such as `xpscript-build`
- [ ] canonical layout proposal: `<temp>/xpscript/<invocation-id>/`
- [ ] create separate `src`, `obj`, `bin`, `publish` and `logs` directories inside the invocation workspace where useful
- [ ] ensure directory creation is atomic enough that an existing directory is never silently reused

## Permissions and symlinks

- [ ] review Windows ACLs for generated temporary directories
- [ ] review Unix directory/file modes on Linux/macOS
- [ ] reject or safely handle symlinks/reparse points in compiler-owned workspace paths
- [ ] compiler cleanup must not follow a symlink/reparse point outside the owned workspace
- [ ] generated source containing secrets must not be world-readable on multi-user systems

## Output safety

- [ ] normalize requested output path with `Path.GetFullPath`
- [ ] reject output paths that resolve to directories when a file target is required
- [ ] never overwrite compiler/runtime source files merely because the requested output name resolves into the repository tree
- [ ] define explicit overwrite behavior for an existing requested output file
- [ ] write final output to a temporary sibling and atomically move/replace where the platform supports it
- [ ] prevent partial executable replacement if publish/copy fails halfway through
- [ ] validate application-local `.dll`, `.so`, `.dylib` copy targets using the same containment rules

## Process execution

- [ ] invoke `dotnet` with an explicit working directory set to the current invocation workspace
- [ ] pass paths as structured process arguments rather than shell-concatenated strings
- [ ] do not inherit user-controlled environment variables that can redirect MSBuild/NuGet output unless deliberately supported
- [ ] review `TMP`, `TEMP`, `TMPDIR`, NuGet cache and MSBuild environment interactions
- [ ] decide which caches may safely be shared read-only/per-user and which generated outputs must always be isolated

## Cleanup/lifetime

- [ ] cleanup happens in `finally` after success or failure
- [ ] cleanup only deletes paths proven to be descendants of the owned invocation root
- [ ] cleanup failure must not mask the original compiler error
- [ ] optionally support `--keep-temp` for debugging while clearly printing the exact isolated workspace path
- [ ] define age-based cleanup of abandoned compiler workspaces from crashed processes without touching active ones

## Concurrency verification when execution is re-enabled

- [ ] run at least 10 concurrent compilations of the same `.xps` source to different outputs
- [ ] run concurrent compilations using identical source filename from different directories
- [ ] verify generated files and diagnostics never cross between invocations
- [ ] deliberately crash/kill one compiler process and verify another process is unaffected
- [ ] test parallel Windows/Linux/macOS compiler invocations
- [ ] test malicious output paths containing `..`, absolute paths, symlinks and platform-specific path tricks
