namespace XPScript.Compiler;

public static class UtilityFunctionsRuntimeSource
{
    public const string Code = """
internal static class XPScriptUtilityRuntime
{
    public static bool FileExists(object? path) =>
        System.IO.File.Exists(XPScriptRuntime.CStr(path));

    public static bool DirExists(object? path) =>
        System.IO.Directory.Exists(XPScriptRuntime.CStr(path));

    public static string StrTemplate(object? template, object? values)
    {
        var text = XPScriptRuntime.CStr(template);
        var output = new System.Text.StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '\\' && i + 1 < text.Length && (text[i + 1] == '{' || text[i + 1] == '}'))
            {
                output.Append(text[i + 1]);
                i++;
                continue;
            }

            if (ch != '{')
            {
                output.Append(ch);
                continue;
            }

            var end = text.IndexOf('}', i + 1);
            if (end < 0)
            {
                output.Append(ch);
                continue;
            }

            var token = text[(i + 1)..end];
            if (token.Length == 0 || !token.All(char.IsDigit))
            {
                output.Append(text, i, end - i + 1);
                i = end;
                continue;
            }

            if (!int.TryParse(token, out var index))
                throw new XPScriptRuntimeException(9, "StrTemplate placeholder index is invalid.");

            output.Append(XPScriptRuntime.CStr(GetTemplateValue(values, index)));
            i = end;
        }

        return output.ToString();
    }

    private static object? GetTemplateValue(object? values, int index)
    {
        try
        {
            if (values is LSArray lsArray)
                return lsArray.Get(index);

            if (values is System.Array array)
            {
                if (array.Rank != 1)
                    throw new XPScriptRuntimeException(9, "StrTemplate values must be a one-dimensional array.");
                return array.GetValue(index);
            }

            if (values is System.Collections.IList list)
                return list[index];
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new XPScriptRuntimeException(9, "StrTemplate placeholder index is outside the supplied values array.");
        }

        throw new XPScriptRuntimeException(13, "StrTemplate values must be an array or list.");
    }
}
""";
}
