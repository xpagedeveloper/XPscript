# UIForm cross-platform actions, buttons and mutable form state

(c) xpagedeveloper.com 2026

## Goal

Use one event/action model for UIForm on Avalonia desktop, Kestrel, CGI, FastCGI and browser-WASM where applicable.

The same XPscript handler should be usable from UI buttons, field OnChange events, list/row actions where appropriate and future UI events. A handler must be able to mutate the live form and bound JSON document before the affected UI is refreshed.

## Button API

```xps
Call form.AddButton("save", "Save", "SaveCustomer")
Call form.AddButton("cancel", "Back", "BackToList")
Call form.SetButtonPosition("save", 10, 7, 3, 1)
Call form.SetButtonPosition("cancel", 10, 10, 3, 1)
Call form.SetButtonStyle("save", "Primary")
```

Button names must be unique within one UIForm. Handler names are configured in trusted XPscript code and must never be accepted from browser/client input.

## Handler contract

```xps
Sub SaveCustomer(form As Variant)
    Dim data As Variant
    data = form.GetData()
    Call data.Set("saved", True)
    Call form.SetFieldValue("status", "Saved")
    Call form.RefreshRegion("statusRegion")
End Sub
```

A handler receives the live UIForm object as a Variant. This allows the same handler to mutate bound JSON and field metadata on web and desktop.

## Form JSON API

```xps
Dim data As Variant
data = form.GetData()
Call form.SetData(data)

Call form.SetFieldValue("name", "Kalle")
value = form.GetFieldValue("name")
valueText = form.GetFieldValueString("name")
```

`GetData()` returns the live bound JsonObject. `SetData()` replaces/rebinds the current form data using the same type rules as `BindData`.

## Mutable field properties

```xps
Call form.SetFieldLabel("email", "Work email")
Call form.SetFieldVisible("email", True)
Call form.SetFieldEnabled("email", True)
Call form.SetFieldReadOnly("email", False)
Call form.SetRequired("email", True)
Call form.SetLength("email", 3, 320)
Call form.SetNumberRange("age", 0, 150)
```

Field state must be reflected after an event refresh on both web and desktop.

## Select and Radio options

```xps
Call form.ClearOptions("city")
Call form.AddOption("city", "Stockholm")
Call form.AddOption("city", "Gothenburg")
Call form.SetFieldValue("city", "Stockholm")
Call form.RefreshRegion("cityRegion")
```

Runtime validation must reject selected values outside the configured allow-list.

## OnChange

```xps
Call form.SetOnChange("country", "CountryChanged")
```

`SetRefreshOnChange` remains supported as a convenience API. Both APIs use the same event dispatcher.

## Refresh API

```xps
Call form.RefreshRegion("cityRegion")
Call form.RefreshRegion("summaryRegion")
Call form.RefreshAll()
```

Multiple region refreshes may be requested during one handler. The runtime should deduplicate them.

## Navigation

Navigation has one public argument only, the target module.

```xps
Sub BackToList(form As Variant)
    Call form.Navigate("customers")
End Sub
```

The `.xps` extension is optional:

```xps
Call form.Navigate("customers")
Call form.Navigate("customers.xps")
```

Navigation parameters are not supported. Use the scope objects for state transfer:

```xps
Request.State.Set("selectedId", form.GetFieldValueString("id"))
Call form.Navigate("customer-list")
```

Use `Request.State` only for the current navigation/request chain. Use `Session.State` for session data, `Application.State` for application-wide state and `Process.State` for process-wide state.

Browser and browser-WASM navigation is real browser navigation. The browser URL changes to the target route. Extensionless targets remain extensionless in the address bar. WASM asset requests such as `_framework/dotnet.js` do not change the visible URL.

Desktop navigation switches to the compiled target module inside the same application.

Security requirements:

- target must be a local relative XPscript module path
- `.xps` is optional
- reject absolute paths
- reject `..`
- reject other file extensions
- matching is case-insensitive

## Event order

For Button and OnChange events:

1. receive current UI values
2. validate and write them to bound JSON
3. resolve the event only from server-side form metadata
4. invoke the configured XPscript handler
5. apply handler mutations to JSON, field properties and options
6. process Navigate/RefreshRegion/RefreshAll actions
7. return/update only required UI parts

The browser/client must never be allowed to supply an arbitrary handler method name.

## Tests

Regression coverage should include:

- configured button handler invocation
- forged handler rejection
- OnChange handler invocation
- live UIForm instance handling
- GetData and SetData
- field-state mutation
- dynamic options
- partial and full refresh
- button and OnChange navigation
- extensionless and `.xps` navigation
- case-insensitive routing
- navigation traversal rejection
- no navigation parameter overload or serialized parameter fields
- Request.State navigation boundary
- browser-WASM URL changes through real navigation
- password values not leaking during event refresh

## Showcase

Keep desktop, server-web and browser-WASM showcase scripts aligned with the same target-only navigation API and state-scope model.
