namespace XPScript.Compiler;

internal static class NotesEmbeddedBinaryArrayFixPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        const string oldValue = """
internal static class XPScriptNotesBinaryArrayFactory
{
    public static LSArray Create(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) return new LSArray("Byte", true);

        // API-produced binary arrays may exceed the normal LotusScript subscript range.
        // Construct the LSArray state directly while preserving normal ReDim validation.
        var array = new LSArray("Byte", true);
        var type = typeof(LSArray);
        var data = new object?[bytes.Length];
        for (var i = 0; i < bytes.Length; i++) data[i] = bytes[i];

        type.GetField("_data", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(array, data);
        type.GetProperty("LowerBounds")!
            .SetValue(array, new[] { 0 });
        type.GetProperty("UpperBounds")!
            .SetValue(array, new[] { bytes.Length - 1 });
        type.GetProperty("IsAllocated")!
            .SetValue(array, true);
        type.GetProperty("Lengths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(array, new[] { bytes.Length });
        return array;
    }
}
""";
        const string newValue = """
internal static class XPScriptNotesBinaryArrayFactory
{
    public static XPScriptBinaryArray Create(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new XPScriptBinaryArray(bytes);
    }
}
""";
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes embedded binary-array fix.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
