namespace XPScript.Compiler;

internal static class NotesRuntimeMemberTrapPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // LotusScript exposes NotesDocument.UniversalID with this exact spelling.
        // Keep the CLR dynamic surface exact so misspellings/casing differences fall through
        // to the Notes-specific runtime member trap below instead of leaking RuntimeBinderException.
        source = source.Replace("public string UniversalId", "public string UniversalID", StringComparison.Ordinal);
        source = source.Replace(".UniversalId", ".UniversalID", StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            "    public static XPScriptNotesSession CreateSession(object? runtimeDirectory) =>\n        new(XPScriptRuntime.CStr(runtimeDirectory), null, null);",
            """
    public static object CreateSession()
    {
        var runtimeDirectory = TryResolveDefaultRuntimeDirectory();
        return runtimeDirectory is null
            ? NothingValue
            : new XPScriptNotesSession(runtimeDirectory, null, null);
    }

    private static string? TryResolveDefaultRuntimeDirectory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\HCL\Notes\Installer");
                var raw = key?.GetValue("PROGDIR") as string;
                if (string.IsNullOrWhiteSpace(raw)) return null;

                var expanded = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
                var path = Path.GetFullPath(expanded);
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "nnotes.dll"))) return null;
                return path;
            }

            if (OperatingSystem.IsMacOS())
            {
                const string path = "/Applications/HCL Notes.app/Contents/MacOS";
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "libnotes.dylib")) &&
                    !File.Exists(Path.Combine(path, "libnotes64.dylib"))) return null;
                return path;
            }

            return null;
        }
        catch
        {
            return null;
        }
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
        result = null;
        throw UnknownMember(binder.Name);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object? value)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: true)) return false;
        throw UnknownMember(binder.Name);
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (HasExactPublicMethod(binder.Name))
        {
            result = null;
            return false;
        }
        result = null;
        throw UnknownMember(binder.Name);
    }

    private bool HasExactPublicProperty(string name, bool requireSetter)
    {
        var property = GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return property is not null && (!requireSetter || property.SetMethod is not null);
    }

    private bool HasExactPublicMethod(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal));

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
        result = null;
        throw UnknownMember(binder.Name);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object? value)
    {
        if (HasExactPublicProperty(binder.Name, requireSetter: true)) return false;
        throw UnknownMember(binder.Name);
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (HasExactPublicMethod(binder.Name))
        {
            result = null;
            return false;
        }
        result = null;
        throw UnknownMember(binder.Name);
    }

    private bool HasExactPublicProperty(string name, bool requireSetter)
    {
        var property = GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return property is not null && (!requireSetter || property.SetMethod is not null);
    }

    private bool HasExactPublicMethod(string name) =>
        GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal));

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
