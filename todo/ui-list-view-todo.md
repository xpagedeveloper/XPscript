# UIListView cross-platform list view

(c) xpagedeveloper.com 2026

## Goal

Add a cross-platform list view that can display a complete JSON array on web and desktop using the same XPscript API.

Implemented core capabilities on the current integration branch:

- JSON array binding
- multiple visible columns
- configurable column labels and widths
- ascending/descending type-aware sorting
- filtering across visible columns
- row selection
- `SetOnSelect` and `SetOnDoubleClick`
- live JSON/list updates after handlers
- row navigation to another `.xps` file
- per-row handler buttons
- per-row navigation buttons
- secure desktop navigation through the XPscript launcher
- web support through Kestrel, CGI and FastCGI
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

Handlers may change the bound JSON array or visible list properties. The web renderer applies a state patch without a full page reload. Avalonia rebuilds only the list/header area and keeps the dialog open.

## Row navigation

```xps
Call list.SetRowAction("customer.xps", "id", "customerId")
```

A normal row open navigates to the configured local `.xps` target with the configured value.

Web example:

```text
customer.xps?customerId=1001
```

Desktop uses the XPscript launcher navigation protocol and validates the target before execution.

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
Call list.AddRowNavigationButton("edit", "Edit", "customer.xps", "id", "customerId")
```

Remove all configured row buttons:

```xps
Call list.ClearRowActions()
```

Handler buttons keep the list open and apply the returned live state. Navigation buttons open the configured target. Clicking an action button does not also trigger the row click/navigation event.

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

The renderer uses semantic HTML table markup and built-in JavaScript. No external JavaScript framework is required.

Requirements implemented or enforced:

- encoded headers and cell values
- client-side sort/filter
- URL-encoded action values
- keyboard-accessible row navigation
- event POSTs to the same XPscript route
- row actions stop event bubbling so they do not also execute row navigation

Interactive web list routes must permit both GET and POST.

## Desktop implementation

Avalonia renders:

- sortable column headers
- filter TextBox
- row selection
- double-click event/action
- per-row buttons
- live list refresh
- secure navigation through the XPscript launcher

## Security

- encode rendered web values and labels
- URL encode selected values
- validate target `.xps` paths
- reject absolute paths
- reject `..`
- reject non-XPscript target extensions
- do not execute target paths supplied by JSON data
- handler names come only from server-side XPscript configuration
- action targets come only from server-side XPscript configuration
- browser/desktop clients send only event/action name and row index

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

They cover JSON binding, columns, selection, sorting/filtering configuration, events, live updates, row navigation and per-row buttons.
