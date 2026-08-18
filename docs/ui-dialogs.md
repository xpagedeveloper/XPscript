# UI dialogs

(c) xpagedeveloper.com 2026

XPscript desktop builds can use generic dialogs and native file pickers through the Avalonia desktop backend.

## ShowDialog

`ShowDialog(message [, title [, kind [, values]]])`

The function returns a String.

Supported `kind` values:

- `OK` returns `OK`.
- `OKCancel` returns `OK` or `Cancel`.
- `YesNo` returns `Yes` or `No`.
- `YesNoCancel` returns `Yes`, `No` or `Cancel`.
- `RetryCancel` returns `Retry` or `Cancel`.
- `AbortRetryIgnore` returns `Abort`, `Retry` or `Ignore`.
- `List` returns the selected list value or `Cancel`.
- `Input` returns entered text or `Cancel`.
- `Password` returns entered text or `Cancel`. Password text is masked in the UI.

Examples:

```text
answer = ShowDialog("Save changes?", "Confirm", "YesNo")

If answer = "Yes" Then
    Print "Save"
End If
```

A list can be supplied as an enumerable value. A pipe-separated String is also accepted:

```text
selected = ShowDialog("Choose environment", "Environment", "List", "Development|Test|Production")
```

For `Input` and `Password`, the first supplied value is used as the initial text. Avoid supplying an initial password unless the application explicitly requires it.

## OpenFileDialog and LoadFileDialog

`LoadFileDialog` is an alias for `OpenFileDialog`.

```text
fileName = OpenFileDialog("Open document")
fileName = LoadFileDialog("Open document", "C:\Data")
fileName = OpenFileDialog("Open data", "C:\Data", "JSON|*.json|CSV|*.csv|All files|*")
```

The function returns the selected local path. It returns an empty String when the user cancels.

Parameters:

1. `title`, optional dialog title.
2. `initialPath`, optional initial folder or file path.
3. `filter`, optional Windows-style name/pattern pairs separated with `|`. Multiple patterns in one group are separated with `;`.

Example filter:

```text
"Images|*.png;*.jpg;*.jpeg|All files|*"
```

## SaveFileDialog

```text
fileName = SaveFileDialog("Save report", "C:\Data\report.json", "JSON|*.json|All files|*")
```

The function returns the selected local path. It returns an empty String when the user cancels.

When `initialPath` contains a file name it is used as the suggested file name. Existing-file overwrite confirmation is requested where the platform supports it.

## Platform behavior

These APIs currently use the desktop Avalonia backend. The file pickers use Avalonia StorageProvider and native platform picker support where available.

`UIForm.ShowDialog()` remains separate. It renders a complete bound form. The standalone `ShowDialog()` function is intended for prompts, confirmations, simple choices and short user input.
