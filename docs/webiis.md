# WebIIS deployment target

`webiis` builds an IIS-deployable XPscript web application package that uses ASP.NET Core Module V2 and Kestrel.

## Build

The application entry file must be named `main.xps`.

Self-contained Windows package:

```text
xpscript compile main.xps --target webiis
```

Framework-dependent package:

```text
xpscript compile main.xps --target webiis --framework-dependent
```

Explicit output directory:

```text
xpscript compile main.xps --target webiis --framework-dependent -o C:\deploy\myapp
```

The output directory must be outside the XPscript source directory.

## Output

A build creates an IIS deployment directory and a ZIP package.

```text
myapp\
  site\
    main.xps
    ...other .xps and static files...
    host\
      xpscript.dll or xpscript.exe
      ...runtime dependencies...
    web.config
  deploy.cmd
  SetParameters.xml
  README-IIS.txt
myapp.zip
```

Repository and build directories such as `.git`, `bin` and `obj` are excluded.

## IIS hosting model

The generated `web.config` uses `AspNetCoreModuleV2` with `hostingModel="outofprocess"`.

IIS receives the public HTTP or HTTPS request. ASP.NET Core Module starts the XPscript host and assigns a private loopback port. XPscript reads the IIS-provided `ASPNETCORE_PORT`, binds Kestrel to loopback only and accepts the original public host value because IIS bindings are the public host boundary.

Normal standalone `xpscript web` hosting keeps its explicit XPscript host allowlist behavior. The IIS behavior is activated only when the ASP.NET Core Module environment contains both its assigned port and pairing token.

## Server requirements

Install IIS and ASP.NET Core Module V2.

For a framework-dependent package, install the .NET 10 Hosting Bundle.

A self-contained package includes the .NET runtime, but ASP.NET Core Module V2 is still required for IIS integration.

Use an IIS application pool configured as `No Managed Code`.

## Manual deployment

1. Create an IIS site or application.
2. Set its physical path to the extracted `site` directory.
3. Give the application pool identity read and execute access.
4. Configure HTTP or HTTPS bindings in IIS.
5. Start or recycle the application pool.
6. Browse to the configured hostname.

TLS certificates and public hostnames are configured on the IIS site bindings.

## Web Deploy

If Microsoft Web Deploy is installed, use the generated command:

```text
deploy.cmd "Default Web Site/MyApp"
```

The command synchronizes the generated `site` directory to the specified IIS application.

`SetParameters.xml` contains the default IIS application name and can be changed for deployment automation.

## Application files

All `.xps` files and static application assets under the directory containing `main.xps` are copied into the package.

The generated IIS `web.config` replaces any source `web.config` in the application directory.

`main.xps` is configured as the default XPscript document.

## Sessions and state

The generated WebIIS host enables XPscript sessions and static file serving.

The existing scope rules remain unchanged:

- `Process.State` belongs to the XPscript worker process.
- `Application.State` is shared by users of the IIS application.
- `Session.State` is isolated per user session and shared between `.xps` files in that session.
- `Request.State` lasts for its request/navigation scope.

An IIS application-pool recycle restarts the XPscript worker process, so in-memory `Process.State`, `Application.State` and sessions do not survive a recycle unless application code persists required data externally.

## Updating an application

Build a new package and deploy the new `site` directory or use `deploy.cmd`.

Replacing `web.config` or host binaries can cause IIS to restart the application. Design deployment so users do not depend on in-memory state surviving an application recycle.

## Security

- Keep `.xps` files handled by XPscript. Do not add a static IIS MIME mapping for `.xps`.
- Terminate public TLS at IIS.
- Keep the XPscript backend on the IIS-assigned loopback port.
- Restrict public hostnames with IIS bindings.
- Give the application-pool identity only the filesystem permissions required by the application.
- Grant write access only to explicit data, upload or log directories that need it.
- Keep ASP.NET Core Hosting Bundle and IIS patched.

For alternative IIS topologies such as reverse proxy or CGI, see [Hosting XPScript on IIS](iis-hosting.md).
