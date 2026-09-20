# Mandatory web logging

Every XPScript web host writes structured logs automatically. Access, error and security/audit logging cannot be disabled. Kestrel, FastCGI and CGI use the same UTF-8 JSON Lines format.

## Files and retention

The default directory is the process user's local application-data directory under `XPScript/logs/<site-id>`. Set an explicit directory with `--log-directory PATH` or `logDirectory` in `web.cfg`. The host validates the canonical path during startup and refuses a directory that is the web root or lies below it. This prevents the route and static-file servers from retrieving log files.

The runtime writes separate files:

| Prefix | Contents |
|---|---|
| `access-` | One entry for every HTTP request, including health checks, static files and rejected requests. |
| `application-` | Events written with `Application.Log`. |
| `security-` | Audit and security events written with `Application.Audit`. |
| `error-` | Requests that end in a 5xx response or an unhandled exception. |

A file rotates when it reaches 100 MiB or the UTC date changes. Closed files are gzip-compressed. Local retention is 14 days with a 2 GiB total quota. The oldest closed files are removed first when the quota is exceeded.

## Format

Each line is one JSON object using schema `xpscript.web.log/1`. The names follow the OpenTelemetry log data model and HTTP semantic conventions where applicable.

Core fields are `timestamp`, `observed_timestamp`, `severity_text`, `severity_number`, `event_name`, `body` and `attributes`. Trace and span IDs are included when a .NET activity is active. Standard attributes include service, environment, site, hosting mode, request ID, HTTP method, path, status, byte counts, duration and client address.

The runtime never logs query strings, request or response bodies, cookies, authorization headers, session identifiers or tokens. Control characters are normalized to prevent log injection.

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
