# XPScript FastCGI with nginx

(c) xpagedeveloper.com 2026

This deployment uses nginx as the public HTTP server and XPScript as a FastCGI responder. XPScript remains responsible for XPScript URL routing, route metadata, compilation, cache, Request/Response/Server objects and script execution.

The examples follow nginx's documented `ngx_http_fastcgi_module` parameter model. nginx supports both TCP and Unix-domain sockets with `fastcgi_pass`.

Official nginx reference:

https://nginx.org/en/docs/http/ngx_http_fastcgi_module.html

## TCP listener

Start the XPScript FastCGI host bound to loopback, for example `127.0.0.1:9000`.

A site configuration can then use:

```nginx
server {
    listen 80;
    server_name example.test;

    root /srv/xpsite;

    location / {
        fastcgi_pass 127.0.0.1:9000;
        include fastcgi_params;

        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        fastcgi_param SCRIPT_NAME     $fastcgi_script_name;
        fastcgi_param PATH_INFO       $fastcgi_path_info;
        fastcgi_param QUERY_STRING    $query_string;
        fastcgi_param REQUEST_METHOD  $request_method;
        fastcgi_param CONTENT_TYPE    $content_type;
        fastcgi_param CONTENT_LENGTH  $content_length;
        fastcgi_param SERVER_NAME     $server_name;
        fastcgi_param SERVER_PORT     $server_port;
        fastcgi_param SERVER_PROTOCOL $server_protocol;
        fastcgi_param REMOTE_ADDR     $remote_addr;
        fastcgi_param HTTPS           $https if_not_empty;

        fastcgi_request_buffering on;
        fastcgi_connect_timeout 5s;
        fastcgi_send_timeout 30s;
        fastcgi_read_timeout 30s;
    }
}
```

`SCRIPT_FILENAME` is never trusted directly by XPScript. The FastCGI adapter canonicalizes it and rejects paths outside the configured site root, including resolved symlink/reparse targets where supported.

The XPScript dispatcher resolves the request URL independently. This is required for extensionless routes such as `/foo` and function routes such as `/foo/save`.

## Unix-domain socket

On Linux and macOS, the XPScript FastCGI listener can use a Unix-domain socket. nginx accepts the `unix:` form documented for `fastcgi_pass`:

```nginx
server {
    listen 80;
    server_name example.test;

    root /srv/xpsite;

    location / {
        fastcgi_pass unix:/run/xpscript/example.sock;
        include fastcgi_params;

        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        fastcgi_param SCRIPT_NAME     $fastcgi_script_name;
        fastcgi_param PATH_INFO       $fastcgi_path_info;
        fastcgi_param QUERY_STRING    $query_string;
        fastcgi_param REQUEST_METHOD  $request_method;
        fastcgi_param CONTENT_TYPE    $content_type;
        fastcgi_param CONTENT_LENGTH  $content_length;
        fastcgi_param SERVER_NAME     $server_name;
        fastcgi_param SERVER_PORT     $server_port;
        fastcgi_param SERVER_PROTOCOL $server_protocol;
        fastcgi_param REMOTE_ADDR     $remote_addr;
        fastcgi_param HTTPS           $https if_not_empty;
    }
}
```

The socket directory must already exist. XPScript refuses to overwrite an existing socket path. This avoids silently deleting an unrelated or stale endpoint. Remove a confirmed stale socket explicitly before starting the listener.

The default socket mode grants read/write to the owning user and group. Run nginx and XPScript with an intentional shared group instead of making the socket world-writable.

## Request routing

Do not expose `.xps` source files through a static-file fallback. Requests intended for XPScript should enter the FastCGI location and be resolved by the XPScript root-constrained router.

Examples handled by the common router include:

```text
/                  -> <root>/index.xps
/foo.xps           -> <root>/foo.xps
/foo               -> <root>/foo.xps
/folder/           -> <root>/folder/index.xps
/foo/save          -> <root>/foo.xps, exported route function save
```

A missing script or route returns 404. A compile/runtime failure returns a generic production error and does not serve source text.

## Security requirements

Bind TCP FastCGI to loopback or a private management network unless an explicit network design requires otherwise. Do not expose the FastCGI port directly to the public Internet.

Keep nginx request-size and timeout limits aligned with, or stricter than, the XPScript FastCGI limits. XPScript independently enforces PARAMS, header and body limits and does not rely on nginx as its only validation layer.

Do not derive the XPScript site root from request data. Configure the same canonical root in the XPScript host and nginx deployment.

Treat forwarded or application identity headers as untrusted unless they are populated by a specifically trusted upstream and validated by the application authentication layer.
