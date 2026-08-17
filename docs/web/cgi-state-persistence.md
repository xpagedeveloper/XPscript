# CGI state persistence

Classic CGI starts a new operating-system process for each request. In-memory Session and Application objects therefore cannot provide the same cross-request lifetime as Kestrel or FastCGI.

XPScript CGI uses a site-isolated persistent state file for Session and Application when state support is enabled.

## Contract

- State is isolated by the configured XPScript site id and state root.
- A bounded exclusive file lock protects one CGI site state transaction from concurrent CGI processes.
- The lock is held while state is loaded, the request executes, and state is saved. This initial implementation intentionally serializes state-enabled CGI requests for correctness.
- Session IdleTimeout and SlidingIdleTimeout use the same runtime semantics as Kestrel and FastCGI.
- Application IdleTimeout and SlidingIdleTimeout use the same runtime semantics as Kestrel and FastCGI.
- RequestScope is never persisted.
- Session identifiers remain stored only in the session cookie. The persistent state file stores server-side values keyed by session id.
- State files are bounded and use the runtime scalar/byte-array state policy. Arbitrary CLR object deserialization is not supported.
- Corrupt, oversized or unavailable persistent state fails closed. The CGI host must not silently discard existing state and continue with a fresh state store.
- State paths are configuration-owned. Request values cannot choose the state directory or file name.

## Configuration

The CGI host will use `XPSCRIPT_STATE_ROOT` for persistent state. When omitted, persistent CGI Session/Application support is disabled rather than writing state into a request-controlled location.

The state root should be writable only by the account running the CGI executable and should not be web-readable.
