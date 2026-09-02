namespace XPScript.Compiler;

internal static class NotesViewColumnNamesPostProcessor
{
    public static string Apply(string source) => ApplyCore(source, "nint", "nint");
    public static string ApplyBuiltSurface(string source) => ApplyCore(source, "ushort", "uint");

    private static string ApplyCore(string source, string viewHandleType, string databaseHandleType)
    {
        ArgumentNullException.ThrowIfNull(source);

        var viewSurface = $$"""
    internal {{viewHandleType}} NativeHandle { get { EnsureAlive(); return _handle; } }
    public string Name { get; }
    public LSArray ColumnNames
    {
        get
        {
            EnsureAlive();
            var columns = Session.Api.GetViewColumns(Database.Handle, Name);
            if (columns.Length == 0) return new LSArray("String", true);
            var result = new LSArray("String", true, [0], [columns.Length - 1]);
            for (var i = 0; i < columns.Length; i++) result.Set(columns[i].ItemName, i);
            return result;
        }
    }
    public XPScriptNotesViewColumn[] Columns
    {
        get
        {
            EnsureAlive();
            var columns = Session.Api.GetViewColumns(Database.Handle, Name);
            var result = new XPScriptNotesViewColumn[columns.Length];
            for (var i = 0; i < columns.Length; i++) result[i] = new XPScriptNotesViewColumn(Session, this, columns[i]);
            return result;
        }
    }
""";

        source = ReplaceRequired(
            source,
            $"    internal {viewHandleType} NativeHandle {{ get {{ EnsureAlive(); return _handle; }} }}\n    public string Name {{ get; }}",
            viewSurface,
            "view-columns-property");

        const string viewClassAnchor = "internal sealed class XPScriptNotesView : XPScriptNotesOwnedObject";
        source = ReplaceRequired(source, viewClassAnchor, ColumnRuntime + "\n\n" + viewClassAnchor, "view-column-runtime");

        var accessMethod = $$"""
    internal int GetDatabaseCurrentAccessLevel({{databaseHandleType}} db)
    {
        EnsureInitialized();
        Resolve<NSFDbAccessGetDelegate>("NSFDbAccessGet")(db, out var level, out _);
        return level;
    }
""";

        var nativeMethods = accessMethod + $$"""

    internal XPScriptNotesViewColumnData[] GetViewColumns({{databaseHandleType}} db, string viewName)
    {
        EnsureInitialized();
        using var name = ToLmbcs(viewName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, name.Pointer, 0x0008, out var viewNoteId), "NIFFindDesignNote(view)");
        var note = OpenNote(db, viewNoteId);
        try
        {
            var info = GetFirstItemInfo(note, "$VIEWFORMAT");
            if (info.DataType != 0x0005)
                throw new XPScriptRuntimeException(13, "NotesView $VIEWFORMAT has an unexpected data type.");
            return ParseViewColumns(CopyItemValueWithoutType(info));
        }
        finally { CloseNote(note); }
    }

    internal string[] GetViewColumnNames({{databaseHandleType}} db, string viewName) =>
        GetViewColumns(db, viewName).Select(column => column.ItemName).ToArray();

    private XPScriptNotesViewColumnData[] ParseViewColumns(byte[] data)
    {
        const int tableFormatSize = 10;
        const int columnFormatSize = 32;
        const ushort columnSignature = 17238;
        if (data.Length < tableFormatSize) throw new XPScriptRuntimeException(5, "Invalid Notes view format data.");
        var count = ReadCanonicalUInt16(data, 2);
        if (count == 0) return Array.Empty<XPScriptNotesViewColumnData>();
        var descriptorBytes = checked(count * columnFormatSize);
        if (data.Length < tableFormatSize + descriptorBytes) throw new XPScriptRuntimeException(5, "Truncated Notes view column format data.");

        var descriptors = new XPScriptNotesViewColumnDescriptor[count];
        for (var i = 0; i < count; i++)
        {
            var offset = tableFormatSize + i * columnFormatSize;
            if (ReadCanonicalUInt16(data, offset) != columnSignature)
                throw new XPScriptRuntimeException(5, "Invalid Notes view column format signature.");

            descriptors[i] = new XPScriptNotesViewColumnDescriptor(
                ReadCanonicalUInt16(data, offset + 2),
                ReadCanonicalUInt16(data, offset + 4),
                ReadCanonicalUInt16(data, offset + 6),
                ReadCanonicalUInt16(data, offset + 8),
                ReadCanonicalUInt16(data, offset + 10),
                ReadCanonicalUInt16(data, offset + 12),
                data[offset + 14], data[offset + 15], data[offset + 16], data[offset + 17],
                ReadCanonicalUInt16(data, offset + 18),
                data[offset + 20], data[offset + 21], data[offset + 22],
                data[offset + 24], data[offset + 25], data[offset + 26], data[offset + 27],
                ReadCanonicalUInt16(data, offset + 28),
                ReadCanonicalUInt16(data, offset + 30));
        }

        var cursor = tableFormatSize + descriptorBytes;
        var result = new XPScriptNotesViewColumnData[count];
        for (var i = 0; i < count; i++)
        {
            var d = descriptors[i];
            var packedSize = checked((int)d.ItemNameSize + d.TitleSize + d.FormulaSize + d.ConstantValueSize);
            if (cursor + packedSize > data.Length) throw new XPScriptRuntimeException(5, "Truncated Notes view column data.");

            var itemName = DecodeLmbcs(data, cursor, d.ItemNameSize);
            cursor += d.ItemNameSize;
            var title = DecodeLmbcs(data, cursor, d.TitleSize);
            cursor += d.TitleSize;
            var formula = d.FormulaSize == 0 ? "" : DecompileViewColumnFormula(data, cursor, d.FormulaSize);
            cursor += d.FormulaSize;
            cursor += d.ConstantValueSize;

            result[i] = new XPScriptNotesViewColumnData(
                i + 1, itemName, title, formula, d.Flags1, d.DisplayWidth,
                d.FontFace, d.FontAttributes, d.FontColor, d.FontPointSize,
                d.Flags2, d.NumberDigits, d.NumberFormat, d.NumberAttributes,
                d.DateFormat, d.TimeFormat, d.TimeZoneFormat, d.TimeDateFormat,
                d.FormatDataType, d.ListSep);
        }
        return result;
    }

    private string DecompileViewColumnFormula(byte[] data, int offset, int length)
    {
        if (length == 0) return "";
        var formula = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, formula, length);
            Check(Resolve<NSFFormulaDecompileDelegate>("NSFFormulaDecompile")(formula, 0, out var textHandle, out var textLength), "NSFFormulaDecompile(view column)");
            if (textHandle == 0 || textLength == 0)
            {
                if (textHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle);
                return "";
            }
            try
            {
                var text = Resolve<OSLockObjectDelegate>("OSLockObject")(textHandle);
                if (text == 0) throw new XPScriptRuntimeException(5, "Unable to lock decompiled view column formula.");
                try { return FromLmbcs(text, textLength); }
                finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(textHandle); }
            }
            finally { Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle); }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(formula); }
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

    private static ushort ReadCanonicalUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
""";

        source = ReplaceRequired(source, accessMethod, nativeMethods, "native-view-columns");

        const string findDelegate = "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindDesignNoteDelegate";
        source = ReplaceRequired(
            source,
            findDelegate,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFFormulaDecompileDelegate(nint formula, int selectionFormula, out nint formulaText, out ushort formulaTextLength);\n" + findDelegate,
            "formula-decompile-delegate");

        if (!source.Contains("public XPScriptNotesViewColumn[] Columns", StringComparison.Ordinal))
            throw new CompilerException("NotesView.Columns must expose NotesViewColumn objects.");

        return source;
    }

