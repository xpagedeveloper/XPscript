# XPScript web environments

(c) xpagedeveloper.com 2026

XPScript web hosting has two explicit runtime environments:

```text
Production
Development
```

`Production` is always the default when no environment is selected.

## Command line

Start the Kestrel host in production mode:

```text
xpscript web --root ./site
```

The explicit equivalent is:

```text
xpscript web --root ./site --environment Production
```

Start in development mode:

```text
xpscript web --root ./site --environment Development
```

Environment names are case-insensitive. Any value other than Production or Development is rejected during startup.

## web.cfg

The environment can be selected in the `web` section:

```json
{
  "web": {
    "root": "site",
    "environment": "Production",
    "address": "127.0.0.1",
    "port": 8080
  }
}
```

Development example:

```json
{
  "web": {
    "root": "site",
    "environment": "Development",
    "address": "127.0.0.1",
    "port": 8080
  }
}
```

An explicit command-line value overrides the config file:

```text
xpscript web --config web.cfg --environment Production
```

## Script access

Web scripts can inspect the effective environment through:

```xpscript
Server.Environment
```

Example:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.Write(Server.Environment)
End Sub
```

The value is either:

```text
Production
```

or:

```text
Development
```

## Security model

Selecting `Development` does not disable authentication, path containment, request limits, Host validation, response validation, CSRF protection or the other web security boundaries.

The environment value is an explicit application/runtime mode. Future diagnostics or development-only behavior must opt in to this environment and must not weaken production defaults implicitly.

Do not use Development as a public production deployment mode unless a future feature explicitly documents that its Development behavior is safe for that deployment.

## FastCGI and CGI

The current environment selector is exposed by the `xpscript web` Kestrel CLI path.

FastCGI and CGI continue to construct their server information with the safe Production default unless the embedding host explicitly supplies another `XpsWebEnvironment` value.

This keeps existing FastCGI and CGI deployments unchanged while sharing the same runtime environment contract.
