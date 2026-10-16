using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

internal static class AstClassConstructorSupport
{
    internal static void Register(IReadOnlyDictionary<string, ClassDeclarationSyntax> classes, SymbolTable symbols, string path, string source)
    {
        foreach (var declaration in classes.Values)
        {
            var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
            if (constructors.Length > 1)
                throw AstExperimentalCompiler.MappedDiagnostic(constructors[1].Span, path, source,
                    "Constructor overloads are not implemented by the AST compiler yet.", "XPS3001", "ast");
            var constructor = constructors.SingleOrDefault();
            if (constructor is not null && declaration.BaseType is not null)
                throw AstExperimentalCompiler.MappedDiagnostic(constructor.Span, path, source,
                    "Derived constructor bodies and Parent.New forwarding are not implemented by the AST compiler yet.", "XPS3001", "ast");
            var parameters = constructor?.Parameters ?? Array.Empty<ParameterSyntax>();
            foreach (var parameter in parameters)
                if (parameter.IsByRef || parameter.IsOptional || parameter.IsList || parameter.DefaultValue is not null ||
                    AstExperimentalCompiler.ResolveRuntimeType(parameter.Type?.Identifier.Text) == typeof(object))
                    throw AstExperimentalCompiler.MappedDiagnostic(parameter.Span, path, source,
                        "AST class constructors currently require non-Optional scalar ByVal parameters.", "XPS3001", "ast");
            var types = parameters.Select(p => AstClassMethodSupport.DeclaredType(p.Type, symbols)).ToArray();
            symbols.Declare(new FunctionSymbol(declaration.Identifier.Text + ".New", typeof(object), types.Select(t => t.RuntimeType).ToArray(),
                XpTypeSymbol.User(declaration.Identifier.Text), types)
            {
                IsUserConstructor = true,
                IsPrivateConstructor = constructor?.Visibility?.Kind == SyntaxKind.PrivateKeyword
            });
        }
        foreach (var declaration in classes.Values)
            if (declaration.BaseType is { } parent)
            {
                var constructor = Find(symbols, parent.Identifier.Text)!;
                if (constructor.ParameterTypes.Count > 0 || constructor.IsPrivateConstructor)
                    throw AstExperimentalCompiler.MappedDiagnostic(parent.Span, path, source,
                        "A base constructor requiring arguments or private access needs constructor forwarding, which is not implemented by this AST increment.", "XPS3001", "ast");
            }
    }

    internal static FunctionSymbol? Find(SymbolTable symbols, string name) =>
        symbols.LookupAll(name + ".New").OfType<FunctionSymbol>().FirstOrDefault(s => s.IsUserConstructor);

    internal static string Emit(ClassDeclarationSyntax declaration, string imageCleanup)
    {
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>().SingleOrDefault();
        var name = "Xp" + declaration.Identifier.Text;
        if (constructor is null) return $"public {name}() {{ }}";
        var parameters = string.Join(", ", constructor.Parameters.Select(p =>
            BoundMethodEmitter.CSharpType(AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)) + " " + p.Identifier.Text));
        var arguments = string.Join(", ", constructor.Parameters.Select(p => p.Identifier.Text));
        var visibility = constructor.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private" : "public";
        // A failed construction owns its fields, but must not invoke user Delete.
        return $"{visibility} {name}({parameters}) {{ try {{ __xps_constructor_body({arguments}); }} catch {{ {imageCleanup} throw; }} }}";
    }
}
