# XPScript web runtime production limits

(c) xpagedeveloper.com 2026

This document records the limits that are currently enforced by the XPScript web runtime. It describes implementation defaults and validation ranges, not recommended values for every deployment.

## Trust boundary

XPScript web source is trusted server-side application code. The current web runtime does not provide a sandbox for untrusted tenant-supplied source code.

Request paths, query values, headers, cookies, forms, request bodies, FastCGI records and CGI environment values are treated as untrusted transport input.

All web transports share the same route resolver, compiler target, compilation cache and request context.

## Kestrel defaults

`XpsKestrelOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Bind address | loopback | explicit IP address |
| Port | 8080 | 0 to 65535 |
| HTTPS certificate | none | explicit existing certificate file enables direct TLS |
| HTTP protocols | HTTP/1.1 + HTTP/2 | HTTP/1.1, HTTP/2 or HTTP/1.1 + HTTP/2 |
| Maximum request body | 1 MiB | 0 to 1 GiB |
| Maximum concurrent connections | 256 | 1 to 1,000,000 |
| Maximum request line | 8 KiB | 1 KiB to 1 MiB |
| Maximum request headers total | 32 KiB | 1 KiB to 4 MiB |
| Request headers timeout | 15 seconds | greater than 0, maximum 10 minutes |
| Keep-alive timeout | 30 seconds | greater than 0, maximum 10 minutes |
| Minimum request body rate | 240 bytes/second | positive rate or explicitly disabled with `null` |
| Request body rate grace | 5 seconds | 1 second to 10 minutes |
| Minimum response rate | 240 bytes/second | positive rate or explicitly disabled with `null` |
| Response rate grace | 5 seconds | 1 second to 10 minutes |
| Allowed hosts | `localhost`, `127.0.0.1`, `[::1]` | at least one valid value |
| Trusted proxies | none | explicitly configured IP addresses only |
| Health endpoint | disabled | opt-in |
| Metrics endpoint | disabled | opt-in |
| Operational endpoint access | loopback only | explicit opt-out required for network access |
| Health path | `/_xps/health` | absolute URL path, maximum 512 characters |
| Metrics path | `/_xps/metrics` | absolute URL path, maximum 512 characters and different from health path |

The public `xpscript web` command keeps the loopback bind and loopback Host allowlist unless the operator explicitly changes them.

Forwarded headers are not trusted unless known proxies are configured.

### Direct HTTPS

Direct Kestrel TLS is enabled by supplying a certificate file. The CLI uses:

```text
XPS_TLS_PASSWORD=secret xpscript web --root ./site --port 8443 --host www.example.com \
  --https-cert ./server.pfx --https-cert-password-env XPS_TLS_PASSWORD
```

The certificate password is intentionally read from an environment variable rather than accepted as a plaintext command-line value. An unprotected certificate file can omit `--https-cert-password-env`.

The initial direct Kestrel protocol contract supports HTTP/1.1, HTTP/2, or HTTP/1.1 + HTTP/2. HTTP/3 is deliberately rejected until XPScript has a separate QUIC deployment and regression contract.

TLS certificate file permissions remain an operator responsibility. The runtime validates that the configured certificate path exists before the listener starts.

## FastCGI TCP defaults

`XpsFastCgiOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Bind address | loopback | explicit IP address |
| Port | 9000 | 0 to 65535 |
| Maximum concurrent connections | 128 | 1 to 100,000 |
| Maximum accumulated PARAMS bytes | 64 KiB | 1 KiB to 16 MiB |
| Maximum parameter count | 256 | 1 to 100,000 |
| Maximum parameter name | 1 KiB | 1 byte up to `MaxParamsBytes` |
| Maximum parameter value | 16 KiB | 1 byte up to `MaxParamsBytes` |
| Maximum request body | 4 MiB | 0 to 256 MiB |
| Maximum HTTP header count | 128 | 1 to 10,000 |
| Maximum HTTP header value | 16 KiB | 1 byte to 1 MiB |

The FastCGI parser validates record framing, parameter lengths, ordering and total accumulated input before dispatching the request.

## FastCGI Unix-domain socket defaults

`XpsFastCgiUnixSocketOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Socket path | required | absolute path, maximum 100 UTF-8 bytes |
| Listen backlog | 128 | 1 to 65,535 |
| Maximum concurrent connections | 128 | 1 to 100,000 |
| Socket file mode | user/group read-write | explicit `UnixFileMode` |

Unix-domain FastCGI hosting is supported on Linux and macOS. The listener refuses to overwrite an already existing socket path.

## CGI defaults

`XpsCgiOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Maximum request body | 4 MiB | 0 to 256 MiB |
| Maximum HTTP header count | 128 | 1 to 10,000 |
| Maximum HTTP header value | 16 KiB | 1 byte to 1 MiB |

