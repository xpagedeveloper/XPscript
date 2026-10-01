namespace XPScript.Compiler.Syntax;

public sealed class StatementParser
{
    private readonly SyntaxToken[] _tokens;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private int _position;

    public StatementParser(string text, int baseOffset = 0)
    {
        var lexer = new Lexer(text, baseOffset);
        _tokens = lexer.Lex().ToArray();
        foreach (var diagnostic in lexer.Diagnostics)
            _diagnostics.Add(new SyntaxDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Span));
    }

    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public StatementSyntax ParseStatement()
    {
        var statement = ParseCurrentStatement();

        if (Current.Kind == SyntaxKind.NewLineToken)
            NextToken();

        if (Current.Kind != SyntaxKind.EndOfFileToken)
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                $"Unexpected token {Current.Kind} after statement.",
                Current.Span));

        return statement;
    }

    private StatementSyntax ParseCurrentStatement() =>
        Current.Kind == SyntaxKind.IfKeyword
            ? ParseIfStatement()
            : ParseExpressionStatement();

    private StatementSyntax ParseIfStatement()
    {
        var ifKeyword = NextToken();
        var thenIndex = FindThenIndex();
        if (thenIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected ThenKeyword in If statement.",
                new TextSpan(Current.Span.Start, 0)));
            return new ExpressionStatementSyntax(ParseExpressionUntilLineEnd());
        }

        var condition = ParseExpressionRange(_position, thenIndex, _tokens[thenIndex].Span.Start);
        _position = thenIndex;
        var thenKeyword = NextToken();

        if (Current.Kind != SyntaxKind.NewLineToken)
            return new IfStatementSyntax(ifKeyword, condition, thenKeyword, ParseExpressionStatement());

        NextToken();

        var thenStatements = ParseBranchStatements();
        var elseIfClauses = new List<ElseIfClauseSyntax>();
        while (Current.Kind == SyntaxKind.ElseIfKeyword)
            elseIfClauses.Add(ParseElseIfClause());

        var elseStatements = new List<StatementSyntax>();
        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            NextToken();
            ConsumeRequiredNewLine("Else");
            elseStatements.AddRange(ParseBranchStatements());
        }

        SyntaxToken? endKeyword = null;
        SyntaxToken? endIfKeyword = null;
        if (Current.Kind == SyntaxKind.EndKeyword)
        {
            endKeyword = NextToken();
            endIfKeyword = Match(SyntaxKind.IfKeyword);
        }
        else
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected 'End If' to close block If statement.",
                new TextSpan(Current.Span.Start, 0)));
        }

        return new IfStatementSyntax(
            ifKeyword,
            condition,
            thenKeyword,
            thenStatements,
            elseIfClauses,
            elseStatements,
            endKeyword,
            endIfKeyword);
    }

    private ElseIfClauseSyntax ParseElseIfClause()
    {
        var elseIfKeyword = NextToken();
        var thenIndex = FindThenIndex();
        if (thenIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected ThenKeyword in ElseIf clause.",
                new TextSpan(Current.Span.Start, 0)));
            var missingThen = new SyntaxToken(
                SyntaxKind.ThenKeyword,
                string.Empty,
                null,
                new TextSpan(Current.Span.Start, 0));
            return new ElseIfClauseSyntax(
                elseIfKeyword,
                ParseExpressionUntilLineEnd(),
                missingThen,
                []);
        }

        var condition = ParseExpressionRange(_position, thenIndex, _tokens[thenIndex].Span.Start);
        _position = thenIndex;
        var thenKeyword = NextToken();
        ConsumeRequiredNewLine("ElseIf");
        var statements = ParseBranchStatements();

        return new ElseIfClauseSyntax(elseIfKeyword, condition, thenKeyword, statements);
    }

    private List<StatementSyntax> ParseBranchStatements()
    {
        var statements = new List<StatementSyntax>();
        while (Current.Kind is not SyntaxKind.ElseIfKeyword
               and not SyntaxKind.ElseKeyword
               and not SyntaxKind.EndKeyword
               and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        return statements;
    }

    private void ConsumeRequiredNewLine(string context)
    {
        if (Current.Kind == SyntaxKind.NewLineToken)
        {
            NextToken();
            return;
        }

        _diagnostics.Add(new SyntaxDiagnostic(
            "XPS1012",
            $"Expected newline after {context}.",
            new TextSpan(Current.Span.Start, 0)));
    }

    private StatementSyntax ParseExpressionStatement() =>
        new ExpressionStatementSyntax(ParseExpressionUntilLineEnd());

    private ExpressionSyntax ParseExpressionUntilLineEnd()
    {
        var end = _position;
        while (_tokens[end].Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
            end++;

        var eofPosition = _tokens[end].Span.Start;
        var expression = ParseExpressionRange(_position, end, eofPosition);
        _position = end;
        return expression;
    }

    private ExpressionSyntax ParseExpressionRange(int start, int end, int eofPosition)
    {
        if (start == end)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected expression.",
                new TextSpan(eofPosition, 0)));
            return new NameExpressionSyntax(new SyntaxToken(
                SyntaxKind.IdentifierToken,
                string.Empty,
                null,
                new TextSpan(eofPosition, 0)));
        }

        var expressionTokens = _tokens[start..end]
            .Append(new SyntaxToken(
                SyntaxKind.EndOfFileToken,
                string.Empty,
                null,
                new TextSpan(eofPosition, 0)));

        var parser = new ExpressionParser(expressionTokens);
        var expression = parser.ParseExpression();
        _diagnostics.AddRange(parser.Diagnostics);
        return expression;
    }

    private int FindThenIndex()
    {
        for (var i = _position; i < _tokens.Length; i++)
        {
            if (_tokens[i].Kind is SyntaxKind.NewLineToken or SyntaxKind.EndOfFileToken)
                return -1;
            if (_tokens[i].Kind == SyntaxKind.ThenKeyword)
                return i;
        }

        return -1;
    }

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

    private SyntaxToken Current => _tokens[Math.Min(_position, _tokens.Length - 1)];

    private SyntaxToken NextToken()
    {
        var current = Current;
        _position++;
        return current;
    }
}
