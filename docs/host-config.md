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
