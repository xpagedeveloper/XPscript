# Hosting XPScript FastCGI with Apache

Apache HTTP Server 2.4 can proxy requests to an XPScript FastCGI worker. Apache handles HTTP/HTTPS; XPScript handles the `.xps` routes.

```text
Browser -> Apache 2.4 -> mod_proxy_fcgi -> 127.0.0.1:9000 -> xpscript fastcgi
```

## Requirements

- Apache 2.4 with `mod_proxy` and `mod_proxy_fcgi`.
- `mod_rewrite` if you need rewrite-based routing.
- A published XPScript host and a separate web-root directory.

On Debian/Ubuntu:

```bash
sudo a2enmod proxy proxy_fcgi rewrite headers ssl
sudo systemctl restart apache2
```

On RHEL/Fedora, ensure `mod_proxy` and `mod_proxy_fcgi` are loaded. Verify with `apachectl -M | grep proxy`.

## Start the XPScript worker

```bash
/opt/xpscript/host/xpscript fastcgi \
  --root /opt/xpscript/sites/example \
  --listen 127.0.0.1:9000 \
  --log-directory /opt/xpscript/logs
```

Run this command with `systemd` or another service manager. Keep the listener private; it speaks FastCGI, not HTTP.

## Configure Apache

Create a VirtualHost such as `/etc/apache2/sites-available/xpscript-example.conf`:

```apache
<VirtualHost *:80>
    ServerName example.com
    DocumentRoot /var/empty/xpscript-example

    <Directory /var/empty/xpscript-example>
        Require all granted
        Options -Indexes
        AllowOverride None
    </Directory>

    # The absolute path supplies a stable CGI script filename.
    ProxyPassMatch ^/(.*)$ fcgi://127.0.0.1:9000/opt/xpscript/sites/example/$1

    # Never expose XPScript source as static content.
    <FilesMatch "\\.xps$">
        Require all denied
    </FilesMatch>

    ErrorLog ${APACHE_LOG_DIR}/xpscript-example-error.log
    CustomLog ${APACHE_LOG_DIR}/xpscript-example-access.log combined
</VirtualHost>
```

Enable and validate it:

```bash
sudo a2ensite xpscript-example.conf
sudo apachectl configtest
sudo systemctl reload apache2
```

On RHEL/Fedora, place the file in `/etc/httpd/conf.d/` and reload `httpd`.

Use one forwarding strategy. If the Apache build does not preserve the route path with `ProxyPassMatch`, use this equivalent rewrite proxy rule instead:

```apache
RewriteEngine On
RewriteRule ^/(.*)$ fcgi://127.0.0.1:9000/opt/xpscript/sites/example/$1 [P,L,QSA]
```

## HTTPS

Create an HTTPS VirtualHost and configure the certificate in Apache. Optionally redirect HTTP:

```apache
<VirtualHost *:80>
    ServerName example.com
    Redirect permanent / https://example.com/
</VirtualHost>
```

The FastCGI connection remains local. Ensure Apache passes the normal CGI HTTPS/server variables when the application needs to know the original scheme.

## Test

```bash
curl -i http://example.com/
curl -i http://example.com/users
curl -i http://example.com/index.xps
```

Routes should return the XPScript response; `.xps` source must never be returned. For local DNS testing use `curl --resolve example.com:80:127.0.0.1 http://example.com/`.

## Troubleshooting

- **502/503:** verify `ss -ltn '( sport = :9000 )'`, the worker logs and Apache's error log.
- **404:** verify `--root`, the absolute path in `ProxyPassMatch`, `ServerName` and the requested route.
- **Source download:** remove static mappings for `.xps`, keep the `<FilesMatch>` denial and ensure the proxy rule is active.
- **Module error:** load both `mod_proxy` and `mod_proxy_fcgi` and run `apachectl configtest`.
- **Hanging requests:** run the persistent `xpscript fastcgi` worker separately; do not configure Apache to start the TCP listener as a per-request CGI executable.

## Security checklist

- Bind FastCGI to loopback or a protected private address.
- Terminate public TLS in Apache.
- Keep host binaries and `.xps` files outside the public `DocumentRoot`.
- Run Apache and XPScript with low-privilege accounts.
- Grant write access only to required logs, uploads and data directories.
- Keep Apache, OpenSSL and .NET patched.
