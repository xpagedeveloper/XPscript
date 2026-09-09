# XPscript debugger

The debugger is implemented as a runtime capability and is exposed to editor integrations through a small transport protocol.

## Current implementation

Generated XPscript code already emits source-line markers. The compiler now preserves both the original source path and line number and forwards them to `XPScriptDebugRuntime`.

Native CLI, desktop and server-side web processes can enable the debugger with these environment variables:

- `XPSCRIPT_DEBUG_PORT` - loopback TCP port used by the debugger transport.
- `XPSCRIPT_DEBUG_TOKEN` - optional authentication token. When configured, every debugger command must contain the token.
- `XPSCRIPT_DEBUG_STOP_ON_ENTRY` - `1` to pause at the first XPscript statement, `0` to run immediately.

The listener binds only to loopback. Remote debugging should therefore use a local tunnel or a future authenticated relay rather than exposing the runtime port publicly.

The initial runtime protocol supports:

- entry stops
- source breakpoints
- continue
- step commands
- disconnect
- source path and line reporting

`StepIn`, `StepOver` and `StepOut` currently share the same single-statement stepping implementation. Proper call-depth-aware stepping is the next runtime milestone.

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
