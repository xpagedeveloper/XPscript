# XPscript debugger

The debugger is implemented as a runtime capability and exposed to editor integrations through a small authenticated transport protocol.

## Enabling debugging

Native CLI, desktop and server-side web processes enable the debugger with:

- `XPSCRIPT_DEBUG_PORT` - loopback TCP port used by the debugger transport.
- `XPSCRIPT_DEBUG_TOKEN` - optional authentication token. When configured, every debugger command must contain the token.
- `XPSCRIPT_DEBUG_STOP_ON_ENTRY` - `1` to hold the first XPscript statement, `0` to run immediately.

The listener binds only to loopback. Remote debugging should use a local tunnel or authenticated relay rather than exposing the runtime port publicly.

When these settings are absent, debugger runtime hooks are effectively disabled. `Debugger.Print(...)` and `Debugger.UpdateVar(...)` are compiler-guarded so their argument expressions are not evaluated outside a debug session.

## Version 1 capabilities

Protocol version 5 is the first complete native debugger contract. The runtime advertises and the VS Code adapter validates the protocol version during handshake.

Version 1 supports:

- source breakpoints
- write data breakpoints on observed scalar values and explicit debugger variables
- Continue
- Pause at the next executable XPscript statement
- Step Into
- Step Over
- Step Out
- mapped XPscript call stacks
- observed scalar Locals
- a separate Debugger Variables scope
- bounded value history
- `Debugger.Print(...)` output in the Debug Console
- exception breakpoints for uncaught or all XPscript runtime exceptions
- disconnect and target-exit handling

Stepping is call-depth aware. Portable PDB sequence points and XPscript source directives are used to recover mapped XPscript stack frames.

## Locals and internal objects

The debugger deliberately does not recursively inspect XPscript runtime objects, Notes objects, arrays, collection internals, generated CLR implementation objects, object properties or ByRef wrapper internals.

Simple scalar assignments can be observed automatically. Complex or internal values are exposed only when the application explicitly publishes a useful scalar representation with `Debugger.UpdateVar`.

This keeps the debugger surface stable and prevents internal implementation details or large object graphs from being retained merely because debugging is active.

## Debugger API

### Debugger.Print

Writes a rendered value to the attached debugger's Debug Console:

```xpscript
Debugger.Print("Processing document " & CStr(doc.NoteID))
```

The output includes the current mapped XPscript source file and line when available. It does not write to the application's normal stdout channel.

When debugging is disabled, the entire call is skipped and the argument expression is not evaluated.

### Debugger.UpdateVar

Publishes an application-defined debugger variable:

```xpscript
Debugger.UpdateVar("DocName", doc.GetItemValue("DocName")(0))
```

`DocName` appears under the separate `Debugger Variables` scope in VS Code and participates in value history and write data breakpoints.

The supplied debugger name must not collide with an automatically observed application scalar. Once a custom debugger name has been registered, automatic scalar tracking cannot overwrite it.

When debugging is disabled, the entire call is skipped and the value expression is not evaluated.

## Value history

The debugger keeps at most the latest 20 observed changes for each tracked scalar or explicit debugger variable. Each entry contains the previous and new rendered values, original XPscript source path and line, procedure, UTC timestamp and monotonic sequence.

Memory is bounded:

- each rendered value is limited to 2,048 characters
- oversized text retains only a bounded prefix and suffix plus original length
- byte arrays are represented by length rather than copied into debugger history
- streams are represented by metadata and are never read into history
- each tracked variable has a maximum history budget of 32,768 characters
- oldest entries are evicted when either the 20-entry or memory budget is exceeded

In the VS Code Debug Console, value history can also be queried with:

```text
history(variableName)
```

or:

```text
@history variableName
```

## Data breakpoints

The runtime supports write data breakpoints for observed scalar values and names published by `Debugger.UpdateVar`. The history entry is recorded first, then execution pauses if the value has an active data breakpoint.

Internal arrays, object properties and ByRef implementation objects are not automatically exposed as data ids. Publish a meaningful value through `Debugger.UpdateVar` when a watchpoint is needed for those cases.

## Exception breakpoints

The debugger hooks XPscript's normal runtime error capture path after an exception has been normalized to XPscript semantics.

VS Code exposes two filters:

- `Uncaught XPscript exceptions` - default; stops only when normal XPscript error handling will not handle the exception.
- `All XPscript exceptions` - also stops for errors that will subsequently be handled by `On Error` or `On Error Resume Next`.

Continuing from an exception stop does not alter XPscript error semantics. Existing handlers still run normally after the debugger resumes.

## Pause and command transport

The native runtime uses a single-reader command transport. Exactly one dedicated background thread owns all reads from the debugger TCP stream. It authenticates incoming commands and places ordinary requests into a thread-safe command queue.

`Pause` is handled as an atomic signal by that reader thread. The XPscript execution thread never reads or polls the socket. At each mapped XPscript statement it only checks the atomic pause flag and processes already queued debugger configuration requests.

When execution is stopped, the XPscript thread waits on a command signal and consumes queued Continue, Step and inspection requests. This guarantees that there is never more than one socket reader while still allowing Pause and Disconnect to arrive asynchronously.

Pause remains cooperative at XPscript statement boundaries, so the program stops at the next executable XPscript source location rather than at an arbitrary CLR instruction.

## Targets

### CLI

The VS Code extension can launch `xpscript run <program>` and inject the debugger environment automatically.

### Desktop

Desktop applications use the same native runtime hook as CLI programs. Launch and attach share the same transport.

### Server-side web

Kestrel, FastCGI and CGI processes can use the loopback debugger transport when explicitly enabled. VS Code attaches by host, port and token.

The current native v1 runtime still models a single debugger execution thread. Per-request concurrent web execution contexts are future work.

### Browser WebAssembly

Browser WASM deliberately does not open the native TCP listener. The planned path remains:

`XPscript WASM -> JavaScript bridge -> WebSocket/DevTools host -> VS Code debug adapter`

The WASM bridge is not part of debugger v1.

## Source mapping

Debugger hooks use the same source mapping markers as compiler diagnostics. Included XPscript files retain their original source path and source line when execution stops.

## CI

`tests/DebuggerCoreProbe` validates debugger source mapping, scalar instrumentation, disabled-debug guards, protocol capabilities, the single-reader Pause transport, exception hooks, bounded history and the rule that complex internal objects are not automatically inspected.

The repository Compile workflow runs this probe on Linux. The VS Code extension branch also contains a Compile workflow that runs its TypeScript build on pull requests.

## Security

- Native transport listens on loopback only.
- VS Code launch generates an ephemeral random token.
- Runtime authentication uses fixed-time token comparison.
- Web deployments should keep debugging disabled unless explicitly enabled for a development session.
- Debugger history is memory-only and bounded.
