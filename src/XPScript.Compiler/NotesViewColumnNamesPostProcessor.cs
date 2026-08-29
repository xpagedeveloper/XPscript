namespace XPScript.Compiler;

internal static class NotesViewColumnNamesPostProcessor
{
    public static string Apply(string source) => ApplyCore(source, "nint", "nint");
    public static string ApplyBuiltSurface(string source) => ApplyCore(source, "ushort", "uint");

    private static string ApplyCore(string source, string viewHandleType, string databaseHandleType)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            $"    internal {viewHandleType} NativeHandle {{ get {{ EnsureAlive(); return _handle; }} }}\n    public string Name {{ get; }}",
            $"    internal {viewHandleType} NativeHandle {{ get {{ EnsureAlive(); return _handle; }} }}\n    public string Name {{ get; }}\n    public string[] ColumnNames {{ get {{ EnsureAlive(); return Session.Api.GetViewColumnNames(Database.Handle, Name); }} }}",
            "view-column-names-property");

        var accessMethod = $"    internal int GetDatabaseCurrentAccessLevel({databaseHandleType} db)\n    {{\n        EnsureInitialized();\n        Resolve<NSFDbAccessGetDelegate>(\"NSFDbAccessGet\")(db, out var level, out _);\n        return level;\n    }}";
        var nativeMethods = accessMethod + $"\n\n    internal string[] GetViewColumnNames({databaseHandleType} db, string viewName)\n    {{\n        EnsureInitialized();\n        using var name = ToLmbcs(viewName);\n        Check(Resolve<NIFFindDesignNoteDelegate>(\"NIFFindDesignNote\")(db, name.Pointer, 0x0008, out var viewNoteId), \"NIFFindDesignNote(view)\");\n        var note = OpenNote(db, viewNoteId);\n        try\n        {{\n            var info = GetFirstItemInfo(note, \"$VIEWFORMAT\");\n            if (info.DataType != 0x0005)\n                throw new XPScriptRuntimeException(13, \"NotesView $VIEWFORMAT has an unexpected data type.\");\n            return ParseViewColumnNames(CopyItemValueWithoutType(info));\n        }}\n        finally {{ CloseNote(note); }}\n    }}\n\n    private string[] ParseViewColumnNames(byte[] data)\n    {{\n        const int tableFormatSize = 10;\n        const int columnFormatSize = 32;\n        const ushort columnSignature = 17238;\n        if (data.Length < tableFormatSize) throw new XPScriptRuntimeException(5, \"Invalid Notes view format data.\");\n        var count = ReadCanonicalUInt16(data, 2);\n        if (count == 0) return Array.Empty<string>();\n        var descriptorBytes = checked(count * columnFormatSize);\n        if (data.Length < tableFormatSize + descriptorBytes) throw new XPScriptRuntimeException(5, \"Truncated Notes view column format data.\");\n        var itemNameSizes = new ushort[count];\n        var titleSizes = new ushort[count];\n        var formulaSizes = new ushort[count];\n        var constantSizes = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            var offset = tableFormatSize + i * columnFormatSize;
            if (ReadCanonicalUInt16(data, offset) != columnSignature) throw new XPScriptRuntimeException(5, "Invalid Notes view column format signature.");
            itemNameSizes[i] = ReadCanonicalUInt16(data, offset + 4);
            titleSizes[i] = ReadCanonicalUInt16(data, offset + 6);
            formulaSizes[i] = ReadCanonicalUInt16(data, offset + 8);
            constantSizes[i] = ReadCanonicalUInt16(data, offset + 10);
        }
        var cursor = tableFormatSize + descriptorBytes;
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            var itemNameSize = itemNameSizes[i];
            var packedSize = checked((int)itemNameSize + titleSizes[i] + formulaSizes[i] + constantSizes[i]);
            if (cursor + packedSize > data.Length) throw new XPScriptRuntimeException(5, "Truncated Notes view column name data.");
            result[i] = DecodeLmbcs(data, cursor, itemNameSize);
            cursor += packedSize;
        }
        return result;
    }

    private string DecodeLmbcs(byte[] data, int offset, int length)
    {
        if (length == 0) return "";
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, pointer, length);
            return FromLmbcs(pointer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private static ushort ReadCanonicalUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));";

        source = ReplaceRequired(source, accessMethod, nativeMethods, "native-view-column-names");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView ColumnNames surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
