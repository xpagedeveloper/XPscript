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
        => Emit(methodName, returnType, parameters, statements, isStatic: true);

    public string Emit(string methodName, Type returnType, IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<BoundStatement> statements, bool isStatic,
        string? source = null, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(parameters);

        string body;
        try
        {
            body = source is not null && sourcePath is not null && BoundStatementTraversal.Descendants(statements).Any(item => item is BoundOnErrorStatement or BoundResumeStatement)
                ? _statements.EmitWithSourceMap(statements, source, sourcePath).Code
                : _statements.Emit(statements);
        }
        catch (NotSupportedException ex)
        {
            throw new BoundEmissionException(ex.Message, statements.FirstOrDefault()?.Span, ex);
        }
        var output = new StringBuilder();
        output.Append("public ").Append(isStatic ? "static " : string.Empty).Append(CSharpType(returnType)).Append(' ')
            .Append(methodName).Append('(')
            .Append(string.Join(", ", parameters.Select(Parameter)))
            .Append(")\n{\n");
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            output.Append("    ").Append(line).Append('\n');
        if (returnType != typeof(void) && BoundStatementTraversal.Descendants(statements).Any(item => item is BoundOnErrorStatement or BoundResumeStatement))
            output.Append("    return default!;\n");
        output.Append("}\n");
        return output.ToString();
    }

    private static string Parameter(ParameterSymbol parameter)
    {
        var mode = parameter.IsByRef ? "ref " : string.Empty;
        var type = parameter.SemanticType?.IsList == true ? $"LSList<{CSharpType(parameter.SemanticType.ElementType!.RuntimeType)}>" : CSharpType(parameter.Type);
        type = BoundExpressionEmitter.JsonRuntimeType(parameter.SemanticType?.Name ?? "") ?? type;
        return mode + type + " " + parameter.Name;
    }

    internal static string CSharpType(Type type) => type == typeof(void) ? "void"
        : type == typeof(long) ? "long"
        : type == typeof(double) ? "double"
        : type == typeof(bool) ? "bool"
        : type == typeof(string) ? "string"
        : type == typeof(object) ? "object"
        : type.FullName?.Replace('+', '.') ?? type.Name;
}
