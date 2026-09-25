# Web and FastCGI host configuration

XPScript can load Kestrel and FastCGI command-line settings from a JSON configuration file.

Use an explicit file with:

```text
xpscript web --config ./production.cfg
xpscript fastcgi --config ./production.cfg
```

For `web`, XPScript also automatically loads `web.cfg` from the directory containing the `xpscript` executable when that file exists. Explicit command-line options override matching values from the configuration file.

Unknown properties are rejected. JSON property names are case-insensitive. Trailing commas and JSON comments are accepted. Relative paths in the file are resolved relative to the directory containing the configuration file.

A single file may contain both a `web` and a `fastCgi` section:

```json
{
  "web": {
    "root": "./site",
    "defaultDocument": "index.xps",
    "environment": "Production",
    "address": "127.0.0.1",
    "port": 8080,
    "allowedHosts": ["localhost", "example.com"],
    "protocols": "http1+2",
    "httpsCertificate": "./certificates/site.pfx",
    "httpsCertificatePasswordEnvironment": "XPSCRIPT_CERT_PASSWORD",
    "health": true,
    "metrics": true,
    "sessions": true,
    "sessionCookie": "XPSSESSION",
    "sessionTimeoutSeconds": 1200,
    "sessionSameSite": "Strict",
    "sessionSecure": true,
    "operationalExternal": false,
    "structuredLog": "./logs/requests.jsonl",
    "logDirectory": "./logs",
    "staticFiles": true,
    "staticMaxBytes": 10485760
  },
  "fastCgi": {
    "root": "./site",
    "defaultDocument": "index.xps",
    "listen": "127.0.0.1:9000",
    "logDirectory": "./logs"
  }
}
```


## Property combinations and conflicts

### Web / Kestrel

Most `web` properties are independent and can be combined. The following relationships are important:

| Properties | Can be combined? | Rule |
|---|---|---|
| `address` + `port` | Yes | Together they select the listener endpoint. |
| `allowedHosts` + `address`/`port` | Yes | Listener binding and accepted HTTP Host values are separate controls. |
| `protocols` + `address`/`port` | Yes | Selects HTTP protocol support on the configured listener. |
| `httpsCertificate` + `httpsCertificatePasswordEnvironment` | Yes | The password environment property supplies the password for the PFX certificate. |
| `sessions` + `sessionCookie` | Yes | Session cookie settings apply when sessions are enabled. |
| `sessions` + `sessionTimeoutSeconds` | Yes | Configures session idle timeout. |
| `sessions` + `sessionSameSite` | Yes | Configures the session cookie SameSite policy. |
| `sessions` + `sessionSecure` | Yes | Configures the session cookie Secure attribute. |
| `health` + `metrics` | Yes | Either or both operational endpoints may be enabled. |
| `health`/`metrics` + `operationalExternal` | Yes | `operationalExternal` changes network exposure of enabled operational endpoints. |
| `staticFiles` + `staticMaxBytes` | Yes | The size limit applies to static-file responses. |
| `structuredLog` + `logDirectory` | Yes | These select different logging paths and may be configured together. |

Settings such as `sessionCookie`, `sessionTimeoutSeconds`, `sessionSameSite`, and `sessionSecure` do not themselves enable sessions; use `sessions: true`. Likewise, `staticMaxBytes` does not itself enable static-file serving; use `staticFiles: true`. `operationalExternal` does not itself enable health or metrics.

### FastCGI

FastCGI has three listener forms. Choose exactly one listener model:

| Properties | Can be combined? | Rule |
|---|---|---|
| `listen` | Yes, by itself | Complete TCP endpoint such as `127.0.0.1:9000`. |
| `address` + `port` | Yes | Split TCP listener configuration. |
| `unixSocket` | Yes, by itself | Unix-domain socket on Linux/macOS. |
| `listen` + `address` | **No** | Conflicting TCP listener definitions. |
| `listen` + `port` | **No** | Conflicting TCP listener definitions. |
| `listen` + `address` + `port` | **No** | Conflicting TCP listener definitions. |
| `unixSocket` + `listen` | **No** | Unix socket and TCP listener cannot both be selected. |
| `unixSocket` + `address` | **No** | Unix socket and TCP listener cannot both be selected. |
| `unixSocket` + `port` | **No** | Unix socket and TCP listener cannot both be selected. |

`root`, `defaultDocument`, and `logDirectory` can be combined with any valid FastCGI listener model.

### Config file and command-line values

Values supplied explicitly on the command line override the corresponding value from the config file. Aliases are treated as the same setting; for example, command-line `--bind` overrides config `address`, and `--allowed-host`/`--host` override config `allowedHosts`.

