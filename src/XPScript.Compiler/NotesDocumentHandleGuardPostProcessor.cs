namespace XPScript.Compiler;

internal static class NotesDocumentHandleGuardPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = GuardRequired(source,
            "    public bool HasItem(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-has-item");
        source = GuardRequired(source,
            "    public object? GetValue(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-get-value");
        source = GuardRequired(source,
            "    public string GetString(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-get-string");
        source = GuardRequired(source,
            "    public double GetNumber(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-get-number");
        source = GuardRequired(source,
            "    public XPScriptNotesDateTime GetDateTime(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-get-datetime");
        source = GuardRequired(source,
            "    public void SetValue(object? nameValue, object? value)\n    {\n        EnsureAlive();\n",
            "document-set-value");
        source = GuardRequired(source,
            "    public void SetString(object? nameValue, object? value)\n    {\n        EnsureAlive();\n",
            "document-set-string");
        source = GuardRequired(source,
            "    public void SetNumber(object? nameValue, object? value)\n    {\n        EnsureAlive();\n",
            "document-set-number");
        source = GuardRequired(source,
            "    public void SetDateTime(object? nameValue, object? value)\n    {\n        EnsureAlive();\n",
            "document-set-datetime");
        source = GuardRequired(source,
            "    public void RemoveItem(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-remove-item");
        source = GuardRequired(source,
            "    public XPScriptNotesItem CreateNotesItem(object? nameValue)\n    {\n        EnsureAlive();\n",
            "document-create-item");
        source = GuardRequired(source,
            "    public XPScriptNotesItem ReplaceItemValue(object? nameValue, object? value)\n    {\n        EnsureAlive();\n",
            "document-replace-item-value");
        source = GuardRequired(source,
            "    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)\n    {\n        EnsureAlive();\n",
            "document-save-attachment");
        source = GuardRequired(source,
            "    public bool ComputeWithForm(object? doDataTypesValue, object? raiseErrorValue)\n    {\n        EnsureAlive();\n",
            "document-compute-with-form");
        source = GuardRequired(source,
            "    public bool ComputeWithForm(object? doDataTypesValue, object? raiseErrorValue, ref object? __xps_byref_failedFields)\n    {\n        EnsureAlive();\n",
            "document-compute-with-form-failed-fields");

        // GetFirstItem reaches the native item API through TryGetItemInfo, which
        // previously passed the raw _handle and therefore bypassed NativeHandle.
        source = GuardRequired(source,
            "    internal bool TryGetItemInfo(string name, out XPScriptNotesItemInfo info)\n    {\n        EnsureAlive();\n",
            "document-try-get-item-info");

        // Rich-text support is feature-dependent, so guard it when present without
        // making reduced runtime feature sets fail source generation.
        source = GuardOptional(source,
            "    public XPScriptNotesRichTextItem CreateRichTextItem(object? nameValue)\n    {\n        EnsureAlive();\n");

        // Item objects and other native consumers use this common accessor. Guard it
        // so deletion stubs cannot reach native APIs through an absent note handle.
        source = ReplaceRequired(source,
            "internal uint NativeHandle { get { EnsureAlive(); return _handle; } }\n    internal XPScriptNotesSession SessionForItem",
            "internal uint NativeHandle { get { EnsureAlive(); RequireOpenNoteHandle(); return _handle; } }\n    internal XPScriptNotesSession SessionForItem",
            "document-native-handle");

        return source;
    }

    private static string GuardRequired(string source, string marker, string stage)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument handle guard (" + stage + ").");
        return source.Replace(marker, marker + "        RequireOpenNoteHandle();\n", StringComparison.Ordinal);
    }

    private static string GuardOptional(string source, string marker)
        => source.Contains(marker, StringComparison.Ordinal)
            ? source.Replace(marker, marker + "        RequireOpenNoteHandle();\n", StringComparison.Ordinal)
            : source;

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument handle guard (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
