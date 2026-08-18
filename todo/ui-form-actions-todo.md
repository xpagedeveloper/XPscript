# UIForm cross-platform actions, buttons and mutable form state

(c) xpagedeveloper.com 2026

## Goal

Add one event/action model for UIForm on Avalonia desktop, Kestrel, CGI and FastCGI.

The same XPscript handler must be usable from:

- UI buttons
- field OnChange
- list/row actions where appropriate
- future UI events

A handler must be able to mutate the live form and the bound JSON document before the affected UI is refreshed.

## Proposed button API

```xps
Call form.AddButton("save", "Save", "SaveCustomer")
Call form.AddButton("cancel", "Back", "BackToList")

Call form.SetButtonPosition("save", 10, 7, 3, 1)
Call form.SetButtonPosition("cancel", 10, 10, 3, 1)
Call form.SetButtonStyle("save", "Primary")
```

Button names must be unique within one UIForm.

Button handler names are configured in trusted XPscript code and must never be accepted from browser/client input.

## Handler contract

Initial handler contract:

```xps
Sub SaveCustomer(form As Variant)
    Dim data As Variant
    data = form.GetData()

    Call data.Set("saved", True)
    Call form.SetFieldValue("status", "Saved")
    Call form.RefreshRegion("statusRegion")
End Sub
```

A handler receives the live UIForm object as a Variant. This allows the same handler to mutate the same bound JSON and field metadata on web and desktop.

Handlers may be Sub or Function. A Function result is reserved for later action-result shortcuts. Initial implementation uses mutations on the supplied form.

## Form JSON API

The existing `Data` property remains available.

Add explicit methods:

```xps
Dim data As Variant
data = form.GetData()

Call form.SetData(data)
```

`GetData()` returns the live bound JsonObject. Changes made through that object are immediately visible to the form.

`SetData(jsonObjectOrDocument)` replaces/rebinds the current form data using the same type rules as `BindData`.

Existing APIs remain valid:

```xps
Call form.SetFieldValue("name", "Kalle")
value = form.GetFieldValue("name")
valueText = form.GetFieldValueString("name")
```

## Mutable field properties

Add:

```xps
Call form.SetFieldLabel("email", "Work email")
Call form.SetFieldVisible("email", True)
Call form.SetFieldEnabled("email", True)
Call form.SetFieldReadOnly("email", False)
Call form.SetRequired("email", True)
Call form.SetLength("email", 3, 320)
Call form.SetNumberRange("age", 0, 150)
```

Field state must be reflected immediately after an event refresh on both web and desktop.

## Select and Radio options

Existing APIs are part of the action model:

```xps
Call form.ClearOptions("city")
Call form.AddOption("city", "Stockholm")
Call form.AddOption("city", "Gothenburg")
Call form.SetFieldValue("city", "Stockholm")
Call form.RefreshRegion("cityRegion")
```

If the current selected value is no longer in the available option set, the handler should explicitly set a replacement or clear the value. Runtime validation must still reject values outside the configured allow-list.

## OnChange

Add a direct OnChange handler:

```xps
Call form.SetOnChange("country", "CountryChanged")
```

Example:

```xps
Sub CountryChanged(form As Variant)
    Dim country As String
    country = form.GetFieldValueString("country")

    Call form.ClearOptions("city")

    If country = "SE" Then
        Call form.AddOption("city", "Stockholm")
        Call form.AddOption("city", "Gothenburg")
    ElseIf country = "NO" Then
        Call form.AddOption("city", "Oslo")
        Call form.AddOption("city", "Bergen")
    End If

    Call form.SetFieldValue("city", "")
    Call form.RefreshRegion("cityRegion")
End Sub
```

`SetRefreshOnChange` remains supported as a convenience API. Internally both APIs should use the same event dispatcher.

## Refresh API

```xps
Call form.RefreshRegion("cityRegion")
Call form.RefreshRegion("summaryRegion")
Call form.RefreshAll()
```

Multiple region refreshes may be requested during one handler. The runtime should deduplicate them.

Web:

- event request posts current form values
- server validates configured handler against server-side form metadata
- handler executes
- response contains only requested region fragments, or complete form when `RefreshAll()` is used
- browser replaces affected DOM regions without full page reload

Desktop:

- event updates the live bound JSON first
- handler executes
- affected Avalonia controls/containers are updated in place
- the form window remains open

## Navigation

Handlers may navigate to another local XPscript file:

```xps
Sub BackToList(form As Variant)
    Call form.Navigate("customers.xps")
End Sub
```

With a parameter:

```xps
Call form.Navigate("customer-list.xps", "selectedId", form.GetFieldValueString("id"))
```

Web:

```text
/customer-list.xps?selectedId=1001
```

Desktop:

The current form closes and the XPscript launcher/runtime opens the target script with the same named navigation value.

Security requirements:

- only target paths configured by server-side XPscript handlers are accepted
- target must be a local relative `.xps` path
- reject absolute paths
- reject `..`
- reject non-XPscript extensions
- URL encode web parameter names and values

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

## Field events

Initial event:

```xps
Call form.SetOnChange("field", "Handler")
```

Later candidates:

```text
OnClick
OnFocus
OnBlur
OnInput
OnDoubleClick
```

Do not add these until the shared dispatcher is stable.

## Tests

Add regression tests for:

- button invokes only configured handler
- forged handler name is rejected/ignored
- OnChange invokes configured handler
- handler receives same live UIForm instance
- GetData returns current bound JSON
- SetData rebinds JSON
- JSON changes appear in fields after refresh
- SetFieldValue updates JSON and UI
- label/visible/enabled/readonly changes
- ClearOptions/AddOption during event
- refresh one region
- refresh multiple regions
- refresh all
- button navigation
- OnChange navigation
- navigation traversal rejection
- web partial event response
- Avalonia in-place control refresh
- password values are not leaked during event refresh

## Showcase

Extend the desktop and web UI showcase scripts with:

- Save button
- Back button
- Country OnChange
- dynamic City options
- status region
- JSON mutation
- enabled/disabled field mutation
- label mutation
- navigation back to a UIListView script
