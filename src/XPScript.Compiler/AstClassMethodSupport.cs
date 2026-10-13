using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Instance procedure bodies use the same binder/flow validation and emitter as module procedures.</summary>
internal static class AstClassMethodSupport
{
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
        foreach (var member in declaration.Members.Where(m => m is SubDeclarationSyntax or FunctionDeclarationSyntax))
        {
            var sub = member as SubDeclarationSyntax;
            var function = member as FunctionDeclarationSyntax;
            var name = sub?.Identifier.Text ?? function!.Identifier.Text;
            if (name.Equals("New", StringComparison.OrdinalIgnoreCase) || name.Equals("Delete", StringComparison.OrdinalIgnoreCase)) continue;
            if (declaration.BaseType is not null)
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Method bodies declared in derived classes are not implemented by the AST compiler yet.", "XPS3001", "ast");
            var syntaxParameters = sub?.Parameters ?? function!.Parameters;
            var statements = sub?.Statements ?? function!.Statements;
            if (syntaxParameters.Any(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) == typeof(object)) ||
                function is not null && AstExperimentalCompiler.ResolveRuntimeType(function.ReturnType?.Identifier.Text) == typeof(object))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "This AST class-method increment supports scalar typed parameters and results; object/array signatures are not implemented yet.", "XPS3001", "ast");
            if (syntaxParameters.Any(p => p.IsOptional || p.IsList) ||
                AstExperimentalCompiler.FlattenStatements(statements).OfType<DimStatementSyntax>().Any(d => d.IsStatic))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Optional/List parameters and Static storage in class methods are not implemented by the AST compiler yet.", "XPS3001", "ast");
            var classScope = globals.CreateChildScope();
            classScope.Declare(new VariableSymbol("Me", typeof(object), XpTypeSymbol.User(declaration.Identifier.Text)));
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
            var returnType = function is null ? typeof(void) : AstExperimentalCompiler.ResolveRuntimeType(function.ReturnType?.Identifier.Text);
            var result = function is null ? null : new LocalSymbol("__xps_class_result", returnType, XpTypeSymbol.FromClr(returnType));
            var bound = AstExperimentalCompiler.BindProcedure(statements, scope, parameters,
                function is null ? null : XpTypeSymbol.FromClr(returnType), function?.Identifier.Text, result,
                declaration.Identifier.Text + "." + name, sourcePath, source);
            string body;
            try { body = new BoundMethodEmitter().Emit(name, returnType, parameters, bound, false, source, sourcePath); }
            catch (BoundEmissionException ex) { throw AstExperimentalCompiler.MappedDiagnostic(ex.Span ?? member.Span, sourcePath, source, ex.Message); }
            if ((sub?.Visibility ?? function?.Visibility)?.Kind == SyntaxKind.PrivateKeyword)
                body = "private " + body["public ".Length..];
            output.Append(body);
        }
        return output.ToString();
    }
}
