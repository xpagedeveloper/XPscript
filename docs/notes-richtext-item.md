# NotesRichTextItem and MIME conversion

XPscript exposes `NotesRichTextItem` as a Notes object compatible with the existing HCL Notes/Domino object model.

## Create rich text

Create a rich-text item from its parent document:

```xpscript
Dim doc As NotesDocument
Dim richText As NotesRichTextItem

Set richText = doc.CreateRichTextItem("Body")
Call richText.AppendText("Hello from XPscript")
```

`CreateRichTextItem(name)` returns a `NotesRichTextItem`. An empty item name raises runtime error 5. Creating a rich-text item with an item name that already exists also raises runtime error 5 rather than silently replacing the existing item.

The rich-text object inherits the normal `NotesItem` surface and currently adds `AppendText(value)` and the existing `SaveAttachment(...)` support.

## Convert a NotesItem to NotesRichTextItem

`NotesItem.GetRichTextItem()` provides a safe typed view of an item:

```xpscript
Dim item As NotesItem
Dim richText As NotesRichTextItem

Set item = doc.GetFirstItem("Body")
Set richText = item.GetRichTextItem()

If richText Is Nothing Then
    Print "Body is not rich text"
End If
```

The method returns a `NotesRichTextItem` only when the Notes item type is rich text (`Type = 1`). For every other item type it returns `Nothing`.

## NotesSession.ConvertMIME

HCL LotusScript defines MIME conversion as the Boolean `NotesSession.ConvertMIME` property. XPscript follows that spelling and behavior:

```xpscript
Dim session As NotesSession

session.ConvertMIME = False
' Open documents while MIME must remain MIME_PART.

session.ConvertMIME = True
' Documents opened after this point convert MIME_PART items to rich text.
```

`ConvertMIME` defaults to `True`. The setting affects documents instantiated after the property is changed. When enabled, XPscript uses the HCL C API MIME conversion routine to convert MIME parts to composite rich text in the opened note without automatically saving the document.

This is distinct from HCL `NotesDocument.ConvertToMIME`, which converts Notes-format document content in the other direction.
