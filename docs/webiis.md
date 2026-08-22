# WebIIS deployment target

`webiis` builds an IIS-deployable XPscript web application package that uses ASP.NET Core Module V2 and Kestrel.

## Build

The application build entry file must be named `main.xps`.

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

`main.xps` is the build entry. It is not the HTTP default document.

The HTTP default document is `index.xps`.

A request for `/` resolves to `index.xps`. The same rule applies to directories, so `/admin/` resolves to `/admin/index.xps` when that file exists.

`index.xps` can be any supported web application type:

- normal server-side XPscript
- server-side UIForm
- `[Platform:browser-wasm]`

## Output

A build creates an IIS deployment directory and a ZIP package.

```text
myapp\
  site\
    main.xps
    index.xps
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

Repository, build and runtime cache directories such as `.git`, `.xpscript-cache`, `bin` and `obj` are excluded. A source-machine `.xpscript-cache` is never copied into the deployment package.

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

A browser-WASM route is compiled and published when it is first requested unless its existing cache can be reused. Its bootstrap document and `_framework` assets are served through the owning XPscript route, for example:

```text
/customer.xps
/customer.xps/_framework/dotnet.js
```

When `index.xps` is browser-WASM and requested as `/`, the generated bootstrap document sets its base route to `index.xps/`. Browser assets therefore continue through the owning XPscript route:

```text
/
/index.xps/main.js
/index.xps/_framework/dotnet.js
```

This also works when the WebIIS package is installed as an IIS application below a parent site because the browser base route is relative to the current application URL.

## Server requirements

Install IIS and ASP.NET Core Module V2.

For a framework-dependent package, install the .NET 10 Hosting Bundle.

A self-contained package includes the .NET runtime, but ASP.NET Core Module V2 is still required for IIS integration.

Use an IIS application pool configured as `No Managed Code`.

Server-side XPscript runtime compilation requires the matching .NET SDK to be available to the IIS worker environment.

If the deployed application contains `[Platform:browser-wasm]` files and uses on-demand browser-WASM compilation, also install the WebAssembly build tools on the IIS server:

```text
dotnet workload install wasm-tools
```

## Manual deployment

1. Create an IIS site or application.
2. Set its physical path to the extracted `site` directory.
3. Give the application pool identity read and execute access to the site.
4. Create `.xpscript-cache` below the site root and give the application pool identity Modify permission only to that cache directory.
5. Configure HTTP or HTTPS bindings in IIS.
6. Start or recycle the application pool.
7. Browse to the configured hostname.

Example Windows permission model:

```text
site\                         Read + Execute
site\.xpscript-cache\        Modify
```

XPscript uses `.xpscript-cache` for persisted server-side compiled units, browser-WASM publish output and the private .NET/NuGet build environment used by the IIS worker. The generated `web.config` redirects `DOTNET_CLI_HOME`, NuGet caches and user-profile locations such as `APPDATA` away from the Windows `systemprofile` directory and into this writable application-local area.

Do not grant Modify permission to the complete site merely to support runtime compilation or browser-WASM caching.

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

`main.xps` remains the mandatory build entry for the WebIIS compiler target.

`index.xps` is the HTTP default document. A request for `/` or for a directory path executes that directory's `index.xps` when present, regardless of whether it is server-side XPscript, server-side UIForm or browser-WASM.

## Sessions and state

The generated WebIIS host enables XPscript sessions and static file serving.

The existing scope rules remain unchanged:

- `Process.State` belongs to the XPscript worker process.
- `Application.State` is shared by users of the IIS application.
- `Session.State` is isolated per user session and shared between `.xps` files in that session.
- `Request.State` lasts for its request/navigation scope.

An IIS application-pool recycle restarts the XPscript worker process, so in-memory `Process.State`, `Application.State` and sessions do not survive a recycle unless application code persists required data externally.

## Runtime compilation cache

Runtime compilation data is stored below:

```text
.xpscript-cache
```

The directory can contain persisted server-side compiled units, the private .NET CLI/NuGet profile and caches, and browser-WASM publish output under:

```text
.xpscript-cache\wasm
```

The application-pool identity needs Modify permission on `.xpscript-cache`.

The cache is not copied from the source application into a newly generated WebIIS deployment package. Create it on the target IIS installation with the required ACL instead.

The cache is not a public IIS static directory. XPscript resolves browser-WASM assets through the corresponding `.xps` route.

The browser-WASM cache identity includes the XPscript browser compiler identity. Updating the compiler therefore creates a new publish bundle even when the `.xps` source itself has not changed.

If browser-WASM applications are precompiled as part of a deployment pipeline in the future, production servers can avoid WebAssembly workload requirements for those already-published applications.

## Updating an application

Build a new package and deploy the new `site` directory or use `deploy.cmd`.

Replacing `web.config` or host binaries can cause IIS to restart the application. Design deployment so users do not depend on in-memory state surviving an application recycle.

A deployment process should preserve or safely rebuild `.xpscript-cache` rather than exposing it as static content.

## Automated IIS verification

The `WebIIS IIS E2E` GitHub Actions workflow provisions a real IIS instance on `windows-latest`, installs ASP.NET Core Module V2 and WebAssembly build tools, deploys a generated WebIIS package and verifies:

- normal XPscript HTTP routing
- routes with and without `.xps`
- case-insensitive route matching
- directory `index.xps` resolution
- `/` resolving to a browser-WASM `index.xps`
- `Application.State`, `Process.State`, `Session.State` and `Request.State` behavior over real HTTP sessions
- server-side UIForm HTML rendering
- browser-WASM bootstrap generation
- browser-WASM `main.js` and `_framework/dotnet.js` delivery through IIS
- actual browser-WASM execution in headless Chromium until UIForm fields are present in the DOM

## Security

- Keep `.xps` files handled by XPscript. Do not add a static IIS MIME mapping for `.xps`.
- Terminate public TLS at IIS.
- Keep the XPscript backend on the IIS-assigned loopback port.
- Restrict public hostnames with IIS bindings.
- Give the application-pool identity only the filesystem permissions required by the application.
- Give runtime compilation Modify permission only to `.xpscript-cache`, not to the whole site.
- Keep the IIS worker's .NET and NuGet profile/cache locations inside the writable `.xpscript-cache` area rather than granting access to the Windows system profile.
- Grant other write access only to explicit data, upload or log directories that need it.
- Keep ASP.NET Core Hosting Bundle, IIS, the .NET SDK used for runtime compilation and installed workloads patched.

For alternative IIS topologies such as reverse proxy or CGI, see [Hosting XPScript on IIS](iis-hosting.md).
