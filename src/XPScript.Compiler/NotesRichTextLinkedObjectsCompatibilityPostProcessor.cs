namespace XPScript.Compiler;

internal static class NotesRichTextLinkedObjectsCompatibilityPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Older linked-object sources used LSArray.Create here. Newer sources either
        // use the runtime array helper directly or remove RowLabels during the rich-text
        // surface audit, so this compatibility normalization must be optional.
        const string legacyRowLabels = "return LSArray.Create(0, -1, labels);";
        const string normalizedRowLabels = "return LSOperatorArrayRuntime.CreateArray(labels);";
        if (source.Contains(legacyRowLabels, StringComparison.Ordinal))
            source = source.Replace(legacyRowLabels, normalizedRowLabels, StringComparison.Ordinal);

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal string DecodeRichTextText(byte[] data, int offset, int length)
    {
        if (length <= 0) return "";
        if (offset < 0 || offset > data.Length || length > data.Length - offset)
            throw new XPScriptRuntimeException(5, "Invalid rich-text text range.");
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, pointer, length);
            return FromLmbcs(pointer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal string DecompileRichTextFormula(byte[] data, int offset, int length)
    {
        if (length <= 0) return "";
        if (offset < 0 || offset > data.Length || length > data.Length - offset)
            throw new XPScriptRuntimeException(5, "Invalid rich-text formula range.");

        var formula = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, formula, length);
            Check(Resolve<NSFFormulaDecompileDelegate>("NSFFormulaDecompile")(
                formula, 0, out var textHandle, out var textLength), "NSFFormulaDecompile(rich text)");
            if (textHandle == 0 || textLength == 0)
            {
                if (textHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle);
                return "";
            }
            try
            {
                var text = Resolve<OSLockObjectDelegate>("OSLockObject")(textHandle);
                if (text == 0) throw new XPScriptRuntimeException(5, "Unable to lock decompiled rich-text formula.");
                try { return FromLmbcs(text, textLength); }
                finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(textHandle); }
            }
            finally { Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle); }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(formula); }
    }
}
""";
}
