# Application and state reference

This page documents the complete XPScript `Application` runtime surface plus the shared `Application.State`, `Process.State`, `Session.State` and `Request.State` proxies. Every row contains syntax, parameter meaning, behavior and a copyable `.xps` example.

The complete runnable reference is [samples/application-object.xps](../samples/application-object.xps).

## Application runtime information

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.ArgCount` | `Application.ArgCount` | none | Returns the number of command-line arguments passed to the program. | [application-object.xps](../samples/application-object.xps) |
| `Application.Args` | `Application.Args(index)` | `index`: zero-based argument index. | Returns one command-line argument and raises an XPScript bounds error for an invalid index. | [application-object.xps](../samples/application-object.xps) |
| `Application.CommandLine` | `Application.CommandLine` | none | Returns the argument list joined into the convenience command-line string. | [application-object.xps](../samples/application-object.xps) |
| `Application.ExecutablePath` | `Application.ExecutablePath` | none | Returns the generated application's executable path. | [application-object.xps](../samples/application-object.xps) |
| `Application.ExecutableFileName` | `Application.ExecutableFileName` | none | Returns only the generated executable file name. | [application-object.xps](../samples/application-object.xps) |
| `Application.ExecutableDirectory` | `Application.ExecutableDirectory` | none | Returns the directory containing the generated executable. | [application-object.xps](../samples/application-object.xps) |
| `Application.TempPath` | `Application.TempPath` | none | Returns the operating-system temporary directory. | [application-object.xps](../samples/application-object.xps) |
| `Application.TempFolder` | `Application.TempFolder` | none | Compatibility alias for `Application.TempPath`. | [application-object.xps](../samples/application-object.xps) |
| `Application.Path` | `Application.Path` | none | Compatibility alias for `Application.ExecutablePath`. | [application-object.xps](../samples/application-object.xps) |
| `Application.FileName` | `Application.FileName` | none | Compatibility alias for `Application.ExecutableFileName`. | [application-object.xps](../samples/application-object.xps) |

## Application UI metadata

These properties are writable. Their values are stored in the application state used by the applicable UI/build runtime.

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.Title` | `Application.Title = value` | `value`: application/window title. | Gets or sets the application title metadata. | [application-object.xps](../samples/application-object.xps) |
| `Application.Icon` | `Application.Icon = path` | `path`: icon path/value. A literal `.ico` path is build-validated when the compiler can resolve it. | Gets or sets application icon metadata. | [application-object.xps](../samples/application-object.xps) |
| `Application.Width` | `Application.Width = value` | `value`: preferred application/window width. | Gets or sets width metadata. | [application-object.xps](../samples/application-object.xps) |
| `Application.Height` | `Application.Height = value` | `value`: preferred application/window height. | Gets or sets height metadata. | [application-object.xps](../samples/application-object.xps) |

## State scopes

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.State` | `Application.State` | none | Application-wide state proxy. In web hosting it bridges to application state; outside web hosting it uses process-local XPScript state. | [application-object.xps](../samples/application-object.xps) |
| `Process.State` | `Process.State` | none | Process-wide state proxy. | [application-object.xps](../samples/application-object.xps) |
| `Session.State` | `Session.State` | none | Session state proxy when hosted by the web runtime; otherwise uses the local XPScript state implementation. | [application-object.xps](../samples/application-object.xps) |
| `Request.State` | `Request.State` | none | Request/navigation state proxy. The local compiled-navigation runtime resets it according to request/navigation boundaries. | [application-object.xps](../samples/application-object.xps) |

All four state proxies expose the same member set below.

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `State.Get` | `scope.Get(name)` | `name`: case-insensitive state key. | Returns the stored value or runtime empty/null when no value exists. | [application-object.xps](../samples/application-object.xps) |
| `State.Set` | `scope.Set(name, value)` | `name`: state key; `value`: value to store. | Adds or replaces a state value. | [application-object.xps](../samples/application-object.xps) |
| `State.Add` | `scope.Add(name, value)` | `name`: state key; `value`: value to store. | Alias with the same replace/add behavior as `Set`. | [application-object.xps](../samples/application-object.xps) |
| `State.Exists` | `scope.Exists(name)` | `name`: state key. | Returns whether the key exists. | [application-object.xps](../samples/application-object.xps) |
| `State.Remove` | `scope.Remove(name)` | `name`: state key. | Removes the key and returns whether it existed. | [application-object.xps](../samples/application-object.xps) |
| `State.Unset` | `scope.Unset(name)` | `name`: state key. | Alias for `Remove`. | [application-object.xps](../samples/application-object.xps) |
| `State.Clear` | `scope.Clear()` | none | Removes all values in that state scope. | [application-object.xps](../samples/application-object.xps) |
| `State.Count` | `scope.Count` | none | Returns the number of keys in the scope. | [application-object.xps](../samples/application-object.xps) |
| `State.Keys` | `scope.Keys` | none | Returns the state keys ordered case-insensitively by the local runtime. | [application-object.xps](../samples/application-object.xps) |

## Read-only runtime properties

`Application.Args`, `ArgCount`, `CommandLine`, `ExecutablePath`, `ExecutableFileName`, `ExecutableDirectory`, `TempPath`, `TempFolder`, `Path` and `FileName` are read-only. Assignments to them are rejected by the compiler. `Application.Title`, `Icon`, `Width` and `Height` are intentionally writable metadata properties.
