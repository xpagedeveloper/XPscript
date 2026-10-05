namespace XPScript.Compiler.Syntax;

public sealed class ExpressionParser
{
    private readonly SyntaxToken[] _tokens;
    private int _position;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];

    public ExpressionParser(string text, int baseOffset = 0)
    {
        var lexer = new Lexer(text, baseOffset);
        _tokens = lexer.Lex().Where(t => t.Kind != SyntaxKind.NewLineToken).ToArray();
        foreach (var diagnostic in lexer.Diagnostics)
            _diagnostics.Add(new SyntaxDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Span));
    }

    internal ExpressionParser(IEnumerable<SyntaxToken> tokens)
    {
        _tokens = tokens.Where(t => t.Kind != SyntaxKind.NewLineToken).ToArray();
        if (_tokens.Length == 0 || _tokens[^1].Kind != SyntaxKind.EndOfFileToken)
            throw new ArgumentException("Expression token sequence must end with EndOfFileToken.", nameof(tokens));
    }

    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public ExpressionSyntax ParseExpression()
    {
        var expression = ParseBinaryExpression();
        if (Current.Kind != SyntaxKind.EndOfFileToken)
            _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"Unexpected token {Current.Kind} after expression.", Current.Span));
        return expression;
    }

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
            left = ParsePostfixExpression();
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

    private ExpressionSyntax ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        while (Current.Kind is SyntaxKind.OpenParenToken or SyntaxKind.DotToken or SyntaxKind.OpenBracketToken)
        {
            if (Current.Kind == SyntaxKind.DotToken)
            {
                var dot = NextToken();
                var name = MatchIdentifier();
                expression = new MemberAccessExpressionSyntax(expression, dot, name);
                continue;
            }

            if (Current.Kind == SyntaxKind.OpenBracketToken)
            {
                var openBracket = NextToken();
                var index = ParseBinaryExpression();
                var closeBracket = Match(SyntaxKind.CloseBracketToken);
                expression = new IndexExpressionSyntax(expression, openBracket, index, closeBracket);
                continue;
            }

            var open = NextToken();
            var arguments = new List<ExpressionSyntax>();
            var commas = new List<SyntaxToken>();

            if (Current.Kind != SyntaxKind.CloseParenToken)
            {
                while (true)
                {
                    arguments.Add(ParseBinaryExpression());
                    if (Current.Kind != SyntaxKind.CommaToken)
                        break;
                    commas.Add(NextToken());
                }
            }

            var close = Match(SyntaxKind.CloseParenToken);
            expression = new CallExpressionSyntax(expression, open, arguments, commas, close);
        }
        return expression;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (Current.Kind == SyntaxKind.IdentifierToken
            && Current.Text.Equals("Array", StringComparison.OrdinalIgnoreCase)
            && Peek(1).Kind == SyntaxKind.OpenParenToken)
        {
            var arrayIdentifier = NextToken();
            var open = NextToken();
            var elements = new List<ExpressionSyntax>();
            var commas = new List<SyntaxToken>();
            if (Current.Kind != SyntaxKind.CloseParenToken)
            {
                while (true)
                {
                    elements.Add(ParseBinaryExpression());
                    if (Current.Kind != SyntaxKind.CommaToken)
                        break;
                    commas.Add(NextToken());
                }
            }
            var close = Match(SyntaxKind.CloseParenToken);
            return new ArrayExpressionSyntax(arrayIdentifier, open, elements, commas, close);
        }

        if (Current.Kind == SyntaxKind.NewKeyword)
        {
            var newKeyword = NextToken();
            var typeName = Match(SyntaxKind.IdentifierToken);
            if (Current.Kind != SyntaxKind.OpenParenToken)
                return new NewExpressionSyntax(newKeyword, typeName, null, [], [], null);

            var open = NextToken();
            var arguments = new List<ExpressionSyntax>();
            var commas = new List<SyntaxToken>();
            if (Current.Kind != SyntaxKind.CloseParenToken)
            {
                while (true)
                {
                    arguments.Add(ParseBinaryExpression());
                    if (Current.Kind != SyntaxKind.CommaToken)
                        break;
                    commas.Add(NextToken());
                }
            }
            var close = Match(SyntaxKind.CloseParenToken);
            return new NewExpressionSyntax(newKeyword, typeName, open, arguments, commas, close);
        }

        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            var open = NextToken();
            var expression = ParseBinaryExpression();
            var close = Match(SyntaxKind.CloseParenToken);
            return new ParenthesizedExpressionSyntax(open, expression, close);
        }

        if (Current.Kind is SyntaxKind.NumberToken or SyntaxKind.StringToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.NullKeyword or SyntaxKind.EmptyKeyword or SyntaxKind.NothingKeyword)
            return new LiteralExpressionSyntax(NextToken());

        if (Current.Kind is SyntaxKind.MeKeyword or SyntaxKind.ParentKeyword)
        {
            var token = NextToken();
            return new NameExpressionSyntax(new SyntaxToken(SyntaxKind.IdentifierToken, token.Text, token.Value, token.Span));
        }

        if (Current.Kind is SyntaxKind.ByValKeyword or SyntaxKind.ByRefKeyword)
        {
            NextToken();
            return ParsePrimaryExpression();
        }

        // Statement keywords remain contextual where expressions allow identifiers.
        if (IsContextualIdentifier(Current.Kind))
        {
            return new NameExpressionSyntax(MatchIdentifier());
        }

        return new NameExpressionSyntax(Match(SyntaxKind.IdentifierToken));
    }

    private SyntaxToken MatchIdentifier()
    {
        if (Current.Kind == SyntaxKind.IdentifierToken)
            return NextToken();

        if (IsContextualIdentifier(Current.Kind))
        {
            var token = NextToken();
            return new SyntaxToken(SyntaxKind.IdentifierToken, token.Text, token.Value, token.Span);
        }

        return Match(SyntaxKind.IdentifierToken);
    }

    private static bool IsContextualIdentifier(SyntaxKind kind) => kind is
        SyntaxKind.ErrorKeyword or
        SyntaxKind.EventKeyword or
        SyntaxKind.FromKeyword or
        SyntaxKind.RemoveKeyword or
        SyntaxKind.OpenKeyword or
        SyntaxKind.CloseKeyword or
        SyntaxKind.InputKeyword or
        SyntaxKind.OutputKeyword or
        SyntaxKind.AppendKeyword or
        SyntaxKind.BinaryKeyword or
        SyntaxKind.RandomKeyword or
        SyntaxKind.LenKeyword or
        SyntaxKind.PrintKeyword or
        SyntaxKind.WriteKeyword or
        SyntaxKind.LineKeyword or
        SyntaxKind.SeekKeyword or
        SyntaxKind.SubKeyword or
        SyntaxKind.FunctionKeyword or
        SyntaxKind.SetKeyword or
        SyntaxKind.GetKeyword or
        SyntaxKind.IsKeyword or
        SyntaxKind.ByValKeyword or
        SyntaxKind.ByRefKeyword or
        SyntaxKind.MeKeyword;

    private SyntaxToken Match(SyntaxKind kind)
    {
        if (Current.Kind == kind)
            return NextToken();

        var span = new TextSpan(Current.Span.Start, 0);
        _diagnostics.Add(new SyntaxDiagnostic(
            "XPS1012",
            $"Expected {kind} but found {Current.Kind}.",
            span));
        return new SyntaxToken(kind, string.Empty, null, span);
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
