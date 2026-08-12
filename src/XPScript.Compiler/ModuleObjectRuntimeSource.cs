namespace XPScript.Compiler;

public static class ModuleObjectRuntimeSource
{
    public const string Code = """
internal static class XPModuleObjectRuntime
{
    private sealed class ReferenceCell
    {
        public LSObjectBase? Value;
    }

    private static readonly Dictionary<string, ReferenceCell> _references =
        new(StringComparer.OrdinalIgnoreCase);

    public static void SetNew(string name, string className, params object?[] args)
    {
        var type = ResolveClass(className);
        var instance = Activator.CreateInstance(type, args) as LSObjectBase
            ?? throw new InvalidOperationException($"Unable to create XPScript class '{className}'.");
        _references[name] = new ReferenceCell { Value = instance };
    }

    public static void Assign(string destination, string source)
    {
        _references[destination] = GetCell(source);
    }

    public static void Clear(string name)
    {
        _references[name] = new ReferenceCell();
    }

    public static void Delete(string name)
    {
        var cell = GetCell(name);
        var value = cell.Value;
        if (value is null) return;
        value.__Delete();
        cell.Value = null;
    }

    public static dynamic Value(string name)
    {
        var value = GetCell(name).Value;
        if (value is null || value.__IsDeleted)
            throw new InvalidOperationException($"Object variable '{name}' is Nothing or deleted.");
        return value;
    }

    public static bool IsNothing(string name)
    {
        if (!_references.TryGetValue(name, out var cell)) return true;
        return cell.Value is null || cell.Value.__IsDeleted;
    }

    public static bool IsSame(string left, string right)
    {
        if (!_references.TryGetValue(left, out var a) || !_references.TryGetValue(right, out var b))
            return false;
        return ReferenceEquals(a, b) && a.Value is not null && !a.Value.__IsDeleted;
    }

    private static ReferenceCell GetCell(string name)
    {
        if (!_references.TryGetValue(name, out var cell))
        {
            cell = new ReferenceCell();
            _references[name] = cell;
        }
        return cell;
    }

    private static Type ResolveClass(string className)
    {
        var assembly = typeof(XPModuleObjectRuntime).Assembly;
        var type = assembly.GetTypes().FirstOrDefault(t =>
            t.Name.Equals(className, StringComparison.OrdinalIgnoreCase) &&
            typeof(LSObjectBase).IsAssignableFrom(t));
        return type ?? throw new InvalidOperationException($"Unknown XPScript class '{className}'.");
    }
}
""";
}
