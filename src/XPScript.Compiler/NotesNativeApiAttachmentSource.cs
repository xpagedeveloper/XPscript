namespace XPScript.Compiler;

internal static class NotesNativeApiAttachmentSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const string AttachmentItemName = "$FILE";
    private const ushort ObjectFile = 0x0000;
    private const int FileObjectFixedSize = 36;

    internal bool SaveAttachment(nint note, string attachmentName, string outputPath)
    {
        EnsureInitialized();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (attachmentName.Length == 0 || outputPath.Length == 0) return false;
        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar) || outputPath.EndsWith(Path.AltDirectorySeparatorChar))
            outputPath = Path.Combine(outputPath, attachmentName);

        using var path = ToLmbcs(outputPath);
        Check(Resolve<NSFNoteExtractFileDelegate>("NSFNoteExtractFile")(note, itemBlock, path.Pointer, 0), "NSFNoteExtractFile");
        return true;
    }

    internal bool SaveRichTextAttachment(nint note, string richTextItemName, string attachmentName, string outputPath)
    {
        if (!TryGetFirstItemInfo(note, richTextItemName, out var richTextInfo) || richTextInfo.DataType != NotesTypeComposite)
            return false;
        if (!CompositeReferencesAttachment(richTextInfo, attachmentName)) return false;
        return SaveAttachment(note, attachmentName, outputPath);
    }

    private bool TryFindAttachment(nint note, string attachmentName, out XPScriptNotesBlockId itemBlock)
    {
        itemBlock = default;
        if (!HasItem(note, AttachmentItemName)) return false;
        using var fileName = ToLmbcs(AttachmentItemName);

        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, fileName.Pointer, checked((ushort)fileName.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeObject && TryReadAttachmentName(valueBlock, valueLength, out var storedName) &&
                string.Equals(storedName, attachmentName, StringComparison.OrdinalIgnoreCase))
            {
                itemBlock = currentItem;
                return true;
            }

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, currentItem, fileName.Pointer, checked((ushort)fileName.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryReadAttachmentName(XPScriptNotesBlockId valueBlock, uint valueLength, out string name)
    {
        name = "";
        if (valueBlock.Pool == 0 || valueLength < 2 + FileObjectFixedSize) return false;
        var basePointer = Resolve<OSLockObjectDelegate>("OSLockObject")(valueBlock.Pool);
        if (basePointer == 0) return false;
        try
        {
            var value = nint.Add(basePointer, valueBlock.Block);
            var type = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(value));
            if (type != NotesTypeObject) return false;
            var objectPointer = nint.Add(value, 2);
            var objectType = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(objectPointer, 0));
            if (objectType != ObjectFile) return false;
            var fileNameLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(objectPointer, 6));
            if (fileNameLength == 0 || 2u + FileObjectFixedSize + fileNameLength > valueLength) return false;
            name = FromLmbcs(nint.Add(objectPointer, FileObjectFixedSize), fileNameLength);
            return name.Length > 0;
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(valueBlock.Pool); }
    }

    private bool CompositeReferencesAttachment(XPScriptNotesItemInfo info, string attachmentName)
    {
        if (info.ValueBlock.Pool == 0 || info.ValueLength <= 2) return false;
        using var expected = ToLmbcs(attachmentName);
        if (expected.Length == 0) return false;
        var needle = new byte[expected.Length];
        System.Runtime.InteropServices.Marshal.Copy(expected.Pointer, needle, 0, needle.Length);

        var basePointer = Resolve<OSLockObjectDelegate>("OSLockObject")(info.ValueBlock.Pool);
        if (basePointer == 0) return false;
        try
        {
            var length = checked((int)info.ValueLength - 2);
            var data = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(nint.Add(basePointer, checked(info.ValueBlock.Block + 2)), data, 0, length);
            return IndexOfIgnoreAsciiCase(data, needle) >= 0;
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(info.ValueBlock.Pool); }
    }

    private static int IndexOfIgnoreAsciiCase(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return -1;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                var a = haystack[i + j];
                var b = needle[j];
                if (a >= (byte)'A' && a <= (byte)'Z') a = (byte)(a + 32);
                if (b >= (byte)'A' && b <= (byte)'Z') b = (byte)(b + 32);
                if (a != b) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemInfoNextDelegate(nint note, XPScriptNotesBlockId nextItem, nint itemName, ushort nameLength, out XPScriptNotesBlockId itemBlock, out ushort dataType, out XPScriptNotesBlockId valueBlock, out uint valueLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFNoteExtractFileDelegate(nint note, XPScriptNotesBlockId itemBlock, nint fileName, nint decryptionKey);
}
""";
}
