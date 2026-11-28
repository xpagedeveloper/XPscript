# Running an XPScript CGI program with HCL Domino

This guide describes how to run the XPScript CGI host behind the HCL Domino HTTP task. Domino receives the HTTP request, starts the CGI executable, passes the standard CGI environment and request body, and returns the CGI response to the browser.

```text
Browser
  -> Domino HTTP task
  -> XPScript.Web.Cgi executable
  -> .xps web root
```

This is a CGI deployment. It is not the Domino REST API and it does not require a Notes database for the web routes themselves. XPScript code can still use Notes/Domino APIs when the server has the required Notes runtime and credentials.

## Requirements

- HCL Domino with the HTTP task enabled.
- An XPScript CGI publish for the Domino server operating system.
- A site directory containing the `.xps` files.
- A Domino server account with execute permission for the CGI host and read permission for the site.
- Write permission only for explicit XPScript log, upload or data directories.

CGI is process-per-request. For high request volume, use XPScript Kestrel or FastCGI behind a proxy instead. Do not expose the XPScript CGI executable as a static download.

## 1. Publish the CGI host

For a Windows Domino server:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj `
  -c Release -r win-x64 --self-contained true -o C:\XPScript\domino-cgi
```

For a Linux Domino server:

```bash
dotnet publish ./src/XPScript.Web.Cgi/XPScript.Web.Cgi.csproj \
  -c Release -r linux-x64 --self-contained true -o /opt/xpscript/domino-cgi
chmod 0755 /opt/xpscript/domino-cgi/XPScript.Web.Cgi
```

Deploy the complete publish directory, not only the executable. A framework-dependent publish is also possible when the matching .NET runtime is installed on the Domino server.

Keep the host outside the public Domino data directory where possible:

```text
C:\XPScript\domino-cgi\XPScript.Web.Cgi.exe
C:\XPScript\sites\example\index.xps
```

## 2. Configure the XPScript web root

Set `XPSCRIPT_WEB_ROOT` in the environment visible to the Domino HTTP task:

```text
XPSCRIPT_WEB_ROOT=C:\XPScript\sites\example
```

On Linux:

```text
XPSCRIPT_WEB_ROOT=/opt/xpscript/sites/example
```

Use the operating-system service environment or the Domino service account environment, then restart Domino so the HTTP task inherits the value. An explicit environment value is safer than relying on `DOCUMENT_ROOT` or `SCRIPT_FILENAME` discovery.

If the Domino service cannot receive the variable directly, use a small native wrapper executable that sets `XPSCRIPT_WEB_ROOT` and then launches `XPScript.Web.Cgi`. The wrapper must preserve CGI stdin, stdout and stderr and must not write diagnostic text to stdout.

## 3. Enable CGI in Domino

The exact menu names vary slightly by Domino release and whether the server uses a Server document or Internet Site documents. Configure the CGI program path in the HTTP configuration:

1. Open the Domino Administrator for the target server.
2. Open the server's HTTP/Internet Site configuration.
3. Set the CGI program path to the directory containing the published XPScript CGI executable, for example `C:\XPScript\domino-cgi` or `/opt/xpscript/domino-cgi`.
4. Ensure the HTTP task is enabled and reload or restart HTTP after saving the configuration.

On servers using an Internet Site document, apply the CGI program path to the specific site that owns the hostname. Do not add the publish directory as a normal static file directory unless you have separately denied executable and source-file downloads.

## 4. Map a CGI URL

Choose a URL prefix that is clearly CGI, for example `/cgi-bin/xpscript/`. Configure that prefix in the Domino Web Site/HTTP mapping so requests under the prefix execute the XPScript CGI host.

The executable mapping must point to the published `XPScript.Web.Cgi` binary. On Windows this is normally:

```text
C:\XPScript\domino-cgi\XPScript.Web.Cgi.exe
```

On Linux:

```text
/opt/xpscript/domino-cgi/XPScript.Web.Cgi
```

Use the Domino CGI mapping facility for the deployment's Domino version; do not create a URL rewrite that merely serves the `.exe` or `.xps` file as static content.

## 5. Create a minimal route

Create `index.xps` in the configured web root:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<h1>Hello from XPScript CGI on Domino</h1>")
End Sub
```

Call the CGI URL, for example:

```text
https://domino.example.com/cgi-bin/xpscript/index.xps
```

The CGI host consumes standard variables such as `REQUEST_METHOD`, `QUERY_STRING`, `CONTENT_TYPE`, `CONTENT_LENGTH`, `SCRIPT_NAME`, `PATH_INFO`, `SCRIPT_FILENAME`, `SERVER_NAME`, `SERVER_PORT`, `SERVER_PROTOCOL`, `REMOTE_ADDR` and `HTTPS`.

## 6. Test GET and POST

From a client machine:

```bash
curl -i "https://domino.example.com/cgi-bin/xpscript/index.xps?check=1"
curl -i -X POST \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data "name=Alice" \
  "https://domino.example.com/cgi-bin/xpscript/form.xps"
```

Confirm that:

- Domino returns the XPScript status and headers.
- The request body reaches the CGI program.
- Query string and path information are preserved.
- `.xps` source is never returned as text.
- Diagnostics are written to Domino/XPScript logs, not CGI stdout.

## HTTPS and authentication

Terminate TLS in Domino or in the trusted proxy in front of Domino. Ensure Domino supplies the `HTTPS` CGI variable so XPScript can apply secure-cookie and request-scheme behavior correctly.

Protect routes with XPScript route attributes and authentication rules. Domino authentication and XPScript authentication are separate layers; document which layer establishes the user identity before relying on `Session` or authorization rules.

## Permissions and Notes access

Run the Domino server and CGI process under a dedicated low-privilege account. Grant:

- read/execute access to the published CGI directory;
- read access to the XPScript site directory;
- write access only to configured logs, uploads, caches or data directories;
- Notes ID/NSF access only when the application explicitly uses Notes/Domino objects.

Never grant the CGI process broad write access to the Domino data directory merely to make a route work.

## Troubleshooting

### Domino returns 404

Check the Internet Site/HTTP CGI mapping, URL prefix, hostname binding and that the HTTP task has been reloaded after configuration changes.

### Domino returns 500 or an empty response

Check that the executable can run under the Domino service account, that the complete publish directory is deployed, and that `XPSCRIPT_WEB_ROOT` is visible to the HTTP task. Run the executable manually under the same account with a captured CGI environment if possible.

### The route cannot be found

Check `XPSCRIPT_WEB_ROOT`, `SCRIPT_NAME`, `PATH_INFO` and `SCRIPT_FILENAME`. The web root must contain the requested `.xps` file and the route must declare the correct HTTP method, such as `[Get]` or `[Post]`.

### The browser shows XPScript source

The CGI mapping is missing or a static-file mapping is taking precedence. Remove the static mapping, map the CGI URL prefix to the executable, and deny direct downloads of `.xps` and executable files.

### Notes/Domino API calls fail

CGI hosting does not grant Notes access automatically. Verify the Domino server identity, Notes runtime availability, ID permissions, NSF ACLs and the exact environment inherited by the CGI process.

## Production checklist

- Use a self-contained publish or install the exact matching .NET runtime.
- Configure an explicit `XPSCRIPT_WEB_ROOT`.
- Use a dedicated CGI URL prefix and server account.
- Keep binaries and `.xps` source outside normal static content paths.
- Enable HTTPS and secure cookies for authenticated sites.
- Limit filesystem and Notes/NSF permissions.
- Capture Domino and XPScript logs and rotate them.
- Load-test before choosing CGI for high-volume traffic; use Kestrel/FastCGI for persistent workers.
