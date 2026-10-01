using CSharpSyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CSharpArgumentSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax;
using CSharpBinaryExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax;
using CSharpExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax;
using CSharpInvocationExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax;
using CSharpParenthesizedExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedExpressionSyntax;
using CSharpPrefixUnaryExpressionSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax;
using XPScript.Compiler;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Binding;
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
Equal(new TextSpan(0, 30), newWithArgs.Span, "New with arguments span");

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

var booleanBinder = new ExpressionBinder();
var boundBoolean = booleanBinder.Bind(new ExpressionParser("Not False Or True").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundBoolean.Kind, "bound boolean root");
Equal(typeof(bool), boundBoolean.Type, "bound boolean type");
Equal(0, booleanBinder.Diagnostics.Count, "bound boolean diagnostics");

var arithmeticBinder = new ExpressionBinder();
var boundArithmetic = arithmeticBinder.Bind(new ExpressionParser("1 + 2 * 3").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundArithmetic.Kind, "bound arithmetic root");
Equal(typeof(long), boundArithmetic.Type, "bound arithmetic type");
Equal(0, arithmeticBinder.Diagnostics.Count, "bound arithmetic diagnostics");

var invalidBinder = new ExpressionBinder();
invalidBinder.Bind(new ExpressionParser("Not 1").ParseExpression());
Equal(1, invalidBinder.Diagnostics.Count, "invalid unary diagnostic count");
Equal("XPS1012", invalidBinder.Diagnostics[0].Code, "invalid unary diagnostic code");

var emitter = new BoundExpressionEmitter();
var newSymbols = new SymbolTable();
newSymbols.Declare(new TypeSymbol("StringBuilder", typeof(System.Text.StringBuilder)));
var newBinder = new ExpressionBinder(newSymbols);
var boundNew = newBinder.Bind(new ExpressionParser("New StringBuilder").ParseExpression());
Equal(BoundNodeKind.NewExpression, boundNew.Kind, "bound New root");
Equal(typeof(System.Text.StringBuilder), boundNew.Type, "bound New result type");
Equal(0, newBinder.Diagnostics.Count, "bound New diagnostics");
Equal("new StringBuilder()", emitter.Emit(boundNew), "bound New C# emission");

var newWithArgsBinder = new ExpressionBinder(newSymbols);
var boundNewWithArgs = newWithArgsBinder.Bind(new ExpressionParser("New StringBuilder(\"hello\")").ParseExpression());
Equal(BoundNodeKind.NewExpression, boundNewWithArgs.Kind, "bound New with args root");
Equal(typeof(System.Text.StringBuilder), boundNewWithArgs.Type, "bound New with args result type");
Equal(0, newWithArgsBinder.Diagnostics.Count, "bound New with args diagnostics");
Equal("new StringBuilder(\"hello\")", emitter.Emit(boundNewWithArgs), "bound New with args C# emission");


Equal("((!false) || true)", emitter.Emit(boundBoolean), "bound boolean C# emission");
Equal("(1 + (2 * 3))", emitter.Emit(boundArithmetic), "bound arithmetic C# emission");

var comparisonBinder = new ExpressionBinder();
var boundComparison = comparisonBinder.Bind(new ExpressionParser("1 + 2 = 3").ParseExpression());
Equal("((1 + 2) == 3)", emitter.Emit(boundComparison), "bound comparison C# emission");
Equal(0, comparisonBinder.Diagnostics.Count, "bound comparison diagnostics");

var callSymbols = new SymbolTable();
callSymbols.Declare(new FunctionSymbol("Array", typeof(string[]), [typeof(string)]));
callSymbols.Declare(new FunctionSymbol("RunCommand", typeof(bool), [typeof(string), typeof(string[])]));
var indexSymbols = new SymbolTable();
indexSymbols.Declare(new VariableSymbol("items", typeof(string[])));
var indexBinder = new ExpressionBinder(indexSymbols);
var boundIndex = indexBinder.Bind(new ExpressionParser("items[1]").ParseExpression());
Equal(BoundNodeKind.IndexExpression, boundIndex.Kind, "bound index root");
Equal(typeof(string), boundIndex.Type, "bound index result type");
Equal(0, indexBinder.Diagnostics.Count, "bound index diagnostics");
Equal("items[1]", emitter.Emit(boundIndex), "bound index C# emission");

