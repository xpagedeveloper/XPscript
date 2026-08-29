namespace XPScript.Compiler;

internal static class NotesNativeApiComputeWithFormSource
{
    public const string Code = """
internal readonly struct XPScriptComputeWithFormResult
{
    internal XPScriptComputeWithFormResult(bool success, ushort validationError, string[] failedFields)
    {
        Success = success;
        ValidationError = validationError;
        FailedFields = failedFields;
    }

    internal bool Success { get; }
    internal ushort ValidationError { get; }
    internal string[] FailedFields { get; }
}

internal sealed partial class XPScriptNotesNativeApi
{
    private const int ComputeWithFormCdFieldFixedLength = 36;
    private const int ComputeWithFormDvLengthOffset = 22;
    private const int ComputeWithFormItLengthOffset = 24;
    private const int ComputeWithFormIvLengthOffset = 28;
    private const int ComputeWithFormNameLengthOffset = 30;

    internal XPScriptComputeWithFormResult ComputeDocumentWithForm(uint note, bool collectFieldNames)
    {
        var validationFailed = false;
        ushort firstValidationError = 0;
        var failedFields = collectFieldNames ? new List<string>() : null;
        var seenFields = collectFieldNames ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;

        ComputeWithFormErrorProcDelegate callback = (field, phase, error, errorText, errorTextSize, context) =>
        {
            validationFailed = true;
            if (firstValidationError == 0) firstValidationError = error;

            if (failedFields is not null && seenFields is not null)
            {
                var fieldName = GetComputeWithFormFieldName(field);
                if (fieldName.Length > 0 && seenFields.Add(fieldName)) failedFields.Add(fieldName);
            }

            return 0;
        };

        var status = Resolve<NSFNoteComputeWithFormDelegate>("NSFNoteComputeWithForm")(
            note,
            0,
            0,
            callback,
            0);
        GC.KeepAlive(callback);
        Check(status, "NSFNoteComputeWithForm");

        return new XPScriptComputeWithFormResult(
            !validationFailed,
            firstValidationError,
            failedFields?.ToArray() ?? []);
    }

    internal void ThrowComputeWithFormValidationError(ushort status)
    {
        if (status != 0)
        {
            Check(status, "NSFNoteComputeWithForm(validation)");
            return;
        }

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
        ComputeWithFormErrorProcDelegate errorRoutine,
        nint context);
}
""";
}
