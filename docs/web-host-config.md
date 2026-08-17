# XPScript web host configuration

(c) xpagedeveloper.com 2026

XPScript can load all current `web` and `fastcgi` host parameters from a JSON config file.

The file extension can be `.cfg`. The content is JSON.

## Config file selection

Explicit file:

```text
xpscript web --config C:\XPScript\production.cfg
xpscript fastcgi --config /etc/xpscript/production.cfg
```

If `--config` is not supplied, XPScript looks for:

```text
web.cfg
```

in the same directory as the running `xpscript` executable/DLL.

If the automatic `web.cfg` does not exist, normal CLI-only behavior is used.

If an explicitly selected `--config` file does not exist, startup fails.

## Precedence

Configuration is applied in this order:

```text
built-in defaults
config file
explicit command-line values
```

For parameters that already have a command-line representation, an explicitly supplied CLI value takes precedence over the config value.

## Relative paths

Paths in the config file are resolved relative to the directory containing that config file.

This applies to:

```text
root
httpsCertificate
structuredLog
unixSocket
```

Example:

```text
C:\XPScript\production\web.cfg
C:\XPScript\production\site\
C:\XPScript\production\logs\
```

With:

```json
{
  "web": {
    "root": "site",
    "structuredLog": "logs/web.jsonl"
  }
}
```

XPScript resolves the values relative to `C:\XPScript\production`.

## Complete Kestrel web example

```json
{
  "web": {
    "root": "site",
    "defaultDocument": "index.xps",
    "address": "127.0.0.1",
    "port": 8080,
    "allowedHosts": [
      "localhost",
      "127.0.0.1",
      "www.example.com"
    ],
    "protocols": "http1+2",
    "httpsCertificate": "certificates/server.pfx",
    "httpsCertificatePasswordEnvironment": "XPS_TLS_PASSWORD",
    "health": true,
    "metrics": true,
    "sessions": true,
    "sessionCookie": "XPSID",
    "sessionTimeoutSeconds": 1200,
    "sessionSameSite": "Lax",
    "sessionSecure": true,
    "operationalExternal": false,
    "structuredLog": "logs/web.jsonl",
    "staticFiles": true,
    "staticMaxBytes": 8388608
  }
}
```

Start it with:

```text
xpscript web --config production.cfg
```

Or, when this file is named `web.cfg` and is next to the executable:

```text
xpscript web
```

## Web properties

The `web` section supports the current Kestrel CLI parameters:

```text
root
    Web application root directory.

defaultDocument
    Default XPScript document. Standard value is index.xps.

address
    Bind IP address. Example: 127.0.0.1 or 0.0.0.0.

port
    Listening TCP port.

allowedHosts
    Array of accepted HTTP Host values.

protocols
    http1, http2 or http1+2.

httpsCertificate
    Path to a PFX certificate.

httpsCertificatePasswordEnvironment
    Name of the environment variable containing the PFX password.

health
    Enables the health endpoint.

metrics
    Enables the metrics endpoint.

sessions
    Enables the bounded in-memory Session store.

sessionCookie
    Session cookie name.

sessionTimeoutSeconds
    Session idle timeout in seconds. Valid range is 10 seconds through 30 days.

sessionSameSite
    Strict, Lax or None.

sessionSecure
    Forces the session cookie Secure flag.

operationalExternal
    Allows enabled operational endpoints to be exposed beyond loopback.

structuredLog
    JSONL structured request log path.

staticFiles
    Enables static-file serving.

staticMaxBytes
    Maximum static-file size in bytes.
```

## FastCGI example

```json
{
  "fastCgi": {
    "root": "site",
    "defaultDocument": "index.xps",
    "listen": "127.0.0.1:9000"
  }
}
```

Start it with:

```text
xpscript fastcgi --config production.cfg
```

### Address and port form

Instead of `listen`:

```json
{
  "fastCgi": {
    "root": "site",
    "address": "127.0.0.1",
    "port": 9000
  }
}
```

Do not combine `listen` with `address` or `port` in the same config section.

## FastCGI Unix socket

Linux and macOS can use:

```json
{
  "fastCgi": {
    "root": "site",
    "unixSocket": "/run/xpscript/site.sock"
  }
}
```

`unixSocket` cannot be combined with TCP listener settings in the same config section.

## One file for both hosting modes

One file may contain both sections:

```json
{
  "web": {
    "root": "site",
    "address": "127.0.0.1",
    "port": 8080,
    "sessions": true
  },
  "fastCgi": {
    "root": "site",
    "listen": "127.0.0.1:9000"
  }
}
```

`xpscript web` reads the `web` section. `xpscript fastcgi` reads the `fastCgi` section.

## CLI override example

Given:

```json
{
  "web": {
    "root": "site",
    "port": 8080,
    "defaultDocument": "home.xps"
  }
}
```

You can override selected values:

```text
xpscript web --config web.cfg --port 9090 --default-document index.xps
```

The effective values are port `9090` and default document `index.xps`.

## Validation

The parser is strict. Unknown properties are rejected.

This config contains a typo and does not start:

```json
{
  "web": {
    "root": "site",
    "porrt": 8080
  }
}
```

This prevents misspelled security or network settings from being silently ignored.

JSON comments and trailing commas are accepted.

## Certificate passwords and secrets

Do not store certificate passwords directly in `web.cfg`.

Store the password in an environment variable:

```text
XPS_TLS_PASSWORD=<secret>
```

Then configure only the environment variable name:

```json
{
  "web": {
    "httpsCertificate": "certificates/server.pfx",
    "httpsCertificatePasswordEnvironment": "XPS_TLS_PASSWORD"
  }
}
```

The same rule should be used for future secret-bearing host settings. The config file should contain references to secret sources rather than plaintext secrets.
