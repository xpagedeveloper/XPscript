namespace XPScript.Compiler.Syntax;

public sealed class DeclarationParser
{
    private readonly string _text;
    private readonly int _baseOffset;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];

    public DeclarationParser(string text, int baseOffset = 0)
    {
        _text = text;
        _baseOffset = baseOffset;
    }

    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public SyntaxNode ParseDeclaration()
    {
        var lines = GetLines();
        if (lines.Count == 0)
            throw new InvalidOperationException("Declaration source is empty.");

        var headerLexer = new Lexer(lines[0].Text, _baseOffset + lines[0].Start);
        var header = headerLexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
        _diagnostics.AddRange(headerLexer.Diagnostics);
        var position = 0;

        SyntaxToken? visibility = null;
        if (Peek(header, position).Kind is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword)
            visibility = header[position++];

        return Peek(header, position).Kind switch
        {
            SyntaxKind.SubKeyword => ParseSub(lines, header, ref position, visibility),
            SyntaxKind.FunctionKeyword => ParseFunction(lines, header, ref position, visibility),
            _ => throw new InvalidOperationException("Expected Sub or Function declaration.")
        };
    }

    private SubDeclarationSyntax ParseSub(IReadOnlyList<SourceLine> lines, SyntaxToken[] header, ref int position, SyntaxToken? visibility)
    {
        var subKeyword = Take(header, ref position, SyntaxKind.SubKeyword);
        var identifier = Take(header, ref position, SyntaxKind.IdentifierToken);
        var (openParen, parameters, commas, closeParen) = ParseParameters(header, ref position);
        var (statements, endKeyword, endTarget) = ParseBody(lines, SyntaxKind.SubKeyword, "Sub");
        return new SubDeclarationSyntax(visibility, subKeyword, identifier, openParen, parameters, commas, closeParen, statements, endKeyword, endTarget);
    }

    private FunctionDeclarationSyntax ParseFunction(IReadOnlyList<SourceLine> lines, SyntaxToken[] header, ref int position, SyntaxToken? visibility)
    {
        var functionKeyword = Take(header, ref position, SyntaxKind.FunctionKeyword);
        var identifier = Take(header, ref position, SyntaxKind.IdentifierToken);
        var (openParen, parameters, commas, closeParen) = ParseParameters(header, ref position);

        SyntaxToken? asKeyword = null;
        TypeSyntax? returnType = null;
        if (Peek(header, position).Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = header[position++];
            returnType = new TypeSyntax(Take(header, ref position, SyntaxKind.IdentifierToken));
        }

        var (statements, endKeyword, endTarget) = ParseBody(lines, SyntaxKind.FunctionKeyword, "Function");
        return new FunctionDeclarationSyntax(visibility, functionKeyword, identifier, openParen, parameters, commas, closeParen, asKeyword, returnType, statements, endKeyword, endTarget);
    }

    private (SyntaxToken OpenParen, List<ParameterSyntax> Parameters, List<SyntaxToken> Commas, SyntaxToken CloseParen) ParseParameters(SyntaxToken[] tokens, ref int position)
    {
        var openParen = Take(tokens, ref position, SyntaxKind.OpenParenToken);
        var parameters = new List<ParameterSyntax>();
        var commas = new List<SyntaxToken>();

        while (Peek(tokens, position).Kind is not SyntaxKind.CloseParenToken and not SyntaxKind.EndOfFileToken)
        {
            SyntaxToken? modifier = null;
            if (Peek(tokens, position).Kind is SyntaxKind.ByRefKeyword or SyntaxKind.ByValKeyword)
                modifier = tokens[position++];

            var identifier = Take(tokens, ref position, SyntaxKind.IdentifierToken);
            SyntaxToken? asKeyword = null;
            TypeSyntax? type = null;
            if (Peek(tokens, position).Kind == SyntaxKind.AsKeyword)
            {
                asKeyword = tokens[position++];
                type = new TypeSyntax(Take(tokens, ref position, SyntaxKind.IdentifierToken));
            }

            parameters.Add(new ParameterSyntax(modifier, identifier, asKeyword, type));
            if (Peek(tokens, position).Kind != SyntaxKind.CommaToken)
                break;
            commas.Add(tokens[position++]);
        }

        var closeParen = Take(tokens, ref position, SyntaxKind.CloseParenToken);
        return (openParen, parameters, commas, closeParen);
    }

    private (List<StatementSyntax> Statements, SyntaxToken EndKeyword, SyntaxToken EndTarget) ParseBody(IReadOnlyList<SourceLine> lines, SyntaxKind targetKind, string targetName)
    {
        var statements = new List<StatementSyntax>();
        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var lexer = new Lexer(line.Text, _baseOffset + line.Start);
            var tokens = lexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
            _diagnostics.AddRange(lexer.Diagnostics);

            if (tokens.Length >= 2 && tokens[0].Kind == SyntaxKind.EndKeyword && tokens[1].Kind == targetKind)
                return (statements, tokens[0], tokens[1]);

            var parser = new StatementParser(line.Text, _baseOffset + line.Start);
            statements.Add(parser.ParseStatement());
            _diagnostics.AddRange(parser.Diagnostics);
        }

        var end = _baseOffset + _text.Length;
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"Expected 'End {targetName}' to close declaration.", new TextSpan(end, 0)));
        return (statements,
            new SyntaxToken(SyntaxKind.EndKeyword, string.Empty, null, new TextSpan(end, 0)),
            new SyntaxToken(targetKind, string.Empty, null, new TextSpan(end, 0)));
    }

    private SyntaxToken Take(SyntaxToken[] tokens, ref int position, SyntaxKind expected)
    {
        var current = Peek(tokens, position);
        if (current.Kind == expected)
        {
            position++;
            return current;
        }

        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"Expected {expected} but found {current.Kind}.", current.Span));
        return new SyntaxToken(expected, string.Empty, null, new TextSpan(current.Span.Start, 0));
    }

    private SyntaxToken Peek(SyntaxToken[] tokens, int position) =>
        position < tokens.Length
            ? tokens[position]
            : new SyntaxToken(SyntaxKind.EndOfFileToken, string.Empty, null, new TextSpan(_baseOffset + _text.Length, 0));

    private List<SourceLine> GetLines()
    {
        var result = new List<SourceLine>();
        var start = 0;
        for (var i = 0; i <= _text.Length; i++)
        {
            if (i != _text.Length && _text[i] != '\n')
                continue;

            var length = i - start;
            if (length > 0 && _text[start + length - 1] == '\r')
                length--;
            result.Add(new SourceLine(start, _text.Substring(start, length)));
            start = i + 1;
        }
        return result;
    }

    private sealed record SourceLine(int Start, string Text);
}
