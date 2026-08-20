# TODO: Desktop UIForm navigation

## Goal

Define and implement a simple application navigation model for XPscript desktop applications that contain several `UIForm` screens.

The developer should be able to move between forms without manually managing Avalonia windows or application lifecycle details.

## Questions to solve

- How should one `UIForm` navigate to another `UIForm`?
- Should forms normally live in separate `.xps` files, include files, or both?
- How does an application define which form is the startup form?
- Should navigation replace the current form, open another form modally, or open another independent window?
- How should Back navigation work?
- How should data and parameters be passed between forms?
- How should Application scope, Session scope and form-local/request-like state behave during desktop navigation?

## Proposed API

Add explicit navigation methods to the desktop UI runtime.

```xpscript
Navigate("customers.xps")
```

or through the form:

```xpscript
form.Navigate("customers.xps")
```

Support parameters:

```xpscript
form.Navigate("customer.xps", customerId)
```

Potential named parameter model:

```xpscript
Dim args As New JsonObject
Call args.Add("customerId", customerId)
Call form.Navigate("customer.xps", args)
```

The target form should be able to read the supplied navigation parameters without using global variables.

## ShowForm

Consider a `ShowForm` API when the developer wants another form without replacing the current navigation entry.

```xpscript
ShowForm("settings.xps")
```

Possible modes:

```xpscript
ShowForm("settings.xps", "Modal")
ShowForm("settings.xps", "Window")
```

Recommended semantic distinction:

- `Navigate()` replaces the visible application page and adds an entry to navigation history.
- `NavigateBack()` returns to the previous page.
- `ShowForm()` opens a modal or separate desktop window.
- `CloseForm()` closes a form opened with `ShowForm()`.

## Recommended project structure

Use separate `.xps` files for screens/forms.

Example:

```text
app/
  main.xps
  customers.xps
  customer.xps
  settings.xps
  includes/
    database.xps
    validation.xps
    common-ui.xps
```

A `.xps` screen file should contain the form and event logic for that screen.

Include files should primarily contain reusable functions, classes, validation, database helpers and shared UI construction logic. They should not normally represent independently navigable screens.

This keeps navigation explicit and prevents one very large `.xps` file from containing the entire desktop application.

## Startup form

XPscript needs an explicit way to determine the first desktop screen.

Preferred design is an application entry point rather than relying on alphabetical file order or directory enumeration.

Possible syntax:

```xpscript
[Startup]
Sub Main()
    Navigate("customers.xps")
End Sub
```

Alternative configuration:

```json
{
  "desktop": {
    "startup": "main.xps"
  }
}
```

The compiler should reject an application with multiple `[Startup]` entry points.

If no explicit startup entry exists, define one deterministic backwards-compatible rule, for example `Main()` in the compiler input file.

Do not select the startup form based on filesystem ordering.

## Navigation stack

Desktop applications should maintain a navigation stack.

Example:

```text
Customers
  -> Customer
      -> Edit address
```

Calling:

```xpscript
NavigateBack()
```

from Edit address returns to Customer.

The runtime should own this stack. Individual forms should not need to track previous filenames.

Consider:

```xpscript
CanNavigateBack()
NavigateBack()
NavigateHome()
```

## Form lifecycle

Define lifecycle events so a form can safely load and release data.

Potential lifecycle:

```xpscript
Sub OnLoad()
End Sub

Sub OnShown()
End Sub

Sub OnNavigatedTo()
End Sub

Sub OnNavigatedFrom()
End Sub

Sub OnClose()
End Sub
```

Avoid executing database loading logic repeatedly when navigation restores an existing form unless the developer explicitly requests refresh.

## Navigation parameters

Navigation parameters should be scoped to the target navigation entry.

Example:

```xpscript
Dim args As New JsonObject
Call args.Add("customerId", "123")
Call Navigate("customer.xps", args)
```

Target:

```xpscript
customerId = Navigation.Get("customerId")
```

Suggested API:

```xpscript
Navigation.Get("name")
Navigation.Has("name")
Navigation.Parameters
Navigation.Previous
```

`Get()` should return `Null` when the parameter does not exist.

## Dirty forms

Navigation must integrate with existing UIForm dirty tracking.

If `form.IsDirty` is true, navigation should support preventing accidental loss of changes.

Potential API:

```xpscript
If form.IsDirty Then
    ' developer decides whether navigation is allowed
End If
```

Consider an optional navigation guard/event rather than forcing confirmation dialogs globally.

Example:

```xpscript
Sub BeforeNavigate(target As String, Cancel As Boolean)
    If form.IsDirty Then
        Cancel = True
    End If
End Sub
```

The application must be able to inspect `form.DirtyFields` when presenting a save/discard decision.

## Shared state

Navigation should preserve application-wide state.

- Application scope remains alive for the entire desktop application process.
- Form-local state belongs to one form/navigation entry.
- Navigation parameters belong to one navigation operation/entry.
- Avoid using Application scope merely to pass parameters between forms.

Desktop Session scope semantics need to be explicitly documented if Session is supported outside web applications.

## Includes

Includes should continue to use normal XPscript include/source mapping semantics.

Example:

```xpscript
[Include:"includes/database.xps"]
[Include:"includes/common-ui.xps"]
```

Compiler errors in an include must report the include filename and the line relative to that include file, not the expanded/generated source line.

Navigation between screens must not be implemented as includes. A navigable `.xps` file is an application screen/module. An include is source composition.

## Compilation model

For a desktop application consisting of multiple `.xps` screen files, investigate compiling the application as one unit rather than compiling every screen into an unrelated executable.

The compiler should:

1. Start from the configured application entry point.
2. Resolve navigable `.xps` modules.
3. Resolve include dependencies.
4. Detect missing navigation targets at compile time when the target is a string literal.
5. Detect duplicate startup forms.
6. Preserve original file and line information in diagnostics.
7. Package the application as one desktop executable/application bundle.

Example compile-time error:

```text
customer.xps(42,5): Navigation target 'orders.xps' was not found.
```

## Security

Navigation targets must be application-local.

Do not allow a target such as:

```text
../../other/script.xps
```

unless an explicit future module/package system permits it.

Resolve paths canonically and prevent traversal outside the application root.

Navigation parameters are data. They must never be interpreted as XPscript source code.

## Web and WebAssembly consideration

Design the API so the same conceptual navigation model can later map cleanly to web and browser WebAssembly.

For example:

```xpscript
Navigate("customer.xps", args)
```

could mean native form navigation on desktop and client-side route/page navigation in browser-WASM.

Do not make the public API depend directly on Avalonia types.

## Initial implementation priority

1. `Navigate(target)`.
2. `Navigate(target, parameters)`.
3. `NavigateBack()` and navigation stack.
4. `Navigation.Get()` and parameter scope.
5. Explicit desktop startup entry point.
6. Multiple `.xps` files compiled into one desktop application.
7. Missing-target compile diagnostics.
8. Dirty-form navigation guard.
9. `ShowForm()` for modal/separate windows.
10. Lifecycle events and optional navigation caching.

## Documentation requirement

After implementation, add a desktop multi-form example under `samples/` and document the recommended application layout under `docs/`.

The sample should contain at least:

- startup form
- customer list form
- customer edit form
- settings form
- navigation parameters
- Back navigation
- dirty tracking before leaving an edit form
- shared include file
