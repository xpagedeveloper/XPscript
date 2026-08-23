# Desktop UI command reference

This is the reference for XPScript desktop dialog commands. These commands require the desktop UI runtime (`XPScript.UI.Desktop`) and are intended for Windows, Linux, and macOS desktop applications.

Every entry includes the command title, syntax, parameters, behavior, and a complete `.xps` example that can be copied and compiled.

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `MsgBox` | `MsgBox(prompt [, boxType [, title]])` | `prompt`: message text; `boxType`: optional integer button/modifier bit mask; `title`: optional window title. Button groups use low-nibble values `0=OK`, `1=OKCancel`, `2=AbortRetryIgnore`, `3=YesNoCancel`, `4=YesNo`, `5=RetryCancel`. | Shows a modal desktop message box and returns `1=OK`, `2=Cancel`, `3=Abort`, `4=Retry`, `5=Ignore`, `6=Yes`, or `7=No`. Modifier bits outside the button-group nibble are accepted but do not change the button group. | [customer-form.xps](../demo/desktop-ui/customer-form.xps) |
| `ShowDialog` | `ShowDialog(message [, title [, kind [, values]]])` | `message`: text; `title`: optional title; `kind`: `OK`, `OKCancel`, `YesNo`, `YesNoCancel`, `RetryCancel`, `AbortRetryIgnore`, `List`, `Input`, or `Password`; `values`: optional list/input values. | Shows the XPScript desktop choice/input dialog and returns the selected value/result string. | [ui-dialogs.xps](../samples/ui-dialogs.xps) |
| `OpenFileDialog` | `OpenFileDialog([title [, initialPath [, filter]]])` | `title`: optional dialog title; `initialPath`: optional starting file/folder; `filter`: optional `Description|*.ext` filter string. | Shows the native desktop open-file picker and returns the selected local path, or an empty string when cancelled. | [ui-dialogs.xps](../samples/ui-dialogs.xps) |
| `LoadFileDialog` | `LoadFileDialog([title [, initialPath [, filter]]])` | Same parameters as `OpenFileDialog`. | Compatibility alias for `OpenFileDialog`. | [ui-dialogs.xps](../samples/ui-dialogs.xps) |
| `SaveFileDialog` | `SaveFileDialog([title [, initialPath [, filter]]])` | `title`: optional dialog title; `initialPath`: optional starting path/file name; `filter`: optional file-type filter. | Shows the native desktop save-file picker and returns the selected local path, or an empty string when cancelled. | [ui-dialogs.xps](../samples/ui-dialogs.xps) |

## MsgBox example

The desktop demo now shows `MsgBox` after its UIForm closes:

```xpscript
Option Declare

Sub Main()
    Dim answer As Integer
    answer = MsgBox("Continue?", 4, "XPScript")
    Print "MSGBOX=" & CStr(answer)
End Sub
```

Compile a desktop program with:

```powershell
xpscriptc .\demo\desktop-ui\customer-form.xps -o .\out\desktop-ui-demo.exe --framework-dependent
```
