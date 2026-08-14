namespace XPScript.Compiler;

internal static class FileIoExtensionsRuntimeSource
{
    public const string Code = """
internal static class XPScriptFileIO
{
    private const System.Reflection.BindingFlags StaticAny = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    private const System.Reflection.BindingFlags InstanceAny = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    private const long WholeFileLockLength = long.MaxValue / 2;
    private const int DarwinFSetLk = 8;
    private const short DarwinFUnlck = 2;
    private const short DarwinFWrlck = 3;
    private const short DarwinSeekSet = 0;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DarwinFlock
    {
        public long Start;
        public long Length;
        public int Pid;
        public short Type;
        public short Whence;
    }

    [System.Runtime.InteropServices.DllImport("libSystem.B.dylib", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int DarwinFcntl(int fd, int command, ref DarwinFlock value);

    public static string InputChars(object? countValue, object? fileNumberValue)
    {
        var count = XPScriptRuntime.CInt(countValue);
        if (count < 0) throw new XPScriptRuntimeException(5, "Input$ count must be zero or greater.");
        if (count == 0) return "";

        var state = GetOpenState(XPScriptRuntime.CInt(fileNumberValue));
        var reader = GetReader(state);
        if (reader is not null)
        {
            var buffer = new char[count];
            var total = 0;
            while (total < count)
            {
                var read = reader.Read(buffer, total, count - total);
                if (read <= 0) throw new EndOfStreamException("Input$ requested more characters than remain in the file.");
                total += read;
            }
            return new string(buffer);
        }

        var stream = GetStream(state) ?? throw new IOException("File is not open for Input or Binary access.");
        var bytes = new byte[count];
        var readTotal = 0;
        while (readTotal < count)
        {
            var read = stream.Read(bytes, readTotal, count - readTotal);
            if (read <= 0) throw new EndOfStreamException("Input$ requested more characters than remain in the file.");
            readTotal += read;
        }
        return Encoding.Default.GetString(bytes);
    }

    public static void LockFile(object? fileNumberValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        LockRegion(stream, 0, WholeFileLockLength);
    }

    public static void UnlockFile(object? fileNumberValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        UnlockRegion(stream, 0, WholeFileLockLength);
    }

    public static void LockBytes(object? fileNumberValue, object? startValue, object? endValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        var (offset, length) = ToOneBasedRange(startValue, endValue, 1);
        LockRegion(stream, offset, length);
    }

    public static void UnlockBytes(object? fileNumberValue, object? startValue, object? endValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        var (offset, length) = ToOneBasedRange(startValue, endValue, 1);
        UnlockRegion(stream, offset, length);
    }

    public static void LockRecords(object? fileNumberValue, object? startValue, object? endValue, object? recordLengthValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        var recordLength = XPScriptRuntime.CLng(recordLengthValue);
        if (recordLength <= 0) throw new XPScriptRuntimeException(5, "Random file record length must be greater than zero.");
        var (offset, length) = ToOneBasedRange(startValue, endValue, recordLength);
        LockRegion(stream, offset, length);
    }

    public static void UnlockRecords(object? fileNumberValue, object? startValue, object? endValue, object? recordLengthValue)
    {
        var stream = RequireLockableStream(fileNumberValue);
        var recordLength = XPScriptRuntime.CLng(recordLengthValue);
        if (recordLength <= 0) throw new XPScriptRuntimeException(5, "Random file record length must be greater than zero.");
        var (offset, length) = ToOneBasedRange(startValue, endValue, recordLength);
        UnlockRegion(stream, offset, length);
    }

    public static void ChDrive(object? driveValue)
    {
        if (!OperatingSystem.IsWindows())
            throw new XPScriptRuntimeException(5, "ChDrive is supported only on Windows.");

        var drive = XPScriptRuntime.CStr(driveValue).Trim();
        if (drive.Length == 0) throw new XPScriptRuntimeException(5, "ChDrive requires a drive letter.");
        var letter = char.ToUpperInvariant(drive[0]);
        if (letter < 'A' || letter > 'Z') throw new XPScriptRuntimeException(5, "Invalid drive specification: " + drive);
        var root = letter + ":\\";
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Drive does not exist: " + root);
        Environment.CurrentDirectory = root;
    }

    private static (long Offset, long Length) ToOneBasedRange(object? startValue, object? endValue, long unitLength)
    {
        var start = XPScriptRuntime.CLng(startValue);
        var end = XPScriptRuntime.CLng(endValue);
        if (start < 1 || end < start) throw new XPScriptRuntimeException(5, "File lock range must be 1-based and end must be greater than or equal to start.");
        checked
        {
            return ((start - 1) * unitLength, (end - start + 1) * unitLength);
        }
    }

    private static FileStream RequireLockableStream(object? fileNumberValue)
    {
        var state = GetOpenState(XPScriptRuntime.CInt(fileNumberValue));
        return GetStream(state) ?? throw new IOException("The open file does not expose an operating-system file handle for locking.");
    }

    private static void LockRegion(FileStream stream, long offset, long length)
    {
        if (OperatingSystem.IsMacOS())
        {
            DarwinSetLock(stream, offset, length, DarwinFWrlck, "lock");
            return;
        }

        try
        {
            stream.Lock(offset, length);
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new XPScriptRuntimeException(5, "Operating-system file locking is not supported on this platform: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new XPScriptRuntimeException(70, "Permission denied while locking file region: " + ex.Message);
        }
        catch (IOException ex)
        {
            throw new XPScriptRuntimeException(70,
                "Unable to lock file region offset " + offset.ToString(CultureInfo.InvariantCulture) +
                " length " + length.ToString(CultureInfo.InvariantCulture) +
                ". Another process or handle may hold an overlapping lock. " + ex.Message);
        }
    }

    private static void UnlockRegion(FileStream stream, long offset, long length)
    {
        if (OperatingSystem.IsMacOS())
        {
            DarwinSetLock(stream, offset, length, DarwinFUnlck, "unlock");
            return;
        }

        try
        {
            stream.Unlock(offset, length);
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new XPScriptRuntimeException(5, "Operating-system file unlocking is not supported on this platform: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new XPScriptRuntimeException(70, "Permission denied while unlocking file region: " + ex.Message);
        }
        catch (IOException ex)
        {
            throw new XPScriptRuntimeException(70,
                "Unable to unlock file region offset " + offset.ToString(CultureInfo.InvariantCulture) +
                " length " + length.ToString(CultureInfo.InvariantCulture) +
                ". The current handle may not own the requested lock. " + ex.Message);
        }
    }

    private static void DarwinSetLock(FileStream stream, long offset, long length, short type, string operation)
    {
        if (stream.SafeFileHandle.IsInvalid || stream.SafeFileHandle.IsClosed)
            throw new XPScriptRuntimeException(70, "Unable to " + operation + " file region because the file handle is closed or invalid.");

        var descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        var value = new DarwinFlock
        {
            Start = offset,
            Length = length == WholeFileLockLength ? 0 : length,
            Pid = 0,
            Type = type,
            Whence = DarwinSeekSet
        };

        if (DarwinFcntl(descriptor, DarwinFSetLk, ref value) == 0)
            return;

        var errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
        throw new XPScriptRuntimeException(70,
            "Unable to " + operation + " file region offset " + offset.ToString(CultureInfo.InvariantCulture) +
            " length " + length.ToString(CultureInfo.InvariantCulture) +
            " on macOS. Native fcntl returned errno " + errno.ToString(CultureInfo.InvariantCulture) + ".");
    }

    private static object GetOpenState(int number)
    {
        // CoreCompatibility lowers normal Input/Output/Append/Binary/Random Open
        // statements to LSFileRuntime. Check that store first so Lock/Unlock share
        // the exact FileStream used by Put/Get/Seek and normal file operations.
        var state = TryGetDictionaryState(typeof(LSFileRuntime), number);
        if (state is not null) return state;

        state = TryInvokeCoreGetFile(number);
        if (state is not null) return state;

        state = TryGetDictionaryState(typeof(XPScriptRuntime), number);
        if (state is not null) return state;
        state = TryGetDictionaryState(typeof(XPScriptTextIO), number);
        if (state is not null) return state;
        throw new IOException("File number is not open: " + number);
    }

    private static object? TryInvokeCoreGetFile(int number)
    {
        var method = typeof(XPScriptRuntime).GetMethod("GetFile", StaticAny);
        if (method is null) return null;
        try
        {
            return method.Invoke(null, new object?[] { number });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is IOException)
        {
            return null;
        }
    }

    private static object? TryGetDictionaryState(Type runtimeType, int number)
    {
        var field = runtimeType.GetField("Files", StaticAny);
        var value = field?.GetValue(null);
        if (value is null) return null;

        if (value is System.Collections.IDictionary dictionary)
            return dictionary.Contains(number) ? dictionary[number] : null;

        if (value is System.Collections.IEnumerable entries)
        {
            foreach (var entry in entries)
            {
                if (entry is null) continue;
                var entryType = entry.GetType();
                var key = entryType.GetProperty("Key", InstanceAny)?.GetValue(entry);
                if (key is null || Convert.ToInt32(key, CultureInfo.InvariantCulture) != number) continue;
                return entryType.GetProperty("Value", InstanceAny)?.GetValue(entry);
            }
        }
        return null;
    }

    private static TextReader? GetReader(object state) =>
        state.GetType().GetProperty("Reader", InstanceAny)?.GetValue(state) as TextReader;

    private static FileStream? GetStream(object state)
    {
        if (state.GetType().GetProperty("Stream", InstanceAny)?.GetValue(state) is FileStream direct)
            return direct;

        if (GetReader(state) is StreamReader reader && reader.BaseStream is FileStream readerFile)
            return readerFile;

        if (state.GetType().GetProperty("Writer", InstanceAny)?.GetValue(state) is StreamWriter writer &&
            writer.BaseStream is FileStream writerFile)
            return writerFile;

        return null;
    }
}
""";
}
