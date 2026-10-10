using System.Text;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>A procedure whose signature and body have already been bound.</summary>
public sealed record BoundMethodDefinition(string Name, Type ReturnType,
    IReadOnlyList<ParameterSymbol> Parameters, IReadOnlyList<BoundStatement> Statements,
    bool IsOptionalForwarding = false);

/// <summary>Emits a console compilation unit without parsing XPscript source.</summary>
public sealed class BoundCompilationUnitEmitter
{
    public string Emit(IReadOnlyList<BoundMethodDefinition> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        return "using System;\npublic static class Program\n{\n" + EmitMembers(methods) + "}\n";
    }

    /// <summary>Allows target hosts to supply runtime support around the same bound procedures.</summary>
    public string EmitMembers(IReadOnlyList<BoundMethodDefinition> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        methods = methods.Where(method => !method.IsOptionalForwarding ||
            !methods.Any(explicitMethod => !explicitMethod.IsOptionalForwarding && SameSignature(method, explicitMethod))).ToArray();
        var output = new StringBuilder();
        var expressions = new BoundExpressionEmitter();
        foreach (var declaration in methods.SelectMany(method => BoundStatementTraversal.Descendants(method.Statements))
                     .OfType<BoundVariableDeclarationStatement>().Where(declaration => declaration.Local.StaticStorageName is not null))
        {
            var type = declaration.Local.SemanticType?.IsList == true
                ? $"LSList<{BoundMethodEmitter.CSharpType(declaration.Local.SemanticType.ElementType!.RuntimeType)}>"
                : BoundMethodEmitter.CSharpType(declaration.Local.Type);
            if (declaration.Local.SemanticType is { RuntimeType: not null } semantic && semantic.RuntimeType == typeof(object) &&
                !semantic.IsVariant && !semantic.IsObject && !semantic.IsEmpty && !semantic.IsNothing && !semantic.IsNull && !semantic.IsList)
                type = $"Xp{semantic.Name}";
            var initialValue = declaration.Initializer is not null
                ? expressions.Emit(declaration.Initializer)
                : declaration.Local.Type.IsArray
                ? $"new {type[..^2]}[0]"
                : declaration.Local.SemanticType?.IsList == true ? $"new {type}()"
                : declaration.Local.Type == typeof(string) ? "string.Empty" : $"default({type})";
            output.Append("private static ").Append(type).Append(' ').Append(declaration.Local.StaticStorageName)
                .Append(" = ").Append(initialValue).Append(";\n");
        }
        var emitter = new BoundMethodEmitter();
        foreach (var method in methods)
            output.Append(emitter.Emit(method.Name, method.ReturnType, method.Parameters, method.Statements));
        return output.ToString();
    }

    private static bool SameSignature(BoundMethodDefinition left, BoundMethodDefinition right)
        => left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase) &&
           left.Parameters.Select(parameter => parameter.Type).SequenceEqual(right.Parameters.Select(parameter => parameter.Type));
}
