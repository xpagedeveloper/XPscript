# XPScript Platform, Shell and Native Libraries

This page documents cross-platform functionality demonstrated by `samples/platform-shell.xps`, `samples/platform-native-library.xps`, `samples/native-architecture-assets.xps`, `samples/native-dependency-packaging.xps` and `samples/native-loader-diagnostics.xps`.

## Platform

Returns a stable platform name at runtime.

```xpscript
Print Platform()
```

Current values include:

- `Windows`
- `Linux`
- `MacOS`
- `FreeBSD`
- `Unknown`

A common pattern is:

```xpscript
If Platform() = "Windows" Then
    ' Windows-specific path
ElseIf Platform() = "Linux" Then
    ' Linux-specific path
ElseIf Platform() = "MacOS" Then
    ' macOS-specific path
End If
```

## Shell

Runs an external executable/script/command according to the current platform.

```xpscript
Call Shell("cmd.exe /c echo XPScript-Windows")
```

On Linux/macOS:

```xpscript
Call Shell("/bin/sh -c \"echo XPScript\"")
```

Supported implementation paths include Windows executables, `.cmd`, `.bat`, `.ps1`, Unix executables/shebang scripts, `.sh`, `.bash` and PowerShell through `pwsh` where available.

Prefer direct executable invocation over unnecessary shell parsing when possible.

Windows `.cmd`/`.bat` arguments reject command-shell metacharacters in the structured batch-script path. A direct call to `cmd.exe /c ...` remains an explicit application-controlled command-shell boundary.

## Declare Function / Declare Sub

Native functions use `Declare`.

```xpscript
Declare Function NativeProcessId Lib "libc.so.6" Alias "getpid" () As Integer
```

### Native parameter ABI rule

Native parameters must currently be declared **explicitly `ByVal`**.

```xpscript
Declare Function NativeLength Lib "mylib" Alias "native_length" _
    (ByVal value As Integer) As Integer
```

`ByRef` native parameters and parameters with an omitted passing mode are intentionally rejected at compile time. The current native emitter does not yet implement target-correct `ref`/`out` marshalling, and silently emitting those declarations as by-value would create an ABI mismatch that could corrupt memory or crash the process.

Parameterless native functions are unaffected.

See the negative regression source `samples/native-byref-error.xps`.

## OS-specific libraries

One declaration can choose different native libraries for each operating system.

```xpscript
Declare Function NativeProcessId Lib "native-process" _
    WindowsLib "kernel32.dll" WindowsAlias "GetCurrentProcessId" _
    LinuxLib "libc.so.6" LinuxAlias "getpid" _
    MacOSLib "libSystem.B.dylib" MacOSAlias "getpid" _
    () As Integer
```

The selection uses the compiler target RID rather than the compiler host OS.

## Architecture-specific libraries

Exact x64/arm64 assets may be supplied:

- `WindowsX64Lib`
- `WindowsArm64Lib`
- `LinuxX64Lib`
- `LinuxArm64Lib`
- `MacOSX64Lib`
- `MacOSArm64Lib`

Architecture-specific aliases are also supported:

- `WindowsX64Alias`
- `WindowsArm64Alias`
- `LinuxX64Alias`
- `LinuxArm64Alias`
- `MacOSX64Alias`
- `MacOSArm64Alias`

Resolution order is:

1. exact target RID
2. operating-system-specific fallback
3. generic `Lib` / `Alias`

Example from the sample:

```xpscript
Declare Function NativeVersion Lib "native/default/nativecore.dll" Alias "native_version" _
    WindowsLib "native/windows/nativecore.dll" _
    WindowsX64Lib "native/windows/x64/nativecore.dll" _
    WindowsArm64Lib "native/windows/arm64/nativecore.dll" _
    LinuxLib "native/linux/libnativecore.so" _
    LinuxX64Lib "native/linux/x64/libnativecore.so" _
    LinuxArm64Lib "native/linux/arm64/libnativecore.so" _
    MacOSLib "native/macos/libnativecore.dylib" _
    MacOSX64Lib "native/macos/x64/libnativecore.dylib" _
    MacOSArm64Lib "native/macos/arm64/libnativecore.dylib" _
    () As Integer
```

## Application-local native dependencies

A `Lib` value containing a project-relative path is treated as an application-local dependency.

```xpscript
WindowsLib "native/windows/mylib.dll"
LinuxLib "native/linux/libmylib.so"
MacOSLib "native/macos/libmylib.dylib"
```

The compiler validates the path and copies the selected target library beside the published executable. Bare system-library names such as `kernel32.dll` or `libc.so.6` remain OS-resolved and are not copied.

Security checks include project-directory containment, symlink/reparse-point escape rejection, missing-file detection, output filename collisions and executable-overwrite prevention.

## Native loader diagnostics

Generated wrappers translate common loader failures into clearer XPScript runtime diagnostics:

- library not found
- entry point not found
- bad image / wrong architecture

The diagnostics include the library and entry-point identity without requiring callers to understand raw P/Invoke exception types.

Native libraries execute unmanaged code with the same OS privileges as the XPScript process. Incorrect signatures can still crash the process; only trusted libraries with verified declarations should be used.

## Managed .NET references

Managed references are deliberately separate from native `Declare`.

```xpscript
Reference "managed/MyLibrary.dll"
```

RID-specific native dependencies belonging to a managed assembly use:

```xpscript
ReferenceNative "managed/runtimes/win-x64/native/helper.dll" Runtime "win-x64"
```

`Reference` only stages the managed assembly for build/publish; it does not automatically expose arbitrary CLR classes as XPScript classes.

## Supported target RIDs

- `win-x64`
- `win-arm64`
- `linux-x64`
- `linux-arm64`
- `osx-x64`
- `osx-arm64`

## Samples

- `samples/platform-shell.xps`
- `samples/platform-native-library.xps`
- `samples/native-architecture-assets.xps`
- `samples/native-dependency-packaging.xps`
- `samples/native-loader-diagnostics.xps`
- `samples/native-byref-error.xps`
