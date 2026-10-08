using System.Text;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>Wraps a bound method body in a deterministic C# method declaration.</summary>
public sealed class BoundMethodEmitter
{
    private readonly BoundStatementEmitter _statements = new();

    public string Emit(string methodName, Type returnType, IReadOnlyList<BoundStatement> statements)
        => Emit(methodName, returnType, [], statements);

    public string Emit(string methodName, Type returnType, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<BoundStatement> statements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(parameters);

        var body = _statements.Emit(statements);
        var output = new StringBuilder();
        output.Append("public static ").Append(CSharpType(returnType)).Append(' ')
            .Append(methodName).Append('(')
            .Append(string.Join(", ", parameters.Select(Parameter)))
            .Append(")\n{\n");
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            output.Append("    ").Append(line).Append('\n');
        output.Append("}\n");
        return output.ToString();
    }

    private static string Parameter(ParameterSymbol parameter)
    {
        var mode = parameter.IsByRef ? "ref " : string.Empty;
        return mode + CSharpType(parameter.Type) + " " + parameter.Name;
    }

    internal static string CSharpType(Type type) => type == typeof(void) ? "void"
        : type == typeof(long) ? "long"
        : type == typeof(double) ? "double"
        : type == typeof(bool) ? "bool"
        : type == typeof(string) ? "string"
        : type == typeof(object) ? "object"
        : type.FullName?.Replace('+', '.') ?? type.Name;
}
