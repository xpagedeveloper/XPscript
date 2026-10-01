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
        MemberAccessExpressionSyntax member => BindMemberAccess(member),
        IndexExpressionSyntax index => BindIndex(index),
        ArrayExpressionSyntax array => BindArray(array),
        NewExpressionSyntax @new => BindNew(@new),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
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
        return Error(syntax, $"Undefined variable '{name}'.");
    }

    private BoundExpression BindNew(NewExpressionSyntax syntax)
    {
        var typeName = syntax.TypeName.Text;
        if (!_symbols.TryLookup(typeName, out var typeSymbol) || typeSymbol is not TypeSymbol typeEntry)
            return Error(syntax, $"Undefined type '{typeName}'.");
        var type = typeEntry.Type;

        var arguments = syntax.Arguments.Select(Bind).ToArray();
        var constructors = type.GetConstructors();
        var constructor = constructors.FirstOrDefault(c =>
            c.GetParameters().Length == arguments.Length &&
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments.Select(a => a.Type)));

        if (constructor is null)
            return Error(syntax, $"No matching constructor for '{typeName}'.");

        return new BoundNewExpression(type, arguments);
    }

    private BoundExpression BindArray(ArrayExpressionSyntax syntax)
    {
        var elements = syntax.Elements.Select(Bind).ToArray();
        return new BoundArrayExpression(elements, typeof(object[]));
    }

    private BoundExpression BindIndex(IndexExpressionSyntax syntax)
    {
        var expression = Bind(syntax.Expression);
        var index = Bind(syntax.Index);
        if (index.Type != typeof(long))
            return Error(syntax.Index, "Array index must be an integer.");
        if (expression.Type.IsArray)
            return new BoundIndexExpression(expression, index, expression.Type.GetElementType()!);
        return Error(syntax, $"Indexing is not defined for {expression.Type.Name}.");
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        var receiver = Bind(syntax.Expression);
        var key = receiver.Type.Name + "." + syntax.NameToken.Text;
        if (_symbols.TryLookup(key, out var symbol) && symbol is FunctionSymbol function)
            return new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, function.ReturnType);
        return Error(syntax, $"Undefined member '{syntax.NameToken.Text}' on '{receiver.Type.Name}'.");
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
            target = BindMemberAccess(memberSyntax);
            if (!_symbols.TryLookup(receiver.Type.Name + "." + name, out var memberSymbol) || memberSymbol is not FunctionSymbol memberFunction)
                return Error(syntax, $"Undefined function '{name}' on '{receiver.Type.Name}'.");
            return BindCallTarget(syntax, target, memberFunction);
        }
        else
        {
            return Error(syntax, "Unsupported call target.");
        }

        if (!_symbols.TryLookup(name, out var symbol) || symbol is not FunctionSymbol function)
            return Error(syntax, $"Undefined function '{name}'.");
        return BindCallTarget(syntax, target, function);
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
        if (op is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken)
            return new BoundBinaryExpression(left, op, right, typeof(bool));

        return Error(syntax, $"Binary operator {syntax.OperatorToken.Text} is not defined for {left.Type.Name} and {right.Type.Name}.");
    }

    private BoundExpression Error(SyntaxNode syntax, string message)
    {
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", message, syntax.Span));
        return new BoundLiteralExpression(null, typeof(object));
    }
}
