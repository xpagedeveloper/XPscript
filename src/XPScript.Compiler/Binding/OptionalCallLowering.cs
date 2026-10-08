using XPScript.Compiler.Emission;

namespace XPScript.Compiler.Binding;

/// <summary>Builds forwarding bodies for omitted trailing parameters from bound defaults.</summary>
public static class OptionalCallLowering
{
    public static BoundMethodDefinition Forward(string name, Type returnType,
        IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<BoundExpression?> defaults, int suppliedCount)
    {
        if (defaults.Count != parameters.Count || suppliedCount < 0 || suppliedCount >= parameters.Count)
            throw new ArgumentException("Optional forwarding requires one default slot per parameter and a shorter signature.");
        var statements = new List<BoundStatement>();
        var arguments = parameters.Take(suppliedCount).Select(p => (BoundExpression)new BoundNameExpression(p)).ToList();
        var names = parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = suppliedCount; i < parameters.Count; i++)
        {
            // Every omitted argument gets fresh storage. A callee may mutate
            // its ByRef default without sharing state with another invocation.
            var localName = "__xpsOptional" + i;
            while (!names.Add(localName)) localName += "_";
            var local = new LocalSymbol(localName, parameters[i].Type, parameters[i].SemanticType);
            var initializer = defaults[i] ?? throw new ArgumentException("Every omitted parameter requires a bound default.", nameof(defaults));
            statements.Add(new BoundVariableDeclarationStatement(local, initializer));
            arguments.Add(new BoundNameExpression(local));
        }
        var function = new FunctionSymbol(name, returnType, parameters.Select(p => p.Type).ToArray(),
            null, parameters.Select(p => p.SemanticType ?? XpTypeSymbol.FromClr(p.Type)).ToArray(),
            parameters.Select(p => p.IsByRef).ToArray());
        var call = new BoundCallExpression(null, function, arguments);
        statements.Add(returnType == typeof(void) ? new BoundExpressionStatement(call) : new BoundReturnStatement(call));
        return new BoundMethodDefinition(name, returnType, parameters.Take(suppliedCount).ToArray(), statements);
    }
}
