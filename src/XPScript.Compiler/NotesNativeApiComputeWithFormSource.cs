namespace XPScript.Compiler;

internal static class NotesNativeApiComputeWithFormSource
{
    public const string Code = """
internal readonly struct XPScriptComputeWithFormResult
{
    internal XPScriptComputeWithFormResult(bool success, string[] failedFields)
    {
        Success = success;
        FailedFields = failedFields;
    }

    internal bool Success { get; }
    internal string[] FailedFields { get; }
}

internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort ComputeWithFormValidationFailedStatus = 0x038d;
    private const ushort ComputeWithFormNextFieldAction = 2;
    private const int ComputeWithFormCdFieldFixedLength = 36;
    private const int ComputeWithFormDvLengthOffset = 22;
    private const int ComputeWithFormItLengthOffset = 24;
    private const int ComputeWithFormIvLengthOffset = 28;
    private const int ComputeWithFormNameLengthOffset = 30;

    internal bool ComputeDocumentWithForm(uint note)
    {
        var status = Resolve<NSFNoteComputeWithFormDelegate>("NSFNoteComputeWithForm")(
            note,
            0,
            0,
            null,
            0);

        if (status == 0) return true;
        if (status == ComputeWithFormValidationFailedStatus) return false;

        Check(status, "NSFNoteComputeWithForm");
        return false;
    }

    internal XPScriptComputeWithFormResult ComputeDocumentWithFormAndCollectErrors(uint note)
    {
        var validationFailed = false;
        var failedFields = new List<string>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ComputeWithFormErrorProcDelegate callback = (field, phase, error, errorText, errorTextSize, context) =>
        {
            validationFailed = true;

            var fieldName = GetComputeWithFormFieldName(field);
            if (fieldName.Length > 0 && seenFields.Add(fieldName)) failedFields.Add(fieldName);

            return ComputeWithFormNextFieldAction;
        };

        var status = Resolve<NSFNoteComputeWithFormDelegate>("NSFNoteComputeWithForm")(
            note,
            0,
            0,
            callback,
            0);
        GC.KeepAlive(callback);

        // Some Domino runtimes return ERR_VALIDATION (0x038D) even after the
        // validation callback has already reported the field-level failure.
        // Treat that status as the validation result, not as a native API failure.
        // Any other non-zero status still represents an unexpected lower-level error.
        if (status != 0 && status != ComputeWithFormValidationFailedStatus)
            Check(status, "NSFNoteComputeWithForm");

        return new XPScriptComputeWithFormResult(
            !validationFailed && status != ComputeWithFormValidationFailedStatus,
            failedFields.ToArray());
    }

    internal void ThrowComputeWithFormValidationError()
    {
        throw new XPScriptRuntimeException(5, "ComputeWithForm validation failed.");
    }

    private string GetComputeWithFormFieldName(nint field)
    {
        if (field == 0) return "";

        var dvLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(field, ComputeWithFormDvLengthOffset));
        var itLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(field, ComputeWithFormItLengthOffset));
        var ivLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(field, ComputeWithFormIvLengthOffset));
        var nameLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(field, ComputeWithFormNameLengthOffset));
        if (nameLength == 0) return "";

        var name = nint.Add(field, ComputeWithFormCdFieldFixedLength + dvLength + itLength + ivLength);
        return FromLmbcs(name, nameLength);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort ComputeWithFormErrorProcDelegate(
        nint field,
        ushort phase,
        ushort error,
        uint errorText,
        ushort errorTextSize,
        nint context);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFNoteComputeWithFormDelegate(
        uint note,
        uint formNote,
        uint flags,
        ComputeWithFormErrorProcDelegate? errorRoutine,
        nint context);
}
""";
}
