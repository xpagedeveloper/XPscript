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
    }

    internal static FunctionSymbol? Find(SymbolTable symbols, string name) =>
        symbols.LookupAll(name + ".New").OfType<FunctionSymbol>().FirstOrDefault(s => s.IsUserConstructor);

    private static CallExpressionSyntax? ParentCall(StatementSyntax statement) =>
        (statement is CallStatementSyntax call ? call.Expression : (statement as ExpressionStatementSyntax)?.Expression)
        is CallExpressionSyntax { Target: MemberAccessExpressionSyntax { Expression: NameExpressionSyntax receiver } member } invocation &&
        receiver.IdentifierToken.Text.Equals("Parent", StringComparison.OrdinalIgnoreCase) &&
        member.NameToken.Text.Equals("New", StringComparison.OrdinalIgnoreCase) ? invocation : null;

    internal static IReadOnlyList<StatementSyntax> Body(ClassDeclarationSyntax declaration, ConstructorDeclarationSyntax constructor,
        string path, string source)
    {
        var calls = AstExperimentalCompiler.FlattenStatements(constructor.Statements).Where(s => ParentCall(s) is not null).ToArray();
        if (calls.Length > 0 && (declaration.BaseType is null || calls.Length != 1 ||
            constructor.Statements.Count == 0 || !ReferenceEquals(calls[0], constructor.Statements[0])))
            throw AstExperimentalCompiler.MappedDiagnostic(calls[^1].Span, path, source,
                "Parent.New must occur exactly once as the first statement of a derived constructor.", "XPS1012", "semantic");
        return calls.Length == 0 ? constructor.Statements : constructor.Statements.Skip(1).ToArray();
    }

    internal static string Emit(ClassDeclarationSyntax declaration, SymbolTable symbols, string path, string source)
    {
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>().SingleOrDefault();
        var name = "Xp" + declaration.Identifier.Text;
        if (constructor is not null) Body(declaration, constructor, path, source);
        var initializer = string.Empty;
        if (declaration.BaseType is { } parent)
        {
            var call = constructor?.Statements.FirstOrDefault() is { } first ? ParentCall(first) : null;
            // Initializer arguments see constructor parameters and module declarations,
            // never Me, fields or body locals: the instance does not exist yet.
            var scope = symbols.CreateChildScope();
            foreach (var parameter in constructor?.Parameters ?? Array.Empty<ParameterSyntax>())
                scope.Declare(new ParameterSymbol(parameter.Identifier.Text,
                    AstExperimentalCompiler.ResolveRuntimeType(parameter.Type?.Identifier.Text), false,
                    AstClassMethodSupport.DeclaredType(parameter.Type, symbols)));
            var binder = new ExpressionBinder(scope);
            var syntax = new NewExpressionSyntax(new SyntaxToken(SyntaxKind.NewKeyword, "New", null, call?.Span ?? parent.Span),
                parent.Identifier, call?.OpenParenToken, call?.Arguments ?? [], call?.CommaTokens ?? [], call?.CloseParenToken);
            var expression = binder.Bind(syntax);
            if (binder.Diagnostics.Count > 0)
                throw AstExperimentalCompiler.SyntaxDiagnosticException(binder.Diagnostics[0], path, source);
            var bound = (BoundNewExpression)expression;
            initializer = " : base(" + string.Join(", ", bound.Arguments.Select(new BoundExpressionEmitter().Emit)) + ")";
        }
        if (constructor is null) return $"public {name}(){initializer} {{ }}";
        var parameters = string.Join(", ", constructor.Parameters.Select(p =>
            BoundMethodEmitter.CSharpType(AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)) + " " + p.Identifier.Text));
        var arguments = string.Join(", ", constructor.Parameters.Select(p => p.Identifier.Text));
        var visibility = constructor.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private" : "public";
        // A failed construction owns its fields, but must not invoke user Delete.
        return $"{visibility} {name}({parameters}){initializer} {{ try {{ __xps_constructor_body({arguments}); }} catch {{ __xps_cleanup_failed_construction(); throw; }} }}";
    }
}
