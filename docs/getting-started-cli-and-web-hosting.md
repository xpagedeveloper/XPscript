# XPScript command-line and web hosting guide

(c) xpagedeveloper.com 2026

This document is the practical starting point for running XPScript from the command line and hosting `.xps` applications on the web.

It covers:

- running one `.xps` file directly
- compiling a permanent executable
- compiling for Windows, Linux and macOS
- choosing framework-dependent or target-runtime output
- using compiler result formats
- creating a web application directory
- running the built-in Kestrel server
- running XPScript behind nginx with FastCGI
- running XPScript through CGI
- configuring the web root and default document
- the minimum production security rules for each hosting mode

For the complete language reference, see `docs/command-reference.md`. For the full web API reference, see `docs/web-runtime.md`.

## 1. Build the tools from source

The repository targets .NET 10.

Build the compiler:

```powershell
dotnet build .\src\XPScript.Compiler\XPScript.Compiler.csproj -c Release
```

Build the web CLI:

```powershell
dotnet build .\src\XPScript.Cli\XPScript.Cli.csproj -c Release
```

Build the CGI host:

```powershell
dotnet build .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release
```

The examples below assume `xpscriptc` and `xpscript` are available in `PATH`, or that you invoke the corresponding built executable directly.

## 2. Minimal command-line script

Create `hello.xps`:

```xpscript
Sub Main()
    Print "Hello from XPScript"
End Sub
```

`Sub Initialize()` is also accepted when `Main` is not present.

## 3. Run a script directly

Use direct execution when you want to run a source file without keeping a compiled executable.

```text
xpscriptc run hello.xps
```

Pass arguments after the script path:

```text
xpscriptc run hello.xps first "second value"
```

Inside XPScript:

```xpscript
Sub Main()
    Print Application.ArgCount
    Print Application.Args(0)
End Sub
```

If a script argument begins with an option-like value, terminate compiler option parsing with `--`:

```text
xpscriptc run hello.xps -- --runtime value-for-the-script
```

Direct execution compiles into an isolated temporary directory, starts the generated application, forwards stdout and stderr, returns the script process exit code and cleans up the temporary build directory when possible.

The working directory of a directly executed script is the directory containing the `.xps` source file. Relative file paths therefore resolve beside the source file unless the script changes its working directory.

## 4. Compile a permanent executable

Compile for the current platform:

```text
xpscriptc hello.xps -o hello
```

Windows example:

```text
xpscriptc hello.xps -o hello.exe
```

Run the resulting application normally:

```text
hello.exe
```

or on Linux/macOS:

```text
./hello
```

## 5. Compile for a specific platform

Supported runtime identifiers include:

```text
win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

Examples:

```text
xpscriptc hello.xps --runtime win-x64 -o hello.exe
xpscriptc hello.xps --runtime win-arm64 -o hello.exe
xpscriptc hello.xps --runtime linux-x64 -o hello
xpscriptc hello.xps --runtime linux-arm64 -o hello
xpscriptc hello.xps --runtime osx-x64 -o hello
xpscriptc hello.xps --runtime osx-arm64 -o hello
```

If `--runtime` is omitted, XPScript targets the current operating system and process architecture.

Direct execution with `xpscriptc run` can only use a runtime compatible with the current host because the generated application must be started immediately.

## 6. Framework-dependent output

A framework-dependent build requires the compatible .NET runtime on the target system.

Example:

```text
xpscriptc hello.xps -o hello --framework-dependent
```

This is useful when .NET 10 is already installed on the server and you want smaller deployment output.

For server deployments, deploy all files produced for the application or host. Do not assume that copying only one executable is sufficient when the build depends on companion assemblies.

## 7. Compiler result formats

Normal human-readable output:

```text
xpscriptc hello.xps --result-format text
```

JSON:

```text
xpscriptc hello.xps --result-format json
```

XML:

```text
xpscriptc hello.xps --result-format xml
```

A successful structured result reports `result = ok`.

A compilation failure reports `result = error` and diagnostics such as source file, line, position, description, source code and marked code.

This is useful when `xpscriptc` is called from CI, build servers, editors or other automation.

## 8. Create a web application directory

A web application is a directory containing `.xps` route files.

Example:

```text
site/
  index.xps
  users.xps
  admin/
    index.xps
```

Create `site/index.xps`:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<h1>XPScript web application</h1>")
End Sub
```

