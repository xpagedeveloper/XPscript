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
            var signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var constructor in constructors.Length == 0 ? [null] : constructors.Cast<ConstructorDeclarationSyntax?>())
            {
                var parameters = constructor?.Parameters ?? Array.Empty<ParameterSyntax>();
                AstExperimentalCompiler.ValidateOptionalDeclarations(parameters, path, source);
                var duplicate = parameters.GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1)?.Skip(1).FirstOrDefault();
                if (duplicate is not null)
                    throw AstExperimentalCompiler.MappedDiagnostic(duplicate.Span, path, source,
                        "A constructor parameter name must be unique.", CompilerDiagnosticCodes.DuplicateOverload, "semantic");
                foreach (var parameter in parameters)
                {
                    var type = AstClassMethodSupport.DeclaredType(parameter.Type, symbols);
                    var sharedReference = type.IsObject || classes.ContainsKey(type.Name);
                    if (parameter.IsList || type.RuntimeType == typeof(object) &&
                        !sharedReference)
                        throw AstExperimentalCompiler.MappedDiagnostic(parameter.Span, path, source,
                            "AST constructors support scalar and Object/user-class parameters; native reference, Variant and List signatures are not implemented yet.", "XPS3001", "ast");
                }
                var types = parameters.Select(p => AstClassMethodSupport.DeclaredType(p.Type, symbols)).ToArray();
                var signature = string.Join(";", types.Select((type, i) => $"{type.Shape}:{type.Name}:{type.ArrayRank}:{parameters[i].IsByRef}"));
                if (!signatures.Add(signature))
                    throw AstExperimentalCompiler.MappedDiagnostic(constructor!.Span, path, source,
                        "Duplicate constructor parameter signature.", CompilerDiagnosticCodes.DuplicateOverload, "semantic");
                symbols.Declare(new FunctionSymbol(declaration.Identifier.Text + ".New", typeof(object), types.Select(t => t.RuntimeType).ToArray(),
                    XpTypeSymbol.User(declaration.Identifier.Text), types, parameters.Select(parameter => parameter.IsByRef).ToArray())
                {
                    IsUserConstructor = true,
                    RequiredConstructorArgumentCount = parameters.TakeWhile(parameter => !parameter.IsOptional).Count(),
                    IsPrivateConstructor = constructor?.Visibility?.Kind == SyntaxKind.PrivateKeyword,
                    UserConstructorTag = constructors.Length > 1 ? "__xps_ctor" + Suffix(declaration, constructor!) : null
                });
            }
        }
    }

    internal static FunctionSymbol? Find(SymbolTable symbols, string name) =>
        FindAll(symbols, name).FirstOrDefault();

    internal static FunctionSymbol[] FindAll(SymbolTable symbols, string name) =>
        symbols.LookupAll(name + ".New").OfType<FunctionSymbol>().Where(s => s.IsUserConstructor).ToArray();

    internal static string Suffix(ClassDeclarationSyntax declaration, ConstructorDeclarationSyntax constructor) =>
        declaration.Members.OfType<ConstructorDeclarationSyntax>().Skip(1).Any() ? "_" + constructor.Span.Start : "";

    internal static string Arguments(FunctionSymbol constructor, IReadOnlyList<BoundExpression> arguments, BoundExpressionEmitter emitter) =>
        TaggedArguments(constructor.UserConstructorTag, "Xp" + constructor.SemanticReturnType!.Name,
            emitter.EmitReferenceArguments(arguments, constructor.ByRefParameters));

    private static string TaggedArguments(string? tag, string type, string arguments) => tag is null ? arguments
        : $"new {type}.{tag}()" + (arguments.Length == 0 ? "" : ", " + arguments);

    private static string TagParameter(ConstructorDeclarationSyntax constructor)
    {
        var name = "__xpsTag";
        while (constructor.Parameters.Any(parameter => parameter.Identifier.Text.Equals(name, StringComparison.OrdinalIgnoreCase)))
            name += "_";
        return name;
    }

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
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        return constructors.Length == 0 ? EmitOne(declaration, null, symbols, path, source)
            : string.Concat(constructors.Select(constructor => EmitOne(declaration, constructor, symbols, path, source)));
    }

    private static string EmitOne(ClassDeclarationSyntax declaration, ConstructorDeclarationSyntax? constructor, SymbolTable symbols, string path, string source)
    {
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
                    AstExperimentalCompiler.ResolveRuntimeType(parameter.Type?.Identifier.Text), parameter.IsByRef,
                    AstClassMethodSupport.DeclaredType(parameter.Type, symbols)));
            var binder = new ExpressionBinder(scope);
            var syntax = new NewExpressionSyntax(new SyntaxToken(SyntaxKind.NewKeyword, "New", null, call?.Span ?? parent.Span),
                parent.Identifier, call?.OpenParenToken, call?.Arguments ?? [], call?.CommaTokens ?? [], call?.CloseParenToken);
            var expression = binder.Bind(syntax);
            if (binder.Diagnostics.Count > 0)
                throw AstExperimentalCompiler.SyntaxDiagnosticException(binder.Diagnostics[0], path, source);
            var bound = (BoundNewExpression)expression;
            initializer = " : base(" + Arguments(bound.Constructor!, bound.Arguments, new BoundExpressionEmitter()) + ")";
        }
        if (constructor is null) return $"public {name}(){initializer} {{ }}";
        var parameters = string.Join(", ", constructor.Parameters.Select(p =>
            (p.IsByRef ? "ref " : "") + BoundMethodEmitter.CSharpType(AstClassMethodSupport.DeclaredType(p.Type, symbols)) + " " + p.Identifier.Text));
        var arguments = string.Join(", ", constructor.Parameters.Select(p => (p.IsByRef ? "ref " : "") + p.Identifier.Text));
        var visibility = constructor.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private" : "public";
        var suffix = Suffix(declaration, constructor);
        var tag = suffix.Length == 0 ? null : "__xps_ctor" + suffix;
        var prefix = tag is null ? "" : $"{tag} {TagParameter(constructor)}" + (parameters.Length == 0 ? "" : ", ");
        var tagDeclaration = tag is null ? "" : $"public readonly struct {tag} {{ }}";
        // A failed construction owns its fields, but must not invoke user Delete.
        return tagDeclaration + $"{visibility} {name}({prefix}{parameters}){initializer} {{ try {{ __xps_constructor_body{suffix}({arguments}); }} catch {{ __xps_cleanup_failed_construction(); throw; }} }}" +
            EmitOptionalForwarders(constructor, name, visibility, symbols, path, source, suffix, tag);
    }

    private static string EmitOptionalForwarders(ConstructorDeclarationSyntax constructor, string name, string visibility,
        SymbolTable symbols, string path, string source, string suffix, string? tag)
    {
        if (!constructor.Parameters.Any(parameter => parameter.IsOptional)) return string.Empty;
        var parameters = constructor.Parameters.Select(parameter => new ParameterSymbol(parameter.Identifier.Text,
            AstExperimentalCompiler.ResolveRuntimeType(parameter.Type?.Identifier.Text), parameter.IsByRef,
            AstClassMethodSupport.DeclaredType(parameter.Type, symbols))).ToArray();
        var scope = symbols.CreateChildScope();
        var defaults = new BoundExpression?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (constructor.Parameters[i].IsOptional)
            {
                var binder = new ExpressionBinder(scope);
                var type = parameters[i].SemanticType!;
                var value = constructor.Parameters[i].DefaultValue is { } syntax ? binder.Bind(syntax)
                    : type.RuntimeType == typeof(object)
                        ? new BoundLiteralExpression(null, typeof(object), XpTypeSymbol.Nothing)
                        : new BoundLiteralExpression(type.RuntimeType == typeof(string) ? "" : Activator.CreateInstance(type.RuntimeType), type.RuntimeType, type);
                if (binder.Diagnostics.Count > 0)
                    throw AstExperimentalCompiler.SyntaxDiagnosticException(binder.Diagnostics[0], path, source);
                var conversion = scope.ClassifyConversion(value.SemanticType, type);
                if (!conversion.IsImplicit)
                    throw AstExperimentalCompiler.MappedDiagnostic(constructor.Parameters[i].Span, path, source,
                        $"Default value for Optional parameter '{parameters[i].Name}' is incompatible with its type.", "XPS2001", "semantic");
                defaults[i] = conversion.IsIdentity ? value : new BoundConversionExpression(value, type, conversion) { Span = value.Span };
            }
            scope.Declare(parameters[i]);
        }
        string Signature(IEnumerable<ParameterSymbol> values) => string.Join(", ", values.Select(parameter =>
            (parameter.IsByRef ? "ref " : "") + BoundMethodEmitter.CSharpType(parameter.SemanticType ?? XpTypeSymbol.FromClr(parameter.Type)) + " " + parameter.Name));
        string Arguments(IEnumerable<ParameterSymbol> values) => string.Join(", ", values.Select(parameter => (parameter.IsByRef ? "ref " : "") + parameter.Name));
        // A private scalar argument carrier crosses C# constructor initializers.
        // Default evaluation itself remains the shared Optional forwarding body;
        // do not inline/substitute expressions and evaluate earlier defaults twice.
        var hasReferences = parameters.Any(parameter => parameter.IsByRef);
        var carrierType = hasReferences ? XpTypeSymbol.User("__xps_constructor_arguments" + suffix) : null;
        var carrier = carrierType is null ? "__xps_constructor_arguments" + suffix : BoundMethodEmitter.CSharpType(carrierType);
        var resultType = hasReferences ? carrier : "object";
        var helper = "__xps_constructor_defaults" + suffix;
        var fields = string.Join(" ", parameters.Select((parameter, i) => $"public {(parameter.IsByRef ? "ref " : "")}{BoundMethodEmitter.CSharpType(parameter.SemanticType ?? XpTypeSymbol.FromClr(parameter.Type))} P{i};"));
        var stores = string.Join(" ", parameters.Select((parameter, i) => $"this.P{i} = {(parameter.IsByRef ? "ref " : "")}{parameter.Name};"));
        var unpacked = string.Join(", ", parameters.Select((parameter, i) => $"{(parameter.IsByRef ? "ref " : "")}__xpsValues.P{i}"));
        var output = new System.Text.StringBuilder();
        // Supplied references and heap-backed omitted slots stay managed
        // references; ByVal locals are copied. No stack-local reference escapes.
        output.Append($"private {(hasReferences ? "ref struct" : "sealed class")} {carrier} {{ {fields} public {carrier}({Signature(parameters)}) {{ {stores} }} }}");
        output.Append($"private {name}({carrier} __xpsValues) : this({TaggedArguments(tag, name, unpacked)}) {{ }}");
        output.Append($"private static {resultType} {helper}({Signature(parameters)}) {{ return new {carrier}({Arguments(parameters)}); }}");
        for (var count = parameters.Length - 1; count >= 0 && constructor.Parameters[count].IsOptional; count--)
        {
            var forward = OptionalCallLowering.Forward(helper, typeof(object), parameters, defaults, count,
                semanticReturnType: carrierType, heapBackedByRefDefaults: true);
            var signature = Signature(forward.Parameters);
            var prefix = tag is null ? "" : $"{tag} {TagParameter(constructor)}" + (signature.Length == 0 ? "" : ", ");
            output.Append($"{visibility} {name}({prefix}{signature}) : this(({carrier}){helper}({Arguments(forward.Parameters)})) {{ }}");
            var body = new BoundMethodEmitter().Emit(helper, typeof(object), forward.Parameters, forward.Statements, true, source, path, semanticReturnType: carrierType);
            output.Append("private ").Append(body["public ".Length..]);
        }
        return output.ToString();
    }
}
