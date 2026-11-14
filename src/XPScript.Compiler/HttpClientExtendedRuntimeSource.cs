namespace XPScript.Compiler;

internal static class HttpClientExtendedRuntimeSource
{
    public const string Code = """
internal sealed partial class XPScriptHttpClient
{
    public object? GetAsync(object? url, object? callbackName, params object?[] callbackArguments) => XPScriptAsyncHttp.GetAsync(this, url, callbackName, callbackArguments);
    public object? DeleteAsync(object? url, object? callbackName, params object?[] callbackArguments) => XPScriptAsyncHttp.DeleteAsync(this, url, callbackName, callbackArguments);
    public object? PostAsync(object? url, object? body, object? callbackName, params object?[] callbackArguments) => XPScriptAsyncHttp.PostAsync(this, url, body, callbackName, callbackArguments);
    public object? PutAsync(object? url, object? body, object? callbackName, params object?[] callbackArguments) => XPScriptAsyncHttp.PutAsync(this, url, body, callbackName, callbackArguments);
    public object? PatchAsync(object? url, object? body, object? callbackName, params object?[] callbackArguments) => XPScriptAsyncHttp.PatchAsync(this, url, body, callbackName, callbackArguments);
}
""";

    public const string FormCode = """
internal sealed partial class XPScriptHttpClient
{
    public void LoadForm(object? form, object? url) => XPScriptHttpUiFormHelpers.LoadForm(this, form, url);
    public XPScriptHttpResponse SaveForm(object? form, object? url) => XPScriptHttpUiFormHelpers.SaveForm(this, form, url);
    public XPScriptHttpResponse PutForm(object? form, object? url) => XPScriptHttpUiFormHelpers.PutForm(this, form, url);
}
""";
}
