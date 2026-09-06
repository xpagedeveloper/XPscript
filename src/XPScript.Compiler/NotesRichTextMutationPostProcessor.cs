namespace XPScript.Compiler;

internal static class NotesRichTextMutationPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            """
    internal IReadOnlyList<XPScriptNotesRichTextRecordData> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.GetRichTextRecords(Document.NativeHandle, ItemName);
    }
""",
            """
    internal IReadOnlyList<XPScriptNotesRichTextRecordData> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.GetRichTextRecords(Document.NativeHandle, ItemName);
    }

    internal void RewriteRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Document.NativeHandle, ItemName, original, records);
        _richTextRevision++;
    }
""", "item-rewrite-entrypoint");

        source = ReplaceRequired(source,
            """
    public void Reset()
    {
        EnsureRangeAlive();
        ResetCore();
    }
""",
            """
    public void Reset()
    {
        EnsureRangeAlive();
        ResetCore();
    }

    public void SetStyle(object? styleValue)
    {
        EnsureRangeAlive();
        if (styleValue is not XPScriptNotesRichTextStyle style)
            throw new XPScriptRuntimeException(13, "NotesRichTextRange.SetStyle requires a NotesRichTextStyle.");
        var state = style.ExportState();
        var records = _item.ReadRichTextRecords();
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.RecordIndex < _beginRecord || record.RecordIndex > _endRecord || record.Signature != 133 || record.Data.Length < 8)
                return XPScriptNotesRichTextRecordRewrite.Preserve(record);
            var data = (byte[])record.Data.Clone();
            ApplyFontStyle(data, state);
            return new XPScriptNotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        _item.RewriteRichTextRecords(rewritten);
    }

    public void Remove()
    {
        EnsureRangeAlive();
        var records = _item.ReadRichTextRecords();
        if (records.Count == 0 || _endRecord < _beginRecord) return;
        if (_beginOffset != 0 || (_endRecord >= 0 && _endRecord < records.Count && _endOffset != records[_endRecord].Text.Length))
            throw new XPScriptRuntimeException(445, "NotesRichTextRange.Remove currently requires range boundaries on complete rich-text elements.");
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
            record.RecordIndex >= _beginRecord && record.RecordIndex <= _endRecord ? null : XPScriptNotesRichTextRecordRewrite.Preserve(record));
        _item.RewriteRichTextRecords(rewritten);
        ResetCore();
    }

    private static void ApplyFontStyle(byte[] data, XPScriptNotesRichTextStyleState style)
    {
        if (style.NotesFont != XPScriptNotesRichTextStyle.StyleNoChange) data[4] = checked((byte)style.NotesFont);
        var attrib = data[5];
        attrib = ApplyFlag(attrib, 0x01, style.Bold);
        attrib = ApplyFlag(attrib, 0x02, style.Italic);
        attrib = ApplyFlag(attrib, 0x04, style.Underline);
        attrib = ApplyFlag(attrib, 0x08, style.Strikethrough);
        if (style.Effects != XPScriptNotesRichTextStyle.StyleNoChange)
        {
            attrib = (byte)(attrib & 0x0f);
            attrib |= style.Effects switch { 1 => (byte)0x10, 2 => (byte)0x20, 3 => (byte)0x80, 4 => (byte)0x90, 5 => (byte)0xa0, _ => (byte)0 };
        }
        data[5] = attrib;
        if (style.NotesColor != XPScriptNotesRichTextStyle.StyleNoChange) data[6] = checked((byte)style.NotesColor);
        if (style.FontSize != XPScriptNotesRichTextStyle.StyleNoChange) data[7] = checked((byte)style.FontSize);
    }

    private static byte ApplyFlag(byte value, byte mask, int setting)
    {
        if (setting == XPScriptNotesRichTextStyle.StyleNoChange) return value;
        return setting != 0 ? (byte)(value | mask) : (byte)(value & ~mask);
    }
""", "range-write-methods");

        source = ReplaceRequired(source,
            """
    public void Remove() => throw UnsupportedWrite("NotesRichTextSection.Remove");
    public void SetBarColor(object? colorValue) => throw UnsupportedWrite("NotesRichTextSection.SetBarColor");
    public void SetTitleStyle(object? styleValue) => throw UnsupportedWrite("NotesRichTextSection.SetTitleStyle");
""",
            """
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var index = CurrentFlatIndex(records);
        var span = RichTextItem.ResolveStructuralSpan(index);
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
            record.RecordIndex >= span.Start && record.RecordIndex <= span.End ? null : XPScriptNotesRichTextRecordRewrite.Preserve(record));
        RichTextItem.RewriteRichTextRecords(rewritten);
    }

    public void SetBarColor(object? colorValue)
    {
        EnsureLinkedAlive();
        if (colorValue is not XPScriptNotesColorObject color)
            throw new XPScriptRuntimeException(13, "NotesRichTextSection.SetBarColor requires a NotesColorObject.");
        RewriteHeader(data =>
        {
            if (data.Length < 12) throw new XPScriptRuntimeException(91, "Invalid rich-text section header.");
            var flags = ReadUInt32(data, 4) | BarHasColor;
            WriteUInt32(data, 4, flags);
            var value = checked((ushort)color.NotesColor);
            if (data.Length < 14) Array.Resize(ref data, 14);
            WriteUInt16(data, 12, value);
            return data;
        });
    }

    public void SetTitleStyle(object? styleValue)
    {
        EnsureLinkedAlive();
        if (styleValue is not XPScriptNotesRichTextStyle style)
            throw new XPScriptRuntimeException(13, "NotesRichTextSection.SetTitleStyle requires a NotesRichTextStyle.");
        var state = style.ExportState();
        RewriteHeader(data =>
        {
            if (data.Length < 12) throw new XPScriptRuntimeException(91, "Invalid rich-text section header.");
            ApplySectionFontStyle(data, state);
            return data;
        });
    }

    private void RewriteHeader(Func<byte[], byte[]> transform)
    {
        var current = CurrentRecord();
        var records = Records();
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.SegmentIndex != current.SegmentIndex || record.RecordIndex != current.RecordIndex)
                return XPScriptNotesRichTextRecordRewrite.Preserve(record);
            var data = transform((byte[])record.Data.Clone());
            FixRecordLength(data);
            return new XPScriptNotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
    }

    private static void ApplySectionFontStyle(byte[] data, XPScriptNotesRichTextStyleState style)
    {
        if (style.NotesFont != XPScriptNotesRichTextStyle.StyleNoChange) data[8] = checked((byte)style.NotesFont);
        var attrib = data[9];
        attrib = ApplySectionFlag(attrib, 0x01, style.Bold);
        attrib = ApplySectionFlag(attrib, 0x02, style.Italic);
        attrib = ApplySectionFlag(attrib, 0x04, style.Underline);
        attrib = ApplySectionFlag(attrib, 0x08, style.Strikethrough);
        if (style.Effects != XPScriptNotesRichTextStyle.StyleNoChange)
        {
            attrib = (byte)(attrib & 0x0f);
            attrib |= style.Effects switch { 1 => (byte)0x10, 2 => (byte)0x20, 3 => (byte)0x80, 4 => (byte)0x90, 5 => (byte)0xa0, _ => (byte)0 };
        }
        data[9] = attrib;
        if (style.NotesColor != XPScriptNotesRichTextStyle.StyleNoChange) data[10] = checked((byte)style.NotesColor);
        if (style.FontSize != XPScriptNotesRichTextStyle.StyleNoChange) data[11] = checked((byte)style.FontSize);
    }

    private static byte ApplySectionFlag(byte value, byte mask, int setting)
    {
        if (setting == XPScriptNotesRichTextStyle.StyleNoChange) return value;
        return setting != 0 ? (byte)(value | mask) : (byte)(value & ~mask);
    }

    private static void WriteUInt16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8);
    }
    private static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24);
    }
    private static void FixRecordLength(byte[] data)
    {
        if (data.Length < 2) return;
        if (data[1] == 0xff && data.Length >= 4) WriteUInt16(data, 2, checked((ushort)data.Length));
        else if (data[1] == 0 && data.Length >= 6)
        {
            var length = checked((uint)data.Length);
            data[2] = (byte)length; data[3] = (byte)(length >> 8); data[4] = (byte)(length >> 16); data[5] = (byte)(length >> 24);
        }
        else data[1] = checked((byte)data.Length);
    }
""", "section-write-methods");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void ReplaceRichTextCdRecords(nint note, string itemName,
        IReadOnlyList<XPScriptNotesRichTextRecordData> original,
        IReadOnlyList<XPScriptNotesRichTextRecordRewrite> replacement)
    {
        EnsureInitialized();
        XPScriptNotesRichTextCdTransform.ValidateForPersistence(replacement);
        var backup = XPScriptNotesRichTextCdTransform.Preserve(original);
        var segmentCount = original.Count == 0 ? 0 : original.Max(r => r.SegmentIndex) + 1;
        DeleteCompositeItems(note, itemName, segmentCount);
        try
        {
            WriteCompositeRecords(note, itemName, replacement);
        }
        catch
        {
            try
            {
                var replacementSegments = replacement.Count == 0 ? 0 : replacement.Max(r => r.SegmentIndex) + 1;
                DeleteCompositeItems(note, itemName, replacementSegments);
                WriteCompositeRecords(note, itemName, backup);
            }
            catch { }
            throw;
        }
    }

    private void DeleteCompositeItems(nint note, string itemName, int count)
    {
        if (count <= 0) return;
        using var name = ToLmbcs(itemName);
        var length = checked((ushort)System.Text.Encoding.UTF8.GetByteCount(itemName));
        var delete = Resolve<NSFItemDeleteForRichTextRewriteDelegate>("NSFItemDelete");
        for (var i = 0; i < count; i++) Check(delete(note, name.Pointer, length), "NSFItemDelete");
    }

    private void WriteCompositeRecords(nint note, string itemName, IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        using var name = ToLmbcs(itemName);
        Check(Resolve<CompoundTextCreateForMutationDelegate>("CompoundTextCreate")(checked((uint)note), name.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            foreach (var segment in XPScriptNotesRichTextCdTransform.GroupCanonicalSegments(records))
            {
                if (segment.Length == 0) continue;
                var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(segment.Length);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(segment, 0, buffer, segment.Length);
                    Check(Resolve<CompoundTextAddCDRecordsForMutationDelegate>("CompoundTextAddCDRecords")(compound, buffer, checked((uint)segment.Length)), "CompoundTextAddCDRecords");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
            }
            Check(Resolve<CompoundTextCloseForMutationDelegate>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (!closed) try { Resolve<CompoundTextDiscardForMutationDelegate>("CompoundTextDiscard")(compound); } catch { }
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFItemDeleteForRichTextRewriteDelegate(nint note, nint itemName, ushort nameLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCreateForMutationDelegate(uint note, nint itemName, out uint compound);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddCDRecordsForMutationDelegate(uint compound, nint records, uint recordLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCloseForMutationDelegate(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void CompoundTextDiscardForMutationDelegate(uint compound);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text mutation patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
