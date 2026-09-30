namespace XPScript.Compiler.Syntax;

public sealed class ExpressionParser
{
    private readonly SyntaxToken[] _tokens;
    private int _position;

    public ExpressionParser(string text)
    {
        var lexer = new Lexer(text);
        _tokens = lexer.Lex().Where(t => t.Kind != SyntaxKind.NewLineToken).ToArray();
        Diagnostics = lexer.Diagnostics;
    }

    public IReadOnlyList<LexerDiagnostic> Diagnostics { get; }

    public ExpressionSyntax ParseExpression() => ParseBinaryExpression();

    private ExpressionSyntax ParseBinaryExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
            var operand = ParseBinaryExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(operatorToken, operand);
        }
        else
        {
            left = ParsePrimaryExpression();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            var operatorToken = NextToken();
            var right = ParseBinaryExpression(precedence);
            left = new BinaryExpressionSyntax(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            var open = NextToken();
            var expression = ParseExpression();
            var close = Match(SyntaxKind.CloseParenToken);
            return new ParenthesizedExpressionSyntax(open, expression, close);
        }

        if (Current.Kind is SyntaxKind.NumberToken or SyntaxKind.StringToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword)
            return new LiteralExpressionSyntax(NextToken());

        return new NameExpressionSyntax(Match(SyntaxKind.IdentifierToken));
    }

    private SyntaxToken Match(SyntaxKind kind)
    {
        if (Current.Kind == kind)
            return NextToken();
        return new SyntaxToken(kind, string.Empty, null, new TextSpan(Current.Span.Start, 0));
    }

    private SyntaxToken Current => Peek(0);
    private SyntaxToken Peek(int offset)
    {
        var index = _position + offset;
        return index >= _tokens.Length ? _tokens[^1] : _tokens[index];
    }
    private SyntaxToken NextToken()
    {
        var current = Current;
        _position++;
        return current;
    }

    private static int GetUnaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.NotKeyword => 6,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 6,
        _ => 0
    };

    private static int GetBinaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.StarToken or SyntaxKind.SlashToken => 5,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.AmpersandToken => 4,
        SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken or SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken => 3,
        SyntaxKind.AndKeyword => 2,
        SyntaxKind.OrKeyword => 1,
        _ => 0
    };
}
