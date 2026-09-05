namespace XPScript.Compiler;

internal static class NotesRichTextLinkedObjectsCompatibilityPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "return LSArray.Create(0, -1, labels);",
            "return LSOperatorArrayRuntime.CreateArray(labels);",
            "table-row-label-array");

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

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes linked rich-text compatibility patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
