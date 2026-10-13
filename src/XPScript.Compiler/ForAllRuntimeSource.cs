namespace XPScript.Compiler;

/// <summary>The normal/AST enumerable contract, including native LSArray values.</summary>
internal static class ForAllRuntimeSource
{
    public const string Code = """
internal static class LSForAllRuntime
{
    public static System.Collections.IEnumerable Enumerate(object? value)
    {
        if (value is null) yield break;
        if (value is string) throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
        if (value is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "ForAll currently supports one-dimensional arrays.");
            for (var i = array.LBound(); i <= array.UBound(); i++) yield return array.Get(new object?[] { i });
            yield break;
        }
        if (value is System.Array managed && managed.Rank != 1)
            throw new XPScriptRuntimeException(13, "ForAll currently supports one-dimensional arrays.");
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
            yield break;
        }
        throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
    }
}
""";
}
