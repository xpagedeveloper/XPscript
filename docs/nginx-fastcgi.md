# Hosting an XPScript FastCGI application with Nginx

Nginx can terminate HTTP/HTTPS and proxy requests to a persistent XPScript FastCGI worker.

```text
Browser -> Nginx -> FastCGI -> xpscript fastcgi -> .xps web root
```

The FastCGI listener is private and speaks FastCGI, not HTTP. Do not expose it directly to the internet.

## Requirements

- Nginx with the FastCGI module.
- A published XPScript host for the server OS.
- A web-root directory containing the application `.xps` files.
- A service account with read/execute access to the host and site.

## 1. Publish and deploy XPScript

Linux example:

```bash
dotnet publish ./src/XPScript.Cli/XPScript.Cli.csproj \
  -c Release -r linux-x64 --self-contained true -o /opt/xpscript/bin
```

Deploy the site separately:

```text
/opt/xpscript/bin/xpscript
/opt/xpscript/sites/example/index.xps
/opt/xpscript/sites/example/users.xps
/opt/xpscript/logs/
```

Keep the host and `.xps` source outside Nginx's static `root` directory. A framework-dependent deployment is also supported when the matching .NET runtime is installed.

## 2. Start the FastCGI worker

Run it on loopback:

```bash
/opt/xpscript/bin/xpscript fastcgi \
  --root /opt/xpscript/sites/example \
  --listen 127.0.0.1:9000 \
  --log-directory /opt/xpscript/logs
```

The worker should be supervised by `systemd`, not started from an interactive shell in production.

Create `/etc/systemd/system/xpscript-fastcgi.service`:

```ini
[Unit]
Description=XPScript FastCGI application
After=network.target

[Service]
Type=simple
User=xpscript
Group=xpscript
ExecStart=/opt/xpscript/bin/xpscript fastcgi --root /opt/xpscript/sites/example --listen 127.0.0.1:9000 --log-directory /opt/xpscript/logs
Restart=always
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
```

Enable it:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now xpscript-fastcgi
sudo systemctl status xpscript-fastcgi
```

Check the private listener:

```bash
ss -ltn '( sport = :9000 )'
```

## 3. Configure Nginx

Create `/etc/nginx/sites-available/xpscript-example`:

```nginx
server {
    listen 80;
    server_name example.com www.example.com;

    # This directory is only a placeholder. Application requests are proxied.
    root /var/empty/xpscript-example;

    # Serve selected static assets directly, if required.
    location /assets/ {
        alias /opt/xpscript/sites/example/assets/;
        try_files $uri =404;
    }

    # Do not let Nginx return source or deployment files as static content.
    location ~* \.(xps|dll|pdb|json)$ {
        return 404;
    }

    # XPScript resolves the request path and dispatches the .xps route.
    location / {
        include fastcgi_params;
        fastcgi_param SCRIPT_FILENAME /opt/xpscript/sites/example$fastcgi_script_name;
        fastcgi_param DOCUMENT_ROOT /opt/xpscript/sites/example;
        fastcgi_param PATH_INFO $fastcgi_path_info;
        fastcgi_param HTTPS $https if_not_empty;
        fastcgi_pass 127.0.0.1:9000;
        fastcgi_read_timeout 120s;
    }
}
```

The `SCRIPT_FILENAME` and `DOCUMENT_ROOT` values should match the `--root` passed to XPScript. The regular-expression source-file block is a safety boundary for accidental static serving; remove it only if the application intentionally exposes an `.xps` route and has a separate safe mapping for that route.

Enable and validate the site:

```bash
sudo ln -s /etc/nginx/sites-available/xpscript-example /etc/nginx/sites-enabled/xpscript-example
sudo nginx -t
sudo systemctl reload nginx
```

On distributions without `sites-enabled`, place the server block in `/etc/nginx/conf.d/xpscript-example.conf`.

## 4. HTTPS

Create an HTTPS server block and configure the certificate paths:

```nginx
server {
    listen 443 ssl http2;
    server_name example.com;

    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    # Use the same locations and fastcgi_pass settings as the HTTP server.
}

server {
    listen 80;
    server_name example.com;
    return 301 https://$host$request_uri;
}
```

Nginx passes the HTTPS state through the CGI variables used by XPScript. Keep the FastCGI hop on loopback; it does not need a public certificate.

## 5. Test the application

```bash
curl -i http://example.com/
curl -i http://example.com/users
curl -i http://example.com/users?active=true
curl -i http://example.com/index.xps
```

Verify that:

- application routes return the expected XPScript response;
- query strings, request bodies and headers arrive at XPScript;
- `.xps`, `.dll` and deployment files are not downloadable;
- Nginx access/error logs and XPScript FastCGI logs contain the request.

For local testing before DNS is configured:

```bash
curl --resolve example.com:80:127.0.0.1 http://example.com/
```

## Windows Nginx

The Nginx server block is the same conceptually on Windows. Start the XPScript worker with `xpscript.exe fastcgi --root C:\XPScript\sites\example --listen 127.0.0.1:9000`, run it through a Windows service manager, and use Windows paths in `SCRIPT_FILENAME` and `DOCUMENT_ROOT`. Keep the FastCGI port bound to loopback.

## Troubleshooting

### Nginx returns 502 Bad Gateway

Check the worker and its logs:

```bash
sudo systemctl status xpscript-fastcgi
sudo journalctl -u xpscript-fastcgi -n 100 --no-pager
sudo tail -n 100 /var/log/nginx/error.log
```

Also verify that Nginx and XPScript use the same address and port and that local firewall rules permit loopback traffic.

### Every route returns 404

Check `--root`, `SCRIPT_FILENAME`, `DOCUMENT_ROOT`, `server_name` and the requested route. Test `/`, `/index` and `/index.xps` separately.

### Source files are exposed

Ensure the Nginx `root` is not the XPScript site directory and keep the source-file deny location before any static-file location. Never add a static MIME mapping for `.xps`.

### Requests hang or time out

Confirm that the persistent FastCGI worker is running independently of Nginx. Increase `fastcgi_read_timeout` only for known long-running operations; do not use it to hide a stopped worker.

## Security checklist

- Bind FastCGI to `127.0.0.1` or a protected private address.
- Terminate public TLS in Nginx.
- Keep XPScript binaries and source outside the static Nginx root.
- Run Nginx and XPScript under low-privilege accounts.
- Grant write access only to required logs, uploads and data directories.
- Restrict accepted host names in the XPScript host configuration where applicable.
- Keep Nginx, OpenSSL and .NET patched.
