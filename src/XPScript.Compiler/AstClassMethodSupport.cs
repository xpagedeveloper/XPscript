using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Instance procedure bodies use the same binder/flow validation and emitter as module procedures.</summary>
internal static class AstClassMethodSupport
{
    internal static IEnumerable<Symbol> Properties(ClassDeclarationSyntax declaration, bool external)
    {
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var readable = getter is not null && (!external || getter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var writable = setter is not null && (!external || setter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            if (!readable && !writable) continue;
            var type = AstExperimentalCompiler.ResolveRuntimeType(getter?.Type?.Identifier.Text ?? setter?.Type?.Identifier.Text ?? setter?.Parameters.LastOrDefault()?.Type?.Identifier.Text);
            var name = group.First().Identifier.Text;
            var indexes = getter?.Parameters ?? setter!.Parameters.Take(Math.Max(0, setter.Parameters.Count - 1)).ToArray();
            if (indexes.Count > 0)
                yield return new IndexedPropertySymbol(declaration.Identifier.Text + "." + name, type,
                    indexes.Select(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray(),
                    XpTypeSymbol.FromClr(type), null, readable, writable, "__xps_index_get_" + name, "__xps_index_let_" + name,
                    indexes.Select(p => p.IsExplicitByRef).ToArray())
                    { RequiredArgumentCount = indexes.TakeWhile(p => !p.IsOptional).Count() };
            else yield return new PropertySymbol(declaration.Identifier.Text + "." + name,
                type, XpTypeSymbol.FromClr(type), readable, writable);
        }
    }

    internal static string ModuleSource(string source)
    {
        // Existing module-field scanning must never turn class-private fields into globals.
        var unit = new DeclarationParser(source).ParseCompilationUnit();
        var text = source.ToCharArray();
        foreach (var declaration in unit.Declarations.OfType<ClassDeclarationSyntax>())
            for (var i = declaration.Span.Start; i < declaration.Span.End && i < text.Length; i++)
                if (text[i] is not ('\r' or '\n')) text[i] = ' ';
        return new string(text);
    }
    internal static string Emit(ClassDeclarationSyntax declaration, SymbolTable globals, string sourcePath, string source)
    {
        var output = new System.Text.StringBuilder();
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            if (group.Count(p => p.IsGetter) > 1 || group.Count(p => p.IsSetter) > 1)
                throw AstExperimentalCompiler.MappedDiagnostic(group.Last().Span, sourcePath, source,
                    $"Property '{group.Key}' has duplicate accessors.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Count != Math.Max(0, setter.Parameters.Count - 1))
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index parameter signatures.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Where((p, i) => p.IsExplicitByRef != setter.Parameters[i].IsExplicitByRef).Any())
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index passing modes.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Where((p, i) =>
                    AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) != AstExperimentalCompiler.ResolveRuntimeType(setter.Parameters[i].Type?.Identifier.Text) ||
                    p.IsOptional != setter.Parameters[i].IsOptional).Any())
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index types and Optional signatures.", "XPS1012", "semantic");
            if (getter is not null && setter is not null &&
                !string.Equals(getter.Type?.Identifier.Text, setter.Type?.Identifier.Text ?? setter.Parameters.LastOrDefault()?.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase))
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible value types.", "XPS1012", "semantic");
        }
        foreach (var member in declaration.Members.Where(m => m is SubDeclarationSyntax or FunctionDeclarationSyntax or PropertyDeclarationSyntax))
        {
            var sub = member as SubDeclarationSyntax;
            var function = member as FunctionDeclarationSyntax;
            var property = member as PropertyDeclarationSyntax;
            var name = sub?.Identifier.Text ?? function?.Identifier.Text ?? property!.Identifier.Text;
            if (name.Equals("New", StringComparison.OrdinalIgnoreCase) || name.Equals("Delete", StringComparison.OrdinalIgnoreCase)) continue;
            if (declaration.BaseType is not null)
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Method bodies declared in derived classes are not implemented by the AST compiler yet.", "XPS3001", "ast");
            var syntaxParameters = sub?.Parameters ?? function?.Parameters ?? property!.Parameters;
            var statements = sub?.Statements ?? function?.Statements ?? property!.Statements;
            var isObjectSetter = property?.AccessorKeyword.Kind == SyntaxKind.SetKeyword;
            if (property?.IsSetter == true && syntaxParameters.Count == 0)
                syntaxParameters = new[] { new ParameterSyntax(new SyntaxToken(SyntaxKind.ByValKeyword, "ByVal", null, property.Identifier.Span),
                    property.Identifier, property.AsKeyword, property.Type) };
            var resultTypeName = function?.ReturnType?.Identifier.Text ?? (property?.IsGetter == true ? property.Type?.Identifier.Text : null);
            if (syntaxParameters.Any(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) == typeof(object) && !isObjectSetter) ||
                function is not null && AstExperimentalCompiler.ResolveRuntimeType(resultTypeName) == typeof(object))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "This AST class-method increment supports scalar typed parameters and results; object/array signatures are not implemented yet.", "XPS3001", "ast");
            if (syntaxParameters.Any(p => p.IsList || p.IsOptional && property is null) ||
                AstExperimentalCompiler.FlattenStatements(statements).OfType<DimStatementSyntax>().Any(d => d.IsStatic))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Optional/List parameters and Static storage in class methods are not implemented by the AST compiler yet.", "XPS3001", "ast");
            if (property is not null)
            {
                var indexParameters = property.IsGetter ? syntaxParameters : syntaxParameters.Take(syntaxParameters.Count - 1).ToArray();
                AstExperimentalCompiler.ValidateOptionalDeclarations(indexParameters, sourcePath, source);
                if (property.IsSetter && (syntaxParameters[^1].IsOptional || syntaxParameters[^1].DefaultValue is not null))
                    throw AstExperimentalCompiler.MappedDiagnostic(syntaxParameters[^1].Span, sourcePath, source,
                        "A Property Let value parameter must be required and cannot have a default value.", "XPS1012", "semantic");
            }
            var classScope = globals.CreateChildScope();
            classScope.Declare(new VariableSymbol("Me", typeof(object), XpTypeSymbol.User(declaration.Identifier.Text)));
            foreach (var ownProperty in Properties(declaration, false))
            {
                classScope.Declare(ownProperty);
                classScope.Declare(ownProperty with { Name = ownProperty.Name.Split('.').Last() });
            }
            foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
            {
                var type = AstExperimentalCompiler.ResolveRuntimeType(field.Type.Identifier.Text);
                classScope.Declare(new FieldSymbol(field.Identifier.Text, type, XpTypeSymbol.FromClr(type)));
                classScope.Declare(new PropertySymbol(declaration.Identifier.Text + "." + field.Identifier.Text, type, XpTypeSymbol.FromClr(type), IsFieldStorage: true));
            }
            foreach (var own in declaration.Members.Where(m => m is SubDeclarationSyntax or FunctionDeclarationSyntax))
            {
                var ownSub = own as SubDeclarationSyntax; var ownFunction = own as FunctionDeclarationSyntax;
                var methodName = ownSub?.Identifier.Text ?? ownFunction!.Identifier.Text;
                var ps = ownSub?.Parameters ?? ownFunction!.Parameters;
                var rt = ownFunction is null ? typeof(void) : AstExperimentalCompiler.ResolveRuntimeType(ownFunction.ReturnType?.Identifier.Text);
                var symbol = new FunctionSymbol(methodName, rt, ps.Select(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray(),
                    XpTypeSymbol.FromClr(rt), ps.Select(p => XpTypeSymbol.FromClr(AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text))).ToArray(), ps.Select(p => p.IsByRef).ToArray());
                classScope.Declare(symbol);
                classScope.Declare(symbol with { Name = declaration.Identifier.Text + "." + methodName });
            }
            var scope = classScope.CreateChildScope();
            var parameters = syntaxParameters.Select(p => new ParameterSymbol(p.Identifier.Text,
                AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text), p.IsByRef)).ToArray();
            // Bind defaults in the instance declaration scope, exposing only earlier
            // indexes. Accessor locals and the incoming setter value are unavailable.
            var defaults = new BoundExpression?[parameters.Length];
            var defaultScope = classScope.CreateChildScope();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (syntaxParameters[i].IsOptional)
                {
                    var binder = new ExpressionBinder(defaultScope);
                    var type = XpTypeSymbol.FromClr(parameters[i].Type);
                    var value = syntaxParameters[i].DefaultValue is { } syntax ? binder.Bind(syntax)
                        : new BoundLiteralExpression(parameters[i].Type == typeof(string) ? "" : Activator.CreateInstance(parameters[i].Type), parameters[i].Type, type);
                    if (binder.Diagnostics.Count > 0)
                        throw AstExperimentalCompiler.SyntaxDiagnosticException(binder.Diagnostics[0], sourcePath, source);
                    var conversion = Conversion.Classify(value.SemanticType, type);
                    if (!conversion.IsImplicit)
                        throw AstExperimentalCompiler.MappedDiagnostic(syntaxParameters[i].Span, sourcePath, source,
                            $"Default value for Optional parameter '{parameters[i].Name}' is incompatible with its type.", "XPS2001", "semantic");
                    defaults[i] = conversion.IsIdentity ? value : new BoundConversionExpression(value, type, conversion) { Span = value.Span };
                }
                defaultScope.Declare(parameters[i]);
            }
            foreach (var parameter in parameters) scope.Declare(parameter);
            var hasResult = function is not null || property?.IsGetter == true;
            var returnType = hasResult ? AstExperimentalCompiler.ResolveRuntimeType(resultTypeName) : typeof(void);
            var result = hasResult ? new LocalSymbol("__xps_class_result", returnType, XpTypeSymbol.FromClr(returnType)) : null;
            var bound = AstExperimentalCompiler.BindProcedure(statements, scope, parameters,
                hasResult ? XpTypeSymbol.FromClr(returnType) : null, hasResult ? name : null, result,
                declaration.Identifier.Text + "." + name, sourcePath, source);
            var emittedName = property is null ? name : "__xps_property_" + (property.IsGetter ? "get_" : "let_") + name;
            string body;
            try { body = new BoundMethodEmitter().Emit(emittedName, returnType, parameters, bound, false, source, sourcePath); }
            catch (BoundEmissionException ex) { throw AstExperimentalCompiler.MappedDiagnostic(ex.Span ?? member.Span, sourcePath, source, ex.Message); }
            if (property is not null || (sub?.Visibility ?? function?.Visibility)?.Kind == SyntaxKind.PrivateKeyword)
                body = "private " + body["public ".Length..];
            output.Append(body);
            if (property is not null)
            {
                var end = parameters.Length - (property.IsSetter ? 1 : 0);
                for (var count = end - 1; count >= 0 && syntaxParameters[count].IsOptional; count--)
                {
                    var forward = OptionalCallLowering.Forward(emittedName, returnType, parameters, defaults, count, property.IsSetter ? 1 : 0);
                    output.Append("private ").Append(new BoundMethodEmitter().Emit(emittedName, returnType,
                        forward.Parameters, forward.Statements, false, source, sourcePath)["public ".Length..]);
                }
            }
        }
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var name = group.First().Identifier.Text;
            var type = AstExperimentalCompiler.ResolveRuntimeType(getter?.Type?.Identifier.Text ?? setter?.Type?.Identifier.Text ?? setter?.Parameters.LastOrDefault()?.Type?.Identifier.Text);
            var indexes = getter?.Parameters ?? setter!.Parameters.Take(Math.Max(0, setter.Parameters.Count - 1)).ToArray();
            if (indexes.Count > 0)
            {
                foreach (var accessor in group)
                {
                    var required = indexes.TakeWhile(p => !p.IsOptional).Count();
                    for (var count = indexes.Count; count >= required; count--)
                    {
                        var supplied = accessor.Parameters.Take(count).Concat(accessor.IsSetter ? accessor.Parameters.TakeLast(1) : []).ToArray();
                        var arguments = string.Join(", ", supplied.Select(p => (p.IsByRef ? "ref " : "") + p.Identifier.Text));
                        output.Append(accessor.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "public ")
                            .Append(accessor.IsGetter ? BoundMethodEmitter.CSharpType(type) : "void")
                            .Append(accessor.IsGetter ? " __xps_index_get_" : " __xps_index_let_").Append(name).Append('(')
                            .Append(string.Join(", ", supplied.Select((p, i) => (i < count && p.IsExplicitByRef ? "ref " : "") + BoundMethodEmitter.CSharpType(AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)) + " " + p.Identifier.Text)))
                            .Append(") => __xps_property_").Append(accessor.IsGetter ? "get_" : "let_").Append(accessor.Identifier.Text)
                            .Append('(').Append(arguments).Append(");\n");
                    }
                }
                continue;
            }
            var publicProperty = group.Any(p => p.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var getterVisibility = publicProperty && getter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            var setterVisibility = publicProperty && setter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            output.Append(publicProperty ? "public " : "private ").Append(BoundMethodEmitter.CSharpType(type))
                .Append(' ').Append(name).Append(" { ");
            if (getter is not null) output.Append(getterVisibility).Append("get => __xps_property_get_").Append(getter.Identifier.Text).Append("(); ");
            if (setter is not null) output.Append(setterVisibility).Append("set => __xps_property_let_").Append(setter.Identifier.Text)
                .Append(setter.Parameters.FirstOrDefault()?.IsByRef == true ? "(ref value); " : "(value); ");
            output.Append("}\n");
        }
        return output.ToString();
    }
}
