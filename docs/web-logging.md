# Mandatory web logging

Every XPScript web host writes structured logs automatically. Access, error and security/audit logging cannot be disabled. XPScript WebServer, FastCGI and CGI use the same UTF-8 JSON Lines format.

## Files and retention

The default directory is the process user's local application-data directory under `XPScript/logs/<site-id>`. Set an explicit directory with `--log-directory PATH` or `logDirectory` in `web.cfg`. The host validates the canonical path during startup and refuses a directory that is the web root or lies below it. This prevents the route and static-file servers from retrieving log files.

The runtime writes separate files:

| Prefix | Contents |
|---|---|
| `access-` | One entry for every HTTP request, including health checks, static files and rejected requests. |
| `application-` | Events written with `Application.Log`. |
| `security-` | Audit and security events written with `Application.Audit`. |
| `error-` | Requests that end in a 5xx response or an unhandled exception. |
| `exchange-` | Opted-in troubleshooting captures with redacted request and response data. |

A file rotates when it reaches 100 MiB or the UTC date changes. Closed files are gzip-compressed. Local retention is 14 days with a 2 GiB total quota. The oldest closed files are removed first when the quota is exceeded.

## Format

Each line is one JSON object using schema `xpscript.web.log/1`. The names follow the OpenTelemetry log data model and HTTP semantic conventions where applicable.

Core fields are `timestamp`, `observed_timestamp`, `severity_text`, `severity_number`, `event_name`, `body` and `attributes`. Trace and span IDs are included when a .NET activity is active. Standard attributes include service, environment, site, hosting mode, request ID, HTTP method, path, status, byte counts, duration and client address.

The runtime never logs query strings, request or response bodies, cookies, authorization headers, session identifiers or tokens. Control characters are normalized to prevent log injection.

## Client and request correlation

Every web response receives a site-specific `XPSLOGID_<site-hash>` correlation cookie when the client does not already have one. The cookie is HttpOnly, SameSite=Lax, valid for 30 days and Secure over HTTPS. Every standard, application, audit, error and exchange entry includes the same `session.id` for that browser or client. The value in the log is a SHA-256 digest. The site-specific name prevents collisions when several XPScript applications share one IIS or web domain. The raw cookie and authentication session identifier are never logged. Each request also has its own `request.id`.

## Full exchange troubleshooting

Set `Application.Log.CaptureExchange = True` during a request to write that request and response to the separate `exchange-` stream. The flag is request-scoped and starts as false on every request.

The capture includes method, path, sanitized query parameters, headers, bodies, status and content types. Authorization, cookies, API keys and secret-bearing JSON or form fields are replaced with `[REDACTED]`. Each body is limited to 256 KiB and records whether it was truncated. Binary bodies use Base64.

```xpscript
Application.Log.CaptureExchange = True
Application.Log.Info("support.capture", "Capturing this request for incident INC-1042")
```

Enable capture only for the affected request. Free-text and non-JSON custom formats cannot be field-redacted reliably.

## Application events

Use stable event names. They may contain letters, digits, dot, underscore and hyphen.

```xpscript
Dim fields As New XPJsonObject
fields.Set("order.id", 42)
fields.Set("order.total", 199.50)

Application.Log.Info("order.created", "Order was created", fields)
Application.Log.Warning("order.payment.delayed", "Payment confirmation is delayed")
Application.Log.Error("order.delivery.failed", "Delivery provider rejected the request")
Application.Audit.Write("user.permission.changed", "Administrator changed a role", fields)
```

Available application levels are `Trace`, `Debug`, `Info`, `Warning`, `Error` and `Critical`. Each method accepts `eventName`, `message` and an optional `XPJsonObject` of attributes. Audit events accept the same arguments and always go to the security stream.

Attribute names reserved by the runtime cannot be overridden. Attributes whose names identify passwords, secrets, tokens, cookies, authorization, connection strings, encryption keys or session identifiers are rejected. Attribute JSON is limited to 64 KiB and eight levels of nesting. Messages are limited to 8192 characters.

## Operations

Give the service identity write access to the log directory and deny access to the web-serving identity where those identities differ. Forward the JSONL files to centralized immutable storage for enterprise retention and alerting. Monitor stderr because compression or retention cleanup failures are reported there.


## Platform contract

XPScript WebServer, IIS reverse proxy, FastCGI and CGI use the same correlation helper, cookie lifetime, hash format, `session.id` field, exchange schema and redaction rules. The only transport-dependent cookie attribute is `Secure`. It is present for externally HTTPS requests and absent for HTTP so both protocols work. IIS must preserve the original scheme through ASP.NET Core Module or trusted forwarded headers.
