using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>Typed Application namespaces over the shared runtime, not a dynamic substitute.</summary>
internal static class AstApplicationSupport
{
    private static readonly Dictionary<string, string> Owners = new()
    {
        ["XPScriptApplicationRuntime"] = "Application", ["XPScriptApplicationExecutableRuntime"] = "Application.Executable",
        ["XPScriptStateProxy"] = "Application.State", ["XPScriptApplicationDebugRuntime"] = "Application.Debug",
        ["XPScriptApplicationRegistryRuntime"] = "Application.Registry", ["XPScriptApplicationRegistryStore"] = "Application.Registry.Store",
        ["XPScriptApplicationCryptoRuntime"] = "Application.Crypto", ["XPScriptApplicationLogRuntime"] = "Application.Log",
        ["XPScriptApplicationAuditRuntime"] = "Application.Audit", ["XPScriptApplicationSystemLogRuntime"] = "Application.SystemLog",
        ["XPScriptApplicationSecretsEncryptionRuntime"] = "Application.Secrets", ["XPScriptApplicationSecretsManagementRuntime"] = "Application.Secrets",
        ["XPScriptApplicationSecretsBackupRuntime"] = "Application.Secrets"
    };
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(ApplicationRuntimeSource.Code).GetRoot()
        .DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "LSArray" or "string[]" => "Variant", "int" or "long" => "Long", "bool" => "Boolean", "string" => "String",
        _ => Owners.GetValueOrDefault(type.TrimEnd('?'), type.TrimEnd('?'))
    };
    internal static bool OwnsSymbol(string name) => name.Equals("Application", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Application.", StringComparison.OrdinalIgnoreCase);
    private static readonly AstNativeObjectSurface.Member[] Members = ReadMembers().GroupBy(m => m.Owner + "." + m.Name + ":" + m.IsProperty + ":" + string.Join(",", m.Parameters)).Select(g => g.First()).ToArray();
    private static IEnumerable<AstNativeObjectSurface.Member> ReadMembers()
    {
        foreach (var cls in Classes.Where(c => Owners.ContainsKey(c.Identifier.ValueText)))
        {
            var owner = Owners[cls.Identifier.ValueText];
            foreach (var member in AstNativeObjectSurface.ReadMembers([cls], [owner], LanguageType))
            {
                if (owner == "Application" && member.Name == "SetArgs") continue; // compiler bootstrap only
                // Backup.Get/Set are storage helpers, not the public encrypted-secret facade.
                if (cls.Identifier.ValueText == "XPScriptApplicationSecretsBackupRuntime" && member.Name is not ("Backup" or "Restore")) continue;
                var name = owner == "Application" && member.Name == "Arg" ? "Args" : member.Name;
                var result = LanguageType(member.Result) == "Variant" ? "object?" : member.Result;
                yield return member with { Name = name, Result = result, NativeTarget = member.IsStatic ? cls.Identifier.ValueText + "." + member.Name : null };
            }
        }
        foreach (var (name, type) in new[] { ("Executable", "Application.Executable"), ("Registry", "Application.Registry"), ("Secrets", "Application.Secrets"),
            ("Crypto", "Application.Crypto"), ("Log", "Application.Log"), ("Audit", "Application.Audit"), ("Debug", "Application.Debug"), ("SystemLog", "Application.SystemLog") })
            yield return new("Application", name, type, [], [], true, true, false, "XPScriptApplicationRuntime.Namespace");
        yield return new("Application", "Args", "object?", [], [], true, true, false, "XPScriptApplicationRuntime.Args()");
        yield return new("Application", "IsDebugging", "bool", [], [], true, true, false, "XPScriptDebugRuntime.IsEnabled");
        foreach (var name in new[] { "Process", "Session", "Request" })
            yield return new(name, "State", "XPScriptStateProxy", [], [], true, true, false, "XPScript" + name + "Runtime.State");
    }
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog([], Members, LanguageType);
    internal static void Import(SymbolTable symbols)
    {
        AstNativeObjectSurface.Import(symbols, Members, LanguageType);
        foreach (var name in new[] { "Application", "Process", "Session", "Request" })
            symbols.Declare(new VariableSymbol(name, typeof(object), XpTypeSymbol.User(name)));
    }
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) &&
        m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && m.IsProperty && !m.Writable);
    internal static string? PropertyTarget(string owner, string name) => Members.FirstOrDefault(m => m.IsProperty &&
        m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.NativeTarget;
    internal static string ConvertPropertyValue(string owner, string name, string value) =>
        Members.FirstOrDefault(m => m.IsProperty && m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } member
            ? AstImageSupport.ConvertArgument(value, member.Result) : value;
    internal static string? RuntimeType(string name) => name switch
    {
        "Application.State" => "XPScriptStateProxy", "Application.Registry.Store" => "XPScriptApplicationRegistryStore",
        _ => OwnsSymbol(name) ? "object" : null
    };
    internal static string MemberName(string owner, string name) => Members.FirstOrDefault(m => m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) &&
        m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool UsesApplication(string source) => AstObjectSupportRegistry.UsesObject(source, "Application") ||
        new[] { "Process", "Session", "Request" }.Any(n => Uses(source, n + ".State"));
    private static bool Uses(string source, string name) => AstObjectSupportRegistry.UsesObject(source, name);
    internal static string RuntimeCode(string source)
    {
        if (!UsesApplication(source)) return "";
        var code = ApplicationRuntimeSource.CoreCode;
        if (Uses(source, "Application.Secrets")) code += ApplicationPersistenceRuntimeSource.Code;
        else if (Uses(source, "Application.Registry")) code += string.Join(Environment.NewLine,
            Classes.Where(c => c.Identifier.ValueText is "XPScriptApplicationRegistryRuntime" or "XPScriptApplicationRegistryStore").Select(c => c.ToFullString()));
        if (Uses(source, "Application.Crypto") || Uses(source, "Application.Secrets")) code += ApplicationCryptoRuntimeSource.Code;
        if (Uses(source, "Application.Secrets")) code += ApplicationSecretsEncryptionRuntimeSource.Code + ApplicationSecretsManagementRuntimeSource.Code + ApplicationSecretsBackupRuntimeSource.Code;
        if (Uses(source, "Application.Log") || Uses(source, "Application.Audit")) code += ApplicationLogRuntimeSource.Code;
        if (Uses(source, "Application.Debug")) code += ApplicationDebugRuntimeSource.Code;
        if (Uses(source, "Application.SystemLog")) code += Classes.Single(c => c.Identifier.ValueText == "XPScriptApplicationSystemLogRuntime").ToFullString();
        if (Uses(source, "Application.IsDebugging")) code += SourceLineRuntimeSource.Code;
        return code;
    }
    internal static string BuildMarkers(string source, string path) => string.Join(Environment.NewLine, source.Replace("\r\n", "\n").Split('\n')
        .Select((line, index) => SourceLineMarkerPreprocessor.BuildApplicationExecutableMetadata(line, path, index + 1))
        .Where(marker => marker is not null).Select(marker => "// " + marker));
}
