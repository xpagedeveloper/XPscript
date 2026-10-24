namespace XPScript.Compiler;

internal static class NotesRuntimeMemberTrapPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // LotusScript exposes NotesDocument.UniversalID with this exact spelling.
        // Preserve that CLR spelling while keeping XPScript member lookup case-insensitive.
        source = source.Replace("public string UniversalId", "public string UniversalID", StringComparison.Ordinal);
        source = source.Replace(".UniversalId", ".UniversalID", StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            "    public static XPScriptNotesSession CreateSession(object? runtimeDirectory) =>\n        new(XPScriptRuntime.CStr(runtimeDirectory), null, null);",
            """
    public static object CreateSession()
    {
        var installation = TryResolveDefaultRuntime();
        return installation is null
            ? NothingValue
            : new XPScriptNotesSession(installation.Value.ProgramDirectory, installation.Value.NotesIni, null);
    }

    private static (string ProgramDirectory, string? NotesIni)? TryResolveDefaultRuntime()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var keyPaths = new[]
                {
                    @"Software\HCL\Notes\Installer",
                    @"Software\Lotus\Notes\Installer",
                    @"Software\WOW6432Node\HCL\Notes\Installer",
                    @"Software\WOW6432Node\Lotus\Notes\Installer"
                };
                foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
                foreach (var keyPath in keyPaths)
                using (var key = hive.OpenSubKey(keyPath))
                {
                    if (key is null) continue;
                    var program = ResolveRegistryPath(key.GetValue("PROGDIR") as string);
                    if (program is null || !Directory.Exists(program)) continue;
                    if (!File.Exists(Path.Combine(program, "nnotes.dll"))) continue;

                    var data = ResolveRegistryPath(key.GetValue("DATADIR") as string);
                    var notesIni = ResolveRegistryPath(key.GetValue("NOTESINI") as string)
                        ?? ResolveRegistryPath(key.GetValue("NOTES.INI") as string)
                        ?? (data is null ? null : Path.Combine(data, "notes.ini"));
                    if (notesIni is not null && !File.Exists(notesIni)) notesIni = null;
                    return (program, notesIni);
                }
                return null;
            }

            if (OperatingSystem.IsMacOS())
            {
                const string path = "/Applications/HCL Notes.app/Contents/MacOS";
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "libnotes.dylib")) &&
                    !File.Exists(Path.Combine(path, "libnotes64.dylib"))) return null;
                return (path, null);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveRegistryPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('\"'));
            return Path.GetFullPath(expanded);
        }
        catch { return null; }
    }

    public static XPScriptNotesSession CreateSession(object? runtimeDirectory) =>
        new(XPScriptRuntime.CStr(runtimeDirectory), null, null);
""",
            "parameterless-session-autodetect");

        source = ReplaceRequired(
            source,
            "internal abstract class XPScriptNotesObject : IDisposable",
            "internal abstract class XPScriptNotesObject : System.Dynamic.DynamicObject, IDisposable",
            "notes-object-dynamic-base");

        source = ReplaceRequired(
            source,
            "    protected abstract void ReleaseNative();",
            """
    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object? result)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: false))
        {
            result = null;
            return false;
        }

        var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: false);
        if (property is not null)
        {
            result = property.GetValue(this);
            return true;
        }

        result = null;
        throw UnknownMember(binder.Name);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object? value)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: true)) return false;

        var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: true);
        if (property is not null)
        {
            property.SetValue(this, value);
            return true;
        }

        throw UnknownMember(binder.Name);
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (HasExactPublicMethod(binder.Name))
        {
            result = null;
            return false;
        }

        var values = args ?? Array.Empty<object?>();
        if (values.Length == 0)
        {
            var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: false);
            if (property is not null)
            {
                result = property.GetValue(this);
                return true;
            }
        }

        if (HasPublicMethodIgnoreCase(binder.Name))
        {
            try
            {
                result = GetType().InvokeMember(
                    binder.Name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.IgnoreCase |
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

        result = null;
        throw UnknownMember(binder.Name);
    }

    private bool HasExactPublicProperty(string name, bool requireSetter)
    {
        var property = GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return property is not null && (!requireSetter || property.SetMethod is not null);
    }

    private System.Reflection.PropertyInfo? FindPublicPropertyIgnoreCase(string name, bool requireSetter) =>
        GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (!requireSetter || property.SetMethod is not null));

    private bool HasExactPublicMethod(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal));

    private bool HasPublicMethodIgnoreCase(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase));

    private XPScriptRuntimeException UnknownMember(string name) =>
        new(438, "Unknown property or method '" + name + "' of " +
            GetType().Name.Replace("XPScript", "", StringComparison.Ordinal) + ".");

    protected abstract void ReleaseNative();
""",
            "notes-object-member-trap");

        source = ReplaceRequired(
            source,
            "internal sealed class XPScriptNotesSession : IDisposable",
            "internal sealed class XPScriptNotesSession : System.Dynamic.DynamicObject, IDisposable",
            "notes-session-dynamic-base");

        source = ReplaceRequired(
            source,
            "    private static void RecycleActiveSessionAtProcessExit()",
            """
    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object? result)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: false))
        {
            result = null;
            return false;
        }

        var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: false);
        if (property is not null)
        {
            result = property.GetValue(this);
            return true;
        }

        result = null;
        throw UnknownMember(binder.Name);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object? value)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: true)) return false;

        var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: true);
        if (property is not null)
        {
            property.SetValue(this, value);
            return true;
        }

        throw UnknownMember(binder.Name);
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (HasExactPublicMethod(binder.Name))
        {
            result = null;
            return false;
        }

        var values = args ?? Array.Empty<object?>();
        if (values.Length == 0)
        {
            var property = FindPublicPropertyIgnoreCase(binder.Name, requireSetter: false);
            if (property is not null)
            {
                result = property.GetValue(this);
                return true;
            }
        }

        if (HasPublicMethodIgnoreCase(binder.Name))
        {
            try
            {
                result = GetType().InvokeMember(
                    binder.Name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.IgnoreCase |
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

        result = null;
        throw UnknownMember(binder.Name);
    }

    private bool HasExactPublicProperty(string name, bool requireSetter)
    {
        var property = GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return property is not null && (!requireSetter || property.SetMethod is not null);
    }

    private System.Reflection.PropertyInfo? FindPublicPropertyIgnoreCase(string name, bool requireSetter) =>
        GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (!requireSetter || property.SetMethod is not null));

    private bool HasExactPublicMethod(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal));

    private bool HasPublicMethodIgnoreCase(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase));

    private XPScriptRuntimeException UnknownMember(string name) =>
        new(438, "Unknown property or method '" + name + "' of NotesSession.");

    private static void RecycleActiveSessionAtProcessExit()
""",
            "notes-session-member-trap");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes runtime member trap (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