var memberSymbols = new SymbolTable();
memberSymbols.Declare(new VariableSymbol("text", typeof(string)));
memberSymbols.Declare(new FunctionSymbol("String.Substring", typeof(string), [typeof(long)]));
var memberBinder = new ExpressionBinder(memberSymbols);
var boundMemberCall = memberBinder.Bind(new ExpressionParser("text.Substring(1)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMemberCall.Kind, "bound member call root");
Equal(typeof(string), boundMemberCall.Type, "bound member call result type");
Equal(0, memberBinder.Diagnostics.Count, "bound member call diagnostics");
Equal("text.Substring(1)", emitter.Emit(boundMemberCall), "bound member call C# emission");

var callBinder = new ExpressionBinder(callSymbols);
var boundRunCommand = callBinder.Bind(new ExpressionParser("Not RunCommand(\"where.exe\", Array(\"winget\"))").ParseExpression());
Equal(BoundNodeKind.UnaryExpression, boundRunCommand.Kind, "bound RunCommand root");
Equal(typeof(bool), boundRunCommand.Type, "bound RunCommand result type");
Equal(0, callBinder.Diagnostics.Count, "bound RunCommand diagnostics");
Equal("(!RunCommand(\"where.exe\", Array(\"winget\")))", emitter.Emit(boundRunCommand), "bound RunCommand C# emission");
var boundRunCommandFalse = callBinder.Bind(new ExpressionParser("RunCommand(\"where.exe\", Array(\"winget\")) = False").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundRunCommandFalse.Kind, "bound RunCommand equals false root");
Equal(typeof(bool), boundRunCommandFalse.Type, "bound RunCommand equals false type");
Equal(0, callBinder.Diagnostics.Count, "bound RunCommand equals false diagnostics");
Equal("(RunCommand(\"where.exe\", Array(\"winget\")) == false)", emitter.Emit(boundRunCommandFalse), "bound RunCommand equals false C# emission");
var legacyRunCommand = ExpressionCompatibilityProbe.EmitLegacy("Not RunCommand(\"where.exe\", Array(\"winget\"))");
var astRunCommand = emitter.Emit(boundRunCommand);
string CanonicalizeCSharpExpression(string value)
{
    CSharpExpressionSyntax RemoveRedundantParentheses(CSharpExpressionSyntax expression)
    {
        while (expression is CSharpParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;
        return expression switch
        {
            CSharpPrefixUnaryExpressionSyntax unary => unary.WithOperand(RemoveRedundantParentheses(unary.Operand)),
            CSharpBinaryExpressionSyntax binary => binary
                .WithLeft(RemoveRedundantParentheses(binary.Left))
                .WithRight(RemoveRedundantParentheses(binary.Right)),
            CSharpInvocationExpressionSyntax invocation => invocation
                .WithExpression(RemoveRedundantParentheses(invocation.Expression))
                .WithArgumentList(invocation.ArgumentList.WithArguments(
                    CSharpSyntaxFactory.SeparatedList(
                        invocation.ArgumentList.Arguments.Select(a => a.WithExpression(RemoveRedundantParentheses(a.Expression)))))),
            _ => expression
        };
    }

    var parsed = CSharpSyntaxFactory.ParseExpression(value);
    return RemoveRedundantParentheses(parsed).ToFullString().Replace(" ", string.Empty);
}
Equal(CanonicalizeCSharpExpression(legacyRunCommand), CanonicalizeCSharpExpression(astRunCommand), "legacy vs AST RunCommand semantic emission");

void EqualLegacyAst(string source, string label)
{
    var binder = new ExpressionBinder(callSymbols);
    var bound = binder.Bind(new ExpressionParser(source).ParseExpression());
    Equal(0, binder.Diagnostics.Count, label + " AST diagnostics");
    var legacy = ExpressionCompatibilityProbe.EmitLegacy(source);
    var ast = emitter.Emit(bound);
    Equal(CanonicalizeCSharpExpression(legacy), CanonicalizeCSharpExpression(ast), label + " legacy vs AST emission");
}

EqualLegacyAst("Not False Or True", "boolean precedence");
EqualLegacyAst("1 + 2 * 3", "arithmetic precedence");
EqualLegacyAst("1 + 2 = 3", "comparison precedence");
EqualLegacyAst("True And False Or True", "boolean associativity");

var badCallBinder = new ExpressionBinder(callSymbols);
badCallBinder.Bind(new ExpressionParser("RunCommand(\"where.exe\")").ParseExpression());
Equal(1, badCallBinder.Diagnostics.Count, "RunCommand arity diagnostic count");

Console.WriteLine("AST lexer, syntax-model, expression-parser, binder and emitter focused tests passed.");
