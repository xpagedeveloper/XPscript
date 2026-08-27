# REST API documentation

XPscript can generate a static documentation site plus OpenAPI 3.1 and Swagger 2.0 descriptions for REST routes.

API documentation is disabled by default.

## Enable from the CLI

```text
xpscript web ./api --api-docs
```

Disable it explicitly, including when project configuration enables it:

```text
xpscript web ./api --no-api-docs
```

CLI settings override project settings.

## Project configuration

Create `xpscript.json` in the web root:

```json
{
  "web": {
    "apiDocs": true
  }
}
```

The precedence is `--api-docs` / `--no-api-docs`, then `xpscript.json`, then the default `false`.

When enabled XPscript creates:

```text
apidoc/
  index.html
  apidoc.css
  openapi.json
  swagger.json
```

The generated site is served as static content by the web host. Open `/apidoc/index.html`.

## Documentation blocks

Use three apostrophes immediately above route metadata. The first prose line is the summary. Following prose lines form the description.

```xpscript
''' Returns one user.
''' The user is selected by its numeric identifier.
''' @tag Users
''' @param id Unique user identifier.
''' @response 200 User found.
''' @response 404 User not found.
[Get:/users/{id}]
Function GetUser(id As Integer) As User
    ' implementation
End Function
```

Supported directives in the first version:

```text
@param NAME DESCRIPTION
@response STATUS DESCRIPTION
@tag NAME
```

Route location, primitive parameter types and return type are inferred from the XPscript declaration. Documentation text should describe meaning rather than duplicate facts already present in code.

## UI

The generated HTML is dependency-free and responsive. It uses a persistent endpoint navigation on larger screens, endpoint search, clear HTTP method labels, parameter and response tables, and direct links to the OpenAPI 3.1 and Swagger 2.0 files. No external JavaScript, fonts, CSS or CDN resources are required.
