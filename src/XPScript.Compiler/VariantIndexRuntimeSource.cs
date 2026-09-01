namespace XPScript.Compiler;

public static class VariantIndexRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptBinaryArray : System.Collections.Generic.IEnumerable<byte>
{
    private readonly byte[] _data;

    internal XPScriptBinaryArray(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public int Length => _data.Length;
    public int Count => _data.Length;

    internal object Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if ((uint)index >= (uint)_data.Length) throw new IndexOutOfRangeException("Subscript out of range.");
        return _data[index];
    }

    internal void Set(object? indexValue, object? value)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if ((uint)index >= (uint)_data.Length) throw new IndexOutOfRangeException("Subscript out of range.");
        _data[index] = XPScriptRuntime.CByte(value);
    }

    internal int LBound(int dimension = 1)
    {
        if (dimension != 1) throw new IndexOutOfRangeException("Invalid array dimension.");
        return 0;
    }

    internal int UBound(int dimension = 1)
    {
        if (dimension != 1) throw new IndexOutOfRangeException("Invalid array dimension.");
        return _data.Length - 1;
    }

    internal byte[] ToArray() => (byte[])_data.Clone();

    public System.Collections.Generic.IEnumerator<byte> GetEnumerator() =>
        ((System.Collections.Generic.IEnumerable<byte>)_data).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _data.GetEnumerator();
}

internal static class LSDynamicIndexRuntime
{
    public static object? Get(object? value, params object?[] indices)
    {
        if (value is ILSList list)
        {
            if (indices.Length != 1)
                throw new IndexOutOfRangeException("List access requires exactly one tag.");
            return list.GetValue(indices[0]);
        }

        if (value is XPScriptBinaryArray binary)
        {
            if (indices.Length != 1)
                throw new IndexOutOfRangeException("Binary array access requires exactly one subscript.");
            return binary.Get(indices[0]);
        }

        return LSArrayRuntime.Get(value, indices);
    }

    public static void Set(object? value, object? newValue, params object?[] indices)
    {
        if (value is ILSList list)
        {
            if (indices.Length != 1)
                throw new IndexOutOfRangeException("List access requires exactly one tag.");
            list.SetValue(indices[0], newValue);
            return;
        }

        if (value is XPScriptBinaryArray binary)
        {
            if (indices.Length != 1)
                throw new IndexOutOfRangeException("Binary array access requires exactly one subscript.");
            binary.Set(indices[0], newValue);
            return;
        }

        LSArrayRuntime.Set(value, newValue, indices);
    }

    public static int LBound(object? value, int dimension = 1) => value switch
    {
        XPScriptBinaryArray binary => binary.LBound(dimension),
        _ => LSArrayRuntime.LBound(value, dimension)
    };

    public static int UBound(object? value, int dimension = 1) => value switch
    {
        XPScriptBinaryArray binary => binary.UBound(dimension),
        _ => LSArrayRuntime.UBound(value, dimension)
    };
}
""";
}
