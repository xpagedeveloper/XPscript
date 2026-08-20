# Hosting XPScript on IIS

XPScript can be hosted on Windows Server with IIS in two practical ways:

1. IIS in front of the XPScript Kestrel host. This is the recommended setup for production because the XPScript process stays persistent and the shared route resolver handles extensionless URLs such as `/users` and `/users/Save`.
2. IIS CGI using `XPScript.Web.Cgi.exe`. This is simpler to wire directly into IIS, but IIS starts the CGI process per request and throughput is lower.

The current `xpscript fastcgi` command exposes a FastCGI TCP listener. IIS FastCGI application mappings launch a FastCGI process directly and do not act as a generic TCP FastCGI proxy, so do not point an IIS FastCGI handler at the XPScript TCP listener. Use Kestrel behind IIS or the CGI host instead.

## Requirements

On the IIS server install:

- IIS Web Server.
- IIS URL Rewrite when using IIS as a reverse proxy.
- Application Request Routing, ARR, when using IIS as a reverse proxy to Kestrel.
- CGI role service when using the direct CGI option.
- .NET 10 runtime when deploying a framework-dependent XPScript package.

A self-contained `win-x64` XPScript package does not require the .NET runtime to be installed separately.

The account running XPScript must have read access to the deployed XPScript binaries and the site directory. Grant write access only to directories that the application actually needs to modify.

Do not configure IIS to serve `.xps` files as static content.

## Recommended layout

Example:

```text
C:\XPScript\
  host\
    xpscript.exe
    ...runtime files...
  sites\
    example\
      index.xps
      users.xps
      admin\
        index.xps
```

Keep the host binaries outside the public site directory.

## Option 1: IIS reverse proxy to XPScript Kestrel

### 1. Publish or install the Kestrel package

From the repository:

```powershell
.\scripts\publish-distributions.ps1 -Package kestrel -Runtime win-x64 -SelfContained
```

Deploy the resulting Kestrel distribution to a directory such as:

```text
C:\XPScript\host
```

Deploy the `.xps` site files to a separate directory such as:

```text
C:\XPScript\sites\example
```

### 2. Test XPScript locally

Start XPScript on loopback only:

```powershell
C:\XPScript\host\xpscript.exe web --root C:\XPScript\sites\example --address 127.0.0.1 --port 8080 --host example.com
```

If IIS also answers `www.example.com`, add another allowed host:

```powershell
C:\XPScript\host\xpscript.exe web --root C:\XPScript\sites\example --address 127.0.0.1 --port 8080 --host example.com --host www.example.com
```

Verify locally before configuring IIS:

```powershell
Invoke-WebRequest http://127.0.0.1:8080/ -Headers @{ Host = "example.com" }
```

Do not bind the XPScript listener to a public address when IIS is the public frontend.

### 3. Run XPScript as a Windows service

For production, run the Kestrel host under a Windows service manager so it starts after reboot and restarts after a failure.

The service command must execute the same tested command, for example:

```text
C:\XPScript\host\xpscript.exe web --root C:\XPScript\sites\example --address 127.0.0.1 --port 8080 --host example.com --host www.example.com
```

Use a dedicated service account with the minimum filesystem permissions required by the application.

### 4. Install IIS reverse proxy components

Install IIS URL Rewrite and Application Request Routing. In IIS Manager, enable proxy support in the ARR proxy settings at server level.

### 5. Create the IIS site

Create an IIS site for the public hostname, for example `example.com`.

The IIS physical path can point to a small proxy-only directory such as:

```text
C:\inetpub\xpscript-proxy\example
```

It does not need to contain the `.xps` source files.

Configure the HTTP and HTTPS bindings normally. TLS terminates at IIS in this topology.

### 6. Add web.config

Create `web.config` in the IIS site's proxy directory:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="XPScript reverse proxy" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:8080/{R:1}" appendQueryString="true" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

This forwards paths such as:

```text
/
/users
/users.xps
/users/Save
/admin/
```

The XPScript router resolves the route after IIS forwards the request.

### 7. Preserve the original host

XPScript can restrict accepted hosts with `--host`. Configure ARR to preserve the original Host header when proxying. The forwarded request must reach XPScript with the hostname that was added with `--host`.

Do not solve a host mismatch by allowing arbitrary public Host values.

### 8. HTTPS

Install the certificate on the IIS site and bind HTTPS in IIS. The backend connection can remain HTTP on `127.0.0.1` because it never leaves the server.

If application code must know that the original request used HTTPS, configure the proxy to forward the scheme information expected by the hosting layer. Do not trust forwarded headers from arbitrary internet clients. Only trust values added by the local IIS reverse proxy.

### 9. Static files

XPScript can serve static files when started with `--static-files`. If IIS should serve static files instead, add explicit IIS rewrite exclusions for those directories before the catch-all proxy rule.

Never add an IIS static MIME mapping for `.xps`.

## Option 2: Direct IIS CGI hosting

Use this option when you specifically want IIS to execute XPScript through CGI.

CGI starts a new process for each request. Use the Kestrel reverse-proxy option for higher request volume or when persistent in-memory state is required.

### 1. Enable CGI in IIS

On Windows Server, enable:

```text
Web Server (IIS)
  Web Server
    Application Development
      CGI
```

This installs the IIS CGI module.

### 2. Publish the CGI host

