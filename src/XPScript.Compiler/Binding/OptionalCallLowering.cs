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
        var omittedLocals = new Dictionary<Symbol, LocalSymbol>();
        for (var i = suppliedCount; i < parameters.Count; i++)
        {
            var localName = "__xpsOptional" + i;
            while (!names.Add(localName)) localName += "_";
            omittedLocals[parameters[i]] = new LocalSymbol(localName, parameters[i].Type, parameters[i].SemanticType);
        }
        for (var i = suppliedCount; i < parameters.Count; i++)
        {
            // Every omitted argument gets fresh storage. A callee may mutate
            // its ByRef default without sharing state with another invocation.
            var local = omittedLocals[parameters[i]];
            var initializer = ReplaceParameters(defaults[i] ?? throw new ArgumentException("Every omitted parameter requires a bound default.", nameof(defaults)), omittedLocals);
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

    private static BoundExpression ReplaceParameters(BoundExpression expression, IReadOnlyDictionary<Symbol, LocalSymbol> replacements)
        => expression switch
        {
            BoundNameExpression name when replacements.TryGetValue(name.Symbol, out var local) => new BoundNameExpression(local),
            BoundBinaryExpression binary => new BoundBinaryExpression(ReplaceParameters(binary.Left, replacements), binary.OperatorKind, ReplaceParameters(binary.Right, replacements), binary.Type),
            BoundUnaryExpression unary => new BoundUnaryExpression(unary.OperatorKind, ReplaceParameters(unary.Operand, replacements), unary.Type),
            BoundConversionExpression conversion => new BoundConversionExpression(ReplaceParameters(conversion.Expression, replacements), conversion.SemanticType, conversion.Conversion),
            BoundCallExpression call => new BoundCallExpression(call.Target is null ? null : ReplaceParameters(call.Target, replacements), call.Function, call.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray()),
            BoundMemberAccessExpression member => new BoundMemberAccessExpression(ReplaceParameters(member.Receiver, replacements), member.Name, member.Type, member.SemanticType),
            BoundIndexExpression index => new BoundIndexExpression(ReplaceParameters(index.Expression, replacements), ReplaceParameters(index.Index, replacements), index.Type, index.SemanticType),
            BoundIndexedPropertyExpression indexed => new BoundIndexedPropertyExpression(ReplaceParameters(indexed.Receiver, replacements), indexed.Property, indexed.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray()),
            BoundNewExpression @new => new BoundNewExpression(@new.Type, @new.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray(), @new.SemanticType),
            _ => expression
        };
}
