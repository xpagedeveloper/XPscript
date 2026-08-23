# Native and managed interop reference

This is the complete reference for XPScript native declaration selectors and managed/native reference directives. Each selector has its own title, syntax, parameters, behavior, and a complete `.xps` example.

## Native declarations

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Declare Function` | `Declare Function Name Lib "library" [Alias "entry"] (...) As Type` | `Name`: XPScript function name; `library`: native library; `entry`: optional exported symbol; parameters/return type define the unmanaged signature. | Declares a native value-returning function. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Declare Sub` | `Declare Sub Name Lib "library" [Alias "entry"] (...)` | same native-library and signature fields as `Declare Function`, without a return type. | Declares a native procedure. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Alias` | `Alias "entry"` | `entry`: generic exported function name. | Selects the generic native entry point when no more specific alias applies. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |

## OS library selectors

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `WindowsLib` | `WindowsLib "library"` | `library`: Windows native library path/name. | Overrides the generic `Lib` value for Windows targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `LinuxLib` | `LinuxLib "library"` | `library`: Linux native library path/name. | Overrides the generic `Lib` value for Linux targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `MacOSLib` | `MacOSLib "library"` | `library`: macOS native library path/name. | Overrides the generic `Lib` value for macOS targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |

## Exact architecture library selectors

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `WindowsX64Lib` | `WindowsX64Lib "library"` | `library`: Windows x64 native library. | Exact `win-x64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `WindowsArm64Lib` | `WindowsArm64Lib "library"` | `library`: Windows ARM64 native library. | Exact `win-arm64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `LinuxX64Lib` | `LinuxX64Lib "library"` | `library`: Linux x64 native library. | Exact `linux-x64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `LinuxArm64Lib` | `LinuxArm64Lib "library"` | `library`: Linux ARM64 native library. | Exact `linux-arm64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `MacOSX64Lib` | `MacOSX64Lib "library"` | `library`: macOS x64 native library. | Exact `osx-x64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `MacOSArm64Lib` | `MacOSArm64Lib "library"` | `library`: macOS ARM64 native library. | Exact `osx-arm64` library override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |

## OS alias selectors

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `WindowsAlias` | `WindowsAlias "entry"` | `entry`: Windows exported symbol name. | Overrides generic `Alias` for Windows targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `LinuxAlias` | `LinuxAlias "entry"` | `entry`: Linux exported symbol name. | Overrides generic `Alias` for Linux targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `MacOSAlias` | `MacOSAlias "entry"` | `entry`: macOS exported symbol name. | Overrides generic `Alias` for macOS targets. | [platform-native-library.xps](../samples/platform-native-library.xps) |

## Exact architecture alias selectors

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `WindowsX64Alias` | `WindowsX64Alias "entry"` | `entry`: Windows x64 exported symbol. | Exact `win-x64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `WindowsArm64Alias` | `WindowsArm64Alias "entry"` | `entry`: Windows ARM64 exported symbol. | Exact `win-arm64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `LinuxX64Alias` | `LinuxX64Alias "entry"` | `entry`: Linux x64 exported symbol. | Exact `linux-x64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `LinuxArm64Alias` | `LinuxArm64Alias "entry"` | `entry`: Linux ARM64 exported symbol. | Exact `linux-arm64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `MacOSX64Alias` | `MacOSX64Alias "entry"` | `entry`: macOS x64 exported symbol. | Exact `osx-x64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `MacOSArm64Alias` | `MacOSArm64Alias "entry"` | `entry`: macOS ARM64 exported symbol. | Exact `osx-arm64` entry-point override. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |

Resolution order for both library and alias selectors is exact runtime identifier, then OS-specific selector, then generic `Lib`/`Alias`.

## Managed and native dependency directives

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Reference` | `Reference "relative/path/Assembly.dll"` | `path`: application-local managed assembly path relative to the `.xps` source tree. | Adds a managed .NET assembly to the generated application. Paths are constrained to the source tree and staged securely. | [managed-reference.xps](../samples/managed-reference.xps) |
| `ReferenceNative` | `ReferenceNative "relative/path/library" Runtime "rid"` | `path`: application-local native dependency; `rid`: exact target runtime such as `linux-x64`. | Adds a native dependency for one target runtime. Repeat the directive for multiple RIDs. | [managed-reference-native.xps](../samples/managed-reference-native.xps) |
| `Runtime` | `Runtime "rid"` | `rid`: runtime identifier attached to `ReferenceNative`. | Selects which compiler target receives that native dependency. | [managed-reference-native.xps](../samples/managed-reference-native.xps) |

## Copyable examples

For platform-specific declarations, copy [samples/native-architecture-assets.xps](../samples/native-architecture-assets.xps). For managed references, follow the fixture setup documented in [demo/README.md](../demo/README.md#managed-and-native-references) and compile [samples/managed-reference.xps](../samples/managed-reference.xps).
