using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Instance procedure bodies use the same binder/flow validation and emitter as module procedures.</summary>
internal static class AstClassMethodSupport
{
    internal static IEnumerable<PropertySymbol> Properties(ClassDeclarationSyntax declaration, bool external)
    {
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var readable = getter is not null && (!external || getter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var writable = setter is not null && (!external || setter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            if (!readable && !writable) continue;
            var type = AstExperimentalCompiler.ResolveRuntimeType(getter?.Type?.Identifier.Text ?? setter?.Type?.Identifier.Text ?? setter?.Parameters.LastOrDefault()?.Type?.Identifier.Text);
            yield return new PropertySymbol(declaration.Identifier.Text + "." + group.First().Identifier.Text,
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
            if (property is not null && (property.AccessorKeyword.Kind == SyntaxKind.SetKeyword ||
                property.Parameters.Count > (property.IsGetter ? 0 : 1)))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Indexed and object Set class properties are not implemented by the AST compiler yet.", "XPS3001", "ast");
            if (property?.IsSetter == true && syntaxParameters.Count == 0)
                syntaxParameters = new[] { new ParameterSyntax(new SyntaxToken(SyntaxKind.ByValKeyword, "ByVal", null, property.Identifier.Span),
                    property.Identifier, property.AsKeyword, property.Type) };
            var resultTypeName = function?.ReturnType?.Identifier.Text ?? (property?.IsGetter == true ? property.Type?.Identifier.Text : null);
            if (syntaxParameters.Any(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) == typeof(object)) ||
                (function is not null || property?.IsGetter == true) && AstExperimentalCompiler.ResolveRuntimeType(resultTypeName) == typeof(object))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "This AST class-method increment supports scalar typed parameters and results; object/array signatures are not implemented yet.", "XPS3001", "ast");
            if (syntaxParameters.Any(p => p.IsOptional || p.IsList) ||
                AstExperimentalCompiler.FlattenStatements(statements).OfType<DimStatementSyntax>().Any(d => d.IsStatic))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Optional/List parameters and Static storage in class methods are not implemented by the AST compiler yet.", "XPS3001", "ast");
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
                classScope.Declare(new PropertySymbol(declaration.Identifier.Text + "." + field.Identifier.Text, type, XpTypeSymbol.FromClr(type)));
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
        }
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var name = group.First().Identifier.Text;
            var type = AstExperimentalCompiler.ResolveRuntimeType(getter?.Type?.Identifier.Text ?? setter?.Type?.Identifier.Text ?? setter?.Parameters.LastOrDefault()?.Type?.Identifier.Text);
            var publicProperty = group.Any(p => p.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var getterVisibility = publicProperty && getter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            var setterVisibility = publicProperty && setter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            output.Append(publicProperty ? "public " : "private ").Append(type == typeof(string) ? "string" : type == typeof(long) ? "long" : type == typeof(bool) ? "bool" : type == typeof(byte) ? "byte" : "double")
                .Append(' ').Append(name).Append(" { ");
            if (getter is not null) output.Append(getterVisibility).Append("get => __xps_property_get_").Append(getter.Identifier.Text).Append("(); ");
            if (setter is not null) output.Append(setterVisibility).Append("set => __xps_property_let_").Append(setter.Identifier.Text)
                .Append(setter.Parameters.FirstOrDefault()?.IsByRef == true ? "(ref value); " : "(value); ");
            output.Append("}\n");
        }
        return output.ToString();
    }
}
