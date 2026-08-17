# XPScript static file serving policy

Kestrel mode may optionally serve static assets from the configured XPScript site root. Static serving is disabled by default.

FastCGI and CGI deployments should normally let nginx, HCL Domino, IIS or another front-end web server serve static assets directly. XPScript FastCGI/CGI transports do not implement a parallel static-file server.

## Kestrel rules

When enabled, only explicitly allowlisted file extensions may be returned. The default allowlist is intended for public web assets such as CSS, JavaScript, images and fonts.

XPScript source files are never eligible for static serving. `.xps` is always handled by the XPScript routing/compiler pipeline or returns a normal route error.

Static-file lookup uses the configured site root and the same canonical path and symbolic-link/reparse-point escape protection as `Server.MapPath`.

Dotfiles are rejected by default. Directory browsing is not supported. Static default documents are not resolved automatically.

Unknown extensions return 404 rather than being served as `application/octet-stream`.

The Kestrel static-file feature is opt-in because production deployments behind nginx or another reverse proxy should normally let that front-end server serve cacheable static assets.