    private const string ColumnRuntime = """
internal sealed record XPScriptNotesViewColumnDescriptor(
    ushort Flags1, ushort ItemNameSize, ushort TitleSize, ushort FormulaSize, ushort ConstantValueSize,
    ushort DisplayWidth, byte FontFace, byte FontAttributes, byte FontColor, byte FontPointSize,
    ushort Flags2, byte NumberDigits, byte NumberFormat, byte NumberAttributes,
    byte DateFormat, byte TimeFormat, byte TimeZoneFormat, byte TimeDateFormat,
    ushort FormatDataType, ushort ListSep);

internal sealed record XPScriptNotesViewColumnData(
    int Position, string ItemName, string Title, string Formula, ushort Flags1, ushort DisplayWidth,
    byte FontFace, byte FontAttributes, byte FontColor, byte FontPointSize,
    ushort Flags2, byte NumberDigits, byte NumberFormat, byte NumberAttributes,
    byte DateFormat, byte TimeFormat, byte TimeZoneFormat, byte TimeDateFormat,
    ushort FormatDataType, ushort ListSep);

internal sealed class XPScriptNotesViewColumn : XPScriptNotesObject
{
    private readonly XPScriptNotesView _parent;
    private readonly XPScriptNotesViewColumnData _data;

    internal XPScriptNotesViewColumn(XPScriptNotesSession session, XPScriptNotesView parent, XPScriptNotesViewColumnData data) : base(session)
    {
        _parent = parent;
        _data = data;
    }

    public XPScriptNotesView Parent { get { EnsureAlive(); return _parent; } }
    public int Position { get { EnsureAlive(); return _data.Position; } }
    public string ItemName { get { EnsureAlive(); return _data.ItemName; } }
    public string Title { get { EnsureAlive(); return _data.Title; } }
    public string Formula { get { EnsureAlive(); return _data.Formula; } }
    public int Width { get { EnsureAlive(); return _data.DisplayWidth; } }

    public int FontFace { get { EnsureAlive(); return _data.FontFace; } }
    public int FontColor { get { EnsureAlive(); return _data.FontColor; } }
    public int FontPointSize { get { EnsureAlive(); return _data.FontPointSize; } }
    public int FontStyle { get { EnsureAlive(); return _data.FontAttributes; } }
    public bool IsFontBold { get { EnsureAlive(); return (_data.FontAttributes & 0x01) != 0; } }
    public bool IsFontItalic { get { EnsureAlive(); return (_data.FontAttributes & 0x02) != 0; } }
    public bool IsFontUnderline { get { EnsureAlive(); return (_data.FontAttributes & 0x04) != 0; } }
    public bool IsFontStrikethrough { get { EnsureAlive(); return (_data.FontAttributes & 0x08) != 0; } }

    public int NumberDigits { get { EnsureAlive(); return _data.NumberDigits; } }
    public int NumberFormat { get { EnsureAlive(); return _data.NumberFormat; } }
    public int NumberAttrib { get { EnsureAlive(); return _data.NumberAttributes; } }
    public bool IsNumberAttribPunctuated { get { EnsureAlive(); return (_data.NumberAttributes & 0x01) != 0; } }
    public bool IsNumberAttribPercent { get { EnsureAlive(); return (_data.NumberAttributes & 0x02) != 0; } }
    public bool IsNumberAttribParens { get { EnsureAlive(); return (_data.NumberAttributes & 0x04) != 0; } }

    public int DateFmt { get { EnsureAlive(); return _data.DateFormat; } }
    public int TimeFmt { get { EnsureAlive(); return _data.TimeFormat; } }
    public int TimeZoneFmt { get { EnsureAlive(); return _data.TimeZoneFormat; } }
    public int TimeDateFmt { get { EnsureAlive(); return _data.TimeDateFormat; } }
    public int ListSep { get { EnsureAlive(); return _data.ListSep; } }

    public bool IsSorted { get { EnsureAlive(); return HasFlag(0); } }
    public bool IsCategory { get { EnsureAlive(); return HasFlag(1); } }
    public bool IsSortDescending { get { EnsureAlive(); return HasFlag(2); } }
    public bool IsHidden { get { EnsureAlive(); return HasFlag(3); } }
    public bool IsResponse { get { EnsureAlive(); return HasFlag(4); } }
    public bool IsHideDetail { get { EnsureAlive(); return HasFlag(5); } }
    public bool IsIcon { get { EnsureAlive(); return HasFlag(6); } }
    public bool IsResize { get { EnsureAlive(); return !HasFlag(7); } }
    public bool IsResortAscending { get { EnsureAlive(); return HasFlag(8); } }
    public bool IsResortDescending { get { EnsureAlive(); return HasFlag(9); } }
    public bool IsShowTwistie { get { EnsureAlive(); return HasFlag(10); } }
    public bool IsResortToView { get { EnsureAlive(); return HasFlag(11); } }
    public bool IsSecondaryResort { get { EnsureAlive(); return HasFlag(12); } }
    public bool IsSecondaryResortDescending { get { EnsureAlive(); return HasFlag(13); } }
    public bool IsCaseSensitiveSort { get { EnsureAlive(); return !HasFlag(14); } }
    public bool IsAccentSensitiveSort { get { EnsureAlive(); return !HasFlag(15); } }

    public bool IsField
    {
        get
        {
            EnsureAlive();
            return _data.Formula.Length == 0 || string.Equals(_data.Formula.Trim(), _data.ItemName, StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool IsFormula { get { EnsureAlive(); return !IsField; } }
    public bool IsValidDominoQueryColumn { get { EnsureAlive(); return _data.ItemName.Length > 0; } }
    public bool IsValidDominoQueryField { get { EnsureAlive(); return IsField && _data.ItemName.Length > 0; } }

    private bool HasFlag(int bit) => (_data.Flags1 & (1 << bit)) != 0;
    protected override void ReleaseNative() { }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView column surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
