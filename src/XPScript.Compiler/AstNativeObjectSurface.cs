using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>Compiler metadata derived from existing native runtime sources, never a second runtime.</summary>
internal static class AstNativeObjectSurface
{
    internal sealed record Member(string Owner, string Name, string Result, string[] Parameters,
        string[] ArgumentNames, bool IsStatic, bool IsProperty, bool Writable, string? NativeTarget = null, bool ReceiverArgument = false);

    internal static XpTypeSymbol SemanticType(string type, Func<string, string> languageType) => type.TrimEnd('?') switch
    {
        "object" or "byte[]" => XpTypeSymbol.Variant,
        "int" or "long" => XpTypeSymbol.FromClr(typeof(long)),
        "double" or "float" => XpTypeSymbol.FromClr(typeof(double)),
        "string" => XpTypeSymbol.FromClr(typeof(string)), "bool" => XpTypeSymbol.FromClr(typeof(bool)),
        "void" => XpTypeSymbol.FromClr(typeof(void)), _ => XpTypeSymbol.User(languageType(type.TrimEnd('?')))
    };

    internal static IEnumerable<Member> ReadMembers(ClassDeclarationSyntax[] classes, string[] names,
        Func<string, string> languageType)
    {
        foreach (var owner in classes.Where(c =>
            names.Contains(c.Identifier.ValueText, StringComparer.OrdinalIgnoreCase) ||
            names.Contains(languageType(c.Identifier.ValueText), StringComparer.OrdinalIgnoreCase)))
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (ClassDeclarationSyntax? current = owner; current is not null; current = classes.FirstOrDefault(c =>
                c.Identifier.ValueText == current.BaseList?.Types.FirstOrDefault()?.Type.ToString()))
            {
                foreach (var p in current.Members.OfType<PropertyDeclarationSyntax>().Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword)))
                    if (seen.Add(p.Identifier.ValueText))
                        yield return new(languageType(owner.Identifier.ValueText), p.Identifier.ValueText, p.Type.ToString(), [], [],
                            p.Modifiers.Any(SyntaxKind.StaticKeyword), true, p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) && !a.Modifiers.Any()) == true);
                foreach (var m in current.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Identifier.ValueText != "GetEnumerator"))
                    foreach (var member in ReadMethod(languageType(owner.Identifier.ValueText), m))
                        if (seen.Add(member.Name + "(" + string.Join(",", member.Parameters) + ")")) yield return member;
            }
        }
    }

    internal static IEnumerable<Member> ReadMethod(string owner, MethodDeclarationSyntax method, string? name = null, string? nativeTarget = null)
    {
        var parameters = method.ParameterList.Parameters;
        var required = parameters.Count(p => p.Default is null);
        for (var count = required; count <= parameters.Count; count++)
            yield return new(owner, name ?? method.Identifier.ValueText, method.ReturnType.ToString(),
                parameters.Take(count).Select(p => p.Type!.ToString()).ToArray(), parameters.Take(count).Select(p => p.Identifier.ValueText).ToArray(),
                method.Modifiers.Any(SyntaxKind.StaticKeyword), false, false, nativeTarget);
    }

    internal static IEnumerable<CompilerSymbolDefinition> Catalog(string[] names, IEnumerable<Member> members, Func<string, string> languageType) =>
        names.Select(name => new CompilerSymbolDefinition(name, "class", name, [], null, [], "api." + name))
        .Concat(members.GroupBy(m => m.Owner.Length == 0 ? m.Name : m.Owner + "." + m.Name).Select(group =>
        {
            var member = group.Last();
            var signature = string.Join("; ", group.Select(m => group.Key + (m.IsProperty ? "" : "(" + string.Join(", ",
                m.ArgumentNames.Zip(m.Parameters, (n, t) => n + " As " + languageType(t))) + ")") +
                (m.Result == "void" ? "" : " As " + languageType(m.Result))));
            return new CompilerSymbolDefinition(group.Key, member.IsProperty ? "property" : "method", signature,
                member.ArgumentNames.Zip(member.Parameters, (n, t) => new CompilerSymbolParameter(n, languageType(t))).ToArray(),
                member.Result == "void" ? null : languageType(member.Result), [], "api." + group.Key);
        }));

    internal static void Import(SymbolTable symbols, IEnumerable<Member> members, Func<string, string> languageType)
    {
        foreach (var member in members)
        {
            var name = member.Owner.Length == 0 ? member.Name : member.Owner + "." + member.Name;
            var result = SemanticType(member.Result, languageType);
            if (member.IsProperty) symbols.Declare(new PropertySymbol(name, result.RuntimeType, result));
            else
            {
                var args = member.Parameters.Select(t => SemanticType(t, languageType)).ToArray();
                symbols.Declare(new FunctionSymbol(name, result.RuntimeType, args.Select(a => a.RuntimeType).ToArray(), result, args)
                {
                    NativeParameterTypes = member.Parameters, IsNativeStatic = member.IsStatic, NativeTargetName = member.NativeTarget, NativeReceiverArgument = member.ReceiverArgument
                });
            }
        }
    }
}
