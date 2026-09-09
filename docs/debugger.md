# XPscript debugger

The debugger is implemented as a runtime capability and is exposed to editor integrations through a small transport protocol.

## Current implementation

Generated XPscript code already emits source-line markers. The compiler now preserves both the original source path and line number and forwards them to `XPScriptDebugRuntime`.

Native CLI, desktop and server-side web processes can enable the debugger with these environment variables:

- `XPSCRIPT_DEBUG_PORT` - loopback TCP port used by the debugger transport.
- `XPSCRIPT_DEBUG_TOKEN` - optional authentication token. When configured, every debugger command must contain the token.
- `XPSCRIPT_DEBUG_STOP_ON_ENTRY` - `1` to pause at the first XPscript statement, `0` to run immediately.

The listener binds only to loopback. Remote debugging should therefore use a local tunnel or a future authenticated relay rather than exposing the runtime port publicly.

The runtime protocol currently supports:

- entry stops
- source breakpoints
- continue
- step into
- step over
- step out
- mapped call stack frames
- disconnect
- source path and line reporting
- observed values for the Variables view
- bounded value-change history

Stepping is call-depth aware. The runtime derives mapped XPscript stack frames from portable PDB sequence points and source directives. `StepOver` stops when execution returns to the same or a shallower XPscript call depth. `StepOut` stops after the current XPscript procedure has returned.

## Observed locals

The VS Code adapter exposes tracked scalar values in the Variables panel. These are debugger-observed values rather than CLR reflection over generated implementation details.

Each observed variable can be expanded to show its recorded value changes. The same tracked value can also be evaluated from hover or the Debug Console.

The compiler currently instruments conservative simple scalar assignments. Complex assignments, indexed array writes, property setters, object mutation and ByRef mutation are intentionally not instrumented yet because they require dedicated semantic hooks.

## Value history

The debugger contains a bounded value-history recorder. It keeps at most the latest 20 observed changes for each tracked variable. Each entry contains:

- variable name
- previous rendered value
- new rendered value
- original XPscript source path
- original source line
- procedure name
- UTC timestamp
- monotonic change sequence

Value snapshots are explicitly memory bounded:

- each rendered value is limited to 2,048 characters
- oversized text keeps only a bounded prefix and suffix together with its original character length
- byte arrays are represented by byte length rather than retaining their contents
- streams are represented only by stream metadata and are never read into debugger history
- each variable has a maximum history budget of 32,768 characters in addition to the maximum of 20 entries
- the oldest entries are evicted whenever either limit is exceeded

This prevents a large file, response body or document value from being retained repeatedly merely because the variable changes many times. `LastValues` also stores only the bounded representation, not the original payload.

In the VS Code Debug Console, use either form while execution is stopped:

`history(variableName)`

`@history variableName`

The output is newest-first and shows where each observed change occurred, for example:

`invoice.xps:84 CalculateTotal: 100 -> 125`

Value history is debugger-only state. It is not persisted to application storage and it is not collected when the debugger transport is disabled.

## Targets

### CLI

The VS Code extension can launch `xpscript run <program>` and inject the debugger environment automatically.

### Desktop

Desktop applications use the same in-process runtime hook as CLI programs. Launch and attach can therefore share the native debugger transport.

### Server-side web

Kestrel, FastCGI and CGI processes can expose the same loopback debugger transport when the debugger environment is enabled. VS Code attaches to the running process by host, port and token.

Debugger transport must not be exposed as an unauthenticated public HTTP endpoint.

### Browser WebAssembly

Browser WASM does not use the TCP transport. `XPScriptDebugRuntime` deliberately avoids opening sockets when `OperatingSystem.IsBrowser()` is true.

The browser target will use a host bridge:

`XPscript WASM -> JavaScript bridge -> WebSocket/DevTools host -> VS Code debug adapter`

The debugger core and VS Code configuration already model `wasm` as a target, but the JavaScript bridge is not implemented yet.

## Source mapping

Debugger line hooks are emitted from the same source mapping markers used by compiler diagnostics. Included XPscript files therefore retain their original source path and source line when the debugger stops.

## Security

- Native transport listens on loopback only.
- VS Code launch generates an ephemeral random token.
- Runtime command authentication uses fixed-time token comparison.
- Web deployments should keep debugging disabled unless explicitly enabled for a development session.
- Value history is kept in memory only and is bounded to reduce accidental retention of application data.
