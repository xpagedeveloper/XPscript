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
        string? source = null, string? sourcePath = null, XpTypeSymbol? semanticReturnType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(parameters);

        string body;
        _statements.ImageScopeEnabled = parameters.Any(p => AstImageSupport.IsImage(p.SemanticType?.Name)) ||
            BoundStatementTraversal.Descendants(statements).OfType<BoundVariableDeclarationStatement>()
                .Any(d => AstImageSupport.IsImage(d.Local.SemanticType?.Name));
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
        output.Append("public ").Append(isStatic ? "static " : string.Empty).Append(semanticReturnType is null ? CSharpType(returnType) : CSharpType(semanticReturnType)).Append(' ')
            .Append(methodName).Append('(')
            .Append(string.Join(", ", parameters.Select(Parameter)))
            .Append(")\n{\n");
        if (_statements.ImageScopeEnabled)
        {
            output.Append("    using var __imageScope = new AstImageScope();\n");
            foreach (var parameter in parameters.Where(p => !p.IsByRef && AstImageSupport.IsImage(p.SemanticType?.Name)))
            {
                output.Append("    ").Append(parameter.Name).Append(" = __imageScope.Own(").Append(parameter.Name).Append("?.Clone());\n");
                output.Append("    __imageScope.Observe(() => ").Append(parameter.Name).Append(");\n");
            }
        }
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
        type = parameter.SemanticType is { } semantic ? CSharpType(semantic) : type;
        return mode + type + " " + parameter.Name;
    }

    internal static string SemanticStorageType(LocalSymbol local, string fallback)
    {
        if (local.IsDynamicArray) return "System.Array";
        if (BoundExpressionEmitter.RuntimeObjectType(local.SemanticType?.Name ?? "") is { } native) return native;
        return local.SemanticType is { RuntimeType: not null } semantic && semantic.RuntimeType == typeof(object) &&
            !semantic.IsVariant && !semantic.IsObject && !semantic.IsNothing && !semantic.IsNull && !semantic.IsEmpty && !semantic.IsList
            ? "Xp" + semantic.Name : fallback;
    }

    internal static string CSharpType(Type type) => type == typeof(void) ? "void"
        : type == typeof(long) ? "long"
        : type == typeof(double) ? "double"
        : type == typeof(bool) ? "bool"
        : type == typeof(string) ? "string"
        : type == typeof(object) ? "object"
        : type.FullName?.Replace('+', '.') ?? type.Name;

    internal static string CSharpType(XpTypeSymbol type) => type.IsList
        ? $"LSList<{CSharpType(type.ElementType!.RuntimeType)}>"
        : BoundExpressionEmitter.RuntimeObjectType(type.Name) ??
          (type.RuntimeType == typeof(object) && !type.IsVariant && !type.IsObject && !type.IsNothing && !type.IsNull && !type.IsEmpty
              ? "Xp" + type.Name : CSharpType(type.RuntimeType));
}