For FastCGI, do not use a config listener model and then select a conflicting listener model on the command line. Keep the effective configuration to one of: `listen`, `address` + `port`, or `unixSocket`.

## Web / Kestrel options

Every setting below can be supplied either in the `web` object of the config file or directly on the command line. The behavior is the same; the config property and CLI option columns show the two forms.

| Config property | Command-line form | What it is used for |
|---|---|---|
| `root` | `--root PATH` | Selects the directory that is the web application's document/source root. XPScript routes, default documents and enabled static files are resolved below this directory. |
| `defaultDocument` | `--default-document FILE.xps` | Selects the XPScript file used when a request resolves to a directory/root URL instead of naming a route file explicitly. |
| `environment` | `--environment Production|Development` | Selects the web runtime environment. Use `Production` for deployed sites and `Development` for development behavior. |
| `address` | `--address IP` or `--bind IP` | Selects the local network interface/IP address on which Kestrel listens. This controls binding, not which HTTP Host headers are accepted. |
| `port` | `--port PORT` | Selects the TCP port on which Kestrel accepts HTTP/HTTPS connections. |
| `allowedHosts` | repeat `--host HOST` or `--allowed-host HOST` | Restricts accepted HTTP Host header values. This is separate from the listener address and is useful when a site should answer only for specific host names. |
| `protocols` | `--protocols http1|http2|http1+2` | Selects which HTTP protocol versions the listener accepts. |
| `httpsCertificate` | `--https-cert FILE` | Supplies the PFX certificate used by Kestrel for HTTPS. |
| `httpsCertificatePasswordEnvironment` | `--https-cert-password-env NAME` | Names an environment variable containing the PFX password, keeping the password itself out of the config file and command line. |
| `health` | `--health` | Enables the operational health endpoint used by monitoring, orchestration or load-balancer health checks. |
| `metrics` | `--metrics` | Enables the operational metrics endpoint. |
| `sessions` | `--sessions` | Enables server-side in-memory web sessions and the XPScript session cookie. |
| `sessionCookie` | `--session-cookie NAME` | Changes the cookie name used for XPScript sessions. It matters only when sessions are enabled. |
| `sessionTimeoutSeconds` | `--session-timeout-seconds SECONDS` | Sets how long an inactive in-memory session remains valid. It matters only when sessions are enabled. |
| `sessionSameSite` | `--session-same-site Strict|Lax|None` | Sets the SameSite policy of the session cookie. It matters only when sessions are enabled. |
| `sessionSecure` | `--session-secure` | Requires the session cookie to carry the Secure attribute. It matters only when sessions are enabled and is intended for HTTPS deployments. |
| `operationalExternal` | `--operational-external` | Allows enabled operational endpoints such as health and metrics to be exposed beyond loopback. It does not enable those endpoints itself. |
| `structuredLog` | `--structured-log FILE` | Selects an explicit JSON-lines structured request log file. |
| `logDirectory` | `--log-directory PATH` | Selects the directory used by the mandatory web logging system. |
| `staticFiles` | `--static-files` | Enables serving permitted static files from the web root. XPScript source files remain protected by the web runtime. |
| `staticMaxBytes` | `--static-max-bytes BYTES` | Sets the maximum size of a static file response. It matters only when static-file serving is enabled. |

### Equivalent Web examples

These two forms configure the same basic listener:

```text
xpscript web --root ./site --address 127.0.0.1 --port 8080 --host example.com --sessions
```

```json
{
  "web": {
    "root": "./site",
    "address": "127.0.0.1",
    "port": 8080,
    "allowedHosts": ["example.com"],
    "sessions": true
  }
}
```

Run the config-file form with:

```text
xpscript web --config ./production.cfg
```

## `web` properties

