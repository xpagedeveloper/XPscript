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
    public string EmitMembers(IReadOnlyList<BoundMethodDefinition> methods, string? source = null, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(methods);
        methods = methods.Where(method => !method.IsOptionalForwarding ||
            !methods.Any(explicitMethod => !explicitMethod.IsOptionalForwarding && SameSignature(method, explicitMethod))).ToArray();
        var output = new StringBuilder();
        foreach (var call in methods.SelectMany(method => method.Statements).SelectMany(BoundListCallLowering.Calls)
                     .DistinctBy(BoundListCallLowering.Name))
            output.Append(BoundListCallLowering.EmitWrapper(call));
        foreach (var declaration in methods.SelectMany(method => BoundStatementTraversal.Descendants(method.Statements))
                     .OfType<BoundVariableDeclarationStatement>().Where(declaration => declaration.Local.StaticStorageName is not null))
        {
            var type = declaration.Local.SemanticType?.IsList == true
                ? $"LSList<{BoundMethodEmitter.CSharpType(declaration.Local.SemanticType.ElementType!.RuntimeType)}>"
                : BoundMethodEmitter.CSharpType(declaration.Local.Type);
            if (declaration.Local.SemanticType is { RuntimeType: not null } semantic && semantic.RuntimeType == typeof(object) &&
                !semantic.IsVariant && !semantic.IsObject && !semantic.IsEmpty && !semantic.IsNothing && !semantic.IsNull && !semantic.IsList)
                type = $"Xp{semantic.Name}";
            type = BoundExpressionEmitter.RuntimeObjectType(declaration.Local.SemanticType?.Name ?? "") ?? type;
            var initialValue = declaration.Local.Type.IsArray
                ? $"new {type[..^2]}[0]"
                : declaration.Local.SemanticType?.IsList == true ? $"new {type}()"
                : declaration.Local.Type == typeof(string) ? "string.Empty" : $"default({type})";
            output.Append("private static ").Append(type).Append(' ').Append(declaration.Local.StaticStorageName)
                .Append(" = ").Append(initialValue).Append(";\n");
            if (declaration.Initializer is not null)
            {
                output.Append("private static int ").Append(declaration.Local.StaticStorageName).Append("_initState;\n");
                output.Append("private static readonly object ").Append(declaration.Local.StaticStorageName).Append("_initGate = new object();\n");
            }
        }
        var emitter = new BoundMethodEmitter();
        foreach (var method in methods)
            output.Append(emitter.Emit(method.Name, method.ReturnType, method.Parameters, method.Statements, true, source, sourcePath));
        return output.ToString();
    }

    private static bool SameSignature(BoundMethodDefinition left, BoundMethodDefinition right)
        => left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase) &&
           left.Parameters.Select(parameter => (parameter.Type, parameter.SemanticType?.Name))
               .SequenceEqual(right.Parameters.Select(parameter => (parameter.Type, parameter.SemanticType?.Name)));
}
