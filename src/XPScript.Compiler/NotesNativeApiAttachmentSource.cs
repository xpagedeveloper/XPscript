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

    internal bool SaveAttachment(nint note, string attachmentName, string outputPath) =>
        SaveAttachmentCore(note, attachmentName, outputPath);

    internal bool SaveRichTextAttachment(nint note, string richTextItemName, string attachmentName, string outputPath)
    {
        EnsureInitialized();
        richTextItemName = richTextItemName.Trim();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (richTextItemName.Length == 0 || attachmentName.Length == 0 || outputPath.Length == 0) return false;
        if (!TryResolveRichTextAttachment(note, richTextItemName, attachmentName, out var internalName)) return false;
        return SaveAttachmentCore(note, internalName, outputPath);
    }

    private bool SaveAttachmentCore(nint note, string attachmentName, string outputPath)
    {
        EnsureInitialized();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (attachmentName.Length == 0 || outputPath.Length == 0) return false;
        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar) || outputPath.EndsWith(Path.AltDirectorySeparatorChar))
            outputPath = Path.Combine(outputPath, Path.GetFileName(attachmentName));
        else
            outputPath = Path.GetFullPath(outputPath);

        using var path = ToLmbcs(outputPath);
        var status = Resolve<NSFNoteExtractFileDelegate>("NSFNoteExtractFile")(note, itemBlock, path.Pointer, 0);
        if (status == 0) return true;
        Check(status, "NSFNoteExtractFile");
        return false;
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

            var objectType = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2));
            if (objectType != 0) return false; // OBJECT_FILE
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

    private bool TryResolveRichTextAttachment(nint note, string itemName, string requestedName, out string internalName)
    {
        internalName = "";
        using var itemNameText = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeComposite && TryResolveAttachmentInComposite(valueBlock, valueLength, requestedName, out internalName))
                return true;

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryResolveAttachmentInComposite(XPScriptNotesBlockId valueBlock, uint valueLength, string requestedName, out string internalName)
    {
        var resolved = "";
        EnumCompositeActionDelegate callback = (record, signature, recordLength, context) =>
        {
            if (resolved.Length > 0 || record == 0 || recordLength < 12 || !IsHotspotBeginSignature(signature)) return 0;

            // HOTSPOTBEGIN records use WSIG: Header(4), Type(2), Flags(4), DataLength(2).
            var hotspotType = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 4));
            if (hotspotType != HotspotTypeFile) return 0;

            var dataLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 10));
            if (dataLength == 0 || dataLength > recordLength - 12) return 0;

            var data = nint.Add(record, 12);
            var firstLength = ZeroTerminatedLength(data, dataLength);
            if (firstLength >= dataLength) return 0;

            var second = nint.Add(data, firstLength + 1);
            var remaining = checked((int)dataLength - firstLength - 1);
            var secondLength = ZeroTerminatedLength(second, remaining);

            var firstName = firstLength == 0 ? "" : FromLmbcs(data, firstLength);
            var secondName = secondLength == 0 ? "" : FromLmbcs(second, secondLength);
            if (string.Equals(firstName, requestedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(secondName, requestedName, StringComparison.OrdinalIgnoreCase))
                resolved = firstName.Length > 0 ? firstName : requestedName;
            return 0;
        };

        _ = Resolve<EnumCompositeBufferDelegate>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
        internalName = resolved;
        return resolved.Length > 0;
    }

    private static bool IsHotspotBeginSignature(ushort signature) =>
        signature == SigCdHotspotBegin || signature == SigCdV4HotspotBegin || signature == SigCdV5HotspotBegin;

    private static int ZeroTerminatedLength(nint pointer, int maximum)
    {
        var length = 0;
        while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(pointer, length) != 0) length++;
        return length;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFNoteExtractFileDelegate(nint note, XPScriptNotesBlockId itemBlock, nint fileName, nint decryptionKey);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort EnumCompositeBufferDelegate(XPScriptNotesBlockId itemValue, uint itemValueLength, EnumCompositeActionDelegate action, nint context);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort EnumCompositeActionDelegate(nint record, ushort signature, uint recordLength, nint context);
}
""";
}
