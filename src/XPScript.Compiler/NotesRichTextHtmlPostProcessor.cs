namespace XPScript.Compiler;

internal static class NotesRichTextHtmlPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        const string anchor = "    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)\n    {\n        EnsureItemAlive();";
        const string replacement = "    public string ConvertToHTML() => ConvertToHTML(false);\n\n    public string ConvertToHTML(object? inlineImagesValue)\n    {\n        EnsureItemAlive();\n        return Session.Api.ConvertRichTextToHtml(Document.ParentDatabase.Handle, Document.NativeHandle, ItemName, XPScriptRuntime.CBool(inlineImagesValue));\n    }\n\n    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)\n    {\n        EnsureItemAlive();";
        var index = source.IndexOf(anchor, StringComparison.Ordinal);
        if (index < 0) throw new CompilerException("Unable to add NotesRichTextItem.ConvertToHTML surface.");
        source = source[..index] + replacement + source[(index + anchor.Length)..];
        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal string ConvertRichTextToHtml(nint db, nint note, string itemName, bool inlineImages)
    {
        EnsureInitialized();
        using var nativeStandardOutput = XPScriptRuntimeDebugTrace.SuppressNativeStandardOutputUnlessDetailed();
        Check(Resolve<HTMLCreateConverterDelegate>("HTMLCreateConverter")(out var converter), "HTMLCreateConverter");
        try
        {
            using var name = ToLmbcs(itemName);
            Check(Resolve<HTMLConvertItemDelegate>("HTMLConvertItem")(converter, db, note, name.Pointer), "HTMLConvertItem");
            var html = ReadHtmlConverterText(converter);
            return inlineImages ? InlineHtmlImages(db, note, itemName, html) : html;
        }
        finally
        {
            var status = Resolve<HTMLDestroyConverterDelegate>("HTMLDestroyConverter")(converter);
            if (status != 0) XPScriptRuntimeDebugTrace.WriteLine("HTMLDestroyConverter failed status=" + status);
        }
    }

    private string ReadHtmlConverterText(uint converter)
    {
        uint length = 0;
        Check(Resolve<HTMLGetPropertyDelegate>("HTMLGetProperty")(converter, 0, ref length), "HTMLGetProperty(TEXTLENGTH)");
        if (length == 0) return "";
        if (length > int.MaxValue) throw new XPScriptRuntimeException(5, "Converted HTML is too large.");
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(checked((int)length + 1));
        try
        {
            uint requested = length;
            Check(Resolve<HTMLGetTextDelegate>("HTMLGetText")(converter, 0, ref requested, buffer), "HTMLGetText");
            return FromLmbcs(buffer, checked((int)requested));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    private string InlineHtmlImages(nint db, nint note, string itemName, string html)
    {
        if (html.Length == 0) return html;
        var escaped = System.Text.RegularExpressions.Regex.Escape(itemName);
        var pattern = "(?<url>[^\\\"']*/" + escaped + "/(?<item>[0-9]+)\\.(?<offset>[0-9A-Fa-f]+)\\?OpenElement)";
        return System.Text.RegularExpressions.Regex.Replace(html, pattern, match =>
        {
            if (!uint.TryParse(match.Groups["item"].Value, out var itemIndex) ||
                !uint.TryParse(match.Groups["offset"].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var offset))
                return match.Value;
            var bytes = ConvertHtmlElement(db, note, itemName, itemIndex, offset);
            if (bytes.Length == 0) return match.Value;
            return "data:" + DetectImageMimeType(bytes) + ";base64," + Convert.ToBase64String(bytes);
        }, System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private byte[] ConvertHtmlElement(nint db, nint note, string itemName, uint itemIndex, uint offset)
    {
        using var nativeStandardOutput = XPScriptRuntimeDebugTrace.SuppressNativeStandardOutputUnlessDetailed();
        Check(Resolve<HTMLCreateConverterDelegate>("HTMLCreateConverter")(out var converter), "HTMLCreateConverter(element)");
        try
        {
            using var name = ToLmbcs(itemName);
            Check(Resolve<HTMLConvertElementDelegate>("HTMLConvertElement")(converter, db, note, name.Pointer, itemIndex, offset), "HTMLConvertElement");
            uint length = 0;
            Check(Resolve<HTMLGetPropertyDelegate>("HTMLGetProperty")(converter, 0, ref length), "HTMLGetProperty(element length)");
            if (length == 0) return [];
            if (length > int.MaxValue) throw new XPScriptRuntimeException(5, "Converted rich-text image is too large.");
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(checked((int)length));
            try
            {
                uint requested = length;
                Check(Resolve<HTMLGetTextDelegate>("HTMLGetText")(converter, 0, ref requested, buffer), "HTMLGetText(element)");
                var bytes = new byte[checked((int)requested)];
                System.Runtime.InteropServices.Marshal.Copy(buffer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        }
        finally { _ = Resolve<HTMLDestroyConverterDelegate>("HTMLDestroyConverter")(converter); }
    }

    private static string DetectImageMimeType(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47) return "image/png";
        if (data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return "image/jpeg";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return "image/gif";
        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4d) return "image/bmp";
        return "application/octet-stream";
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLCreateConverterDelegate(out uint converter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLDestroyConverterDelegate(uint converter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLConvertItemDelegate(uint converter, nint db, nint note, nint itemName);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLConvertElementDelegate(uint converter, nint db, nint note, nint itemName, uint itemIndex, uint offset);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLGetPropertyDelegate(uint converter, uint property, ref uint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort HTMLGetTextDelegate(uint converter, uint startingOffset, ref uint textLength, nint text);
}
""";
}