Example self-contained deployment:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release -r win-x64 --self-contained true -o C:\XPScript\cgi
```

The executable is:

```text
C:\XPScript\cgi\XPScript.Web.Cgi.exe
```

### 3. Set the XPScript web root

The CGI host reads `XPSCRIPT_WEB_ROOT`. Set it for the IIS worker process or CGI application environment to the directory containing the site:

```text
XPSCRIPT_WEB_ROOT=C:\XPScript\sites\example
```

When `XPSCRIPT_WEB_ROOT` is not set, the CGI host can use CGI values such as `DOCUMENT_ROOT` and `SCRIPT_FILENAME` for root discovery, but an explicit root is safer and easier to audit.

### 4. Add a CGI handler mapping

In IIS Manager for the site:

1. Open Handler Mappings.
2. Add a Module Mapping.
3. Request path: `*.xps`.
4. Module: `CgiModule`.
5. Executable: `C:\XPScript\cgi\XPScript.Web.Cgi.exe`.
6. Name: `XPScript CGI`.
7. Allow script execution when IIS asks whether the executable should be allowed.

IIS must also allow this executable through CGI and ISAPI Restrictions where that IIS configuration requires an explicit allow entry.

Do not create a static content MIME type for `.xps`.

### 5. Test an explicit XPScript route

Create:

```text
C:\XPScript\sites\example\index.xps
```

with:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<h1>Hello from XPScript on IIS</h1>")
End Sub
```

Test:

```text
https://example.com/index.xps
```

The CGI transport supplies request data through CGI variables such as `REQUEST_METHOD`, `QUERY_STRING`, `CONTENT_TYPE`, `CONTENT_LENGTH`, `SCRIPT_NAME`, `PATH_INFO`, `SERVER_NAME`, `SERVER_PORT`, `SERVER_PROTOCOL`, `REMOTE_ADDR` and `HTTPS`.

### 6. Extensionless URLs

A `*.xps` CGI handler naturally handles requests that contain the `.xps` extension. If you want clean URLs such as `/users` or `/users/Save`, use the recommended Kestrel reverse-proxy setup, or add carefully tested IIS URL Rewrite rules that route those requests into the CGI handler without exposing source files.

Do not use a catch-all CGI mapping unless you have verified that static files and operational endpoints are handled correctly.

## Application pool settings

For a reverse-proxy-only IIS site, the application pool does not execute XPScript code. Keep it dedicated to the proxy site.

For CGI hosting:

- Use a dedicated application pool.
- Use `No Managed Code` because XPScript CGI is an external .NET executable, not an ASP.NET Framework application loaded into the IIS worker process.
- Use a dedicated identity when filesystem isolation is required.
- Grant only read and execute access to `C:\XPScript\cgi`.
- Grant read access to the XPScript site directory.
- Add write permissions only to explicit data, upload or log directories that require them.

## Updating a site

The `.xps` files can be deployed independently from the host binaries.

For Kestrel hosting, replace site files using an atomic deployment strategy when possible. XPScript recompiles routes when their source/dependency snapshot changes.

When replacing host binaries, stop the XPScript Windows service, replace the distribution, then start the service again. IIS can remain online and will return a proxy error only while the backend is unavailable.

For CGI hosting, make sure no deployment process leaves a partially replaced executable or dependency set in `C:\XPScript\cgi`.

## Troubleshooting

### IIS returns 502 through the reverse proxy

Check that XPScript is listening locally:

```powershell
Test-NetConnection 127.0.0.1 -Port 8080
```

Then test the backend directly with the correct Host header:

```powershell
Invoke-WebRequest http://127.0.0.1:8080/ -Headers @{ Host = "example.com" }
```

If the direct request works, inspect IIS URL Rewrite and ARR proxy settings.

### XPScript rejects the Host header

Make sure every public IIS hostname is included in the XPScript startup command with `--host` and that ARR preserves the original Host header.

### IIS serves XPScript source code

Remove any static MIME mapping for `.xps`. `.xps` is source code and must never be returned as static content.

### CGI returns 500

Verify:

- The IIS CGI feature is installed.
- `XPScript.Web.Cgi.exe` exists and can execute.
- The CGI executable is allowed by IIS restrictions.
- The application pool identity has read and execute access to the host files.
- `XPSCRIPT_WEB_ROOT` points to the intended site directory.
- The site identity has read access to the `.xps` files.
- The requested route has a valid HTTP method attribute such as `[Get]` or `[Post]`.

### Framework-dependent deployment does not start

Install the matching .NET 10 runtime, or deploy a self-contained `win-x64` distribution.

## Security checklist

- Terminate public TLS at IIS.
- Keep Kestrel bound to `127.0.0.1` when IIS is the frontend.
- Restrict accepted hostnames with `--host`.
- Never serve `.xps` as static files.
- Keep XPScript binaries outside the site directory.
- Run the XPScript service or CGI handler with a low-privilege identity.
- Grant write permissions only where required.
- Treat query strings, headers, cookies, form values and uploaded filenames as untrusted input.
- Protect admin and authenticated routes with XPScript authentication and role rules.
- Patch Windows Server, IIS and the installed .NET runtime.

See [Getting started](getting-started.md) for XPScript package creation and host parameters, and [Web programming](web.md) for routing, Request, Response, sessions and authorization rules.
