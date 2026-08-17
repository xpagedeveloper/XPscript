# XPScript web CLI sessions

(c) xpagedeveloper.com 2026

The stock Kestrel host can enable the bounded in-memory XPScript session store with `--sessions`.

## Start the server with sessions

```text
xpscript web --root ./site --sessions
```

Sessions are disabled by default. Without `--sessions`, scripts that access `Session` are rejected at runtime.

When enabled, one `XpsSessionStore` is created for the lifetime of the CLI host process. Session data can therefore survive across HTTP requests handled by that process.

## Configure the session cookie and timeout

The CLI exposes the initial session-store controls directly:

```text
--session-cookie NAME
--session-timeout-seconds SECONDS
--session-same-site Strict|Lax|None
--session-secure
```

These options require `--sessions`.

Example:

```text
xpscript web --root ./site --sessions \
  --session-cookie MYSESSION \
  --session-timeout-seconds 3600 \
  --session-same-site Strict
```

To force the Secure attribute even when the hosting integration reports a non-HTTPS scheme:

```text
xpscript web --root ./site --sessions --session-secure
```

`SameSite=None` requires `--session-secure`. The timeout can be configured from 10 seconds through 30 days.

## Basic example

Create `session.xps`:

```xpscript
[Anonymous]
[Get]
Sub SetValue()
    Session.Set("value", "hello")
    Response.Write("stored")
End Sub

[Anonymous]
[Get]
Sub GetValue()
    Response.Write(Session.Get("value"))
End Sub
```

Start the host:

```text
xpscript web --root ./site --sessions
```

Call `/session/SetValue` first, then call `/session/GetValue` with the same browser or HTTP client's cookies. The second request returns `hello`.

## Session cookie

The default cookie is named:

```text
XPSID
```

The session identifier is generated from 32 cryptographically secure random bytes and encoded using URL-safe Base64.

The cookie uses `HttpOnly` and `SameSite=Lax` by default. When the request uses HTTPS, the session cookie is also marked `Secure`.

## Default limits

The CLI uses the standard bounded runtime session configuration:

```text
Idle timeout:          20 minutes
Maximum sessions:      10,000
Entries per session:   128
Maximum value size:    64 KiB
Maximum session size:  1 MiB
Cookie name:           XPSID
SameSite:              Lax
```

When capacity is reached, the runtime rejects creation of additional sessions rather than silently evicting an active session.

## Common operations

```xpscript
Session.Set("name", "Fredrik")
value = Session.Get("name")

If Session.Exists("name") Then
    Call Session.Remove("name")
End If

Call Session.Clear()
```

Other available members include:

```text
Session.Id
Session.Started
Session.Count
Session.Keys
Session.Get(name)
Session.Set(name, value)
Session.Exists(name)
Session.Remove(name)
Session.Unset(name)
Session.Clear()
Session.RotateId()
Session.RegenerateId()
Session.Abandon()
Session.Destroy()
```

## Authentication

The session object supports the authentication convention used by `[Authenticated]` and `[Rule:*]` routes.

```xpscript
[Anonymous]
[Post]
Sub Login()
    ' Validate credentials before authenticating.
    Session.Authenticate("42", "Fredrik", "admin,editor")
    Response.Write("OK")
End Sub

[Authenticated]
[Rule:admin]
[Get]
Sub Admin()
    Response.Write(Session.UserName)
End Sub
```

`Session.Authenticate` rotates the session identifier after storing the authenticated identity and rules. This limits session fixation risk.

To sign out:

```xpscript
Session.SignOut()
```

Sign-out also rotates the session identifier.

## Persistence model

The CLI session store is in-memory and process-local.

Session data is lost when the `xpscript web` process stops or restarts. It is not shared between multiple server processes or nodes.

For a load-balanced or multi-node deployment, use a hosting integration with a distributed session implementation rather than relying on the CLI in-memory store.

CGI is process-per-request and cannot provide useful in-memory session persistence between requests. The `--sessions` option applies to the Kestrel `web` command, not CGI.

## Security guidance

Use HTTPS for authenticated sessions. Do not place secrets in URLs or query strings. Rotate the session id when authentication or privilege changes. Use `Session.Authenticate` for the built-in authentication convention because it performs rotation automatically.

Session state accepts only the bounded runtime state types. Arbitrary CLR objects are rejected.
