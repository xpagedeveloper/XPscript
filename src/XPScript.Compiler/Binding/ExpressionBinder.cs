using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class ExpressionBinder(SymbolTable? symbols = null, bool allowDynamicMembers = false,
    string? functionName = null, LocalSymbol? functionResult = null)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly bool _allowDynamicMembers = allowDynamicMembers;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundExpression Bind(ExpressionSyntax syntax)
    {
        BoundExpression bound = syntax switch
        {
        LiteralExpressionSyntax literal => BindLiteral(literal),
        NameExpressionSyntax name => BindName(name),
        CallExpressionSyntax call => BindCall(call),
        ArrayExpressionSyntax array => BindArray(array),
        MemberAccessExpressionSyntax member => BindMemberAccess(member),
        IndexExpressionSyntax index => BindIndex(index),
        NewExpressionSyntax @new => BindNew(@new),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
        ParenthesizedExpressionSyntax parenthesized => Bind(parenthesized.Expression),
        _ => Error(syntax, $"Binding is not implemented for {syntax.Kind}.")
        };
        bound.Span = syntax.Span;
        return bound;
    }

    private BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        if (syntax.LiteralToken.Kind == SyntaxKind.TrueKeyword)
            return new BoundLiteralExpression(true, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.FalseKeyword)
            return new BoundLiteralExpression(false, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.EmptyKeyword)
            return new BoundLiteralExpression(null, typeof(object), XpTypeSymbol.Empty);
        if (syntax.LiteralToken.Kind == SyntaxKind.NullKeyword)
            return new BoundLiteralExpression(DBNull.Value, typeof(object), XpTypeSymbol.Null);
        if (syntax.LiteralToken.Kind == SyntaxKind.NothingKeyword)
            return new BoundLiteralExpression(null, typeof(object), XpTypeSymbol.Nothing);
        if (syntax.LiteralToken.Kind == SyntaxKind.NumberToken)
            return syntax.Value switch
            {
                long => new BoundLiteralExpression(syntax.Value, typeof(long)),
                double => new BoundLiteralExpression(syntax.Value, typeof(double)),
                _ => Error(syntax, "Unsupported numeric literal.")
            };
        if (syntax.LiteralToken.Kind == SyntaxKind.StringToken)
            return new BoundLiteralExpression(syntax.Value, typeof(string));
        return Error(syntax, "Unsupported literal.");
    }

    private BoundExpression BindName(NameExpressionSyntax syntax)
    {
        var name = syntax.IdentifierToken.Text;
        // A bare function name denotes its result slot; a call still resolves
        // the procedure signature, so recursion is unaffected.
        if (functionResult is not null && name.Equals(functionName, StringComparison.OrdinalIgnoreCase))
            return new BoundNameExpression(functionResult);
        // Statement-specific parsers can recover an omitted optional argument
        // with a zero-width identifier. Dynamic AST compilation must preserve
        // the surrounding statement and let its runtime helper supply the
        // default, rather than reporting a second synthetic unknown-variable
        // error for the recovery token.
        if (_allowDynamicMembers && string.IsNullOrEmpty(name))
            return new BoundLiteralExpression(null, typeof(object));
        if (_symbols.TryLookup(name, out var symbol) &&
            symbol is VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol)
            return new BoundNameExpression(symbol);
        if (_symbols.LookupAll(name).OfType<FunctionSymbol>().FirstOrDefault(function => function.ParameterTypes.Count == 0) is { } zeroArgumentFunction)
            return new BoundCallExpression(null, zeroArgumentFunction, []);
        if (_allowDynamicMembers && name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            return new BoundLiteralExpression(null, typeof(object));
        return Error(syntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined variable '{name}'.");
    }

    private BoundExpression BindNew(NewExpressionSyntax syntax)
    {
        var typeName = syntax.TypeName.Text;
        if (_allowDynamicMembers && (!_symbols.TryLookup(typeName, out var knownSymbol) || knownSymbol is not TypeSymbol))
        {
            var dynamicArguments = syntax.Arguments.Select(Bind).ToArray();
            return new BoundNewExpression(typeof(object), dynamicArguments, XpTypeSymbol.User(typeName));
        }
        if (!_symbols.TryLookup(typeName, out var typeSymbol) || typeSymbol is not TypeSymbol typeEntry)
        {
            if (_allowDynamicMembers)
            {
                var dynamicArguments = syntax.Arguments.Select(Bind).ToArray();
                return new BoundNewExpression(typeof(object), dynamicArguments, XpTypeSymbol.User(typeName));
            }
            return new BoundNewExpression(typeof(object), [], XpTypeSymbol.User(typeName));
        }
        var type = typeEntry.Type;

        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (_allowDynamicMembers && type == typeof(object))
            return new BoundNewExpression(typeof(object), arguments, typeEntry.SemanticType ?? XpTypeSymbol.User(typeName));
        var constructors = type.GetConstructors();
        var constructor = constructors.FirstOrDefault(c =>
            c.GetParameters().Length == arguments.Length &&
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments.Select(a => a.Type)));

        if (constructor is null)
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");

        return new BoundNewExpression(type, arguments, typeEntry.SemanticType ?? new XpTypeSymbol(typeEntry.Name, type));
    }

    private BoundExpression BindIndex(IndexExpressionSyntax syntax)
    {
        var expression = Bind(syntax.Expression);
        var index = Bind(syntax.Index);
        if (index.Type != typeof(long))
            return Error(syntax.Index, CompilerDiagnosticCodes.ArgumentTypeMismatch, "Array index must be an integer.");
        if (expression.SemanticType.IsArray && expression.SemanticType.ElementType is not null)
            return new BoundIndexExpression(
                expression,
                index,
                expression.SemanticType.ElementType.RuntimeType,
                expression.SemanticType.ElementType);
        if (_allowDynamicMembers && (expression.Type == typeof(object) || expression.SemanticType.IsVariant))
            return new BoundIndexExpression(expression, index, typeof(object), XpTypeSymbol.Variant);
        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Indexing is not defined for {expression.SemanticType.Name}.");
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        var receiver = Bind(syntax.Expression);
        if (_symbols.TryLookupMember(receiver.SemanticType, syntax.NameToken.Text, out var symbol))
        {
            if (symbol is PropertySymbol property)
                return new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, property.PropertyType, property.SemanticType);
            if (symbol is FunctionSymbol function)
                return new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, function.ReturnType, function.SemanticReturnType);
        }
        return _allowDynamicMembers && receiver.Type == typeof(object)
            ? new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, typeof(object), XpTypeSymbol.Variant)
            : Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{syntax.NameToken.Text}' on '{receiver.SemanticType.Name}'.");
    }

    private BoundExpression BindArray(ArrayExpressionSyntax syntax)
    {
        if (syntax.ArrayIdentifier.Text.Equals("Array", StringComparison.OrdinalIgnoreCase))
        {
            var arrayArguments = syntax.Elements.Select(Bind).ToArray();
            var arrayFunction = new FunctionSymbol("Array", typeof(object[]), arrayArguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant, arrayArguments.Select(_ => XpTypeSymbol.Variant).ToArray());
            return new BoundCallExpression(null, arrayFunction, arrayArguments);
        }
        if (_allowDynamicMembers && (syntax.ArrayIdentifier.Text.Equals("Files", StringComparison.OrdinalIgnoreCase) || syntax.ArrayIdentifier.Text.Equals("Directories", StringComparison.OrdinalIgnoreCase)))
        {
            var fileArguments = syntax.Elements.Select(Bind).ToArray();
            var function = new FunctionSymbol(syntax.ArrayIdentifier.Text, typeof(object), fileArguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant, fileArguments.Select(_ => XpTypeSymbol.Variant).ToArray());
            return new BoundCallExpression(null, function, fileArguments);
        }
        if (_allowDynamicMembers && syntax.Elements.Count >= 1 &&
            !_symbols.LookupAll(syntax.ArrayIdentifier.Text).OfType<FunctionSymbol>().Any())
        {
            var local = _symbols.TryLookup(syntax.ArrayIdentifier.Text, out var localSymbol) && localSymbol is LocalSymbol found && found.Type == typeof(object)
                ? found
                : new LocalSymbol(syntax.ArrayIdentifier.Text, typeof(object), XpTypeSymbol.Variant);
            var index = Bind(syntax.Elements[0]);
            return new BoundIndexExpression(new BoundNameExpression(local), index, typeof(object), XpTypeSymbol.Variant);
        }
        var functions = _symbols.LookupAll(syntax.ArrayIdentifier.Text).OfType<FunctionSymbol>().ToArray();
        if (functions.Length == 0)
        {
            if (_allowDynamicMembers)
                return new BoundNameExpression(new LocalSymbol(syntax.ArrayIdentifier.Text, typeof(object), XpTypeSymbol.Variant));
            return Error(syntax.ArrayIdentifier, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{syntax.ArrayIdentifier.Text}'.");
        }

        var arguments = syntax.Elements.Select(Bind).ToArray();
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
            .Where(function => ParameterModesMatch(function, arguments))
            .ToArray();

        if (candidates.Length == 0)
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching overload for function '{syntax.ArrayIdentifier.Text}'.");
        if (candidates.Length > 1)
            return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Call to function '{syntax.ArrayIdentifier.Text}' is ambiguous.");

        return new BoundCallExpression(null, candidates[0], arguments);
    }

    private BoundExpression BindCall(CallExpressionSyntax syntax)
    {
        BoundExpression? target;
        string name;
        if (syntax.Target is NameExpressionSyntax nameSyntax)
        {
            target = null;
            name = nameSyntax.IdentifierToken.Text;
            if (_symbols.TryLookup(name, out var indexedSymbol) && indexedSymbol is LocalSymbol local && local.Type.IsArray && syntax.Arguments.Count == 1)
            {
                var array = new BoundNameExpression(local);
                var index = Bind(syntax.Arguments[0]);
                return new BoundIndexExpression(array, index, local.Type.GetElementType()!, XpTypeSymbol.FromClr(local.Type.GetElementType()!));
            }
            if (_symbols.TryLookup(name, out indexedSymbol) && indexedSymbol is LocalSymbol arrayLocal && arrayLocal.Type.IsArray && syntax.Arguments.Count == 0)
                return new BoundNameExpression(arrayLocal);
            if (_allowDynamicMembers && _symbols.TryLookup(name, out var dynamicSymbol) && dynamicSymbol is LocalSymbol dynamicLocal && dynamicLocal.Type == typeof(object) && dynamicLocal.SemanticType?.IsVariant == true && syntax.Arguments.Count == 1)
            {
                var index = Bind(syntax.Arguments[0]);
                return new BoundIndexExpression(new BoundNameExpression(dynamicLocal), index, typeof(object), XpTypeSymbol.Variant);
            }
        }
        else if (syntax.Target is MemberAccessExpressionSyntax memberSyntax)
        {
            var receiver = Bind(memberSyntax.Expression);
            name = memberSyntax.NameToken.Text;
            var memberKey = receiver.SemanticType.Name + "." + name;
            var memberSymbols = _symbols.LookupMembers(receiver.SemanticType, name);
            if (memberSymbols.Count == 0 && _allowDynamicMembers && receiver.Type == typeof(object))
            {
                var arguments = syntax.Arguments.Select(Bind).ToArray();
                var dynamicFunction = new FunctionSymbol(name, typeof(object), arguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant);
                target = new BoundMemberAccessExpression(receiver, name, typeof(object), XpTypeSymbol.Variant);
                return new BoundCallExpression(target, dynamicFunction, arguments);
            }
            if (memberSymbols.Count == 0)
                return Error(memberSyntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{name}' on '{receiver.SemanticType.Name}'.");
            var indexedProperty = memberSymbols.OfType<IndexedPropertySymbol>().LastOrDefault();
            if (indexedProperty is not null)
                return BindIndexedProperty(syntax, receiver, indexedProperty);
            var memberFunctions = memberSymbols.OfType<FunctionSymbol>().ToArray();
            if (memberFunctions.Length == 0)
                return Error(memberSyntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Member '{name}' on '{receiver.SemanticType.Name}' is not callable.");
            target = new BoundMemberAccessExpression(receiver, name, memberFunctions[0].ReturnType, memberFunctions[0].SemanticReturnType);
            return BindOverloadSet(syntax, target, memberKey, memberFunctions);
        }
        else
        {
            return Error(syntax, "Unsupported call target.");
        }

        var functions = _symbols.LookupAll(name).OfType<FunctionSymbol>().ToArray();
        if (functions.Length == 0)
        {
            if (_allowDynamicMembers && syntax.Arguments.Count == 0)
                return new BoundNameExpression(new LocalSymbol(name, typeof(object), XpTypeSymbol.Variant));
        if (_allowDynamicMembers && syntax.Arguments.Count >= 1)
        {
            var dynamicLocal = new LocalSymbol(name, typeof(object), XpTypeSymbol.Variant);
            return new BoundIndexExpression(new BoundNameExpression(dynamicLocal), Bind(syntax.Arguments[0]), typeof(object), XpTypeSymbol.Variant);
        }
        return Error(nameSyntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{name}'.");
        }
        return BindOverloadSet(syntax, target, name, functions);
    }

    private BoundExpression BindOverloadSet(CallExpressionSyntax syntax, BoundExpression? target, string name, IReadOnlyList<FunctionSymbol> functions)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
            .Where(function => ParameterModesMatch(function, arguments))
            .ToArray();

        if (candidates.Length == 0)
        {
            // This set contains declared signatures. Dynamic receivers are
            // handled before overload selection; fabricating a signature here
            // would suppress arity, argument-type and ByRef diagnostics.
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching overload for function '{name}'.");
        }
        if (candidates.Length > 1)
            return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Call to function '{name}' is ambiguous.");

        return new BoundCallExpression(target, candidates[0], ConvertArguments(candidates[0], arguments));
    }

    private static BoundExpression[] ConvertArguments(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        var converted = new BoundExpression[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = Conversion.Classify(arguments[i].SemanticType, targetType);
            converted[i] = conversion.IsIdentity
                ? arguments[i]
                : new BoundConversionExpression(arguments[i], targetType, conversion) { Span = arguments[i].Span };
        }
        return converted;
    }

    private static bool ParameterModesMatch(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        if (function.ByRefParameters is null)
            return true;
        if (function.ByRefParameters.Count != arguments.Count)
            return false;

        for (var i = 0; i < arguments.Count; i++)
        {
            if (!function.ByRefParameters[i])
                continue;
            if (arguments[i] is not BoundNameExpression name ||
                name.Symbol is not (VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol))
                return false;
        }

        return true;
    }

    private static bool ParametersMatch(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        if (function.ParameterTypes.Count != arguments.Count)
            return false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = Conversion.Classify(arguments[i].SemanticType, targetType);
            var isByRef = function.ByRefParameters is not null && function.ByRefParameters[i];
            if (targetType.IsVariant)
                continue;
            if (!conversion.IsImplicit || isByRef && !conversion.IsIdentity)
                return false;
        }

        return true;
    }

    private BoundExpression BindIndexedProperty(CallExpressionSyntax syntax, BoundExpression receiver, IndexedPropertySymbol property)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length != property.ParameterTypes.Count)
            return Error(syntax, CompilerDiagnosticCodes.ArgumentCountMismatch, $"Indexed property '{property.Name}' expects {property.ParameterTypes.Count} argument(s), but received {arguments.Length}.");
        for (var i = 0; i < arguments.Length; i++)
        {
            var targetType = property.SemanticParameterTypes is not null
                ? property.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(property.ParameterTypes[i]);
            var conversion = Conversion.Classify(arguments[i].SemanticType, targetType);
            if (!conversion.IsImplicit)
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{property.Name}' must be {targetType.Name}, not {arguments[i].SemanticType.Name}.");
            if (!conversion.IsIdentity)
                arguments[i] = new BoundConversionExpression(arguments[i], targetType, conversion);
        }
        return new BoundIndexedPropertyExpression(receiver, property, arguments);
    }

    private BoundExpression BindCallTarget(CallExpressionSyntax syntax, BoundExpression? target, FunctionSymbol function)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length != function.ParameterTypes.Count)
            return Error(syntax, CompilerDiagnosticCodes.ArgumentCountMismatch, $"Function '{function.Name}' expects {function.ParameterTypes.Count} argument(s), but received {arguments.Length}.");

        for (var i = 0; i < arguments.Length; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = Conversion.Classify(arguments[i].SemanticType, targetType);
            if (!conversion.IsImplicit)
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{function.Name}' must be {targetType.Name}, not {arguments[i].SemanticType.Name}.");
            if (!conversion.IsIdentity)
                arguments[i] = new BoundConversionExpression(arguments[i], targetType, conversion);
        }

        return new BoundCallExpression(target, function, arguments);
    }

    private BoundExpression BindUnary(UnaryExpressionSyntax syntax)
    {
        var operand = Bind(syntax.Operand);
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(bool))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(bool));
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(long))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(long));
        if (syntax.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken &&
            (operand.Type == typeof(long) || operand.Type == typeof(double)))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, operand.Type);
        if (_allowDynamicMembers && operand.SemanticType.IsVariant)
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(object));
        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Unary operator {syntax.OperatorToken.Text} is not defined for {operand.SemanticType.Name}.");
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax syntax)
    {
        var left = Bind(syntax.Left);
        var right = Bind(syntax.Right);
        var op = syntax.OperatorToken.Kind;

        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(bool) && right.Type == typeof(bool))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(long) && right.Type == typeof(long))
            return new BoundBinaryExpression(left, op, right, typeof(long));
        if (op is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, left.Type);
        if (op == SyntaxKind.AmpersandToken && left.Type == typeof(string) && right.Type == typeof(string))
            return new BoundBinaryExpression(left, op, right, typeof(string));
        if (op == SyntaxKind.LikeKeyword && left.Type == typeof(string) && right.Type == typeof(string))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (_allowDynamicMembers && op == SyntaxKind.PlusToken && (left.Type == typeof(string) || right.Type == typeof(string)))
            return new BoundBinaryExpression(left, op, right, typeof(string));
        if (op is SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken && left.Type == right.Type)
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (_allowDynamicMembers && (left.SemanticType.IsVariant || right.SemanticType.IsVariant || left.Type == typeof(object) || right.Type == typeof(object)))
            return new BoundBinaryExpression(left, op, right, typeof(object));
        if (_allowDynamicMembers && (left.Type == typeof(long) || left.Type == typeof(double)) && (right.Type == typeof(long) || right.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, typeof(object));

        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Binary operator {syntax.OperatorToken.Text} is not defined for {left.SemanticType.Name} and {right.SemanticType.Name}.");
    }

    private BoundExpression Error(SyntaxNode syntax, string message) =>
        Error(syntax.Span, CompilerDiagnosticCodes.InvalidSyntax, message);

    private BoundExpression Error(SyntaxNode syntax, string code, string message) =>
        Error(syntax.Span, code, message);

    private BoundExpression Error(SyntaxToken token, string code, string message) =>
        Error(token.Span, code, message);

    private BoundExpression Error(TextSpan span, string code, string message)
    {
        _diagnostics.Add(new SyntaxDiagnostic(code, message, span));
        return new BoundLiteralExpression(null, typeof(object));
    }
}
