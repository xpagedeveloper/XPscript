namespace XPScript.Compiler;

internal static class BrowserNavigationStateRuntimeSource
{
    public const string Code = """
internal static class XPScriptBrowserNavigationStateRuntime
{
    public static void Stage()
    {
        if (!OperatingSystem.IsBrowser()) return;
        var browserHost = ResolveBrowserHost();
        var stage = browserHost?.GetMethod("StageRequestState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (stage is null)
            throw new XPScriptRuntimeException(5, "Browser Request.State navigation backend is unavailable.");

        var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Keys())
            entries[key] = Encode(XPScriptRequestRuntime.State.Get(key));
        var json = System.Text.Json.JsonSerializer.Serialize(entries);
        stage.Invoke(null, [json]);
    }

    public static void Restore()
    {
        if (!OperatingSystem.IsBrowser()) return;
        var browserHost = ResolveBrowserHost();
        var consume = browserHost?.GetMethod("ConsumeRequestState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (consume is null) return;
        var json = Convert.ToString(consume.Invoke(null, null), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (json.Length == 0) return;
        if (json.Length > 1024 * 1024)
            throw new XPScriptRuntimeException(5, "Browser Request.State navigation payload exceeds 1 MiB.");

        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new XPScriptRuntimeException(5, "Browser Request.State navigation payload is invalid.");

        XPScriptRequestRuntime.State.Clear();
        var count = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (++count > 128)
                throw new XPScriptRuntimeException(5, "Browser Request.State navigation payload exceeds 128 entries.");
            XPScriptRequestRuntime.State.Set(property.Name, Decode(property.Value));
        }
    }

    private static Type? ResolveBrowserHost()
    {
        var direct = Type.GetType("XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser", throwOnError: false, ignoreCase: false);
        if (direct is not null) return direct;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var candidate = assembly.GetType("XPScript.UI.Browser.BrowserFormHost", throwOnError: false, ignoreCase: false);
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> Keys()
    {
        var keys = XPScriptRequestRuntime.State.Keys;
        if (keys is IEnumerable<string> typed) return typed.ToArray();
        if (keys is System.Collections.IEnumerable values)
            return values.Cast<object?>().Select(XPScriptRuntime.CStr).Where(value => value.Length > 0).ToArray();
        return Array.Empty<string>();
    }

    private static object Encode(object? value)
    {
        var type = value switch
        {
            null => "null",
            string => "string",
            bool => "bool",
            byte => "byte",
            sbyte => "sbyte",
            short => "short",
            ushort => "ushort",
            int => "int",
            uint => "uint",
            long => "long",
            ulong => "ulong",
            float => "float",
            double => "double",
            decimal => "decimal",
            char => "char",
            DateTime => "datetime",
            DateTimeOffset => "datetimeoffset",
            Guid => "guid",
            byte[] => "bytes",
            _ => throw new XPScriptRuntimeException(5, "Browser Request.State only supports scalar values, strings and byte arrays.")
        };

        object? encoded = value switch
        {
            DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D"),
            byte[] bytes => Convert.ToBase64String(bytes),
            char character => character.ToString(),
            _ => value
        };
        return new Dictionary<string, object?> { ["type"] = type, ["value"] = encoded };
    }

    private static object? Decode(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !element.TryGetProperty("type", out var typeElement) ||
            !element.TryGetProperty("value", out var valueElement))
            throw new XPScriptRuntimeException(5, "Browser Request.State navigation entry is invalid.");

        var type = typeElement.GetString() ?? string.Empty;
        return type switch
        {
            "null" => null,
            "string" => valueElement.GetString() ?? string.Empty,
            "bool" => valueElement.GetBoolean(),
            "byte" => valueElement.GetByte(),
            "sbyte" => checked((sbyte)valueElement.GetInt32()),
            "short" => valueElement.GetInt16(),
            "ushort" => valueElement.GetUInt16(),
            "int" => valueElement.GetInt32(),
            "uint" => valueElement.GetUInt32(),
            "long" => valueElement.GetInt64(),
            "ulong" => valueElement.GetUInt64(),
            "float" => valueElement.GetSingle(),
            "double" => valueElement.GetDouble(),
            "decimal" => valueElement.GetDecimal(),
            "char" => DecodeChar(valueElement),
            "datetime" => DateTime.Parse(valueElement.GetString() ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
            "datetimeoffset" => DateTimeOffset.Parse(valueElement.GetString() ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
            "guid" => Guid.Parse(valueElement.GetString() ?? string.Empty),
            "bytes" => Convert.FromBase64String(valueElement.GetString() ?? string.Empty),
            _ => throw new XPScriptRuntimeException(5, "Browser Request.State navigation entry contains an unsupported type.")
        };
    }

    private static char DecodeChar(System.Text.Json.JsonElement element)
    {
        var text = element.GetString() ?? string.Empty;
        if (text.Length != 1)
            throw new XPScriptRuntimeException(5, "Browser Request.State navigation char value is invalid.");
        return text[0];
    }
}
""";
}
