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
        foreach (var declaration in methods.SelectMany(method => BoundStatementTraversal.Descendants(method.Statements))
                     .OfType<BoundVariableDeclarationStatement>().Where(declaration => declaration.Local.StaticStorageName is not null))
        {
            var type = BoundMethodEmitter.CSharpType(declaration.Local.Type);
            var initialValue = declaration.Local.Type == typeof(string) ? "string.Empty" : $"default({type})";
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
