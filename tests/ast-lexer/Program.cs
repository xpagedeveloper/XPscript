using XPScript.Compiler.Syntax;

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{message}: expected '{expected}', actual '{actual}'.");
}

static SyntaxToken[] Lex(string text) => new Lexer(text).Lex().ToArray();

var keywords = Lex("not AND Or true FALSE if THEN else elseif END name");
var expectedKeywords = new[]
{
    SyntaxKind.NotKeyword, SyntaxKind.AndKeyword, SyntaxKind.OrKeyword,
    SyntaxKind.TrueKeyword, SyntaxKind.FalseKeyword, SyntaxKind.IfKeyword,
    SyntaxKind.ThenKeyword, SyntaxKind.ElseKeyword, SyntaxKind.ElseIfKeyword,
    SyntaxKind.EndKeyword, SyntaxKind.IdentifierToken, SyntaxKind.EndOfFileToken
};
Equal(string.Join(",", expectedKeywords), string.Join(",", keywords.Select(t => t.Kind)), "keyword kinds");

var number = Lex("12345")[0];
Equal(SyntaxKind.NumberToken, number.Kind, "number kind");
Equal(12345L, (long)number.Value!, "number value");
Equal(new TextSpan(0, 5), number.Span, "number span");

var str = Lex("\"hello \"\"XP\"\"\"")[0];
Equal(SyntaxKind.StringToken, str.Kind, "string kind");
Equal("hello \"XP\"", (string)str.Value!, "escaped string value");
Equal(new TextSpan(0, 14), str.Span, "string span");

var operators = Lex("() , . + - * / & = < <= > >= <>");
var expectedOperators = new[]
{
    SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken, SyntaxKind.CommaToken,
    SyntaxKind.DotToken, SyntaxKind.PlusToken, SyntaxKind.MinusToken,
    SyntaxKind.StarToken, SyntaxKind.SlashToken, SyntaxKind.AmpersandToken,
    SyntaxKind.EqualsToken, SyntaxKind.LessToken, SyntaxKind.LessOrEqualsToken,
    SyntaxKind.GreaterToken, SyntaxKind.GreaterOrEqualsToken, SyntaxKind.LessGreaterToken,
    SyntaxKind.EndOfFileToken
};
Equal(string.Join(",", expectedOperators), string.Join(",", operators.Select(t => t.Kind)), "operator kinds");

var lf = Lex("a\nb");
Equal(new TextSpan(1, 1), lf[1].Span, "LF span");
Equal(new TextSpan(2, 1), lf[2].Span, "token after LF span");

var crlf = Lex("a\r\nb");
Equal(new TextSpan(1, 2), crlf[1].Span, "CRLF span");
Equal(new TextSpan(3, 1), crlf[2].Span, "token after CRLF span");

const string regression = "Not RunCommand(\"where.exe\", Array(\"winget\"))";
var regressionTokens = Lex(regression);
var regressionKinds = new[]
{
    SyntaxKind.NotKeyword, SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken,
    SyntaxKind.StringToken, SyntaxKind.CommaToken, SyntaxKind.IdentifierToken,
    SyntaxKind.OpenParenToken, SyntaxKind.StringToken, SyntaxKind.CloseParenToken,
    SyntaxKind.CloseParenToken, SyntaxKind.EndOfFileToken
};
Equal(string.Join(",", regressionKinds), string.Join(",", regressionTokens.Select(t => t.Kind)), "Not RunCommand tokenization");
foreach (var token in regressionTokens.Where(t => t.Kind != SyntaxKind.EndOfFileToken))
    Equal(token.Text, regression.Substring(token.Span.Start, token.Span.Length), $"source span for {token.Kind}");

Console.WriteLine("AST lexer focused tests passed.");