Create `site/users.xps`:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.Write("Users")
End Sub

[Anonymous]
[Post]
Sub Save()
    Response.Write("Saved")
End Sub
```

The common XPScript router resolves examples such as:

```text
/                  -> site/index.xps
/users             -> site/users.xps
/users.xps         -> site/users.xps
/users/Save        -> procedure Save in site/users.xps
/admin/            -> site/admin/index.xps
```

`index.xps` is the normal default document.

## 9. Built-in Kestrel web server

Kestrel is the simplest hosting mode because XPScript runs its own HTTP server.

Start the site:

```text
xpscript web --root ./site
```

Default listener:

```text
http://127.0.0.1:8080
```

Open:

```text
http://127.0.0.1:8080/
```

Select another address and port:

```text
xpscript web --root ./site --address 127.0.0.1 --port 8085
```

Listen on all interfaces only when the network design requires it:

```text
xpscript web --root /srv/xpsite --address 0.0.0.0 --port 8080
```

Change the default document:

```text
xpscript web --root ./site --default-document home.xps
```

The default-document value must be one `.xps` filename, not a path.

### Kestrel configuration file

A JSON configuration file can be used instead of a long command line.

Example `web.cfg`:

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
    "health": true,
    "metrics": true,
    "sessions": true,
    "staticFiles": true,
    "staticMaxBytes": 8388608
  }
}
```

Start with:

```text
xpscript web --config web.cfg
```

If a file named `web.cfg` is stored next to the running XPScript CLI executable, it can be discovered automatically.

Configuration precedence is:

```text
built-in defaults
config file
explicit command-line options
```

### Kestrel with HTTPS

The web config supports a PFX certificate and an environment-variable reference for its password.

Example:

```json
{
  "web": {
    "root": "site",
    "address": "0.0.0.0",
    "port": 443,
    "httpsCertificate": "certificates/server.pfx",
    "httpsCertificatePasswordEnvironment": "XPS_TLS_PASSWORD"
  }
}
```

Set the password outside the config file:

```powershell
$env:XPS_TLS_PASSWORD = "secret"
```

Do not store certificate passwords directly in `web.cfg`.

## 10. FastCGI behind nginx

FastCGI is appropriate when nginx is the public HTTP server and XPScript runs as a persistent application process behind it.

Start XPScript on loopback:

```text
xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
```

You can also configure the default document:

```text
xpscript fastcgi --root /srv/xpsite --default-document index.xps --listen 127.0.0.1:9000
```

Example nginx site:

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

The XPScript root must match the intended application directory. XPScript does not trust `SCRIPT_FILENAME` as an authorization boundary and constrains resolved files to the configured root.

Do not configure nginx to serve `.xps` source files as ordinary static files.

### FastCGI config file

Example:

```json
{
  "fastCgi": {
    "root": "/srv/xpsite",
    "defaultDocument": "index.xps",
    "listen": "127.0.0.1:9000"
  }
}
```

Start:

```text
xpscript fastcgi --config /etc/xpscript/web.cfg
```

### FastCGI Unix socket

Linux and macOS can use a Unix-domain socket.

Example config:

```json
{
  "fastCgi": {
    "root": "/srv/xpsite",
    "unixSocket": "/run/xpscript/site.sock"
  }
}
```

nginx:

```nginx
fastcgi_pass unix:/run/xpscript/site.sock;
```

The socket directory must already exist. Use an intentional shared user/group permission model between nginx and XPScript instead of making the socket world writable.

## 11. CGI hosting

CGI uses a separate executable, `XPScript.Web.Cgi`.

The web server starts the CGI host for a request. The host reads the standard CGI environment and request body from stdin, runs the common XPScript dispatcher and writes CGI status, headers and body to stdout.

CGI is process-per-request. Use FastCGI or Kestrel when you need persistent in-process state, lower process-start overhead or in-memory sessions.

### Publish the CGI host on Windows

Example Windows x64 framework-dependent publish:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release -r win-x64 --self-contained false -o C:\XPScript\cgi
```

Install a compatible .NET 10 runtime on the server when using framework-dependent deployment.

Copy the complete publish directory.

### Point CGI at the web directory

Set:

```text
XPSCRIPT_WEB_ROOT=C:\Sites\MyXpsSite
```

Example site:

```text
C:\Sites\MyXpsSite\index.xps
C:\Sites\MyXpsSite\orders.xps
C:\Sites\MyXpsSite\admin\index.xps
```

The web server process must inherit `XPSCRIPT_WEB_ROOT` before it starts the CGI application.

If `XPSCRIPT_WEB_ROOT` is not set, the CGI host can use `DOCUMENT_ROOT`. An explicit XPScript root is preferred because it makes the trust boundary clear.

### CGI request mapping

For interpreter-style CGI routing, the web server executes the CGI host and supplies the requested XPScript route as `PATH_INFO`.

Example URL:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/orders/Save
```

