namespace XPScript.Compiler;

internal static class NotesRichTextCdRewritePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + RuntimeSupport + "\n\n" + NativeRuntime;
    }

    private const string RuntimeSupport = """
internal readonly record struct XPScriptNotesRichTextRecordRewrite(
    int SegmentIndex,
    int RecordIndex,
    ushort Signature,
    byte[] Data)
{
    internal static XPScriptNotesRichTextRecordRewrite Preserve(XPScriptNotesRichTextRecordData record) =>
        new(record.SegmentIndex, record.RecordIndex, record.Signature, (byte[])record.Data.Clone());
}

internal static class XPScriptNotesRichTextCdTransform
{
    internal static IReadOnlyList<XPScriptNotesRichTextRecordRewrite> Preserve(
        IReadOnlyList<XPScriptNotesRichTextRecordData> records) =>
        Transform(records, static record => XPScriptNotesRichTextRecordRewrite.Preserve(record));

    internal static IReadOnlyList<XPScriptNotesRichTextRecordRewrite> Transform(
        IReadOnlyList<XPScriptNotesRichTextRecordData> records,
        Func<XPScriptNotesRichTextRecordData, XPScriptNotesRichTextRecordRewrite?> transform)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(transform);

        var result = new List<XPScriptNotesRichTextRecordRewrite>(records.Count);
        var previousSegment = -1;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < previousSegment ||
                (record.SegmentIndex == previousSegment && record.RecordIndex <= previousRecord))
                throw new XPScriptRuntimeException(91, "Rich text CD records are not in physical item order.");

            var rewritten = transform(record);
            if (rewritten is { } value)
            {
                if (value.SegmentIndex != record.SegmentIndex || value.RecordIndex != record.RecordIndex)
                    throw new XPScriptRuntimeException(5, "A CD transform cannot change physical record identity.");
                ValidateCanonicalRecord(value.Signature, value.Data);
                result.Add(value with { Data = (byte[])value.Data.Clone() });
            }

            previousSegment = record.SegmentIndex;
            previousRecord = record.RecordIndex;
        }
        return result;
    }

    internal static IReadOnlyList<byte[]> GroupCanonicalSegments(
        IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0) return Array.Empty<byte[]>();

        var segments = new List<byte[]>();
        var bytes = new List<byte>();
        var segmentIndex = records[0].SegmentIndex;
        var previousRecord = -1;
        foreach (var record in records)
        {
            ValidateCanonicalRecord(record.Signature, record.Data);
            if (record.SegmentIndex < segmentIndex ||
                (record.SegmentIndex == segmentIndex && record.RecordIndex <= previousRecord))
                throw new XPScriptRuntimeException(91, "Transformed CD records are not in physical item order.");

            if (record.SegmentIndex != segmentIndex)
            {
                segments.Add(bytes.ToArray());
                bytes.Clear();
                segmentIndex = record.SegmentIndex;
                previousRecord = -1;
            }

            bytes.AddRange(record.Data);
            if ((record.Data.Length & 1) != 0) bytes.Add(0);
            previousRecord = record.RecordIndex;
        }
        segments.Add(bytes.ToArray());
        return segments;
    }

    internal static void ValidateForPersistence(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var previousSegment = -1;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < 0 || record.RecordIndex < 0)
                throw new XPScriptRuntimeException(5, "Rich text CD record identity cannot be negative.");
            if (record.SegmentIndex < previousSegment ||
                (record.SegmentIndex == previousSegment && record.RecordIndex <= previousRecord))
                throw new XPScriptRuntimeException(91, "Transformed CD records are not in physical item order.");
            ValidateCanonicalRecord(record.Signature, record.Data);
            previousSegment = record.SegmentIndex;
            previousRecord = record.RecordIndex;
        }
    }

    private static void ValidateCanonicalRecord(ushort signature, byte[]? data)
    {
        if (data is null || data.Length < 2)
            throw new XPScriptRuntimeException(5, "A preserved or replaced CD record must contain canonical record bytes.");
        var encodedSignature = (ushort)(data[0] | (data[1] << 8));
        if (encodedSignature != signature)
            throw new XPScriptRuntimeException(5, "A CD rewrite signature must match the canonical record header.");
    }
}
""";

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void RewriteRichTextCdRecords(
        nint note,
        string itemName,
        IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureInitialized();
        itemName = itemName.Trim();
        if (itemName.Length == 0) throw new XPScriptRuntimeException(5, "Rich text item name cannot be empty.");
        XPScriptNotesRichTextCdTransform.ValidateForPersistence(records);

        // This primitive deliberately remains append/create-only. Destructive callers
        // must not use it until the physical TYPE_COMPOSITE replacement transaction
        // has staging, commit and rollback semantics.
        using var itemNameText = ToLmbcs(itemName);
        Check(Resolve<CompoundTextCreateForRewriteDelegate>("CompoundTextCreate")(
            checked((uint)note), itemNameText.Pointer, out var compound), "CompoundTextCreate");

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
                    Check(Resolve<CompoundTextAddCDRecordsForRewriteDelegate>("CompoundTextAddCDRecords")(
                        compound, buffer, checked((uint)segment.Length)), "CompoundTextAddCDRecords");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
            }

            Check(Resolve<CompoundTextCloseForRewriteDelegate>("CompoundTextClose")(
                compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (!closed)
            {
                try { Resolve<CompoundTextDiscardForRewriteDelegate>("CompoundTextDiscard")(compound); }
                catch { }
            }
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCreateForRewriteDelegate(uint note, nint itemName, out uint compound);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddCDRecordsForRewriteDelegate(uint compound, nint records, uint recordLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCloseForRewriteDelegate(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void CompoundTextDiscardForRewriteDelegate(uint compound);
}
""";
}