CGI validates `CONTENT_LENGTH` before reading the request body and rejects truncated bodies.

CGI is process-per-request. In-memory Session or Application state must not be treated as persistent across separate CGI invocations.

## Compilation cache defaults

`XpsWebCompilationCacheOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Maximum entries | 128 | 1 to 4096 |
| Maximum source bytes | 4 MiB | 1 byte to 64 MiB |
| Idle TTL | 20 minutes | 1 second to 1 day |
| Failed compilation backoff | 2 seconds | 100 ms to 5 minutes |
| Configuration identity | `default` | 1 to 512 characters |

The cache uses single-flight compilation for the same active source identity. Source/dependency identity changes retire the previous generation instead of mutating an assembly that is in use by active requests.

## Session defaults

`XpsSessionOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Cookie name | `XPSID` | valid response-header token, must not begin with `$` |
| Idle timeout | 20 minutes | 10 seconds to 30 days |
| Maximum sessions | 10,000 | 1 to 1,000,000 |
| Maximum entries per session | 128 | 1 to 10,000 |
| Maximum value size | 64 KiB | 1 byte to 16 MiB |
| Maximum bytes per session | 1 MiB | at least `MaxValueBytes`, maximum 64 MiB |
| SameSite | `Lax` | `Strict`, `Lax` or `None` |
| Secure cookie required | false | `SameSite=None` requires true |

Session IDs are generated cryptographically by the runtime. Session rotation replaces the identifier without copying values into a separate session record.

Responses that set cookies enforce `Cache-Control: no-store`. This is reasserted at response completion so later application code cannot accidentally make a session-bearing response cacheable.

The default session store is in-memory and process-local. Multi-node deployments need an explicitly designed shared session provider before session affinity can be removed.

## Application state defaults

`XpsApplicationStateOptions` currently uses:

| Setting | Default | Accepted range / rule |
|---|---:|---|
| Maximum entries | 256 | 1 to 100,000 |
| Maximum value size | 64 KiB | 1 byte to 16 MiB |
| Maximum total bytes | 4 MiB | at least `MaxValueBytes`, maximum 256 MiB |
| State name length | maximum 256 characters | non-empty |

Application state is in-memory and process-local. Access is synchronized by the runtime.

## Health and metrics

Kestrel health and metrics are disabled by default. Enable them through `XpsKestrelOptions` or the public CLI:

```text
xpscript web --root ./site --health --metrics
```

The default endpoints are:

```text
/_xps/health
/_xps/metrics
```

Both endpoints accept only GET and HEAD. Other methods return 405.

Operational endpoints remain loopback-only by default even when the application bind address is external. A non-loopback caller receives 404. The CLI requires the explicit `--operational-external` switch before these endpoints become network-accessible.

Health returns JSON. Healthy state returns HTTP 200. Once application shutdown begins, health state changes to `Stopping` and returns HTTP 503 while the endpoint remains reachable.

Metrics use Prometheus text exposition and currently expose:

- health state
- active requests
- total requests
- failed requests
- 2xx, 3xx, 4xx and 5xx response counters
- request body byte counter
- response body byte counter
- process-local web runtime uptime

Operational endpoint requests themselves are intentionally excluded from application request counters.

## Structured request events

`XpsWebTelemetry` can receive an `IXpsWebEventSink`. `XpsWebJsonLineEventSink` writes one JSON object per completed application request.

The public CLI enables this with:

```text
xpscript web --root ./site --structured-log ./logs/web.jsonl
```

The stable request event contains only:

- UTC timestamp
- transport name
- HTTP method
- status code
- duration in milliseconds
- request body byte count
- response body byte count
- failure flag

It deliberately excludes request path, query string, headers, cookies, request body, response body, remote address and site root. This reduces accidental credential and personal-data disclosure in operational logs.

The JSONL file is append-only for the lifetime of the host process and is opened with read sharing so log collectors can consume it while the service is running.

## Capacity planning

The accepted validation maximum is a safety ceiling, not a deployment recommendation. Increasing request bodies, parameter sizes, session counts, state memory or concurrent connections increases memory and denial-of-service exposure.

Set deployment limits from measured workload and available process memory. Keep reverse-proxy limits at least as strict as the corresponding XPScript transport limits when the proxy is the first request boundary.
