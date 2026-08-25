namespace XPScript.Compiler;

internal static class NotesNativeApiAttachmentSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const string AttachmentItemName = "$FILE";
    private const int FileObjectFixedSize = 36;
    private const ushort HotspotTypeFile = 4;
    private const ushort SigCdHotspotBegin = unchecked((ushort)-87);
    private const ushort SigCdV4HotspotBegin = unchecked((ushort)-83);
    private const ushort SigCdV5HotspotBegin = unchecked((ushort)-130);
    private const ushort SigCdV6HotspotBeginContinuation = unchecked((ushort)-140);

    internal bool SaveAttachment(nint note, string attachmentName, string outputPath, string? richTextItemName)
    {
        EnsureInitialized();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (attachmentName.Length == 0 || outputPath.Length == 0) return false;

        if (richTextItemName is not null && !RichTextReferencesAttachment(note, richTextItemName, attachmentName))
            return false;

        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar) || outputPath.EndsWith(Path.AltDirectorySeparatorChar))
            outputPath = Path.Combine(outputPath, Path.GetFileName(attachmentName));
        else
            outputPath = Path.GetFullPath(outputPath);

        using var path = ToLmbcs(outputPath);
        return Resolve<NSFNoteExtractFileDelegate>("NSFNoteExtractFile")(note, itemBlock, path.Pointer, 0) == 0;
    }

    private bool TryFindAttachment(nint note, string attachmentName, out XPScriptNotesBlockId itemBlock)
    {
        itemBlock = default;
        using var fileItemName = ToLmbcs(AttachmentItemName);

        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, fileItemName.Pointer, checked((ushort)fileItemName.Length),
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
                note, currentItem, fileItemName.Pointer, checked((ushort)fileItemName.Length),
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
            var valuePointer = nint.Add(basePointer, valueBlock.Block);
            var rawLength = checked((int)valueLength - 2);
            var raw = new byte[rawLength];
            System.Runtime.InteropServices.Marshal.Copy(nint.Add(valuePointer, 2), raw, 0, rawLength);
            if (raw.Length < FileObjectFixedSize) return false;

            var fileNameLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(6, 2));
            if (fileNameLength == 0 || FileObjectFixedSize + fileNameLength > raw.Length) return false;

            var namePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(fileNameLength);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(raw, FileObjectFixedSize, namePointer, fileNameLength);
                name = FromLmbcs(namePointer, fileNameLength);
                return name.Length > 0;
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(namePointer); }
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(valueBlock.Pool); }
    }

    private bool RichTextReferencesAttachment(nint note, string itemName, string attachmentName)
    {
        if (!TryGetFirstItemInfo(note, itemName, out var info) || info.DataType != NotesTypeComposite)
            return false;

        var found = false;
        EnumCompositeActionDelegate callback = (record, signature, recordLength, context) =>
        {
            if (found || record == 0 || recordLength < 12 || !IsHotspotBeginSignature(signature)) return 0;

            var hotspotType = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 4));
            if (hotspotType != HotspotTypeFile) return 0;

            var dataLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 10));
            if (dataLength == 0 || dataLength > recordLength - 12) return 0;

            var data = nint.Add(record, 12);
            var internalLength = ZeroTerminatedLength(data, dataLength);
            if (internalLength >= dataLength) return 0;

            var original = nint.Add(data, internalLength + 1);
            var remaining = checked((int)dataLength - internalLength - 1);
            var originalLength = ZeroTerminatedLength(original, remaining);

            var internalName = internalLength == 0 ? "" : FromLmbcs(data, internalLength);
            var originalName = originalLength == 0 ? "" : FromLmbcs(original, originalLength);
            found = string.Equals(originalName, attachmentName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(internalName, attachmentName, StringComparison.OrdinalIgnoreCase);
            return 0;
        };

        var status = Resolve<EnumCompositeBufferDelegate>("EnumCompositeBuffer")(
            info.ValueBlock, info.ValueLength, callback, 0);
        GC.KeepAlive(callback);
        return status == 0 && found;
    }

    private static bool IsHotspotBeginSignature(ushort signature) =>
        signature == SigCdHotspotBegin || signature == SigCdV4HotspotBegin ||
        signature == SigCdV5HotspotBegin || signature == SigCdV6HotspotBeginContinuation;

    private static int ZeroTerminatedLength(nint pointer, int maximum)
    {
        var length = 0;
        while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(pointer, length) != 0) length++;
        return length;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemInfoNextDelegate(nint note, XPScriptNotesBlockId currentItem, nint itemName, ushort nameLength, out XPScriptNotesBlockId itemBlock, out ushort dataType, out XPScriptNotesBlockId valueBlock, out uint valueLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFNoteExtractFileDelegate(nint note, XPScriptNotesBlockId itemBlock, nint fileName, nint decryptionKey);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort EnumCompositeBufferDelegate(XPScriptNotesBlockId itemValue, uint itemValueLength, EnumCompositeActionDelegate action, nint context);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort EnumCompositeActionDelegate(nint record, ushort signature, uint recordLength, nint context);
}
""";
}
