using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class ExpressionBinder(SymbolTable? symbols = null)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundExpression Bind(ExpressionSyntax syntax) => syntax switch
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

    private BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        if (syntax.LiteralToken.Kind == SyntaxKind.TrueKeyword)
            return new BoundLiteralExpression(true, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.FalseKeyword)
            return new BoundLiteralExpression(false, typeof(bool));
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
        if (_symbols.TryLookup(name, out var symbol) && symbol is VariableSymbol variable)
            return new BoundNameExpression(variable);
        return Error(syntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined variable '{name}'.");
    }

    private BoundExpression BindNew(NewExpressionSyntax syntax)
    {
        var typeName = syntax.TypeName.Text;
        if (!_symbols.TryLookup(typeName, out var typeSymbol) || typeSymbol is not TypeSymbol typeEntry)
            return Error(syntax.TypeName, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined type '{typeName}'.");
        var type = typeEntry.Type;

        var arguments = syntax.Arguments.Select(Bind).ToArray();
        var constructors = type.GetConstructors();
        var constructor = constructors.FirstOrDefault(c =>
            c.GetParameters().Length == arguments.Length &&
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments.Select(a => a.Type)));

        if (constructor is null)
            return Error(syntax, $"No matching constructor for '{typeName}'.");

        return new BoundNewExpression(type, arguments, typeEntry.SemanticType ?? new XpTypeSymbol(typeEntry.Name, type));
    }

    private BoundExpression BindIndex(IndexExpressionSyntax syntax)
    {
        var expression = Bind(syntax.Expression);
        var index = Bind(syntax.Index);
        if (index.Type != typeof(long))
            return Error(syntax.Index, "Array index must be an integer.");
        if (expression.Type.IsArray)
            return new BoundIndexExpression(
                expression,
                index,
                expression.Type.GetElementType()!,
                expression.SemanticType.ElementType);
        return Error(syntax, $"Indexing is not defined for {expression.SemanticType.Name}.");
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        var receiver = Bind(syntax.Expression);
        var key = receiver.SemanticType.Name + "." + syntax.NameToken.Text;
        if (_symbols.TryLookup(key, out var symbol))
        {
            if (symbol is PropertySymbol property)
                return new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, property.PropertyType, property.SemanticType);
            if (symbol is FunctionSymbol function)
                return new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, function.ReturnType, function.SemanticReturnType);
        }
        return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{syntax.NameToken.Text}' on '{receiver.SemanticType.Name}'.");
    }

    private BoundExpression BindArray(ArrayExpressionSyntax syntax)
    {
        var functions = _symbols.LookupAll(syntax.ArrayIdentifier.Text).OfType<FunctionSymbol>().ToArray();
        if (functions.Length == 0)
            return Error(syntax.ArrayIdentifier, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{syntax.ArrayIdentifier.Text}'.");

        var arguments = syntax.Elements.Select(Bind).ToArray();
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
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
        }
        else if (syntax.Target is MemberAccessExpressionSyntax memberSyntax)
        {
            var receiver = Bind(memberSyntax.Expression);
            name = memberSyntax.NameToken.Text;
            var memberKey = receiver.SemanticType.Name + "." + name;
            var memberSymbols = _symbols.LookupAll(memberKey);
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
            return Error(nameSyntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{name}'.");
        return BindOverloadSet(syntax, target, name, functions);
    }


    private BoundExpression BindOverloadSet(CallExpressionSyntax syntax, BoundExpression? target, string name, IReadOnlyList<FunctionSymbol> functions)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
            .ToArray();

        if (candidates.Length == 0)
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching overload for function '{name}'.");
        if (candidates.Length > 1)
            return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Call to function '{name}' is ambiguous.");

        return new BoundCallExpression(target, candidates[0], arguments);
    }

    private static bool ParametersMatch(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        if (!function.ParameterTypes.SequenceEqual(arguments.Select(argument => argument.Type)))
            return false;

        if (function.SemanticParameterTypes is null)
            return true;
        if (function.SemanticParameterTypes.Count != arguments.Count)
            return false;

        for (var i = 0; i < arguments.Count; i++)
            if (!string.Equals(function.SemanticParameterTypes[i].Name, arguments[i].SemanticType.Name, StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }

    private BoundExpression BindIndexedProperty(CallExpressionSyntax syntax, BoundExpression receiver, IndexedPropertySymbol property)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length != property.ParameterTypes.Count)
            return Error(syntax, $"Indexed property '{property.Name}' expects {property.ParameterTypes.Count} argument(s), but received {arguments.Length}.");
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Type != property.ParameterTypes[i])
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{property.Name}' must be {property.ParameterTypes[i].Name}, not {arguments[i].Type.Name}.");
            if (property.SemanticParameterTypes is not null &&
                !string.Equals(property.SemanticParameterTypes[i].Name, arguments[i].SemanticType.Name, StringComparison.OrdinalIgnoreCase))
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{property.Name}' must be {property.SemanticParameterTypes[i].Name}, not {arguments[i].SemanticType.Name}.");
        }
        return new BoundIndexedPropertyExpression(receiver, property, arguments);
    }

    private BoundExpression BindCallTarget(CallExpressionSyntax syntax, BoundExpression? target, FunctionSymbol function)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length != function.ParameterTypes.Count)
            return Error(syntax, $"Function '{function.Name}' expects {function.ParameterTypes.Count} argument(s), but received {arguments.Length}.");

        for (var i = 0; i < arguments.Length; i++)
            if (arguments[i].Type != function.ParameterTypes[i])
                return Error(syntax.Arguments[i], $"Argument {i + 1} to '{function.Name}' must be {function.ParameterTypes[i].Name}, not {arguments[i].Type.Name}.");

        return new BoundCallExpression(target, function, arguments);
    }

    private BoundExpression BindUnary(UnaryExpressionSyntax syntax)
    {
        var operand = Bind(syntax.Operand);
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(bool))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(bool));
        if (syntax.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken &&
            (operand.Type == typeof(long) || operand.Type == typeof(double)))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, operand.Type);
        return Error(syntax, $"Unary operator {syntax.OperatorToken.Text} is not defined for {operand.Type.Name}.");
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax syntax)
    {
        var left = Bind(syntax.Left);
        var right = Bind(syntax.Right);
        var op = syntax.OperatorToken.Kind;

        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(bool) && right.Type == typeof(bool))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, left.Type);
        if (op == SyntaxKind.AmpersandToken && left.Type == typeof(string) && right.Type == typeof(string))
            return new BoundBinaryExpression(left, op, right, typeof(string));
        if (op is SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken && left.Type == right.Type)
            return new BoundBinaryExpression(left, op, right, typeof(bool));

        return Error(syntax, $"Binary operator {syntax.OperatorToken.Text} is not defined for {left.Type.Name} and {right.Type.Name}.");
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