| Property | Type | CLI equivalent | Description |
|---|---|---|---|
| `root` | string | `--root` | Web application root. Relative paths are resolved from the config-file directory. |
| `defaultDocument` | string | `--default-document` | Default XPScript document. |
| `environment` | string | `--environment` | `Production` or `Development`. |
| `address` | string | `--address` / `--bind` | Listener IP address. |
| `port` | integer | `--port` | Listener TCP port. |
| `allowedHosts` | string[] | `--host` / `--allowed-host` | Accepted HTTP Host values. |
| `protocols` | string | `--protocols` | `http1`, `http2`, or `http1+2`. |
| `httpsCertificate` | string | `--https-cert` | PFX certificate path, resolved relative to the config file. |
| `httpsCertificatePasswordEnvironment` | string | `--https-cert-password-env` | Environment variable containing the certificate password. |
| `health` | boolean | `--health` | Enables the health endpoint. |
| `metrics` | boolean | `--metrics` | Enables the metrics endpoint. |
| `sessions` | boolean | `--sessions` | Enables in-memory sessions. |
| `sessionCookie` | string | `--session-cookie` | Session cookie name. |
| `sessionTimeoutSeconds` | integer | `--session-timeout-seconds` | Session idle timeout in seconds. |
| `sessionSameSite` | string | `--session-same-site` | Session cookie SameSite setting. |
| `sessionSecure` | boolean | `--session-secure` | Enables the Secure session-cookie attribute. |
| `operationalExternal` | boolean | `--operational-external` | Allows enabled operational endpoints beyond loopback. |
| `structuredLog` | string | `--structured-log` | Structured request-log path, resolved relative to the config file. |
| `logDirectory` | string | `--log-directory` | Host log directory, resolved relative to the config file. |
| `staticFiles` | boolean | `--static-files` | Enables static-file serving. |
| `staticMaxBytes` | integer | `--static-max-bytes` | Maximum static-file response size. |

## FastCGI options

Every FastCGI setting below can likewise be supplied in the `fastCgi` config object or directly on the command line.

| Config property | Command-line form | What it is used for |
|---|---|---|
| `root` | `--root PATH` | Selects the XPScript web application root handled by the FastCGI worker. |
| `defaultDocument` | `--default-document FILE.xps` | Selects the XPScript default document for directory/root requests. |
| `listen` | `--listen ADDRESS:PORT` | Configures the complete private TCP endpoint used by a reverse proxy/web server to communicate with the FastCGI worker. |
| `address` | `--address IP` or `--bind IP` | Configures only the FastCGI TCP bind address when the split address/port form is preferred. |
| `port` | `--port PORT` | Configures only the FastCGI TCP port when the split address/port form is used. |
| `unixSocket` | `--unix-socket PATH` | Uses a Unix-domain socket instead of TCP on Linux/macOS. This is useful for a local reverse proxy and avoids exposing a TCP listener. |
| `logDirectory` | `--log-directory PATH` | Selects the directory used by the mandatory FastCGI web logging system. |

### Equivalent FastCGI examples

Complete TCP endpoint on the command line:

```text
xpscript fastcgi --root ./site --listen 127.0.0.1:9000 --log-directory ./logs
```

The same settings in `production.cfg`:

```json
{
  "fastCgi": {
    "root": "./site",
    "listen": "127.0.0.1:9000",
    "logDirectory": "./logs"
  }
}
```

started with:

```text
xpscript fastcgi --config ./production.cfg
```

The split TCP form is:

```text
xpscript fastcgi --root ./site --address 127.0.0.1 --port 9000
```

or:

```json
{
  "fastCgi": {
    "root": "./site",
    "address": "127.0.0.1",
    "port": 9000
  }
}
```

A Unix-socket setup is:

```text
xpscript fastcgi --root /srv/xpsite --unix-socket /run/xpscript/site.sock
```

or:

```json
{
  "fastCgi": {
    "root": "/srv/xpsite",
    "unixSocket": "/run/xpscript/site.sock"
  }
}
```

## `fastCgi` properties

| Property | Type | CLI equivalent | Description |
|---|---|---|---|
| `root` | string | `--root` | Web application root. Relative paths are resolved from the config-file directory. |
| `defaultDocument` | string | `--default-document` | Default XPScript document. |
| `listen` | string | `--listen` | TCP endpoint, for example `127.0.0.1:9000`. |
| `address` | string | `--address` / `--bind` | TCP listener address when `listen` is not used. |
| `port` | integer | `--port` | TCP listener port when `listen` is not used. |
| `unixSocket` | string | `--unix-socket` | Unix-domain socket path on Linux/macOS, resolved relative to the config file. |
| `logDirectory` | string | `--log-directory` | Host log directory, resolved relative to the config file. |

For FastCGI, `listen` cannot be combined with `address` or `port`. `unixSocket` cannot be combined with any TCP listener setting.

A minimal FastCGI production file can therefore be:

```json
{
  "fastCgi": {
    "root": "./site",
    "listen": "127.0.0.1:9000"
  }
}
```

and started with:

```text
xpscript fastcgi --config ./production.cfg
```

A minimal Kestrel configuration can be:

```json
{
  "web": {
    "root": "./site",
    "address": "127.0.0.1",
    "port": 8080
  }
}
```

and started with:

```text
xpscript web --config ./production.cfg
```
