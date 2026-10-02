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
        AddLexerDiagnostics(headerLexer.Diagnostics);
        var position = 0;

        SyntaxToken? visibility = null;
        if (Peek(header, position).Kind is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword)
            visibility = header[position++];

        return Peek(header, position).Kind switch
        {
            SyntaxKind.SubKeyword => ParseSub(lines, header, ref position, visibility),
            SyntaxKind.FunctionKeyword => ParseFunction(lines, header, ref position, visibility),
            SyntaxKind.ClassKeyword => ParseClass(lines, header, ref position, visibility),
            _ => throw new InvalidOperationException("Expected Sub, Function or Class declaration.")
        };
    }

    private ClassDeclarationSyntax ParseClass(IReadOnlyList<SourceLine> lines, SyntaxToken[] header, ref int position, SyntaxToken? visibility)
    {
        var classKeyword = Take(header, ref position, SyntaxKind.ClassKeyword);
        var identifier = Take(header, ref position, SyntaxKind.IdentifierToken);
        SyntaxToken? extendKeyword = null;
        TypeSyntax? baseType = null;
        if (Peek(header, position).Kind == SyntaxKind.ExtendKeyword)
        {
            extendKeyword = header[position++];
            if (Peek(header, position).Kind == SyntaxKind.IdentifierToken)
                baseType = new TypeSyntax(header[position++]);
            else
                Take(header, ref position, SyntaxKind.IdentifierToken);
        }
        var members = new List<SyntaxNode>();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var lexer = new Lexer(line.Text, _baseOffset + line.Start);
            var tokens = lexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
            AddLexerDiagnostics(lexer.Diagnostics);

            if (tokens.Length >= 2 && tokens[0].Kind == SyntaxKind.EndKeyword && tokens[1].Kind == SyntaxKind.ClassKeyword)
                return new ClassDeclarationSyntax(visibility, classKeyword, identifier, extendKeyword, baseType, members, tokens[0], tokens[1]);

            var memberPosition = 0;
            SyntaxToken? memberVisibility = null;
            if (Peek(tokens, memberPosition).Kind is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword)
                memberVisibility = tokens[memberPosition++];

            if (Peek(tokens, memberPosition).Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword)
            {
                var memberStart = line.Start;
                var targetKind = Peek(tokens, memberPosition).Kind;
                var endLine = FindDeclarationEnd(lines, i + 1, targetKind);
                var memberEnd = endLine < lines.Count
                    ? lines[endLine].Start + lines[endLine].Text.Length
                    : _text.Length;
                var memberText = _text.Substring(memberStart, memberEnd - memberStart);
                var parser = new DeclarationParser(memberText, _baseOffset + memberStart);
                members.Add(parser.ParseDeclaration());
                _diagnostics.AddRange(parser.Diagnostics);
                i = endLine;
                continue;
            }

            var fieldIdentifier = Take(tokens, ref memberPosition, SyntaxKind.IdentifierToken);
            var asKeyword = Take(tokens, ref memberPosition, SyntaxKind.AsKeyword);
            var type = new TypeSyntax(Take(tokens, ref memberPosition, SyntaxKind.IdentifierToken));
            members.Add(new FieldDeclarationSyntax(memberVisibility, fieldIdentifier, asKeyword, type));
        }

        var end = _baseOffset + _text.Length;
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Expected 'End Class' to close declaration.", new TextSpan(end, 0)));
        return new ClassDeclarationSyntax(visibility, classKeyword, identifier, extendKeyword, baseType, members,
            new SyntaxToken(SyntaxKind.EndKeyword, string.Empty, null, new TextSpan(end, 0)),
            new SyntaxToken(SyntaxKind.ClassKeyword, string.Empty, null, new TextSpan(end, 0)));
    }

    private static int FindDeclarationEnd(IReadOnlyList<SourceLine> lines, int start, SyntaxKind targetKind)
    {
        for (var i = start; i < lines.Count; i++)
        {
            var lexer = new Lexer(lines[i].Text);
            var tokens = lexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
            if (tokens.Length >= 2 && tokens[0].Kind == SyntaxKind.EndKeyword && tokens[1].Kind == targetKind)
                return i;
        }
        return lines.Count;
    }

    private SyntaxNode ParseSub(IReadOnlyList<SourceLine> lines, SyntaxToken[] header, ref int position, SyntaxToken? visibility)
    {
        var subKeyword = Take(header, ref position, SyntaxKind.SubKeyword);
        var isConstructor = Peek(header, position).Kind == SyntaxKind.NewKeyword;
        var identifier = isConstructor
            ? header[position++]
            : Take(header, ref position, SyntaxKind.IdentifierToken);
        var (openParen, parameters, commas, closeParen) = ParseParameters(header, ref position);
        var (statements, endKeyword, endTarget) = ParseBody(lines, SyntaxKind.SubKeyword, "Sub");

        if (isConstructor)
            return new ConstructorDeclarationSyntax(visibility, subKeyword, identifier, openParen, parameters, commas, closeParen, statements, endKeyword, endTarget);

        if (identifier.Text.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            if (parameters.Count != 0)
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Sub Delete cannot have parameters.", parameters[0].Span));
            return new DestructorDeclarationSyntax(visibility, subKeyword, identifier, openParen, closeParen, statements, endKeyword, endTarget);
        }

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
        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var lexer = new Lexer(line.Text, _baseOffset + line.Start);
            var tokens = lexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
            AddLexerDiagnostics(lexer.Diagnostics);

            if (tokens.Length < 2 || tokens[0].Kind != SyntaxKind.EndKeyword || tokens[1].Kind != targetKind)
                continue;

            var bodyStart = lines[0].Start + lines[0].Text.Length;
            if (bodyStart < _text.Length && _text[bodyStart] == '\r')
                bodyStart++;
            if (bodyStart < _text.Length && _text[bodyStart] == '\n')
                bodyStart++;

            var bodyLength = Math.Max(0, line.Start - bodyStart);
            var bodyText = _text.Substring(bodyStart, bodyLength);
            var parser = new StatementParser(bodyText, _baseOffset + bodyStart);
            var statements = parser.ParseStatements().ToList();
            _diagnostics.AddRange(parser.Diagnostics);
            return (statements, tokens[0], tokens[1]);
        }

        var end = _baseOffset + _text.Length;
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"Expected 'End {targetName}' to close declaration.", new TextSpan(end, 0)));

        var bodyStartAtEof = lines[0].Start + lines[0].Text.Length;
        if (bodyStartAtEof < _text.Length && _text[bodyStartAtEof] == '\r')
            bodyStartAtEof++;
        if (bodyStartAtEof < _text.Length && _text[bodyStartAtEof] == '\n')
            bodyStartAtEof++;
        var bodyParser = new StatementParser(_text.Substring(bodyStartAtEof), _baseOffset + bodyStartAtEof);
        var bodyStatements = bodyParser.ParseStatements().ToList();
        _diagnostics.AddRange(bodyParser.Diagnostics);

        return (bodyStatements,
            new SyntaxToken(SyntaxKind.EndKeyword, string.Empty, null, new TextSpan(end, 0)),
            new SyntaxToken(targetKind, string.Empty, null, new TextSpan(end, 0)));
    }

    private void AddLexerDiagnostics(IEnumerable<LexerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            _diagnostics.Add(new SyntaxDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Span));
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
