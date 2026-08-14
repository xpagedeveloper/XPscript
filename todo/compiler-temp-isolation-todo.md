# XPScript compiler temp/build isolation TODO

(c) xpagedeveloper.com 2026

Security and concurrency checklist for compiler-generated temporary files, build directories and final output paths.

Status:
- `[x]` implemented and verified
- `[>]` implemented/reviewed, awaiting verification
- `[ ]` not implemented/reviewed

## Goals

- [x] every compiler invocation gets a unique GUID build workspace
- [x] concurrent compiler invocations do not intentionally share generated source, project or publish paths
- [x] generated source and project files use predictable names only inside a unique invocation directory
- [>] cleanup targets only the workspace created by the current invocation
- [>] failed builds do not intentionally reuse writable workspace state in later builds

## Workspace creation

- [x] GUID-based invocation identifier
- [x] workspaces are created beneath `<Path.GetTempPath()>/XPScript/<guid>/`
- [x] `Path.GetTempPath()` is only the root; generated files are not written directly into a fixed shared build directory
- [x] generated project/source/publish paths are invocation-local
- [>] compiler invokes `dotnet publish` with `WorkingDirectory` set to the invocation workspace
- [>] process-temp, dotnet-home and NuGet package directories are invocation-local children
- [ ] consider separate explicit `src`, `obj`, `bin`, `publish` and `logs` children if later tooling requires them
- [ ] runtime verification that an existing invocation directory can never be accidentally reused

## Permissions and symlinks

- [>] Windows invocation/staging directories remove inherited ACLs and grant the current Windows account full control through `icacls.exe`; child files inherit this restricted ACL
- [>] Unix invocation and staging directories are hardened to user-only `0700` semantics where `UnixFileMode` is supported
- [>] generated/staged temporary files are hardened to user-only read/write `0600` semantics where supported
- [>] project-local managed/native dependency paths reject symlink/reparse-point resolution outside the source tree
- [>] compiler cleanup refuses to recursively clean a workspace root that is itself a symlink/reparse point
- [>] cleanup enumerates descendants and deletes symlink/reparse entries without recursively following their targets
- [>] generated source/project files are not intentionally world-readable after platform-specific hardening
- [ ] Windows ACL behavior must be verified with local, domain and service accounts
- [ ] Windows junction/symlink behavior must be verified on a real Windows filesystem
- [ ] Linux/macOS symlink behavior must be verified on real target filesystems

## Output safety

- [>] requested output path is normalized with `Path.GetFullPath`
- [>] output paths resolving to an existing directory are rejected
- [>] an explicitly requested existing regular output file is allowed to be replaced as the compiler's upgrade/overwrite behavior
- [>] the requested output may not equal the `.xps` source file
- [>] output directory components and existing output targets may not be symbolic links/junctions/reparse points
- [>] native/managed-native dependency output names are reduced to file names and collision checked
- [>] a native dependency already located exactly at its final target path is left in place instead of replacing its own source
- [>] executable plus dependencies are first copied into a unique staging directory beside the final output
- [>] existing output files are backed up within the same output filesystem before replacement
- [>] dependencies are committed before the executable; the executable is made visible last
- [>] publication failure rolls back newly installed files and restores backed-up prior files on a best-effort basis
- [>] sibling staging keeps final `File.Move` operations on the same filesystem where normal platform semantics permit atomic rename
- [ ] decide whether the compiler should reject other output paths pointing into its own installed compiler/runtime files
- [ ] verify destination-directory symlink/reparse rejection on Windows/Linux/macOS
- [ ] verify rollback behavior under forced copy/move/delete failures

## Project-local dependency containment

- [>] `Reference` and `ReferenceNative` reject rooted paths
- [>] application-local native dependency paths reject rooted paths
- [>] lexical `..` escape outside the source directory is rejected
- [>] each existing dependency path component is checked for symlink/reparse-point resolution
- [>] links resolving outside the XPScript source directory are rejected
- [>] unresolved reparse points/links are rejected rather than trusted
- [ ] review TOCTOU race between dependency validation and staging copy
- [ ] add filesystem-level regression setup for project-local symlink escape when execution is re-enabled

## Process execution

- [>] `dotnet` is invoked with an explicit working directory set to the current invocation workspace
- [>] publish arguments are passed through `ProcessStartInfo.ArgumentList`, not shell-concatenated command strings
- [>] `UseShellExecute` is disabled
- [>] `dotnet` is resolved to an absolute host path; relative/current-directory PATH entries are ignored
- [>] `TEMP`, `TMP`, `TMPDIR`, `DOTNET_CLI_HOME` and `NUGET_PACKAGES` are redirected into the invocation workspace
- [>] dotnet first-run/telemetry side effects are disabled for generated builds
- [>] common inherited MSBuild path-redirection environment variables are removed before publish
- [>] generated output/cache state is intentionally invocation-local rather than shared writable state
- [ ] verify SDK resolution still works for supported Windows/Linux/macOS .NET 10 installations
- [ ] decide whether any read-only/per-user NuGet caches should be optionally shared for performance after security verification

## Cleanup/lifetime

- [>] cleanup happens in `finally` after success or failure
- [>] cleanup only accepts a descendant of the compiler XPScript temp root
- [>] cleanup does not recursively follow symlink/reparse-point descendants
- [>] cleanup failure is swallowed so it does not mask the original compiler error
- [ ] optionally support `--keep-temp` for debugging while clearly printing the exact isolated workspace path
- [ ] define age-based cleanup of abandoned compiler workspaces from crashed processes without touching active ones

## Concurrency verification when execution is re-enabled

- [x] run at least 10 concurrent compilations of the same `.xps` source to different outputs
- [ ] run concurrent compilations using identical source filename from different directories
- [ ] verify generated files and diagnostics never cross between invocations
- [ ] deliberately crash/kill one compiler process and verify another process is unaffected
- [x] test parallel Windows/Linux/macOS compiler invocations
- [ ] test malicious dependency/output paths containing `..`, absolute paths, symlinks, junctions and platform-specific path tricks
- [ ] force staged-publication failures and verify old executable/dependencies are restored

`Compiler Workspace Isolation` runs two real compiler invocations on Windows, Ubuntu and macOS. It observes each live workspace beneath `<Path.GetTempPath()>/XPScript/`, requires a unique 32-hex-character GUID directory for each invocation, verifies `Generated.csproj`, `Program.cs` and `publish` stay beneath that invocation directory, and verifies the workspace is removed after successful compilation.

`Compiler Parallel Isolation` runs 10 real compiler invocations concurrently on Windows, Ubuntu and macOS. All invocations intentionally read the same root `.xps` source and the same include files, which are allowed to be shared read-only inputs, while each invocation must use a distinct compiler-owned GUID workspace and distinct output path. The test requires 10 unique workspaces, invocation-local `Generated.csproj`, `Program.cs` and `publish` state, successful distinct outputs and cleanup of every observed workspace.
