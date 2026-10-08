namespace XPScript.Compiler.Syntax;

public sealed class DeclarationParser
{
    private readonly string _text;
    private readonly int _baseOffset;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private readonly bool _classMemberContext;
    private readonly bool _classHasBase;

    public DeclarationParser(string text, int baseOffset = 0, bool classMemberContext = false, bool classHasBase = false)
    {
        _text = text;
        _baseOffset = baseOffset;
        _classMemberContext = classMemberContext;
        _classHasBase = classHasBase;
    }

    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public CompilationUnitSyntax ParseCompilationUnit()
    {
        var lines = GetLines();
        var declarations = new List<SyntaxNode>();
        for (var i = 0; i < lines.Count;)
        {
            if (string.IsNullOrWhiteSpace(lines[i].Text) || lines[i].Text.TrimStart().StartsWith("'", StringComparison.Ordinal)) { i++; continue; }
            var match = System.Text.RegularExpressions.Regex.Match(lines[i].Text, @"^\s*(?:Public\s+|Private\s+|Static\s+)*(Sub|Function|Class)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) { i++; continue; }
            var end = i + 1;
            var terminator = match.Groups[1].Value.Equals("Class", StringComparison.OrdinalIgnoreCase) ? "End Class" : match.Groups[1].Value.Equals("Function", StringComparison.OrdinalIgnoreCase) ? "End Function" : "End Sub";
            while (end < lines.Count && !lines[end].Text.Trim().StartsWith(terminator, StringComparison.OrdinalIgnoreCase)) end++;
            if (end < lines.Count) end++;
            var startOffset = lines[i].Start;
            var endOffset = end < lines.Count ? lines[end].Start : _text.Length;
            if (match.Groups[1].Value.Equals("Class", StringComparison.OrdinalIgnoreCase))
            {
                i = Math.Max(end, i + 1);
                continue;
            }
            var parser = new DeclarationParser(_text[startOffset..endOffset].TrimStart(), _baseOffset + startOffset);
            try
            {
                declarations.Add(parser.ParseDeclaration());
                _diagnostics.AddRange(parser.Diagnostics);
            }
            catch (InvalidOperationException exception)
            {
                _ = exception;
            }
            i = Math.Max(end, i + 1);
        }
        return new CompilationUnitSyntax(declarations);
    }

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
            {
                ValidateIndexedPropertySignatures(members);
                return new ClassDeclarationSyntax(visibility, classKeyword, identifier, extendKeyword, baseType, members, tokens[0], tokens[1]);
            }

            var memberPosition = 0;
            if (Peek(tokens, memberPosition).Kind == SyntaxKind.IdentifierToken &&
                Peek(tokens, memberPosition).Text.Equals("Const", StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Constants are not supported as class members.", Peek(tokens, memberPosition).Span));
                continue;
            }

            if (Peek(tokens, memberPosition).Kind == SyntaxKind.IdentifierToken &&
                Peek(tokens, memberPosition).Text.Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Static class members are not supported.", Peek(tokens, memberPosition).Span));
                var staticTarget = Peek(tokens, memberPosition + 1).Kind;
                if (staticTarget is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword or SyntaxKind.PropertyKeyword)
                {
                    var staticEnd = FindDeclarationEnd(lines, i + 1, staticTarget);
                    i = staticEnd;
                }
                continue;
            }

            SyntaxToken? memberVisibility = null;
            if (Peek(tokens, memberPosition).Kind is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword)
                memberVisibility = tokens[memberPosition++];

            if (Peek(tokens, memberPosition).Kind == SyntaxKind.ClassKeyword)
            {
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Nested class declarations are not allowed.", Peek(tokens, memberPosition).Span));
                var nestedEnd = FindDeclarationEnd(lines, i + 1, SyntaxKind.ClassKeyword);
                i = nestedEnd;
                continue;
            }

            if (Peek(tokens, memberPosition).Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword or SyntaxKind.PropertyKeyword)
            {
                var memberStart = line.Start;
                var targetKind = Peek(tokens, memberPosition).Kind;
                var endLine = FindDeclarationEnd(lines, i + 1, targetKind);
                var memberEnd = endLine < lines.Count
                    ? lines[endLine].Start + lines[endLine].Text.Length
                    : _text.Length;
                var memberText = _text.Substring(memberStart, memberEnd - memberStart);
                var parser = new DeclarationParser(memberText, _baseOffset + memberStart, classMemberContext: true, classHasBase: baseType is not null);
                members.Add(targetKind == SyntaxKind.PropertyKeyword
                    ? parser.ParseProperty(lines: parser.GetLines())
                    : parser.ParseDeclaration());
                _diagnostics.AddRange(parser.Diagnostics);
                i = endLine;
                continue;
            }

            var fieldIdentifier = Take(tokens, ref memberPosition, SyntaxKind.IdentifierToken);
            var asKeyword = Take(tokens, ref memberPosition, SyntaxKind.AsKeyword);
            var type = new TypeSyntax(Take(tokens, ref memberPosition, SyntaxKind.IdentifierToken));
            members.Add(new FieldDeclarationSyntax(memberVisibility, fieldIdentifier, asKeyword, type));

            if (Peek(tokens, memberPosition).Kind != SyntaxKind.EndOfFileToken)
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Class fields must use one declaration per line and cannot have an initializer.", Peek(tokens, memberPosition).Span));
        }

        var end = _baseOffset + _text.Length;
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Expected 'End Class' to close declaration.", new TextSpan(end, 0)));
        return new ClassDeclarationSyntax(visibility, classKeyword, identifier, extendKeyword, baseType, members,
            new SyntaxToken(SyntaxKind.EndKeyword, string.Empty, null, new TextSpan(end, 0)),
            new SyntaxToken(SyntaxKind.ClassKeyword, string.Empty, null, new TextSpan(end, 0)));
    }

