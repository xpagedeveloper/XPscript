namespace XPScript.Compiler;

internal static class NotesComputeWithFormPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string documentMarker = """
    public void PutInFolder(object? folderNameValue) => PutInFolder(folderNameValue, false);
""";
        const string documentReplacement = """
    private ushort __computeWithFormCapturedError;
    private object? __computeWithFormCapturedFields;

    public bool ComputeWithForm(object? doDataTypesValue, object? raiseErrorValue)
    {
        EnsureAlive();
        var raiseError = XPScriptRuntime.CBool(raiseErrorValue);
        var success = __ComputeWithFormCapture(doDataTypesValue, raiseErrorValue, false);
        if (!success && raiseError) __RaiseComputeWithFormCapturedError();
        return success;
    }

    // Internal entry point used by the XPscript ByRef lowering for the optional
    // failed-fields Variant. The output slot only exists for the 3-argument call.
    public bool __ComputeWithFormCapture(object? doDataTypesValue, object? raiseErrorValue, object? collectFieldNamesValue)
    {
        EnsureAlive();
        _ = XPScriptRuntime.CBool(doDataTypesValue); // LotusScript keeps this parameter for compatibility but ignores it.
        var raiseError = XPScriptRuntime.CBool(raiseErrorValue);
        var collectFieldNames = raiseError && XPScriptRuntime.CBool(collectFieldNamesValue);

        __computeWithFormCapturedError = 0;
        __computeWithFormCapturedFields = null;

        var result = Session.Api.ComputeDocumentWithForm(_handle, collectFieldNames);
        __computeWithFormCapturedError = result.FirstError;
        if (collectFieldNames && result.FailedFields.Length > 0)
            __computeWithFormCapturedFields = LSOperatorArrayRuntime.CreateArray(result.FailedFields.Cast<object?>().ToArray());
        return result.Success;
    }

    public object? __ComputeWithFormCapturedFields() => __computeWithFormCapturedFields;

    public void __RaiseComputeWithFormCapturedError()
    {
        if (__computeWithFormCapturedError != 0)
            Session.Api.ThrowComputeWithFormError(__computeWithFormCapturedError);
    }

    public void PutInFolder(object? folderNameValue) => PutInFolder(folderNameValue, false);
""";
        source = ReplaceRequired(source, documentMarker, documentReplacement, "document-surface");

        const string nativeMarker = """
    internal void PutDocumentInFolder(uint db, uint noteId, string folderName, bool createOnFail)
""";
        const string nativeReplacement = """
    internal (bool Success, ushort FirstError, string[] FailedFields) ComputeDocumentWithForm(uint note, bool collectFieldNames)
    {
        var failedFields = new List<string>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ushort firstError = 0;

        CwfErrorProcDelegate callback = (cdField, phase, error, errorText, errorTextSize, context) =>
        {
            if (firstError == 0) firstError = error;
            if (collectFieldNames && cdField != 0)
            {
                var fieldName = ReadComputeWithFormFieldName(cdField);
                if (fieldName.Length > 0 && seenFields.Add(fieldName)) failedFields.Add(fieldName);
            }

            // CWF_NEXT_FIELD. Keep processing so a 3-argument call can report every field.
            return 1;
        };

        var status = Resolve<NSFNoteComputeWithFormDelegate>("NSFNoteComputeWithForm")(note, 0, 0, callback, 0);
        GC.KeepAlive(callback);
        if (status != 0) Check(status, "NSFNoteComputeWithForm");
        return (firstError == 0, firstError, failedFields.ToArray());
    }

    internal void ThrowComputeWithFormError(ushort error)
    {
        if (error != 0) Check(error, "NSFNoteComputeWithForm");
    }

    private string ReadComputeWithFormFieldName(nint cdField)
    {
        // CDFIELD's fixed ODS portion is 36 bytes. Its packed variable data is
        // DV formula, IT formula, IV formula, field name, description, text value.
        var dvLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(cdField, 22));
        var itLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(cdField, 24));
        var ivLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(cdField, 28));
        var nameLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(cdField, 30));
        if (nameLength == 0) return "";
        var name = nint.Add(cdField, 36 + dvLength + itLength + ivLength);
        return FromLmbcs(name, nameLength);
    }

    internal void PutDocumentInFolder(uint db, uint noteId, string folderName, bool createOnFail)
""";
        source = ReplaceRequired(source, nativeMarker, nativeReplacement, "native-compute-with-form");

        const string delegateMarker = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderCreateDelegate(uint dataDb, uint folderDb, uint formatNoteId, uint formatDb, nint name, ushort nameLength, uint folderType, uint flags, out uint folderNoteId);
""";
        const string delegateReplacement = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort CwfErrorProcDelegate(nint cdField, ushort phase, ushort error, uint errorText, ushort errorTextSize, nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteComputeWithFormDelegate(uint note, uint formNote, uint flags, CwfErrorProcDelegate errorRoutine, nint callersContext);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderCreateDelegate(uint dataDb, uint folderDb, uint formatNoteId, uint formatDb, nint name, ushort nameLength, uint folderType, uint flags, out uint folderNoteId);
""";
        source = ReplaceRequired(source, delegateMarker, delegateReplacement, "native-compute-with-form-delegates");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument ComputeWithForm surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
