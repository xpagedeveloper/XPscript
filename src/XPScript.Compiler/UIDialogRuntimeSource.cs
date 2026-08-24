namespace XPScript.Compiler;

internal static class UIDialogRuntimeSource
{
    public const string Code = """
internal static class XPScriptUIDialogRuntime
{
    private const string HostTypeName = "XPScript.UI.Desktop.DesktopDialogHost, XPScript.UI.Desktop";
    private static Type? HostType => Type.GetType(HostTypeName, throwOnError: false, ignoreCase: false);

    public static string ShowDialog(object? message) => ShowDialog(message, string.Empty, "OK", null);
    public static string ShowDialog(object? message, object? title) => ShowDialog(message, title, "OK", null);
    public static string ShowDialog(object? message, object? title, object? kind) => ShowDialog(message, title, kind, null);
    public static string ShowDialog(object? message, object? title, object? kind, object? values)
    {
        var options = ToOptions(values);
        var request = new
        {
            message = XPScriptRuntime.CStr(message),
            title = XPScriptRuntime.CStr(title),
            kind = XPScriptRuntime.CStr(kind),
            options
        };
        return Invoke("ShowChoiceDialog", System.Text.Json.JsonSerializer.Serialize(request));
    }

    public static int MsgBox(object? prompt) => MsgBox(prompt, 0, string.Empty);
    public static int MsgBox(object? prompt, object? boxType) => MsgBox(prompt, boxType, string.Empty);
    public static int MsgBox(object? prompt, object? boxType, object? title)
    {
        var style = XPScriptRuntime.CInt(boxType);
        var kind = (style & 0x0F) switch
        {
            0 => "OK",
            1 => "OKCancel",
            2 => "AbortRetryIgnore",
            3 => "YesNoCancel",
            4 => "YesNo",
            5 => "RetryCancel",
            _ => throw new XPScriptRuntimeException(5, "Unsupported MsgBox button style. Use 0 through 5 for the button group.")
        };

        var result = ShowDialog(prompt, title, kind, null);
        return result.ToLowerInvariant() switch
        {
            "ok" => 1,
            "cancel" => 2,
            "abort" => 3,
            "retry" => 4,
            "ignore" => 5,
            "yes" => 6,
            "no" => 7,
            _ => 0
        };
    }

    public static string LoadFileDialog() => OpenFileDialog();
    public static string LoadFileDialog(object? title) => OpenFileDialog(title);
    public static string LoadFileDialog(object? title, object? initialPath) => OpenFileDialog(title, initialPath);
    public static string LoadFileDialog(object? title, object? initialPath, object? filter) => OpenFileDialog(title, initialPath, filter);

    public static string OpenFileDialog() => OpenFileDialog(string.Empty, string.Empty, string.Empty);
    public static string OpenFileDialog(object? title) => OpenFileDialog(title, string.Empty, string.Empty);
    public static string OpenFileDialog(object? title, object? initialPath) => OpenFileDialog(title, initialPath, string.Empty);
    public static string OpenFileDialog(object? title, object? initialPath, object? filter)
    {
        var request = new
        {
            title = XPScriptRuntime.CStr(title),
            initialPath = XPScriptRuntime.CStr(initialPath),
            filter = XPScriptRuntime.CStr(filter)
        };
        return Invoke("ShowOpenFileDialog", System.Text.Json.JsonSerializer.Serialize(request));
    }

    public static string SaveFileDialog() => SaveFileDialog(string.Empty, string.Empty, string.Empty);
    public static string SaveFileDialog(object? title) => SaveFileDialog(title, string.Empty, string.Empty);
    public static string SaveFileDialog(object? title, object? initialPath) => SaveFileDialog(title, initialPath, string.Empty);
    public static string SaveFileDialog(object? title, object? initialPath, object? filter)
    {
        var request = new
        {
            title = XPScriptRuntime.CStr(title),
            initialPath = XPScriptRuntime.CStr(initialPath),
            filter = XPScriptRuntime.CStr(filter)
        };
        return Invoke("ShowSaveFileDialog", System.Text.Json.JsonSerializer.Serialize(request));
    }

    private static string[] ToOptions(object? values)
    {
        if (values is null) return [];
        if (values is string text)
            return text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values is System.Collections.IEnumerable enumerable)
        {
            var result = new List<string>();
            foreach (var item in enumerable) result.Add(XPScriptRuntime.CStr(item));
            return result.ToArray();
        }
        return [XPScriptRuntime.CStr(values)];
    }

    private static string Invoke(string methodName, string requestJson)
    {
        var type = HostType ?? throw new XPScriptRuntimeException(5, "XPScript desktop dialog backend is unavailable.");
        var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, $"XPScript desktop dialog backend does not implement {methodName}.");
        try
        {
            return Convert.ToString(method.Invoke(null, [requestJson]), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "Desktop dialog failed: " + ex.InnerException.Message);
        }
    }
}
""";
}