    private void ValidateIndexedPropertySignatures(IReadOnlyList<SyntaxNode> members)
    {
        foreach (var group in members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            if (getter is null || getter.Parameters.Count == 0)
                continue;

            foreach (var setter in group.Where(p => p.IsSetter))
            {
                var setterIndexCount = Math.Max(0, setter.Parameters.Count - 1);
                var compatible = setter.Parameters.Count > 0 && setterIndexCount == getter.Parameters.Count;
                if (compatible)
                {
                    for (var i = 0; i < getter.Parameters.Count; i++)
                    {
                        var getterType = getter.Parameters[i].Type?.Identifier.Text ?? "Variant";
                        var setterType = setter.Parameters[i].Type?.Identifier.Text ?? "Variant";
                        if (!getterType.Equals(setterType, StringComparison.OrdinalIgnoreCase) || getter.Parameters[i].IsByRef != setter.Parameters[i].IsByRef)
                        {
                            compatible = false;
                            break;
                        }
                    }
                }

                if (!compatible)
                    _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"Indexed property '{group.Key}' accessors must use compatible index parameter signatures.", setter.Identifier.Span));
            }
        }
    }

    private PropertyDeclarationSyntax ParseProperty(IReadOnlyList<SourceLine> lines)
    {
        var lexer = new Lexer(lines[0].Text, _baseOffset + lines[0].Start);
        var header = lexer.Lex().Where(t => t.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken).ToArray();
        AddLexerDiagnostics(lexer.Diagnostics);
        var position = 0;
        SyntaxToken? visibility = null;
        if (Peek(header, position).Kind is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword)
            visibility = header[position++];
        var propertyKeyword = Take(header, ref position, SyntaxKind.PropertyKeyword);
        var accessor = Peek(header, position).Kind is SyntaxKind.GetKeyword or SyntaxKind.LetKeyword or SyntaxKind.SetKeyword
            ? header[position++]
            : Take(header, ref position, SyntaxKind.GetKeyword);
        var identifier = Take(header, ref position, SyntaxKind.IdentifierToken);
        SyntaxToken? openParen = null;
        SyntaxToken? closeParen = null;
        var parameters = new List<ParameterSyntax>();
        var commas = new List<SyntaxToken>();
        if (Peek(header, position).Kind == SyntaxKind.OpenParenToken)
        {
            var parsed = ParseParameters(header, ref position);
            openParen = parsed.OpenParen;
            parameters = parsed.Parameters;
            commas = parsed.Commas;
            closeParen = parsed.CloseParen;
        }
        SyntaxToken? asKeyword = null;
        TypeSyntax? type = null;
        if (Peek(header, position).Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = header[position++];
            type = new TypeSyntax(Take(header, ref position, SyntaxKind.IdentifierToken));
        }
        var (statements, endKeyword, endTarget) = ParseBody(lines, SyntaxKind.PropertyKeyword, "Property");
        return new PropertyDeclarationSyntax(visibility, propertyKeyword, accessor, identifier, openParen, parameters, commas, closeParen, asKeyword, type, statements, endKeyword, endTarget);
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
        var hasNewName = Peek(header, position).Kind == SyntaxKind.NewKeyword;
        var isConstructor = _classMemberContext && hasNewName;
        var identifier = hasNewName
            ? header[position++]
            : TakeMemberIdentifier(header, ref position);
        var (openParen, parameters, commas, closeParen) = ParseParameters(header, ref position);
        var (statements, endKeyword, endTarget) = ParseBody(lines, SyntaxKind.SubKeyword, "Sub");

        if (isConstructor)
            return new ConstructorDeclarationSyntax(visibility, subKeyword, identifier, openParen, parameters, commas, closeParen, statements, endKeyword, endTarget);

        if (_classMemberContext && identifier.Text.Equals("Delete", StringComparison.OrdinalIgnoreCase))
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
        var identifier = TakeMemberIdentifier(header, ref position);
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
            SyntaxToken? optionalKeyword = null;
            if (Peek(tokens, position).Text.Equals("Optional", StringComparison.OrdinalIgnoreCase))
                optionalKeyword = tokens[position++];
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

            SyntaxToken? equalsToken = null;
            ExpressionSyntax? defaultValue = null;
            if (Peek(tokens, position).Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = tokens[position++];
                var start = position;
                var depth = 0;
                // A comma inside a nested expression is not a parameter separator.
                while (Peek(tokens, position).Kind != SyntaxKind.EndOfFileToken)
                {
                    var kind = Peek(tokens, position).Kind;
                    if (depth == 0 && kind is SyntaxKind.CommaToken or SyntaxKind.CloseParenToken) break;
                    if (kind is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken) depth++;
                    if (kind is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken) depth--;
                    position++;
                }
                var end = Peek(tokens, position).Span.Start;
                var expressionTokens = tokens[start..position].Append(new SyntaxToken(SyntaxKind.EndOfFileToken, "", null, new TextSpan(end, 0)));
                var parser = new ExpressionParser(expressionTokens);
                defaultValue = parser.ParseExpression();
                _diagnostics.AddRange(parser.Diagnostics);
            }
            parameters.Add(new ParameterSyntax(modifier, identifier, asKeyword, type, optionalKeyword, equalsToken, defaultValue));
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
            ValidateClassReferenceContext(bodyText, _baseOffset + bodyStart);
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
        ValidateClassReferenceContext(_text.Substring(bodyStartAtEof), _baseOffset + bodyStartAtEof);

        return (bodyStatements,
            new SyntaxToken(SyntaxKind.EndKeyword, string.Empty, null, new TextSpan(end, 0)),
            new SyntaxToken(targetKind, string.Empty, null, new TextSpan(end, 0)));
    }

    private void ValidateClassReferenceContext(string bodyText, int bodyOffset)
    {
        var lexer = new Lexer(bodyText, bodyOffset);
        foreach (var token in lexer.Lex())
        {
            if (token.Kind == SyntaxKind.MeKeyword && !_classMemberContext)
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "'Me' is only valid inside class instance members.", token.Span));
            else if (token.Kind == SyntaxKind.ParentKeyword && (!_classMemberContext || !_classHasBase))
                _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "'Parent' requires the current class to Extend a base class.", token.Span));
        }
    }

    private void AddLexerDiagnostics(IEnumerable<LexerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            _diagnostics.Add(new SyntaxDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Span));
    }

    private SyntaxToken TakeMemberIdentifier(SyntaxToken[] tokens, ref int position)
    {
        var token = Peek(tokens, position);
        if (token.Kind == SyntaxKind.IdentifierToken || ExpressionParserContextual(token.Kind))
            return new SyntaxToken(SyntaxKind.IdentifierToken, tokens[position++].Text, tokens[position - 1].Value, token.Span);
        return Take(tokens, ref position, SyntaxKind.IdentifierToken);
    }

    private static bool ExpressionParserContextual(SyntaxKind kind) => kind is SyntaxKind.AppendKeyword or SyntaxKind.SetKeyword or SyntaxKind.GetKeyword or SyntaxKind.OpenKeyword or SyntaxKind.CloseKeyword;

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
