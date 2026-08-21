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

## Supported XPscript web application types

A WebIIS package can contain all current XPscript web application types in the same IIS application:

- normal server-side XPscript routes using `Response`, `Request`, `Session` and the other web runtime objects
- server-side UIForm routes rendered as HTML by XPscript
- `[Platform:browser-wasm]` applications that publish and run as browser WebAssembly

Normal server-side routes and UIForm routes are handled by the persistent XPscript host behind IIS.

A browser-WASM route is compiled and published when it is first requested unless its existing cache can be reused. Its bootstrap document and `_framework` assets are still served through the owning XPscript route, for example:

```text
/wasm.xps
/wasm.xps/_framework/dotnet.js
```

## Server requirements

Install IIS and ASP.NET Core Module V2.

For a framework-dependent package, install the .NET 10 Hosting Bundle.

A self-contained package includes the .NET runtime, but ASP.NET Core Module V2 is still required for IIS integration.

Use an IIS application pool configured as `No Managed Code`.

If the deployed application contains `[Platform:browser-wasm]` files and uses on-demand browser-WASM compilation, install the matching .NET SDK and WebAssembly build tools on the IIS server:

```text
dotnet workload install wasm-tools
```

## Manual deployment

1. Create an IIS site or application.
2. Set its physical path to the extracted `site` directory.
3. Give the application pool identity read and execute access to the site.
4. If browser-WASM on-demand compilation is used, create `.xpscript-cache` below the site root and give the application pool identity Modify permission only to that cache directory.
5. Configure HTTP or HTTPS bindings in IIS.
6. Start or recycle the application pool.
7. Browse to the configured hostname.

Example Windows permission model:

```text
site\                         Read + Execute
site\.xpscript-cache\        Modify
```

Do not grant Modify permission to the complete site merely to support browser-WASM caching.

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

## Browser WebAssembly cache

Browser-WASM publish output is stored under:

```text
.xpscript-cache\wasm
```

The application-pool identity needs Modify permission on `.xpscript-cache` when on-demand compilation is enabled.

The cache is not a public IIS static directory. XPscript resolves browser-WASM assets through the corresponding `.xps` route.

If browser-WASM applications are precompiled as part of a deployment pipeline in the future, production servers can avoid SDK and write-permission requirements for those already-published applications.

## Updating an application

Build a new package and deploy the new `site` directory or use `deploy.cmd`.

Replacing `web.config` or host binaries can cause IIS to restart the application. Design deployment so users do not depend on in-memory state surviving an application recycle.

A deployment process should preserve or safely rebuild `.xpscript-cache` rather than exposing it as static content.

## Automated IIS verification

The `WebIIS IIS E2E` GitHub Actions workflow provisions a real IIS instance on `windows-latest`, installs ASP.NET Core Module V2 and WebAssembly build tools, deploys a generated WebIIS package and verifies:

- normal XPscript HTTP routing
- routes with and without `.xps`
- case-insensitive route matching
- `Application.State`, `Process.State`, `Session.State` and `Request.State` behavior over real HTTP sessions
- server-side UIForm HTML rendering
- browser-WASM bootstrap generation
- browser-WASM `_framework/dotnet.js` delivery through IIS

## Security

- Keep `.xps` files handled by XPscript. Do not add a static IIS MIME mapping for `.xps`.
- Terminate public TLS at IIS.
- Keep the XPscript backend on the IIS-assigned loopback port.
- Restrict public hostnames with IIS bindings.
- Give the application-pool identity only the filesystem permissions required by the application.
- Give browser-WASM on-demand compilation Modify permission only to `.xpscript-cache`, not to the whole site.
- Grant other write access only to explicit data, upload or log directories that need it.
- Keep ASP.NET Core Hosting Bundle, IIS, the .NET SDK used for browser-WASM compilation and installed workloads patched.

For alternative IIS topologies such as reverse proxy or CGI, see [Hosting XPScript on IIS](iis-hosting.md).
