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

var commentLexer = new Lexer("Print \"it's fine\" ' trailing comment\nnext");
var commentTokens = commentLexer.Lex().ToArray();
Equal(SyntaxKind.IdentifierToken, commentTokens[0].Kind, "comment prefix identifier");
Equal("it's fine", (string)commentTokens[1].Value!, "apostrophe inside string");
Equal(SyntaxKind.NewLineToken, commentTokens[2].Kind, "comment preserves newline");
Equal("next", commentTokens[3].Text, "token after comment");
Equal(0, commentLexer.Diagnostics.Count, "valid comment diagnostics");

var badLexer = new Lexer("@");
var badTokens = badLexer.Lex().ToArray();
Equal(SyntaxKind.BadToken, badTokens[0].Kind, "bad token kind");
Equal(1, badLexer.Diagnostics.Count, "bad token diagnostic count");
Equal("XPS1012", badLexer.Diagnostics[0].Code, "bad token diagnostic code");
Equal(new TextSpan(0, 1), badLexer.Diagnostics[0].Span, "bad token diagnostic span");

var unterminatedLexer = new Lexer("\"unterminated");
var unterminatedTokens = unterminatedLexer.Lex().ToArray();
Equal(SyntaxKind.StringToken, unterminatedTokens[0].Kind, "unterminated string token kind");
Equal(1, unterminatedLexer.Diagnostics.Count, "unterminated diagnostic count");
Equal("XPS1006", unterminatedLexer.Diagnostics[0].Code, "unterminated diagnostic code");
Equal(new TextSpan(0, 13), unterminatedLexer.Diagnostics[0].Span, "unterminated diagnostic span");

var notToken = new SyntaxToken(SyntaxKind.NotKeyword, "Not", null, new TextSpan(0, 3));
var runCommandToken = new SyntaxToken(SyntaxKind.IdentifierToken, "RunCommand", null, new TextSpan(4, 10));
ExpressionSyntax manualAst = new UnaryExpressionSyntax(notToken, new NameExpressionSyntax(runCommandToken));
Equal(SyntaxKind.UnaryExpression, manualAst.Kind, "manual AST unary kind");
Equal(new TextSpan(0, 14), manualAst.Span, "manual AST composed span");
Equal(SyntaxKind.NameExpression, ((UnaryExpressionSyntax)manualAst).Operand.Kind, "manual AST operand kind");

var one = new LiteralExpressionSyntax(new SyntaxToken(SyntaxKind.NumberToken, "1", 1L, new TextSpan(0, 1)));
var two = new LiteralExpressionSyntax(new SyntaxToken(SyntaxKind.NumberToken, "2", 2L, new TextSpan(4, 1)));
var binary = new BinaryExpressionSyntax(one, new SyntaxToken(SyntaxKind.PlusToken, "+", null, new TextSpan(2, 1)), two);
Equal(SyntaxKind.BinaryExpression, binary.Kind, "manual AST binary kind");
Equal(new TextSpan(0, 5), binary.Span, "manual AST binary span");

var precedenceAst = new ExpressionParser("1 + 2 * 3").ParseExpression();
Equal(SyntaxKind.BinaryExpression, precedenceAst.Kind, "precedence root kind");
var precedenceRoot = (BinaryExpressionSyntax)precedenceAst;
Equal(SyntaxKind.PlusToken, precedenceRoot.OperatorToken.Kind, "precedence root operator");
Equal(SyntaxKind.StarToken, ((BinaryExpressionSyntax)precedenceRoot.Right).OperatorToken.Kind, "precedence nested operator");

var unaryAst = new ExpressionParser("Not False Or True").ParseExpression();
Equal(SyntaxKind.BinaryExpression, unaryAst.Kind, "unary/boolean root kind");
var booleanRoot = (BinaryExpressionSyntax)unaryAst;
Equal(SyntaxKind.OrKeyword, booleanRoot.OperatorToken.Kind, "boolean root operator");
Equal(SyntaxKind.UnaryExpression, booleanRoot.Left.Kind, "Not binds before Or");
Equal(SyntaxKind.NotKeyword, ((UnaryExpressionSyntax)booleanRoot.Left).OperatorToken.Kind, "Not operator");

var parenAst = new ExpressionParser("(1 + 2) * 3").ParseExpression();
Equal(SyntaxKind.BinaryExpression, parenAst.Kind, "parenthesized root kind");
var parenRoot = (BinaryExpressionSyntax)parenAst;
Equal(SyntaxKind.StarToken, parenRoot.OperatorToken.Kind, "parenthesized root operator");
Equal(SyntaxKind.ParenthesizedExpression, parenRoot.Left.Kind, "parenthesized left kind");

