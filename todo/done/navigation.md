# XPscript navigation

## Current contract

Navigation between compiled XPscript modules uses one target only.

```xpscript
Navigate("customers")
```

or:

```xpscript
Navigate("customers.xps")
```

Through a form:

```xpscript
form.Navigate("customers")
```

Navigation parameters are not supported. Data is passed through the existing runtime scopes:

- `Request.State`
- `Session.State`
- `Application.State`
- `Process.State`

`Request.State` is for the current request/navigation chain. `Session.State` is shared between `.xps` files for the same session. `Application.State` and `Process.State` follow their platform runtime lifetime.

## Target rules

Navigation targets must be local XPscript module paths.

Allowed examples:

```text
customers
customers.xps
admin/customers
admin/customers.xps
```

Targets are matched case-insensitively and `.xps` is optional.

Absolute URLs, absolute filesystem paths and traversal outside the application root are rejected.

## Desktop

Desktop multi-file applications are compiled as one application unit.

`main.xps` is required as the desktop application entry file.

`Navigate(target)` switches to the compiled target module. Navigation does not start a second executable.

Application and process state remain available across compiled modules. Session state is shared across `.xps` modules for the desktop application session.

## Web

Web routing supports both explicit and extensionless module URLs.

```text
/customers
/customers.xps
```

`index.xps` is the default HTTP document for `/` and directory routes.

Examples:

```text
/                 -> /index.xps
/admin/            -> /admin/index.xps
```

The public URL does not need to expose `.xps`.

## Browser WebAssembly

Browser-WASM uses real browser navigation between XPscript modules.

Example:

```xpscript
form.Navigate("page2")
```

From the application root this changes the browser URL to:

```text
/page2
```

Explicit extension is preserved:

```xpscript
form.Navigate("page2.xps")
```

becomes:

```text
/page2.xps
```

Navigation from a nested route remains in the current logical route directory.

Example:

```text
/folder/index.xps + Navigate("page2") -> /folder/page2
```

WASM framework assets are loaded beneath the owning `.xps` route and do not change the browser address bar.

For a WASM `index.xps` opened as `/`, its generated bootstrap uses the owning `index.xps` route for assets while the visible browser URL remains `/`.

## Request state across navigation

A navigation operation can consume the current `Request.State` according to the request/navigation chain rules already implemented by the runtime.

A later independent browser or HTTP navigation starts a new request scope and must not see old `Request.State` values.

Use `Session.State` when data must survive between separate browser navigations for one user.

## Includes

Includes are source composition and are not navigation targets.

```xpscript
[Include:"includes/database.xps"]
```

Compiler diagnostics for include files must preserve the original include filename and line number.

## Security

Navigation targets are application-local.

The runtime must reject traversal such as:

```text
../../other/script.xps
```

Navigation data must never be interpreted as XPscript source.

## Remaining navigation work

Potential future additions are separate from the current `Navigate(target)` contract:

- `NavigateBack()` and browser/history integration
- `NavigateHome()`
- navigation guards for dirty forms
- lifecycle events such as `OnNavigatedTo` and `OnNavigatedFrom`
- optional navigation caching
- `ShowForm()` for modal or separate desktop windows

None of these should reintroduce navigation parameters. Shared data continues to use the state scopes.
