namespace XPScript.Compiler;

internal static class UIExtensionRuntimeSource
{
    public const string Code = """
internal static class XPScriptUI
{
    public static XPScriptUIForm CreateForm()
        => new(string.Empty, null, null, true);

    public static XPScriptUIForm CreateForm(object? title)
        => new(XPScriptRuntime.CStr(title), null, null, true);

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height)
        => new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), true);

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height, object? resizable)
        => new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), Convert.ToBoolean(resizable, System.Globalization.CultureInfo.CurrentCulture));

    private static int? ToOptionalPositiveInt(object? value, string name)
    {
        if (value is null) return null;
        int converted;
        try
        {
            converted = Convert.ToInt32(value, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, $"UIForm {name} must be an Integer value.");
        }

        if (converted <= 0)
            throw new XPScriptRuntimeException(5, $"UIForm {name} must be greater than zero.");
        return converted;
    }
}

internal sealed class XPScriptUIForm
{
    private string _title;
    private int? _width;
    private int? _height;
    private bool _resizable;

    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)
    {
        _title = title;
        _width = width;
        _height = height;
        _resizable = resizable;
    }

    public string Title
    {
        get => _title;
        set => _title = value ?? string.Empty;
    }

    public int Width
    {
        get => _width ?? 0;
        set
        {
            if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm width must be greater than zero.");
            _width = value;
        }
    }

    public int Height
    {
        get => _height ?? 0;
        set
        {
            if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm height must be greater than zero.");
            _height = value;
        }
    }

    public bool Resizable
    {
        get => _resizable;
        set => _resizable = value;
    }

    public bool HasExplicitSize => _width.HasValue || _height.HasValue;
}
""";
}
