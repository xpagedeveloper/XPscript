using System.Globalization;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>Shared typed allocation for local and persistent array storage.</summary>
internal static class BoundArrayAllocation
{
    internal static string Emit(LocalSymbol local, bool empty = false, int? upper = null)
    {
        var element = local.SemanticType?.ElementType ?? XpTypeSymbol.FromClr(local.Type.GetElementType() ?? typeof(object));
        var elementType = BoundMethodEmitter.CSharpType(element);
        if (empty || local.IsDynamicArray) return $"new {elementType}[0]";
        if (local.ArrayRank > 1)
        {
            var type = BoundMethodEmitter.CSharpType(local.SemanticType ?? XpTypeSymbol.FromClr(local.Type));
            var lengths = string.Join(", ", local.ArrayBounds.Select(bound => (bound.Upper - bound.Lower + 1).ToString(CultureInfo.InvariantCulture)));
            var lowers = string.Join(", ", local.ArrayBounds.Select(bound => bound.Lower.ToString(CultureInfo.InvariantCulture)));
            return local.ArrayBounds.Any(bound => bound.Lower != 0)
                ? $"({type})System.Array.CreateInstance(typeof({elementType}), new[] {{ {lengths} }}, new[] {{ {lowers} }})"
                : $"new {elementType}[{lengths}]";
        }
        var length = ((local.ArrayBounds.Count > 0 ? local.ArrayBounds[0].Upper : upper ?? local.ArrayLength ?? 0) - local.ArrayLowerBound + 1)
            .ToString(CultureInfo.InvariantCulture);
        return element.RuntimeType == typeof(string)
            ? $"System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(string.Empty, {length}))"
            : $"new {elementType}[{length}]";
    }
}
