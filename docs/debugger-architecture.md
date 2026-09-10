# XPScript debugger architecture

The debugger core is runtime-facing and transport-independent. VS Code, CLI, desktop, web server, and WebAssembly clients should all use the same DebugSession model.

## Layers

1. XPScript runtime/compiler instrumentation emits source locations, frames, variable state, exceptions, and execution events.
2. DebugSession owns breakpoints, stepping state, stack frames, variable inspection, and stop events.
3. IDebugTransport exposes a target over an environment-specific channel.
4. DAP adapter maps VS Code Debug Adapter Protocol requests to DebugSession and transport operations.

## Target transports

CLI and desktop can use an in-process transport first. Desktop can later expose a local named pipe or loopback socket for attach scenarios.

Web applications should expose a dedicated debug endpoint only when debug mode is enabled. Preferred transport is WebSocket over TLS. The endpoint should bind explicitly, require an ephemeral debug token, reject cross-origin browser access by default, and remain disabled in production builds unless explicitly configured.

WebAssembly requires two modes. For browser-hosted WASM, the runtime relays debug events through JavaScript to the VS Code adapter using WebSocket. For WASI or standalone WASM, use a host transport supplied by the embedding runtime. The debugger core must not depend on browser APIs.

## Source mapping

XPScript already tracks expanded source through SourceMap. The debugger should reuse that mapping so breakpoints and stack frames point back to the original .xps file even after includes and preprocessing.

## First milestone

Expose DebugSession, breakpoints, basic stepping state, stack frames, variables, target metadata, and transport interfaces. Runtime instrumentation and DAP integration are added incrementally on top of these contracts.