Typical CGI values:

```text
SCRIPT_NAME=/xps-bin/XPScript.Web.Cgi.exe
PATH_INFO=/orders/Save
REQUEST_METHOD=POST
QUERY_STRING=
```

XPScript resolves `/orders/Save` inside `XPSCRIPT_WEB_ROOT` and calls the exported `Save` route in `orders.xps`.

### HCL Domino CGI example

For HCL Domino on Windows, publish `XPScript.Web.Cgi.exe` to a CGI directory or another directory configured with Execute access.

A dedicated mapping can look conceptually like:

```text
Incoming URL: /xps-bin/*
Physical CGI directory: C:\Domino\xps-cgi
Access: Execute
```

Then request:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/index.xps
```

Keep the CGI executable directory separate from the `.xps` application directory. Give Execute access to the CGI directory, not to the source directory.

See `docs/hcl-domino-cgi.md` for the full Domino-specific configuration.

## 12. Which web hosting mode should you choose?

Use Kestrel when:

- you want the simplest standalone deployment
- XPScript should own the HTTP listener
- you want persistent application/session state in one server process
- a reverse proxy can be added separately when required

Use FastCGI when:

- nginx or another FastCGI-capable web server owns the public HTTP endpoint
- you want a persistent XPScript worker process
- you need persistent in-process state
- you want the external server to handle public TLS, buffering or other front-end concerns

Use CGI when:

- the existing web server provides CGI execution
- process-per-request behavior is acceptable
- compatibility with an existing CGI host such as HCL Domino is more important than process reuse

For new high-throughput deployments, prefer Kestrel or FastCGI over CGI.

## 13. Web root security

The web root is a security boundary.

Good layout:

```text
/srv/xpscript/bin/       XPScript binaries
/srv/xpsite/             .xps application files
/srv/xpsite/static/      optional allowed static files
```

Do not use a client-supplied value to select the XPScript root.

Do not expose `.xps` source files through an ordinary static-file mapping.

The XPScript router rejects traversal, malformed/double-encoded traversal and resolved symlink or reparse-point escapes outside the configured root.

Run the server process with only the filesystem and network permissions required by the application.

## 14. Production deployment checklist

Before exposing an XPScript web application:

1. Use an explicit canonical web root.
2. Keep binaries and source/application directories separate where practical.
3. Do not serve `.xps` files as static source files.
4. Bind FastCGI to loopback, a Unix socket or an intentionally private network.
5. Configure allowed Host values for public Kestrel deployments.
6. Use HTTPS directly or through a trusted reverse proxy for sensitive traffic.
7. Keep request-size and timeout limits enabled.
8. Store certificate passwords and other secrets outside config files.
9. Run with a dedicated least-privilege operating-system account.
10. Treat all Request values, headers, cookies, uploaded files and route input as untrusted.
11. Use Kestrel or FastCGI when persistent Session/Application state is needed.
12. Verify the application from the public HTTP path after deployment, including a missing route and a rejected traversal request.

## 15. Quick command summary

Run source directly:

```text
xpscriptc run app.xps
```

Compile current platform:

```text
xpscriptc app.xps -o app
```

Compile Windows x64:

```text
xpscriptc app.xps --runtime win-x64 -o app.exe
```

Run Kestrel:

```text
xpscript web --root ./site --address 127.0.0.1 --port 8080
```

Run FastCGI:

```text
xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
```

Configure CGI web root:

```text
XPSCRIPT_WEB_ROOT=/srv/xpsite
```

or on Windows:

```text
XPSCRIPT_WEB_ROOT=C:\Sites\MyXpsSite
```

## Related documentation

- `docs/direct-script-execution.md`
- `docs/command-reference.md`
- `docs/web-runtime.md`
- `docs/web-host-config.md`
- `docs/web-fastcgi-nginx.md`
- `docs/hcl-domino-cgi.md`
- `docs/web-security.md`
- `docs/web-runtime-production-limits.md`