var runCommandAst = new ExpressionParser("Not RunCommand(\"where.exe\", Array(\"winget\"))").ParseExpression();
Equal(SyntaxKind.UnaryExpression, runCommandAst.Kind, "Not RunCommand root");
var notRunCommand = (UnaryExpressionSyntax)runCommandAst;
Equal(SyntaxKind.NotKeyword, notRunCommand.OperatorToken.Kind, "Not RunCommand operator");
Equal(SyntaxKind.CallExpression, notRunCommand.Operand.Kind, "Not operand is call");
var runCommandCall = (CallExpressionSyntax)notRunCommand.Operand;
Equal("RunCommand", ((NameExpressionSyntax)runCommandCall.Target).IdentifierToken.Text, "RunCommand target");
Equal(2, runCommandCall.Arguments.Count, "RunCommand argument count");
Equal(SyntaxKind.CallExpression, runCommandCall.Arguments[1].Kind, "nested Array call");
var arrayCall = (CallExpressionSyntax)runCommandCall.Arguments[1];
Equal("Array", ((NameExpressionSyntax)arrayCall.Target).IdentifierToken.Text, "Array target");
Equal(new TextSpan(0, 44), runCommandAst.Span, "Not RunCommand full span");

var falseComparisonAst = new ExpressionParser("RunCommand(\"where.exe\", Array(\"winget\")) = False").ParseExpression();
Equal(SyntaxKind.BinaryExpression, falseComparisonAst.Kind, "RunCommand False comparison root");
var falseComparison = (BinaryExpressionSyntax)falseComparisonAst;
Equal(SyntaxKind.EqualsToken, falseComparison.OperatorToken.Kind, "RunCommand False comparison operator");
Equal(SyntaxKind.CallExpression, falseComparison.Left.Kind, "comparison left is call");
Equal(SyntaxKind.LiteralExpression, falseComparison.Right.Kind, "comparison right is False");

var memberAst = new ExpressionParser("service.GetItem(1).Name").ParseExpression();
Equal(SyntaxKind.MemberAccessExpression, memberAst.Kind, "member chain root");
var memberRoot = (MemberAccessExpressionSyntax)memberAst;
Equal("Name", memberRoot.NameToken.Text, "member chain final name");
Equal(SyntaxKind.CallExpression, memberRoot.Expression.Kind, "member chain call before final member");
var memberCall = (CallExpressionSyntax)memberRoot.Expression;
Equal(SyntaxKind.MemberAccessExpression, memberCall.Target.Kind, "member call target");
Equal("GetItem", ((MemberAccessExpressionSyntax)memberCall.Target).NameToken.Text, "member call name");

var ambiguousParenAst = new ExpressionParser("sortedStrings(0)").ParseExpression();
Equal(SyntaxKind.CallExpression, ambiguousParenAst.Kind, "parenthesized postfix syntax remains unresolved");
Equal("sortedStrings", ((NameExpressionSyntax)((CallExpressionSyntax)ambiguousParenAst).Target).IdentifierToken.Text, "array-or-call target");

var newWithArgsAst = new ExpressionParser("New Person(\"Fredrik\", \"Admin\")").ParseExpression();
Equal(SyntaxKind.NewExpression, newWithArgsAst.Kind, "New with arguments kind");
var newWithArgs = (NewExpressionSyntax)newWithArgsAst;
Equal("Person", newWithArgs.TypeName.Text, "New type name");
Equal(2, newWithArgs.Arguments.Count, "New argument count");
Equal(new TextSpan(0, 29), newWithArgs.Span, "New with arguments span");

var newWithoutArgsAst = new ExpressionParser("New XPJsonObject").ParseExpression();
Equal(SyntaxKind.NewExpression, newWithoutArgsAst.Kind, "New without parentheses kind");
var newWithoutArgs = (NewExpressionSyntax)newWithoutArgsAst;
Equal("XPJsonObject", newWithoutArgs.TypeName.Text, "New without parentheses type");
Equal(0, newWithoutArgs.Arguments.Count, "New without parentheses arguments");

var missingParenParser = new ExpressionParser("RunCommand(\"where.exe\"");
var missingParenAst = missingParenParser.ParseExpression();
Equal(SyntaxKind.CallExpression, missingParenAst.Kind, "missing close paren still produces call");
Equal(1, missingParenParser.Diagnostics.Count, "missing close paren diagnostic count");
Equal("XPS1012", missingParenParser.Diagnostics[0].Code, "missing close paren diagnostic code");
Equal(new TextSpan(22, 0), missingParenParser.Diagnostics[0].Span, "missing close paren diagnostic span");

var missingMemberParser = new ExpressionParser("service.");
var missingMemberAst = missingMemberParser.ParseExpression();
Equal(SyntaxKind.MemberAccessExpression, missingMemberAst.Kind, "missing member still produces member access");
Equal(1, missingMemberParser.Diagnostics.Count, "missing member diagnostic count");
Equal("XPS1012", missingMemberParser.Diagnostics[0].Code, "missing member diagnostic code");
Equal(new TextSpan(8, 0), missingMemberParser.Diagnostics[0].Span, "missing member diagnostic span");

Console.WriteLine("AST lexer, syntax-model and expression-parser focused tests passed.");
