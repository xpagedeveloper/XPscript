namespace XPScript.Compiler;

internal static class NotesDocumentComputeWithFormPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string marker = """
    public void PutInFolder(object? folderNameValue) => PutInFolder(folderNameValue, false);
""";

        const string replacement = """
    public bool ComputeWithForm(object? doDataTypesValue, object? raiseErrorValue)
    {
        EnsureAlive();
        _ = XPScriptRuntime.CBool(doDataTypesValue);
        var raiseError = XPScriptRuntime.CBool(raiseErrorValue);
        var result = Session.Api.ComputeDocumentWithForm(_handle, false);
        if (!result.Success && raiseError)
            Session.Api.ThrowComputeWithFormValidationError(result.ValidationError);
        return result.Success;
    }

    public bool ComputeWithForm(object? doDataTypesValue, object? raiseErrorValue, object? __xps_byref_failedFields)
    {
        EnsureAlive();
        _ = XPScriptRuntime.CBool(doDataTypesValue);
        var raiseError = XPScriptRuntime.CBool(raiseErrorValue);
        __xps_byref_failedFields = null;

        var result = Session.Api.ComputeDocumentWithForm(_handle, raiseError);
        if (!result.Success && raiseError)
        {
            __xps_byref_failedFields = LSOperatorArrayRuntime.CreateArray(
                result.FailedFields.Cast<object?>().ToArray());
            Session.Api.ThrowComputeWithFormValidationError(result.ValidationError);
        }

        return result.Success;
    }

    public void PutInFolder(object? folderNameValue) => PutInFolder(folderNameValue, false);
""";

        if (!source.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument ComputeWithForm surface.");

        return source.Replace(marker, replacement, StringComparison.Ordinal);
    }
}
