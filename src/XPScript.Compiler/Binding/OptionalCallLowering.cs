using XPScript.Compiler.Emission;

namespace XPScript.Compiler.Binding;

/// <summary>Builds forwarding bodies for omitted trailing parameters from bound defaults.</summary>
public static class OptionalCallLowering
{
    public static BoundMethodDefinition Forward(string name, Type returnType,
        IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<BoundExpression?> defaults, int suppliedCount, int requiredTailCount = 0,
        XpTypeSymbol? semanticReturnType = null, bool heapBackedByRefDefaults = false)
    {
        var optionalEnd = parameters.Count - requiredTailCount;
        if (defaults.Count != parameters.Count || requiredTailCount < 0 || suppliedCount < 0 || suppliedCount >= optionalEnd)
            throw new ArgumentException("Optional forwarding requires one default slot per parameter and a shorter signature.");
        var statements = new List<BoundStatement>();
        var arguments = parameters.Take(suppliedCount).Select(p => (BoundExpression)new BoundNameExpression(p)).ToList();
        var names = parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var omittedLocals = new Dictionary<Symbol, LocalSymbol>();
        var replacements = new Dictionary<Symbol, BoundExpression>();
        for (var i = suppliedCount; i < optionalEnd; i++)
        {
            var localName = "__xpsOptional" + i;
            while (!names.Add(localName)) localName += "_";
            var parameter = parameters[i];
            var cell = heapBackedByRefDefaults && parameter.IsByRef;
            var type = parameter.SemanticType ?? XpTypeSymbol.FromClr(parameter.Type);
            var referenceCell = cell && parameter.Type == typeof(object);
            var local = new LocalSymbol(localName, referenceCell ? typeof(object) : cell ? parameter.Type.MakeArrayType() : parameter.Type,
                referenceCell ? XpTypeSymbol.ReferenceCellOf(type) : cell ? XpTypeSymbol.ArrayOf(type) : type);
            omittedLocals[parameter] = local;
            replacements[parameter] = referenceCell
                ? new BoundMemberAccessExpression(new BoundNameExpression(local), "Value", parameter.Type, type, isFieldStorage: true)
                : cell
                ? new BoundIndexExpression(new BoundNameExpression(local), new BoundLiteralExpression(0L, typeof(long)), parameter.Type, type)
                : new BoundNameExpression(local);
        }
        for (var i = suppliedCount; i < optionalEnd; i++)
        {
            // Every omitted argument gets fresh storage. A callee may mutate
            // its ByRef default without sharing state with another invocation.
            var local = omittedLocals[parameters[i]];
            var initializer = ReplaceParameters(defaults[i] ?? throw new ArgumentException("Every omitted parameter requires a bound default.", nameof(defaults)), replacements);
            var value = replacements[parameters[i]];
            if (heapBackedByRefDefaults && parameters[i].IsByRef)
            {
                if (local.SemanticType?.IsReferenceCell == true)
                {
                    // The interior managed reference roots the exact typed cell.
                    statements.Add(new BoundVariableDeclarationStatement(local,
                        new BoundNewExpression(typeof(object), [initializer], local.SemanticType)));
                    arguments.Add(value);
                    continue;
                }
                // A constructor carrier may outlive this forwarding frame. An
                // interior managed reference keeps its fresh one-element array
                // alive; never return a reference to an omitted stack local.
                // Materialize at the declared scalar type before Array.SetValue
                // boxes it (in particular Byte zero literals are C# int literals).
                var scalarName = local.Name + "_value";
                while (!names.Add(scalarName)) scalarName += "_";
                var scalar = new LocalSymbol(scalarName, parameters[i].Type, parameters[i].SemanticType);
                statements.Add(new BoundVariableDeclarationStatement(scalar, initializer));
                statements.Add(new BoundVariableDeclarationStatement(local, null, arrayLength: 0));
                statements.Add(new BoundAssignmentStatement(value, new BoundNameExpression(scalar), false));
            }
            else statements.Add(new BoundVariableDeclarationStatement(local, initializer));
            arguments.Add(value);
        }
        // A Property Let's incoming value is required, even after Optional indexes.
        arguments.AddRange(parameters.Skip(optionalEnd).Select(p => new BoundNameExpression(p)));
        var function = new FunctionSymbol(name, returnType, parameters.Select(p => p.Type).ToArray(),
            semanticReturnType, parameters.Select(p => p.SemanticType ?? XpTypeSymbol.FromClr(p.Type)).ToArray(),
            parameters.Select(p => p.IsByRef).ToArray());
        var call = new BoundCallExpression(null, function, arguments);
        statements.Add(returnType == typeof(void) ? new BoundExpressionStatement(call) : new BoundReturnStatement(call));
        return new BoundMethodDefinition(name, returnType, parameters.Take(suppliedCount).Concat(parameters.Skip(optionalEnd)).ToArray(), statements, true);
    }

    private static BoundExpression ReplaceParameters(BoundExpression expression, IReadOnlyDictionary<Symbol, BoundExpression> replacements)
        => expression switch
        {
            BoundNameExpression name when replacements.TryGetValue(name.Symbol, out var value) => value,
            BoundBinaryExpression binary => new BoundBinaryExpression(ReplaceParameters(binary.Left, replacements), binary.OperatorKind, ReplaceParameters(binary.Right, replacements), binary.Type),
            BoundUnaryExpression unary => new BoundUnaryExpression(unary.OperatorKind, ReplaceParameters(unary.Operand, replacements), unary.Type),
            BoundConversionExpression conversion => new BoundConversionExpression(ReplaceParameters(conversion.Expression, replacements), conversion.SemanticType, conversion.Conversion),
            BoundCallExpression call => new BoundCallExpression(call.Target is null ? null : ReplaceParameters(call.Target, replacements), call.Function, call.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray()),
            BoundMemberAccessExpression member => new BoundMemberAccessExpression(ReplaceParameters(member.Receiver, replacements), member.Name, member.Type, member.SemanticType, member.IsFieldStorage, member.AssignmentKind),
            BoundIndexExpression index => new BoundIndexExpression(ReplaceParameters(index.Expression, replacements), ReplaceParameters(index.Index, replacements), index.Type, index.SemanticType),
            BoundIndexedPropertyExpression indexed => new BoundIndexedPropertyExpression(ReplaceParameters(indexed.Receiver, replacements), indexed.Property, indexed.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray()),
            BoundNewExpression @new => new BoundNewExpression(@new.Type, @new.Arguments.Select(argument => ReplaceParameters(argument, replacements)).ToArray(), @new.SemanticType) { Constructor = @new.Constructor },
            _ => expression
        };
}
