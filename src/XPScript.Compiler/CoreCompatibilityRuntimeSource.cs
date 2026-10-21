namespace XPScript.Compiler;

public static class CoreCompatibilityRuntimeSource
{
    public const string Code = """
internal interface IXPScriptArrayStorage
{
    int Rank { get; }
    bool IsAllocated { get; }
    object? Get(params object?[] indices);
    void Set(object? value, params object?[] indices);
    int LBound(int dimension = 1);
    int UBound(int dimension = 1);
    void Erase();
}

internal sealed class LSArray : IXPScriptArrayStorage
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
                throw new IndexOutOfRangeException("Invalid XPScript array bounds.");
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
        var indices = values.Select(XPScriptRuntime.CInt).ToArray();
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
        "string" => XPScriptRuntime.CStr(value),
        "boolean" => XPScriptRuntime.CBool(value),
        "byte" => XPScriptRuntime.CByte(value),
        "integer" => XPScriptRuntime.CInt(value),
        "long" => XPScriptRuntime.CLng(value),
        "single" => XPScriptRuntime.CSng(value),
        "double" => XPScriptRuntime.CDbl(value),
        "currency" => XPScriptRuntime.CCur(value),
        "date" => XPScriptRuntime.CDat(value),
        _ => value
    };
}

internal static class LSArrayRuntime
{
    public static byte[]? ByteVector(object? value)
    {
        if (value is null) return null;
        if (value is IXPScriptArrayStorage storage)
        {
            if (storage.Rank != 1) throw new InvalidCastException("Byte vector requires a one-dimensional array.");
            var bytes = new List<byte>();
            for (var index = storage.LBound(); index <= storage.UBound(); index++)
                bytes.Add(XPScriptRuntime.CByte(storage.Get(index)));
            return bytes.ToArray();
        }
        if (value is not Array array || array.Rank != 1)
            throw new InvalidCastException("Byte vector requires a one-dimensional array.");
        if (array is byte[] vector) return vector;
        var bytesFromArray = new byte[array.GetLength(0)];
        var lower = array.GetLowerBound(0);
        for (var offset = 0; offset < bytesFromArray.Length; offset++)
            bytesFromArray[offset] = XPScriptRuntime.CByte(array.GetValue(lower + offset));
        return bytesFromArray;
    }

    public static Array? RequireArray<T>(object? value, int rank)
    {
        if (value is null) return null;
        if (value is not Array array || array.Rank != rank || array.GetType().GetElementType() != typeof(T))
            throw new InvalidCastException("Array element type and rank must match the declared type.");
        return array;
    }

    public static Array CreateManaged<T>(int[] lengths, int[] lower)
    {
        var array = Array.CreateInstance(typeof(T), lengths, lower);
        if (typeof(T) == typeof(string)) ClearManaged(array);
        return array;
    }

    private static void ClearManaged(Array array)
    {
        if (array.GetType().GetElementType() == typeof(string) && array.Length > 0)
        {
            var lower = Enumerable.Range(0, array.Rank).Select(array.GetLowerBound).ToArray();
            var indices = (int[])lower.Clone();
            for (var item = 0; item < array.Length; item++)
            {
                array.SetValue(string.Empty, indices);
                for (var dimension = array.Rank - 1; dimension >= 0; dimension--)
                {
                    if (indices[dimension] < array.GetUpperBound(dimension))
                    {
                        indices[dimension]++;
                        break;
                    }
                    indices[dimension] = lower[dimension];
                }
            }
        }
        else Array.Clear(array);
    }

    // Obtain a managed reference only after exact element-type and complete bounds checks.
    // This also supports non-zero-based arrays without a snapshot/copy-back wrapper.
    public static ref T Reference<T>(object? value, params object?[] indices)
    {
        if (value is not Array array || array.GetType().GetElementType() != typeof(T))
            throw new InvalidCastException("ByRef requires an array with the matching element type.");
        if (indices.Length != array.Rank)
            throw new IndexOutOfRangeException("Wrong number of array subscripts.");
        long offset = 0;
        for (var dimension = 0; dimension < indices.Length; dimension++)
        {
            var index = XPScriptRuntime.CInt(indices[dimension]);
            var lower = array.GetLowerBound(dimension);
            if (index < lower || index > array.GetUpperBound(dimension))
                throw new IndexOutOfRangeException("Subscript out of range.");
            offset = checked(offset * array.GetLength(dimension) + ((long)index - lower));
        }
        return ref System.Runtime.CompilerServices.Unsafe.Add(
            ref System.Runtime.CompilerServices.Unsafe.As<byte, T>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array)), checked((nint)offset));
    }

    public static LSArray Fixed(string type, int[] lower, int[] upper) => new(type, false, lower, upper);
    public static LSArray Dynamic(string type) => new(type, true);
    public static LSArray Dynamic(string type, int[] lower, int[] upper) => new(type, true, lower, upper);

    public static LSArray ReDim(object? value, string type, bool preserve, int[] lower, int[] upper)
    {
        var array = value as LSArray ?? new LSArray(type, true);
        array.ReDim(lower, upper, preserve);
        return array;
    }

    public static Array ReDimManaged(object? value, bool preserve, int lower, int upper)
        => ReDimManaged(value, preserve, [lower], [upper]);

    public static Array ReDimManaged(object? value, bool preserve, int[] lower, int[] upper)
    {
        if (lower.Length == 0 || lower.Length != upper.Length || lower.Length > 8)
            throw new InvalidOperationException("Arrays must have between one and eight dimensions.");
        var source = value as Array;
        if (preserve && source is not null)
        {
            if (source.Rank != lower.Length)
                throw new InvalidOperationException("ReDim cannot change the number of dimensions.");
            for (var i = 0; i < lower.Length; i++)
            {
                if (source.GetLowerBound(i) != lower[i])
                    throw new InvalidOperationException("ReDim Preserve cannot change lower bounds.");
                if (i < lower.Length - 1 && source.GetUpperBound(i) != upper[i])
                    throw new InvalidOperationException("ReDim Preserve can change only the upper bound of the last dimension.");
            }
        }
        var elementType = source?.GetType().GetElementType() ?? typeof(object);
        var lengths = lower.Select((bound, i) => checked(upper[i] - bound + 1)).ToArray();
        if (lengths.Any(length => length < 0))
            throw new IndexOutOfRangeException("Invalid XPScript array bounds.");
        var result = Array.CreateInstance(elementType, lengths, lower);
        if (elementType == typeof(string)) ClearManaged(result);
        if (preserve && source is not null)
        {
            var indices = new int[lower.Length];
            CopyDimension(0);

            void CopyDimension(int dimension)
            {
                var low = Math.Max(source.GetLowerBound(dimension), result.GetLowerBound(dimension));
                var high = Math.Min(source.GetUpperBound(dimension), result.GetUpperBound(dimension));
                for (var index = low; index <= high; index++)
                {
                    indices[dimension] = index;
                    if (dimension + 1 < indices.Length)
                        CopyDimension(dimension + 1);
                    else
                        result.SetValue(source.GetValue(indices), indices);
                }
            }
        }
        return result;
    }

    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2)
        => ReDimManaged(value, preserve, [lower1, lower2], [upper1, upper2]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3], [upper1, upper2, upper3]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3, int upper4, int lower4)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3, lower4], [upper1, upper2, upper3, upper4]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3, int upper4, int lower4, int upper5, int lower5)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3, lower4, lower5], [upper1, upper2, upper3, upper4, upper5]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3, int upper4, int lower4, int upper5, int lower5, int upper6, int lower6)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3, lower4, lower5, lower6], [upper1, upper2, upper3, upper4, upper5, upper6]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3, int upper4, int lower4, int upper5, int lower5, int upper6, int lower6, int upper7, int lower7)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3, lower4, lower5, lower6, lower7], [upper1, upper2, upper3, upper4, upper5, upper6, upper7]);
    public static Array ReDimManaged(object? value, bool preserve, int upper1, int lower1, int upper2, int lower2, int upper3, int lower3, int upper4, int lower4, int upper5, int lower5, int upper6, int lower6, int upper7, int lower7, int upper8, int lower8)
        => ReDimManaged(value, preserve, [lower1, lower2, lower3, lower4, lower5, lower6, lower7, lower8], [upper1, upper2, upper3, upper4, upper5, upper6, upper7, upper8]);

    public static object? Get(object? value, params object?[] indices)
    {
        return Storage(value).Get(indices);
    }

    public static object? Index(object? value, object? index)
    {
        if (value is IXPScriptArrayStorage or Array) return Get(value, index);
        return ((dynamic)value!)[(dynamic)index!];
    }

    public static void SetIndex(object? value, object? index, object? newValue)
    {
        if (value is IXPScriptArrayStorage or Array) Set(value, newValue, index);
        else ((dynamic)value!)[(dynamic)index!] = newValue;
    }

    public static void Set(object? value, object? newValue, params object?[] indices)
    {
        Storage(value).Set(newValue, indices);
    }

    public static int LBound(object? value, int dimension = 1)
    {
        return Storage(value).LBound(dimension);
    }

    public static int UBound(object? value, int dimension = 1)
    {
        return Storage(value).UBound(dimension);
    }

    public static void Erase(object? value)
    {
        if (!IsNothing(value)) Storage(value).Erase();
    }

    public static bool IsNothing(object? value) =>
        value is ILSObjectReference reference && reference.IsNothing;

    public static LSArray NothingArray()
    {
        var array = new LSArray("String", true, [0], [0]);
        array.Set("", 0);
        return array;
    }

    private static IXPScriptArrayStorage Storage(object? value) => value switch
    {
        null => NothingArray(),
        ILSObjectReference reference when reference.IsNothing => NothingArray(),
        IXPScriptArrayStorage storage => storage,
        Array managed => new ManagedArrayStorage(managed),
        _ => throw new InvalidOperationException("Value is not an XPScript array.")
    };

    private sealed class ManagedArrayStorage(Array array) : IXPScriptArrayStorage
    {
        public int Rank => array.Rank;
        public bool IsAllocated => true;
        public object? Get(params object?[] indices) => array.GetValue(Indices(indices));
        public void Set(object? value, params object?[] indices) => array.SetValue(value, Indices(indices));
        public int LBound(int dimension = 1) { ValidateDimension(dimension); return array.GetLowerBound(dimension - 1); }
        public int UBound(int dimension = 1) { ValidateDimension(dimension); return array.GetUpperBound(dimension - 1); }
        public void Erase()
        {
            ClearManaged(array);
        }
        private int[] Indices(object?[] indices)
        {
            if (indices.Length != array.Rank) throw new IndexOutOfRangeException("Wrong number of array subscripts.");
            return indices.Select(XPScriptRuntime.CInt).ToArray();
        }
        private void ValidateDimension(int dimension)
        {
            if (dimension < 1 || dimension > array.Rank) throw new IndexOutOfRangeException("Invalid array dimension.");
        }
    }
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

internal sealed class XPScriptTlsValidationState
{
    public const int CertificateErrorNumber = 1201;
    private string _mode = "Strict";
    public string LastError { get; private set; } = string.Empty;

    public string Mode
    {
        get => _mode;
        set
        {
            var mode = (value ?? string.Empty).Trim();
            if (mode.Equals("Strict", StringComparison.OrdinalIgnoreCase)) _mode = "Strict";
            else if (mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase)) _mode = "AllowSelfSigned";
            else if (mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)) _mode = "Insecure";
            else throw new XPScriptRuntimeException(5, "CertificateValidation must be Strict, AllowSelfSigned, or Insecure.");
        }
    }

    public void Reset() => LastError = string.Empty;

    public bool Validate(System.Net.Http.HttpRequestMessage request,
        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        System.Net.Security.SslPolicyErrors errors)
    {
        if (errors == System.Net.Security.SslPolicyErrors.None)
        {
            LastError = string.Empty;
            return true;
        }

        var details = new List<string>();
        if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            details.Add("the server did not provide a certificate");
        if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            details.Add("the certificate hostname does not match '" + (request.RequestUri?.Host ?? "the requested host") + "'");

        var statuses = chain?.ChainStatus ?? [];
        foreach (var status in statuses)
        {
            var text = status.Status switch
            {
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot => "the certificate root is not trusted (self-signed certificate or private CA)",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain => "the certificate chain is incomplete or its issuer is not trusted",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NotTimeValid => "the certificate is expired or not yet valid",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.Revoked => "the certificate has been revoked",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown => "the certificate revocation status could not be verified",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NotSignatureValid => "the certificate signature is invalid",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.InvalidBasicConstraints => "the certificate has invalid basic constraints",
                _ => status.StatusInformation.Trim().Length > 0 ? status.StatusInformation.Trim() : status.Status.ToString()
            };
            if (text.Length > 0) details.Add(text);
        }
        if (details.Count == 0) details.Add("the certificate chain is not trusted");
        LastError = string.Join("; ", details.Distinct(StringComparer.OrdinalIgnoreCase));

        if (_mode == "Insecure") return true;
        if (_mode == "AllowSelfSigned")
        {
            if ((errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                return false;
            return statuses.Length > 0 && statuses.All(s =>
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot ||
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain ||
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError);
        }
        return false;
    }

    public XPScriptRuntimeException Failure(string operation)
        => new(CertificateErrorNumber, operation + " TLS certificate validation failed: " +
            (LastError.Length > 0 ? LastError : "the server certificate was rejected by the operating system trust policy") + ".");
}

internal sealed class XPScriptRuntimeException : Exception
{
    public XPScriptRuntimeException(int number, string description) : base(description)
    {
        Number = number;
    }

    public int Number { get; }
}

internal static class XPScriptErrorRuntime
{
    [ThreadStatic] private static int _number;
    [ThreadStatic] private static string? _description;
    [ThreadStatic] private static int _erl;

    public static int Err => _number;
    public static int Erl => _erl;
    public static string Description => _description ?? "";

    public static int Capture(Exception exception, int line)
    {
        _number = exception is XPScriptRuntimeException ls ? ls.Number : exception.HResult != 0 ? exception.HResult : 1;
        _description = exception.Message;
        _erl = line;
        return _number;
    }

    public static void Raise(int number, object? description = null)
    {
        var text = description is null ? Error(number) : XPScriptRuntime.CStr(description);
        _number = number;
        _description = text;
        throw new XPScriptRuntimeException(number, text);
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
        _ => "XPScript error " + number.ToString(CultureInfo.InvariantCulture)
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
        if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right)) return XPScriptRuntime.CDbl(left).CompareTo(XPScriptRuntime.CDbl(right));
        if (left is DateTime || right is DateTime) return XPScriptRuntime.CDat(left).CompareTo(XPScriptRuntime.CDat(right));
        return string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
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
        var path = Path.GetFullPath(XPScriptRuntime.CStr(pathValue));
        var mode = XPScriptRuntime.CStr(modeValue).ToLowerInvariant();
        lock (Sync)
        {
            if (Files.ContainsKey(number)) throw new XPScriptRuntimeException(55, "File already open");
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
        var text = string.Concat(values.Select(XPScriptRuntime.CStr));
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

    public static string InputChars(int number, int count)
    {
        if (count < 0) throw new XPScriptRuntimeException(5, "Input$ count must be zero or greater.");
        if (count == 0) return string.Empty;
        var state = Get(number);
        if (state.Reader is not null)
        {
            var buffer = new char[count];
            var total = 0;
            while (total < count)
            {
                var read = state.Reader.Read(buffer, total, count - total);
                if (read <= 0) throw new EndOfStreamException("Input$ requested more characters than remain in the file.");
                total += read;
            }
            return new string(buffer);
        }
        if (state.Mode is not ("binary" or "random")) throw new IOException("File is not open for Input or Binary access.");
        var bytes = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = state.Stream.Read(bytes, offset, count - offset);
            if (read <= 0) throw new EndOfStreamException("Input$ requested more characters than remain in the file.");
            offset += read;
        }
        return Encoding.Latin1.GetString(bytes);
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

    public static void LockBytes(int number, long start, long end)
    {
        if (start < 1 || end < start) throw new IOException("Lock range must be a valid one-based interval.");
        Get(number).Stream.Lock(checked(start - 1), checked(end - start + 1));
    }

    public static void UnlockBytes(int number, long start, long end)
    {
        if (start < 1 || end < start) throw new IOException("Unlock range must be a valid one-based interval.");
        Get(number).Stream.Unlock(checked(start - 1), checked(end - start + 1));
    }

    public static void ChDrive(object? driveValue)
    {
        if (!OperatingSystem.IsWindows()) throw new XPScriptRuntimeException(5, "ChDrive is supported only on Windows.");
        var drive = XPScriptRuntime.CStr(driveValue).Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(drive, "^[A-Za-z]:$"))
            throw new XPScriptRuntimeException(5, "ChDrive requires a drive letter.");
        var root = drive + "\\";
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Drive not found: " + drive);
        Environment.CurrentDirectory = root;
    }

    public static void Put(int number, object? recordNumber, object? value, string xpscriptType)
    {
        var state = Get(number);
        EnsureBinary(state);
        PositionForRecord(state, recordNumber);
        using var writer = new BinaryWriter(state.Stream, Encoding.Default, true);
        WriteTyped(writer, state, value, xpscriptType);
        FinishRecord(state);
    }

    public static object? GetValue(int number, object? recordNumber, string xpscriptType, object? currentValue)
    {
        var state = Get(number);
        EnsureBinary(state);
        PositionForRecord(state, recordNumber);
        using var reader = new BinaryReader(state.Stream, Encoding.Default, true);
        var value = ReadTyped(reader, state, xpscriptType, currentValue);
        FinishRecord(state);
        return value;
    }

    private static void PositionForRecord(State state, object? recordNumber)
    {
        if (recordNumber is null) return;
        var record = XPScriptRuntime.CLng(recordNumber);
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
            case "byte": writer.Write(XPScriptRuntime.CByte(value)); break;
            case "boolean": writer.Write((short)(XPScriptRuntime.CBool(value) ? -1 : 0)); break;
            case "integer": writer.Write((short)XPScriptRuntime.CInt(value)); break;
            case "long": writer.Write((int)XPScriptRuntime.CLng(value)); break;
            case "single": writer.Write(XPScriptRuntime.CSng(value)); break;
            case "double": writer.Write(XPScriptRuntime.CDbl(value)); break;
            case "currency": writer.Write((long)(XPScriptRuntime.CCur(value) * 10000m)); break;
            case "date": writer.Write(XPScriptRuntime.CDat(value).ToOADate()); break;
            case "string":
            {
                var bytes = Encoding.Default.GetBytes(XPScriptRuntime.CStr(value));
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
        var length = state.Mode == "random" ? reader.ReadUInt16() : Encoding.Default.GetByteCount(XPScriptRuntime.CStr(current));
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
        // Binary files are created on first Open, matching LotusScript/legacy
        // behavior for a new output file.
        var fileMode = FileMode.OpenOrCreate;
        var stream = new FileStream(path, fileMode, FileAccess.ReadWrite, FileShare.ReadWrite);
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
