# Android build diagnostics

Enable persistent diagnostic logs before running the existing APK build script:

```bash
export XPSCRIPT_BUILD_LOG_DIR=/tmp/xpscript-android-build-logs
bash scripts/compile-android-uiform-controls.sh /tmp/android-controls
```

Each build gets a unique log directory containing `publish.binlog`, diagnostic
`msbuild.log`, streaming `stdout.log` and `stderr.log`, .NET host tracing in
`dotnet-host.log`, `exit-code.txt` when publishing finishes, and `project-path.txt`.
The generated project is preserved when logging is enabled; its path is also
printed to stderr. Logs can therefore be inspected while a build is running.
Unset `XPSCRIPT_BUILD_LOG_DIR` to restore normal temporary-project cleanup.

The Android Build workflow records these logs and uploads them even after failure.
Diagnostic logs can contain MSBuild properties and local paths; inspect them before
sharing outside the project. Binary logs omit embedded project imports.

Progress text is a heuristic, not proof of the active MSBuild task. Inspect the
last task in `stdout.log` or `msbuild.log` before attributing delays to AOT.

For local probe builds, disable MSBuild node reuse. In this managed environment
the default project-reference probe can otherwise exit as `Build FAILED` with
zero reported errors:

```bash
dotnet build tests/AndroidCompilerProbe/AndroidCompilerProbe.csproj \
  -c Release --no-restore -m:1 -nr:false
dotnet tests/AndroidCompilerProbe/bin/Release/net10.0/AndroidCompilerProbe.dll
```

Microsoft's troubleshooting guidance:

- https://learn.microsoft.com/en-us/visualstudio/msbuild/obtaining-build-logs-with-msbuild
- https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference
- https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders
