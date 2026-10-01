using System.Globalization;

namespace XPScript.Compiler.Syntax;

public sealed class Lexer
{
    private readonly string _text;
    private readonly int _baseOffset;
    private int _position;
    private readonly List<LexerDiagnostic> _diagnostics = [];

    public IReadOnlyList<LexerDiagnostic> Diagnostics => _diagnostics;

    public Lexer(string text, int baseOffset = 0)
    {
        _text = text ?? string.Empty;
        _baseOffset = baseOffset;
    }

    public IReadOnlyList<SyntaxToken> Lex()
    {
        var tokens = new List<SyntaxToken>();
        while (true)
        {
            var token = NextToken();
            tokens.Add(token);
            if (token.Kind == SyntaxKind.EndOfFileToken)
                return tokens;
        }
    }

    public SyntaxToken NextToken()
    {
        while (true)
        {
            while (Current is ' ' or '\t')
                _position++;

            if (Current != '\'')
                break;

            while (_position < _text.Length && Current is not '\r' and not '\n')
                _position++;
        }

        var start = _position;

        if (_position >= _text.Length)
            return Token(SyntaxKind.EndOfFileToken, start, 0);

        if (Current is '\r' or '\n')
        {
            if (Current == '\r' && Peek(1) == '\n')
                _position += 2;
            else
                _position++;
            return Token(SyntaxKind.NewLineToken, start, _position - start);
        }

        if (char.IsDigit(Current))
        {
            while (char.IsDigit(Current))
                _position++;

            var isDecimal = false;
            if (Current == '.' && char.IsDigit(Peek(1)))
            {
                isDecimal = true;
                _position++;
                while (char.IsDigit(Current))
                    _position++;
            }

            var text = _text[start.._position];
            object? value = isDecimal
                ? double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var doubleValue) ? doubleValue : null
                : long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var longValue) ? longValue : null;
            return new SyntaxToken(SyntaxKind.NumberToken, text, value, Span(start, text.Length));
        }

        if (Current == '"')
        {
            _position++;
            var value = new System.Text.StringBuilder();
            while (_position < _text.Length)
            {
                if (Current == '"')
                {
                    if (Peek(1) == '"')
                    {
                        value.Append('"');
                        _position += 2;
                        continue;
                    }
                    _position++;
                    break;
                }
                value.Append(Current);
                _position++;
            }
            var text = _text[start.._position];
            if (text.Length == 0 || text[^1] != '"')
                _diagnostics.Add(new LexerDiagnostic("XPS1006", "Unterminated string literal.", Span(start, text.Length)));
            return new SyntaxToken(SyntaxKind.StringToken, text, value.ToString(), Span(start, text.Length));
        }

        if (char.IsLetter(Current) || Current == '_')
        {
            _position++;
            while (char.IsLetterOrDigit(Current) || Current == '_')
                _position++;
            var text = _text[start.._position];
            return new SyntaxToken(KeywordKind(text), text, null, Span(start, text.Length));
        }

        _position++;
        return CurrentAt(start) switch
        {
            '(' => Token(SyntaxKind.OpenParenToken, start, 1),
            ')' => Token(SyntaxKind.CloseParenToken, start, 1),
            ',' => Token(SyntaxKind.CommaToken, start, 1),
            '.' => Token(SyntaxKind.DotToken, start, 1),
            '[' => Token(SyntaxKind.OpenBracketToken, start, 1),
            ']' => Token(SyntaxKind.CloseBracketToken, start, 1),
            '+' => Token(SyntaxKind.PlusToken, start, 1),
            '-' => Token(SyntaxKind.MinusToken, start, 1),
            '*' => Token(SyntaxKind.StarToken, start, 1),
            '/' => Token(SyntaxKind.SlashToken, start, 1),
            '&' => Token(SyntaxKind.AmpersandToken, start, 1),
            '=' => Token(SyntaxKind.EqualsToken, start, 1),
            '<' when Current == '=' => ConsumeSecond(SyntaxKind.LessOrEqualsToken, start),
            '<' when Current == '>' => ConsumeSecond(SyntaxKind.LessGreaterToken, start),
            '<' => Token(SyntaxKind.LessToken, start, 1),
            '>' when Current == '=' => ConsumeSecond(SyntaxKind.GreaterOrEqualsToken, start),
            '>' => Token(SyntaxKind.GreaterToken, start, 1),
            _ => BadToken(start)
        };
    }

    private char Current => _position < _text.Length ? _text[_position] : '\0';
    private char Peek(int offset) => _position + offset < _text.Length ? _text[_position + offset] : '\0';
    private char CurrentAt(int position) => position < _text.Length ? _text[position] : '\0';

    private SyntaxToken BadToken(int start)
    {
        var token = Token(SyntaxKind.BadToken, start, 1);
        _diagnostics.Add(new LexerDiagnostic("XPS1012", $"Invalid character '{token.Text}'.", token.Span));
        return token;
    }

    private SyntaxToken ConsumeSecond(SyntaxKind kind, int start)
    {
        _position++;
        return Token(kind, start, 2);
    }

    private TextSpan Span(int start, int length) => new(_baseOffset + start, length);

    private SyntaxToken Token(SyntaxKind kind, int start, int length) =>
        new(kind, _text.Substring(start, length), null, Span(start, length));

    private static SyntaxKind KeywordKind(string text) => text.ToUpperInvariant() switch
    {
        "NOT" => SyntaxKind.NotKeyword,
        "AND" => SyntaxKind.AndKeyword,
        "OR" => SyntaxKind.OrKeyword,
        "TRUE" => SyntaxKind.TrueKeyword,
        "FALSE" => SyntaxKind.FalseKeyword,
        "IF" => SyntaxKind.IfKeyword,
        "THEN" => SyntaxKind.ThenKeyword,
        "ELSE" => SyntaxKind.ElseKeyword,
        "ELSEIF" => SyntaxKind.ElseIfKeyword,
        "END" => SyntaxKind.EndKeyword,
        "SET" => SyntaxKind.SetKeyword,
        "DIM" => SyntaxKind.DimKeyword,
        "AS" => SyntaxKind.AsKeyword,
        "WHILE" => SyntaxKind.WhileKeyword,
        "WEND" => SyntaxKind.WendKeyword,
        "FOR" => SyntaxKind.ForKeyword,
        "TO" => SyntaxKind.ToKeyword,
        "STEP" => SyntaxKind.StepKeyword,
        "NEXT" => SyntaxKind.NextKeyword,
        "FORALL" => SyntaxKind.ForAllKeyword,
        "IN" => SyntaxKind.InKeyword,
        "DO" => SyntaxKind.DoKeyword,
        "LOOP" => SyntaxKind.LoopKeyword,
        "UNTIL" => SyntaxKind.UntilKeyword,
        "SELECT" => SyntaxKind.SelectKeyword,
        "CASE" => SyntaxKind.CaseKeyword,
        "IS" => SyntaxKind.IsKeyword,
        "CALL" => SyntaxKind.CallKeyword,
        "ON" => SyntaxKind.OnKeyword,
        "ERROR" => SyntaxKind.ErrorKeyword,
        "GOTO" => SyntaxKind.GoToKeyword,
        "RESUME" => SyntaxKind.ResumeKeyword,
        "NEW" => SyntaxKind.NewKeyword,
        _ => SyntaxKind.IdentifierToken
    };
}
