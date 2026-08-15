namespace XPScript.Compiler;

public static class VariantIndexRuntimeSource
{
    public const string Code = """
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

        return LSArrayRuntime.Get(value, indices);
    }
}
""";
}
