namespace XPScript.Compiler;

internal static class CaseInsensitiveDynamicObjectRuntimeSource
{
    public const string Code = """
internal abstract class XPScriptCaseInsensitiveDynamicObject : System.Dynamic.DynamicObject
{
    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object? result)
    {
        var property = FindProperty(binder.Name, requireSetter: false);
        if (property is null)
        {
            result = null;
            throw UnknownMember(binder.Name);
        }

        result = property.GetValue(this);
        return true;
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object? value)
    {
        var property = FindProperty(binder.Name, requireSetter: true);
        if (property is null)
            throw UnknownMember(binder.Name);

        property.SetValue(this, value);
        return true;
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var values = args ?? Array.Empty<object?>();
        var methods = GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(method => string.Equals(method.Name, binder.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (methods.Length == 0 && values.Length == 0)
        {
            var property = FindProperty(binder.Name, requireSetter: false);
            if (property is not null)
            {
                result = property.GetValue(this);
                return true;
            }
        }

        if (methods.Length == 0)
        {
            result = null;
            throw UnknownMember(binder.Name);
        }

        try
        {
            result = GetType().InvokeMember(
                methods[0].Name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                this,
                values);
            return true;
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private System.Reflection.PropertyInfo? FindProperty(string name, bool requireSetter) =>
        GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (!requireSetter || property.SetMethod is not null));

    private XPScriptRuntimeException UnknownMember(string name) =>
        new(438, "Unknown property or method '" + name + "' of " + GetType().Name.Replace("XPScript", "", StringComparison.Ordinal) + ".");
}
""";
}
