# HCL Domino CGI hosting for XPScript

(c) xpagedeveloper.com 2026

This guide describes the supported CGI deployment model for running XPScript `.xps` applications behind the HCL Domino HTTP task.

## Supported model

Domino starts `XPScript.Web.Cgi.exe` once per HTTP request. The executable reads the standard CGI environment and request body from stdin, executes the requested XPScript route through the common XPScript web dispatcher, and writes CGI status, headers and body to stdout.

For Domino interpreter-style URLs, the executable is the CGI program and the requested `.xps` route is supplied as `PATH_INFO`.

Example:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/index.xps?name=Fredrik
```

The relevant CGI values are expected to look like:

```text
SCRIPT_NAME=/xps-bin/XPScript.Web.Cgi.exe
PATH_INFO=/index.xps
QUERY_STRING=name=Fredrik
```

`XPScript.Web.Cgi` normalizes this to the XPScript request path `/index.xps`. The target path is still constrained to the configured XPScript site root before the dispatcher can execute it.

Direct CGI mode remains supported as well, where `SCRIPT_NAME` and `SCRIPT_FILENAME` identify the `.xps` target directly.

## 1. Publish the CGI host

On the Domino Windows server, publish the CGI host for Windows x64:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release -r win-x64 --self-contained false -o C:\Domino\xps-cgi
```

The server needs a compatible .NET 10 runtime when using a framework-dependent publish.

Copy the complete publish output together. Do not copy only the `.exe`, because the host depends on the XPScript runtime/compiler assemblies produced by the publish operation.

## 2. Configure the XPScript application root

The preferred configuration is an explicit application root:

```text
XPSCRIPT_WEB_ROOT=C:\Sites\MyXpsSite
```

The Domino HTTP task process must inherit this environment variable before it starts. Restart the HTTP task or Domino service after changing the server process environment.

Example site:

```text
C:\Sites\MyXpsSite\index.xps
C:\Sites\MyXpsSite\orders.xps
C:\Sites\MyXpsSite\admin\index.xps
```

If `XPSCRIPT_WEB_ROOT` is not set, the CGI host can use `DOCUMENT_ROOT`. Explicit `XPSCRIPT_WEB_ROOT` is recommended when the `.xps` application is not stored directly under Domino's HTML document root.

## 3. Configure Domino to execute the CGI host

Domino supports CGI executables from its CGI directory or from a directory mapped with Execute access.

### Option A, default CGI directory

Place the published host under the CGI directory configured in the Web Site document. Domino's standard default is the `domino\cgi-bin` directory with the CGI URL path `/cgi-bin`.

Example URL:

```text
https://www.example.com/cgi-bin/XPScript.Web.Cgi.exe/index.xps
```

### Option B, dedicated XPScript CGI directory

A dedicated directory is easier to isolate operationally.

In Domino Administrator:

```text
Configuration
  -> Web
  -> Internet Sites
  -> <your Web Site>
  -> Web Site Rules
```

Create a Directory rule similar to:

```text
Description: XPScript CGI
Type of Rule: Directory
Incoming URL pattern: /xps-bin/*
Target server directory: C:\Domino\xps-cgi
Access level: Execute
```

Then request:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/index.xps
```

Use Execute access only for the directory that contains CGI programs. Do not expose the `.xps` source directory through an Execute mapping.

## 4. Routing examples

Root script:

```text
/xps-bin/XPScript.Web.Cgi.exe/index.xps
```

Extensionless script route:

```text
/xps-bin/XPScript.Web.Cgi.exe/orders
```

Function route:

```text
/xps-bin/XPScript.Web.Cgi.exe/orders/save
```

Directory index:

```text
/xps-bin/XPScript.Web.Cgi.exe/admin/
```

The portion after `XPScript.Web.Cgi.exe` becomes the XPScript request path. Normal XPScript routing, authorization attributes, compiler/cache behavior and error handling then apply.

## 5. Request data

The CGI adapter consumes standard CGI values including:

```text
REQUEST_METHOD
SCRIPT_NAME
PATH_INFO
QUERY_STRING
CONTENT_TYPE
CONTENT_LENGTH
SERVER_NAME
SERVER_PROTOCOL
REMOTE_ADDR
HTTPS
HTTP_*
```

Request bodies are read from stdin using the declared `CONTENT_LENGTH`. Oversized, negative, malformed or truncated bodies are rejected before the XPScript route executes.

HTTP headers supplied as CGI `HTTP_*` variables are normalized into the common XPScript `Request.Headers` model. Cookies are parsed from `HTTP_COOKIE`.

## 6. Security requirements

Keep the CGI executable directory separate from the XPScript source root.

Use Execute access only on the CGI executable directory. Never configure the `.xps` source directory as a readable public file mapping merely to make routing work.

`PATH_INFO` does not bypass the site root. The CGI host converts the requested route into a candidate target under `XPSCRIPT_WEB_ROOT`, and the existing canonical path validation rejects traversal outside the root. The dispatcher performs its normal path validation again when resolving the request.

Do not allow an HTTP request to control `XPSCRIPT_WEB_ROOT` or other process environment configuration.

CGI is a process-per-request model. In-memory `Session` and `Application` state cannot be relied on across separate CGI process invocations. Use FastCGI or Kestrel when persistent in-process state is required.

## 7. Basic verification

Create `C:\Sites\MyXpsSite\index.xps`:

```text
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("XPScript CGI OK")
End Sub
```

Request:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/index.xps
```

Expected response body:

```text
XPScript CGI OK
```

Also verify that traversal is rejected:

```text
/xps-bin/XPScript.Web.Cgi.exe/../outside.xps
```

The request must not execute a source file outside `XPSCRIPT_WEB_ROOT`.

## 8. HCL Domino configuration facts used by this integration

HCL Domino documents a default CGI directory and CGI URL path, and supports CGI programs in directories with Execute access. HCL also documents interpreter-style CGI URLs for PHP where the executable is followed by the target script path. Domino exposes `PATH_INFO` as the extra path after the CGI program and `PATH_TRANSLATED` as the server-translated physical form.

The XPScript integration intentionally uses `PATH_INFO` for routing and independently constrains the target to `XPSCRIPT_WEB_ROOT` instead of trusting `PATH_TRANSLATED` as an authorization boundary.
