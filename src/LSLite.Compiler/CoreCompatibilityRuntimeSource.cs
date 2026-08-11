namespace LSLite.Compiler;

public static class CoreCompatibilityRuntimeSource
{
    public const string Code = """
internal sealed class LSArray
{
    private object?[] _data = [];

    public LSArray(string elementType, bool dynamic, int[]? lower = null, int[]? upper = null)
    {
        ElementType = elementType;
        IsDynamic = dynamic;
        if (lower is not null && upper is not null)
            Allocate(lower, upper);
    }

    public string ElementType { get; }
    public bool IsDynamic { get; }
    public bool IsAllocated { get; private set; }
    public int Rank => LowerBounds.Length;
    public int[] LowerBounds { get; private set; } = [];
    public int[] UpperBounds { get; private set; } = [];
    private int[] Lengths { get; set; } = [];

    public object? Get(params object?[] indices)
    {
        EnsureAllocated();
        return _data[Offset(indices)];
    }

    public void Set(object? value, params object?[] indices)
    {
        EnsureAllocated();
        _data[Offset(indices)] = Coerce(value, ElementType);
    }

    public int LBound(int dimension = 1)
    {
        EnsureAllocated();
        if (dimension < 1 || dimension > Rank) throw new IndexOutOfRangeException("Invalid array dimension.");
        return LowerBounds[dimension - 1];
    }

    public int UBound(int dimension = 1)
    {
        EnsureAllocated();
        if (dimension < 1 || dimension > Rank) throw new IndexOutOfRangeException("Invalid array dimension.");
        return UpperBounds[dimension - 1];
    }

    public void ReDim(int[] lower, int[] upper, bool preserve)
    {
        if (IsAllocated && lower.Length != Rank)
            throw new InvalidOperationException("ReDim cannot change the number of dimensions.");

        if (!preserve || !IsAllocated)
        {
            Allocate(lower, upper);
            return;
        }

        for (var i = 0; i < Rank; i++)
        {
            if (lower[i] != LowerBounds[i])
                throw new InvalidOperationException("ReDim Preserve cannot change lower bounds.");
            if (i < Rank - 1 && upper[i] != UpperBounds[i])
                throw new InvalidOperationException("ReDim Preserve can change only the upper bound of the last dimension.");
        }

        var oldLower = LowerBounds;
        var oldUpper = UpperBounds;
        var oldLengths = Lengths;
        var oldData = _data;
        Allocate(lower, upper);

        var current = new int[Rank];
        CopyDimension(0);

        void CopyDimension(int dimension)
        {
            var low = Math.Max(oldLower[dimension], LowerBounds[dimension]);
            var high = Math.Min(oldUpper[dimension], UpperBounds[dimension]);
            for (var i = low; i <= high; i++)
            {
                current[dimension] = i;
                if (dimension + 1 < Rank)
                {
                    CopyDimension(dimension + 1);
                    continue;
                }

                var oldOffset = OffsetFor(current, oldLower, oldUpper, oldLengths);
                var newOffset = OffsetFor(current, LowerBounds, UpperBounds, Lengths);
                _data[newOffset] = oldData[oldOffset];
            }
        }
    }

    public void Erase()
    {
        if (IsDynamic)
        {
            _data = [];
            LowerBounds = [];
            UpperBounds = [];
            Lengths = [];
            IsAllocated = false;
            return;
        }

        for (var i = 0; i < _data.Length; i++)
            _data[i] = DefaultValue(ElementType);
    }

    private void Allocate(int[] lower, int[] upper)
    {
        if (lower.Length == 0 || lower.Length != upper.Length || lower.Length > 8)
            throw new InvalidOperationException("Arrays must have between one and eight dimensions.");

        var lengths = new int[lower.Length];
        long total = 1;
        for (var i = 0; i < lower.Length; i++)
        {
            if (lower[i] < -32768 || upper[i] > 32767 || upper[i] < lower[i])
                throw new IndexOutOfRangeException("Invalid LotusScript array bounds.");
            lengths[i] = checked(upper[i] - lower[i] + 1);
            total = checked(total * lengths[i]);
        }

        LowerBounds = [.. lower];
        UpperBounds = [.. upper];
        Lengths = lengths;
        _data = new object?[checked((int)total)];
        var initial = DefaultValue(ElementType);
        if (initial is not null)
            Array.Fill(_data, initial);
        IsAllocated = true;
    }

    private int Offset(object?[] values)
    {
        if (values.Length != Rank) throw new IndexOutOfRangeException("Wrong number of array subscripts.");
        var indices = values.Select(LotusRuntime.CInt).ToArray();
        return OffsetFor(indices, LowerBounds, UpperBounds, Lengths);
    }

    private static int OffsetFor(int[] indices, int[] lower, int[] upper, int[] lengths)
    {
        var offset = 0;
        for (var i = 0; i < indices.Length; i++)
        {
            if (indices[i] < lower[i] || indices[i] > upper[i]) throw new IndexOutOfRangeException("Subscript out of range.");
            offset = checked(offset * lengths[i] + indices[i] - lower[i]);
        }
        return offset;
    }

    private void EnsureAllocated()
    {
        if (!IsAllocated) throw new InvalidOperationException("Array has not been allocated. Use ReDim first.");
    }

    private static object? DefaultValue(string type) => type.ToLowerInvariant() switch
    {
        "string" => "",
        "boolean" => false,
        "byte" => (byte)0,
        "integer" => 0,
        "long" => 0L,
        "single" => 0f,
        "double" => 0d,
        "currency" => 0m,
        "date" => default(DateTime),
        _ => null
    };

    private static object? Coerce(object? value, string type) => type.ToLowerInvariant() switch
    {
        "string" => LotusRuntime.CStr(value),
        "boolean" => LotusRuntime.CBool(value),
        "byte" => LotusRuntime.CByte(value),
        "integer" => LotusRuntime.CInt(value),
        "long" => LotusRuntime.CLng(value),
        "single" => LotusRuntime.CSng(value),
        "double" => LotusRuntime.CDbl(value),
        "currency" => LotusRuntime.CCur(value),
        "date" => LotusRuntime.CDat(value),
        _ => value
    };
}

internal static class LSArrayRuntime
{
    public static LSArray Fixed(string type, int[] lower, int[] upper) => new(type, false, lower, upper);
    public static LSArray Dynamic(string type) => new(type, true);
    public static LSArray Dynamic(string type, int[] lower, int[] upper) => new(type, true, lower, upper);

    public static LSArray ReDim(object? value, string type, bool preserve, int[] lower, int[] upper)
    {
        var array = value as LSArray ?? new LSArray(type, true);
        array.ReDim(lower, upper, preserve);
        return array;
    }

    public static object? Get(object? value, params object?[] indices) => Require(value).Get(indices);
    public static void Set(object? value, object? newValue, params object?[] indices) => Require(value).Set(newValue, indices);
    public static int LBound(object? value, int dimension = 1) => Require(value).LBound(dimension);
    public static int UBound(object? value, int dimension = 1) => Require(value).UBound(dimension);
    public static void Erase(object? value) => Require(value).Erase();
    private static LSArray Require(object? value) => value as LSArray ?? throw new InvalidOperationException("Value is not an LS Lite array.");
}

internal sealed class LSByRefValue
{
    private readonly Func<object?> _get;
    private readonly Action<object?> _set;

    public LSByRefValue(Func<object?> get, Action<object?> set)
    {
        _get = get;
        _set = set;
    }

    public object? Value
    {
        get => _get();
        set => _set(value);
    }
}

internal static class LSByRefRuntime
{
    public static LSByRefValue Create(Func<object?> get, Action<object?> set) => new(get, set);
}

internal sealed class LotusScriptRuntimeException : Exception
{
    public LotusScriptRuntimeException(int number, string description) : base(description)
    {
        Number = number;
    }

    public int Number { get; }
}

internal static class LotusErrorRuntime
{
    [ThreadStatic] private static int _number;
    [ThreadStatic] private static string? _description;
    [ThreadStatic] private static int _erl;

    public static int Err => _number;
    public static int Erl => _erl;
    public static string Description => _description ?? "";

    public static int Capture(Exception exception, int line)
    {
        _number = exception is LotusScriptRuntimeException ls ? ls.Number : exception.HResult != 0 ? exception.HResult : 1;
        _description = exception.Message;
        _erl = line;
        return _number;
    }

    public static void Raise(int number, object? description = null)
    {
        var text = description is null ? Error(number) : LotusRuntime.CStr(description);
        _number = number;
        _description = text;
        throw new LotusScriptRuntimeException(number, text);
    }

    public static string Error() => Description;
    public static string Error(int number) => number switch
    {
        5 => "Invalid procedure call",
        6 => "Overflow",
        9 => "Subscript out of range",
        11 => "Division by zero",
        13 => "Type mismatch",
        53 => "File not found",
        55 => "File already open",
        62 => "Input past end of file",
        _ => "LotusScript error " + number.ToString(CultureInfo.InvariantCulture)
    };

    public static void Clear()
    {
        _number = 0;
        _description = "";
        _erl = 0;
    }
}

internal static class LSCoreCompare
{
    public static bool Equal(object? left, object? right) => Compare(left, right) == 0;
    public static bool Between(object? value, object? low, object? high) => Compare(value, low) >= 0 && Compare(value, high) <= 0;
    public static bool Rel(object? value, string op, object? other)
    {
        var c = Compare(value, other);
        return op switch { "=" => c == 0, "<>" => c != 0, ">" => c > 0, ">=" => c >= 0, "<" => c < 0, "<=" => c <= 0, _ => false };
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        if (LotusRuntime.IsNumeric(left) && LotusRuntime.IsNumeric(right)) return LotusRuntime.CDbl(left).CompareTo(LotusRuntime.CDbl(right));
        if (left is DateTime || right is DateTime) return LotusRuntime.CDat(left).CompareTo(LotusRuntime.CDat(right));
        return string.Compare(LotusRuntime.CStr(left), LotusRuntime.CStr(right), StringComparison.CurrentCulture);
    }
}

internal static class LSFileRuntime
{
    private sealed class State
    {
        public required FileStream Stream { get; init; }
        public StreamReader? Reader { get; init; }
        public StreamWriter? Writer { get; init; }
        public required string Mode { get; init; }
        public int RecordLength { get; init; }
        public long LastLoc { get; set; }
        public long SequentialBytes { get; set; }
    }

    private static readonly Dictionary<int, State> Files = new();
    private static readonly object Sync = new();

    public static int FreeFile()
    {
        lock (Sync)
            for (var i = 1; i <= 255; i++) if (!Files.ContainsKey(i)) return i;
        throw new IOException("No free file numbers are available.");
    }

    public static void Open(object? pathValue, object? modeValue, int number, int recordLength = 0)
    {
        var path = Path.GetFullPath(LotusRuntime.CStr(pathValue));
        var mode = LotusRuntime.CStr(modeValue).ToLowerInvariant();
        lock (Sync)
        {
            if (Files.ContainsKey(number)) throw new LotusScriptRuntimeException(55, "File already open");
            State state = mode switch
            {
                "input" => ReadState(path),
                "output" => WriteState(path, false),
                "append" => WriteState(path, true),
                "binary" => BinaryState(path, "binary", 0),
                "random" => BinaryState(path, "random", recordLength > 0 ? recordLength : 128),
                _ => throw new IOException("Unsupported file mode: " + mode)
            };
            Files[number] = state;
        }
    }

    public static void Close(params int[] numbers)
    {
        lock (Sync)
        {
            if (numbers.Length == 0) numbers = Files.Keys.ToArray();
            foreach (var number in numbers)
            {
                if (!Files.Remove(number, out var state)) continue;
                state.Writer?.Flush();
                state.Reader?.Dispose();
                state.Writer?.Dispose();
                state.Stream.Dispose();
            }
        }
    }

    public static void PrintFile(int number, params object?[] values)
    {
        var state = Get(number);
        var writer = state.Writer ?? throw new IOException("File is not open for output.");
        var text = string.Concat(values.Select(LotusRuntime.CStr));
        writer.WriteLine(text);
        state.SequentialBytes += Encoding.Default.GetByteCount(text + Environment.NewLine);
    }

    public static void WriteFile(int number, params object?[] values)
    {
        var state = Get(number);
        var writer = state.Writer ?? throw new IOException("File is not open for output.");
        var encoded = values.Select(v => v switch
        {
            null => "#NULL#",
            DateTime dt => "#" + dt.ToString(CultureInfo.InvariantCulture) + "#",
            string s => "\"" + s.Replace("\"", "\"\"") + "\"",
            _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
        });
        var text = string.Join(",", encoded);
        writer.WriteLine(text);
        state.SequentialBytes += Encoding.Default.GetByteCount(text + Environment.NewLine);
    }

    public static string LineInput(int number)
    {
        var state = Get(number);
        var reader = state.Reader ?? throw new IOException("File is not open for input.");
        var line = reader.ReadLine() ?? "";
        state.SequentialBytes += Encoding.Default.GetByteCount(line + Environment.NewLine);
        return line;
    }

    public static string Input(int number)
    {
        var state = Get(number);
        var reader = state.Reader ?? throw new IOException("File is not open for input.");
        var sb = new StringBuilder();
        var quoted = false;
        while (true)
        {
            var n = reader.Read();
            if (n < 0) break;
            var c = (char)n;
            state.SequentialBytes += Encoding.Default.GetByteCount([c]);
            if (c == '"') { quoted = !quoted; continue; }
            if (!quoted && (c == ',' || c == '\n')) break;
            if (c != '\r') sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static bool EOF(int number)
    {
        var state = Get(number);
        if (state.Reader is not null) return state.Reader.Peek() < 0;
        return state.Stream.Position >= state.Stream.Length;
    }

    public static long LOF(int number) => Get(number).Stream.Length;

    public static long Seek(int number)
    {
        var state = Get(number);
        return state.Mode == "random" ? state.Stream.Position / state.RecordLength + 1 : state.Stream.Position + 1;
    }

    public static void SeekSet(int number, long position)
    {
        var state = Get(number);
        state.Writer?.Flush();
        state.Reader?.DiscardBufferedData();
        var offset = state.Mode == "random" ? checked((position - 1) * state.RecordLength) : position - 1;
        state.Stream.Seek(Math.Max(0, offset), SeekOrigin.Begin);
    }

    public static long Loc(int number)
    {
        var state = Get(number);
        return state.Mode switch
        {
            "random" => state.LastLoc,
            "binary" => state.LastLoc,
            _ => state.SequentialBytes / 128
        };
    }

    public static void Put(int number, object? recordNumber, object? value, string lotusType)
    {
        var state = Get(number);
        EnsureBinary(state);
        PositionForRecord(state, recordNumber);
        using var writer = new BinaryWriter(state.Stream, Encoding.Default, true);
        WriteTyped(writer, state, value, lotusType);
        FinishRecord(state);
    }

    public static object? GetValue(int number, object? recordNumber, string lotusType, object? currentValue)
    {
        var state = Get(number);
        EnsureBinary(state);
        PositionForRecord(state, recordNumber);
        using var reader = new BinaryReader(state.Stream, Encoding.Default, true);
        var value = ReadTyped(reader, state, lotusType, currentValue);
        FinishRecord(state);
        return value;
    }

    private static void PositionForRecord(State state, object? recordNumber)
    {
        if (recordNumber is null) return;
        var record = LotusRuntime.CLng(recordNumber);
        if (record < 1) throw new IOException("Record/byte position must be at least 1.");
        var offset = state.Mode == "random" ? checked((record - 1) * state.RecordLength) : record - 1;
        state.Stream.Seek(offset, SeekOrigin.Begin);
    }

    private static void FinishRecord(State state)
    {
        if (state.Mode == "random")
        {
            var record = state.Stream.Position / state.RecordLength;
            var remainder = state.Stream.Position % state.RecordLength;
            if (remainder != 0)
            {
                var next = checked((record + 1) * state.RecordLength);
                state.Stream.Seek(next, SeekOrigin.Begin);
                record++;
            }
            state.LastLoc = Math.Max(0, record);
        }
        else
        {
            state.LastLoc = state.Stream.Position;
        }
    }

    private static void WriteTyped(BinaryWriter writer, State state, object? value, string type)
    {
        switch (type.ToLowerInvariant())
        {
            case "byte": writer.Write(LotusRuntime.CByte(value)); break;
            case "boolean": writer.Write((short)(LotusRuntime.CBool(value) ? -1 : 0)); break;
            case "integer": writer.Write((short)LotusRuntime.CInt(value)); break;
            case "long": writer.Write((int)LotusRuntime.CLng(value)); break;
            case "single": writer.Write(LotusRuntime.CSng(value)); break;
            case "double": writer.Write(LotusRuntime.CDbl(value)); break;
            case "currency": writer.Write((long)(LotusRuntime.CCur(value) * 10000m)); break;
            case "date": writer.Write(LotusRuntime.CDat(value).ToOADate()); break;
            case "string":
            {
                var bytes = Encoding.Default.GetBytes(LotusRuntime.CStr(value));
                if (state.Mode == "random") writer.Write((ushort)Math.Min(bytes.Length, ushort.MaxValue));
                writer.Write(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
                break;
            }
            default:
                throw new NotSupportedException("Get/Put type is not supported: " + type);
        }
    }

    private static object? ReadTyped(BinaryReader reader, State state, string type, object? current)
    {
        return type.ToLowerInvariant() switch
        {
            "byte" => reader.ReadByte(),
            "boolean" => reader.ReadInt16() != 0,
            "integer" => (int)reader.ReadInt16(),
            "long" => (long)reader.ReadInt32(),
            "single" => reader.ReadSingle(),
            "double" => reader.ReadDouble(),
            "currency" => reader.ReadInt64() / 10000m,
            "date" => DateTime.FromOADate(reader.ReadDouble()),
            "string" => ReadStringValue(reader, state, current),
            _ => throw new NotSupportedException("Get/Put type is not supported: " + type)
        };
    }

    private static string ReadStringValue(BinaryReader reader, State state, object? current)
    {
        var length = state.Mode == "random" ? reader.ReadUInt16() : Encoding.Default.GetByteCount(LotusRuntime.CStr(current));
        return Encoding.Default.GetString(reader.ReadBytes(length));
    }

    private static void EnsureBinary(State state)
    {
        if (state.Mode is not ("binary" or "random")) throw new IOException("Get/Put require Binary or Random mode.");
    }

    private static State Get(int number) => Files.TryGetValue(number, out var state) ? state : throw new IOException("File number is not open: " + number);

    private static State ReadState(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new State { Stream = stream, Reader = new StreamReader(stream, Encoding.Default, true, 1024, true), Mode = "input" };
    }

    private static State WriteState(string path, bool append)
    {
        var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        return new State { Stream = stream, Writer = new StreamWriter(stream, Encoding.Default, 1024, true) { AutoFlush = true }, Mode = append ? "append" : "output", SequentialBytes = append ? stream.Length : 0 };
    }

    private static State BinaryState(string path, string mode, int recordLength)
    {
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        return new State { Stream = stream, Mode = mode, RecordLength = recordLength };
    }
}

// Marker calls are removed by CoreCompatibilityTranspiler after the normal transpiler emits C#.
internal static class LSCoreMarker
{
    public static void Label(string name) { }
    public static void GoTo(string name) { }
    public static void GoSub(string name, int id) { }
    public static void GosubReturn() { }
    public static void Statement(int id) { }
    public static void OnErrorGoto(string label, int handlerId, int errorNumber) { }
    public static void OnErrorResumeNext() { }
    public static void OnErrorOff(int errorNumber) { }
    public static void ResumeCurrent() { }
    public static void ResumeNext() { }
    public static void ResumeLabel(string label) { }
}
""";
}
