# XPscript debugger

The debugger is implemented as a runtime capability and exposed to editor integrations through a small authenticated transport protocol.

## Enabling debugging

Native CLI, desktop and server-side web processes enable the debugger with:

- `XPSCRIPT_DEBUG_PORT` - loopback TCP port used by the debugger transport.
- `XPSCRIPT_DEBUG_TOKEN` - optional authentication token. When configured, every debugger command must contain the token.
- `XPSCRIPT_DEBUG_STOP_ON_ENTRY` - `1` to hold the first XPscript statement, `0` to run immediately.

The listener binds only to loopback. Remote debugging should use a local tunnel or authenticated relay rather than exposing the runtime port publicly.

When these settings are absent, debugger runtime hooks are effectively disabled. `Debugger.Print(...)` and `Debugger.UpdateVar(...)` are compiler-guarded so their argument expressions are not evaluated outside a debug session.

## Detecting a debugger session

`Application.IsDebugging` is a read-only Boolean runtime property. It is `True` when the application is running with the XPscript debugger enabled and `False` during a normal run. It reflects runtime debugger state, not whether the executable contains debug symbols or was built with debug optimization settings.

```xpscript
If Application.IsDebugging Then
    Debugger.Print("Extra diagnostics enabled")
End If
```

See `samples/application-is-debugging.xps` for a complete runnable example.

## Version 1 capabilities

Protocol version 6 is the current native debugger contract. The runtime advertises and the VS Code adapter validates the protocol version during handshake.

Version 1 supports source breakpoints, conditional breakpoints, hit-count breakpoints, log points, write data breakpoints, Continue, cooperative Pause, Step Into/Over/Out, mapped XPscript call stacks, observed scalar Locals, Debugger Variables, bounded value history, Debug Console output, exception breakpoints, graceful completion, and disconnect/target-exit handling.

Stepping is call-depth aware. Portable PDB sequence points and XPscript source directives are used to recover mapped XPscript stack frames.

## Breakpoint behavior

Breakpoint rules are evaluated inside the XPscript runtime before a `stopped` event is sent to the editor. This avoids a stop/query/continue round trip for every false condition.

A conditional breakpoint can reference automatically observed scalar variables and names published with `Debugger.UpdateVar`:

```text
Counter == 10
Counter >= 25
Ready == true
Name == "Example"
```

Supported comparison operators are `=`, `==`, `!=`, `<`, `<=`, `>` and `>=`. Bare variable names are treated as Boolean/truthy conditions.

Hit-count breakpoints support forms such as:

```text
10
== 10
>= 10
> 20
```

Log points write to the Debug Console without stopping. Braced variable names are expanded from the runtime's latest observed values, for example:

```text
Counter is {Counter}
```

Breakpoint source identifiers are normalized to their source file name for matching, while the editor keeps the full source path for navigation.

## Locals and internal objects

The debugger deliberately does not recursively inspect XPscript runtime objects, Notes objects, arrays, collection internals, generated CLR implementation objects, object properties or ByRef wrapper internals. Simple scalar assignments can be observed automatically. Complex or internal values are exposed only when the application explicitly publishes a useful scalar representation with `Debugger.UpdateVar`.

## Debugger API

### Debugger.Print

Writes a rendered value to the attached debugger's Debug Console:

```xpscript
Debugger.Print("Processing document " & CStr(doc.NoteID))
```

The output includes the current mapped XPscript source file and line when available. When debugging is disabled, the entire call is skipped and the argument expression is not evaluated.

### Debugger.UpdateVar

Publishes an application-defined debugger variable:

```xpscript
Debugger.UpdateVar("DocName", doc.GetItemValue("DocName")(0))
```

The variable appears under `Debugger Variables` in VS Code and participates in value history, conditional breakpoints and write data breakpoints. The supplied name must not collide with an automatically observed application scalar. When debugging is disabled, the entire call is skipped and the value expression is not evaluated.

## Value history

The debugger keeps at most the latest 20 observed changes for each tracked scalar or explicit debugger variable. Each rendered value is limited to 2,048 characters and each variable has a maximum history budget of 32,768 characters. Byte arrays and streams are represented without retaining their full contents.

In the VS Code Debug Console, history can be queried with `history(variableName)` or `@history variableName`.

## Data and exception breakpoints

Write data breakpoints are supported for observed scalar values and names published by `Debugger.UpdateVar`. Internal arrays, object properties and ByRef implementation objects are not automatically exposed as data ids.

Exception breakpoints hook XPscript's normal runtime error capture path. VS Code exposes `Uncaught XPscript exceptions` and `All XPscript exceptions`. Continuing from an exception stop does not alter normal XPscript error semantics.

## Pause and command transport

The native runtime uses a single-reader command transport. One dedicated background thread owns debugger TCP reads. `Pause` is an atomic signal and execution stops at the next mapped XPscript statement. The XPscript execution thread never polls or reads the socket while running.

When the script finishes, the generated entry point calls the debugger runtime's completion hook from `finally`. The runtime sends a `complete` frame, flushes the writer, and performs a graceful socket shutdown so late Debug Console output is not lost.

## Targets

CLI and desktop applications use the native runtime debugger hook. Server-side web processes can use the loopback transport when explicitly enabled. Browser WASM does not open the native TCP listener; its debugger bridge remains future work.

## Source mapping

Debugger hooks use the same source mapping markers as compiler diagnostics. Included XPscript files retain their original source path and source line when execution stops.

## CI

`tests/DebuggerCoreProbe` validates debugger source mapping, scalar instrumentation, disabled-debug guards, protocol capabilities, runtime conditional breakpoint support, Pause transport, exception hooks, graceful completion, bounded history and the rule that complex internal objects are not automatically inspected.

## Security

- Native transport listens on loopback only.
- VS Code launch generates an ephemeral random token.
- Runtime authentication uses fixed-time token comparison.
- Web deployments should keep debugging disabled unless explicitly enabled for a development session.
- Debugger history is memory-only and bounded.
