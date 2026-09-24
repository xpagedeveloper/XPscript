namespace XPScript.Compiler;

internal static class HttpUiFormRuntimeSource
{
    public const string Code = """
internal static class XPScriptHttpUiFormHelpers
{
    public static void LoadForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        var document = XPScriptHttpJsonHelpers.GetJson(clientValue, url);
        if (document.Root.AsObject() is null) throw new XPScriptRuntimeException(13, "UIForm.LoadForm requires a JSON object response.");
        form.BindData(document);
    }

    public static XPScriptHttpResponse SaveForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        EnsureFormDataValid(form, "SaveForm");
        var response = XPScriptHttpJsonHelpers.PostJson(clientValue, url, form.Data);
        if (response.IsSuccess) MarkFormClean(form);
        return response;
    }

    public static XPScriptHttpResponse PutForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        EnsureFormDataValid(form, "PutForm");
        var response = XPScriptHttpJsonHelpers.PutJson(clientValue, url, form.Data);
        if (response.IsSuccess) MarkFormClean(form);
        return response;
    }

    private static void EnsureFormDataValid(XPScriptUIForm form, string operation)
    {
        var validation = form.ValidateData();
        if (validation.Valid) return;
        var paths = validation.FailedPaths;
        var failed = paths.Count == 0 ? "unknown path" : string.Join(", ", Enumerable.Range(0, paths.Count).Select(index => XPScriptRuntime.CStr(paths.Get(index))));
        throw new XPScriptRuntimeException(5, $"UIForm.{operation} JSON Schema validation failed: {failed}.");
    }

    private static void MarkFormClean(XPScriptUIForm form)
    {
        var method = form.GetType().GetMethod("MarkClean", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (method is null) throw new XPScriptRuntimeException(5, "UIForm dirty tracking runtime is unavailable.");
        method.Invoke(form, null);
    }

    private static XPScriptUIForm Form(object? value)
        => value as XPScriptUIForm ?? throw new XPScriptRuntimeException(13, "UIForm HTTP helper requires a UIForm instance.");
}
""";
}
