# UIListView cross-platform list view

(c) xpagedeveloper.com 2026

## Goal

Provide a cross-platform list view that displays JSON arrays on web and desktop using the same XPscript API.

Current capabilities include:

- JSON array binding
- multiple visible columns
- configurable labels and widths
- ascending/descending type-aware sorting
- filtering across visible columns
- row selection
- `SetOnSelect` and `SetOnDoubleClick`
- live JSON/list updates after handlers
- target-only row navigation
- per-row handler buttons
- per-row target-only navigation buttons
- secure desktop navigation through the XPscript launcher
- Kestrel, CGI, FastCGI and WebIIS web support
- Avalonia desktop support

## Core API

```xps
Dim rows As JsonArray
Dim list As New UIListView("Customers")

Call list.BindData(rows)
Call list.AddColumn("name", "Name")
Call list.AddColumn("email", "Email")
Call list.AddColumn("country", "Country")
Call list.SetColumnWidth("name", 240)
Call list.SetFilterEnabled(True)
Call list.SetSortable(True)
Call list.SetKeyField("id")
```

## Selection events

```xps
Call list.SetOnSelect("CustomerSelected")
Call list.SetOnDoubleClick("CustomerDoubleClicked")

Sub CustomerSelected(list As Variant)
    Dim row As Variant
    row = list.GetSelectedRow()
    Call row.Set("status", "Selected")
End Sub
```

Handlers may change the bound JSON array or visible list properties. The web renderer applies a state patch without a full page reload. Avalonia updates the list while keeping the dialog open.

## Row navigation

Row navigation now takes only the target module:

```xps
Call list.SetRowAction("customer")
```

The `.xps` extension is optional:

```xps
Call list.SetRowAction("customer")
Call list.SetRowAction("customer.xps")
```

No querystring or navigation parameter is generated. If the target needs data, set the appropriate XPscript state before navigation in a handler-driven flow.

Use:

- `Request.State` for the current request/navigation chain
- `Session.State` for session-scoped data
- `Application.State` for application-wide data
- `Process.State` for process-wide data

Browser navigation changes the visible URL to the configured target route. Desktop uses the XPscript launcher/runtime navigation protocol.

## Per-row action buttons

Handler button:

```xps
Call list.AddRowButton("delete", "Delete", "DeleteCustomer")

Sub DeleteCustomer(list As Variant)
    Call list.RemoveSelectedRow()
End Sub
```

Navigation button:

```xps
Call list.AddRowNavigationButton("edit", "Edit", "customer")
```

Remove all configured row buttons:

```xps
Call list.ClearRowActions()
```

Handler buttons keep the list open and apply returned live state. Navigation buttons open only the configured target. Clicking an action button does not also trigger row navigation.

## JSON binding

The list binds to a JSON array whose elements are JSON objects.

```json
[
  {
    "id": "1001",
    "name": "Kalle Andersson",
    "email": "kalle@example.com",
    "country": "SE"
  },
  {
    "id": "1002",
    "name": "Sven Svensson",
    "email": "sven@example.com",
    "country": "SE"
  }
]
```

Only configured columns are rendered. Missing JSON keys render as an empty cell. Values are converted to display strings without changing the source JSON type.

Useful data methods include:

```text
RowCount
ColumnCount
SelectedIndex
GetRow(index)
GetRowValue(index, field)
GetRowValueString(index, field)
GetSelectedRow()
GetSelectedValue(field)
GetSelectedValueString(field)
GetSelectedKey()
SelectRow(index)
ClearSelection()
RemoveSelectedRow()
```

## Sorting

Clicking a sortable column header toggles ascending/descending order.

Sorting is type-aware where possible:

- numbers as numbers
- booleans as booleans
- ISO dates/date-times chronologically
- strings case-insensitively

## Filtering

A single filter input searches all visible columns. Hidden JSON properties do not participate. Filtering is case-insensitive by default.

`SetFilterEnabled(True/False)` and `SetSortable(True/False)` may also be changed by an event handler while the list is open.

## Web implementation

The renderer uses semantic HTML table markup and built-in JavaScript.

Requirements:

- encoded headers and cell values
- client-side sort/filter
- keyboard-accessible row navigation
- event POSTs to the same XPscript route
- row actions stop event bubbling
- target-only navigation URLs
- no row id or other data encoded as navigation query parameters

Interactive web list routes must permit both GET and POST.

## Desktop implementation

Avalonia renders:

- sortable column headers
- filter TextBox
- row selection
- double-click event/action
- per-row buttons
- live list refresh
- secure target-only navigation through the XPscript launcher

## Security

- encode rendered web values and labels
- validate local XPscript target paths
- allow optional `.xps` extension
- reject absolute paths
- reject `..`
- reject non-XPscript target extensions
- do not execute target paths supplied by JSON data
- handler names come only from trusted XPscript configuration
- action targets come only from trusted XPscript configuration
- browser/desktop clients send only registered event/action names and row indexes
- do not expose selected row data through navigation querystrings

## Remaining performance work

For very large lists add later:

- paging
- virtualization
- server-side/provider mode
- incremental loading

## Tests and showcase scripts

Current showcase/regression files include:

```text
samples/ui-list-view-core.xps
samples/ui-list-view-desktop.xps
samples/ui-list-view-web.xps
samples/customer-form.xps
```

They cover JSON binding, columns, selection, sorting/filtering configuration, events, live updates, target-only row navigation and per-row buttons.
