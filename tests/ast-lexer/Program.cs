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

var decimalNumber = Lex("10.5")[0];
Equal(SyntaxKind.NumberToken, decimalNumber.Kind, "decimal number kind");
Equal(10.5d, (double)decimalNumber.Value!, "decimal number value");
Equal(new TextSpan(0, 4), decimalNumber.Span, "decimal number span");

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

var singleLineIfParser = new StatementParser("If Not RunCommand(\"where.exe\", Array(\"winget\")) Then Print(\"missing\")");
var singleLineIf = singleLineIfParser.ParseStatement();
Equal(SyntaxKind.IfStatement, singleLineIf.Kind, "single-line If statement kind");
Equal(0, singleLineIfParser.Diagnostics.Count, "single-line If diagnostics");
var singleLineIfSyntax = (IfStatementSyntax)singleLineIf;
Equal(SyntaxKind.UnaryExpression, singleLineIfSyntax.Condition.Kind, "single-line If Not condition kind");
Equal(new TextSpan(3, 44), singleLineIfSyntax.Condition.Span, "single-line If condition span");
Equal(SyntaxKind.ExpressionStatement, singleLineIfSyntax.ThenStatement.Kind, "single-line If body kind");
Equal(new TextSpan(53, 16), singleLineIfSyntax.ThenStatement.Span, "single-line If body span");

var comparisonIfParser = new StatementParser("If RunCommand(\"where.exe\", Array(\"winget\")) = False Then Print(\"missing\")");
var comparisonIf = comparisonIfParser.ParseStatement();
Equal(SyntaxKind.IfStatement, comparisonIf.Kind, "comparison If statement kind");
Equal(0, comparisonIfParser.Diagnostics.Count, "comparison If diagnostics");
Equal(SyntaxKind.BinaryExpression, ((IfStatementSyntax)comparisonIf).Condition.Kind, "comparison If condition kind");

const string blockIfSource = "If True Then\nPrint(\"yes\")\nElseIf False Then\nPrint(\"elseif\")\nElse\nPrint(\"no\")\nEnd If";
var blockIfParser = new StatementParser(blockIfSource);
var blockIf = blockIfParser.ParseStatement();
Equal(SyntaxKind.IfStatement, blockIf.Kind, "block If statement kind");
Equal(0, blockIfParser.Diagnostics.Count, "block If diagnostics");
var blockIfSyntax = (IfStatementSyntax)blockIf;
Equal(1, blockIfSyntax.ThenStatements.Count, "block If then statement count");
Equal(1, blockIfSyntax.ElseIfClauses.Count, "block If ElseIf count");
Equal(1, blockIfSyntax.ElseStatements.Count, "block If else statement count");
Equal(SyntaxKind.LiteralExpression, blockIfSyntax.Condition.Kind, "block If condition kind");
Equal(SyntaxKind.LiteralExpression, blockIfSyntax.ElseIfClauses[0].Condition.Kind, "block ElseIf condition kind");
Equal(new TextSpan(0, blockIfSource.Length), blockIfSyntax.Span, "block If full span");

var callStatementParser = new StatementParser("Call Sleep(1)");
var callStatement = callStatementParser.ParseStatement();
Equal(SyntaxKind.CallStatement, callStatement.Kind, "Call statement kind");
Equal(0, callStatementParser.Diagnostics.Count, "Call statement diagnostics");
var callStatementSyntax = (CallStatementSyntax)callStatement;
Equal(SyntaxKind.CallExpression, callStatementSyntax.Expression.Kind, "Call statement expression kind");
Equal(new TextSpan(0, 13), callStatementSyntax.Span, "Call statement full span");

var assignmentParser = new StatementParser("count = 1 + 2");
var assignmentStatement = assignmentParser.ParseStatement();
Equal(SyntaxKind.AssignmentStatement, assignmentStatement.Kind, "assignment statement kind");
Equal(0, assignmentParser.Diagnostics.Count, "assignment diagnostics");
var assignmentSyntax = (AssignmentStatementSyntax)assignmentStatement;
Equal(SyntaxKind.NameExpression, assignmentSyntax.Target.Kind, "assignment target kind");
Equal(SyntaxKind.BinaryExpression, assignmentSyntax.Expression.Kind, "assignment value kind");
Equal(new TextSpan(0, 13), assignmentSyntax.Span, "assignment full span");

var memberAssignmentParser = new StatementParser("person.Name = \"Fredrik\"");
var memberAssignment = memberAssignmentParser.ParseStatement();
Equal(SyntaxKind.AssignmentStatement, memberAssignment.Kind, "member assignment statement kind");
Equal(SyntaxKind.MemberAccessExpression, ((AssignmentStatementSyntax)memberAssignment).Target.Kind, "member assignment target kind");
Equal(0, memberAssignmentParser.Diagnostics.Count, "member assignment diagnostics");

var setStatementParser = new StatementParser("Set person = New Person");
var setStatement = setStatementParser.ParseStatement();
Equal(SyntaxKind.SetStatement, setStatement.Kind, "Set statement kind");
Equal(0, setStatementParser.Diagnostics.Count, "Set statement diagnostics");
var setSyntax = (SetStatementSyntax)setStatement;
Equal(SyntaxKind.NameExpression, setSyntax.Target.Kind, "Set target kind");
Equal(SyntaxKind.NewExpression, setSyntax.Expression.Kind, "Set value kind");

var dimParser = new StatementParser("Dim count");
var dimStatement = dimParser.ParseStatement();
Equal(SyntaxKind.DimStatement, dimStatement.Kind, "Dim statement kind");
Equal(0, dimParser.Diagnostics.Count, "Dim diagnostics");
var dimSyntax = (DimStatementSyntax)dimStatement;
Equal("count", dimSyntax.IdentifierToken.Text, "Dim identifier");
Equal(false, dimSyntax.AsKeyword is not null, "Dim without type has no As");
Equal(false, dimSyntax.Initializer is not null, "Dim without initializer");

var typedDimParser = new StatementParser("Dim name As String");
var typedDimStatement = (DimStatementSyntax)typedDimParser.ParseStatement();
Equal(0, typedDimParser.Diagnostics.Count, "typed Dim diagnostics");
Equal("String", typedDimStatement.TypeNameToken!.Text, "typed Dim type name");
Equal(new TextSpan(0, 18), typedDimStatement.Span, "typed Dim full span");

var initializedDimParser = new StatementParser("Dim total As Integer = 1 + 2");
var initializedDimStatement = (DimStatementSyntax)initializedDimParser.ParseStatement();
Equal(0, initializedDimParser.Diagnostics.Count, "initialized Dim diagnostics");
Equal("Integer", initializedDimStatement.TypeNameToken!.Text, "initialized Dim type");
Equal(SyntaxKind.BinaryExpression, initializedDimStatement.Initializer!.Kind, "initialized Dim expression kind");
Equal(new TextSpan(0, 28), initializedDimStatement.Span, "initialized Dim full span");

const string whileSource = "While True\ncount = count + 1\nWend";
var whileParser = new StatementParser(whileSource);
var whileStatement = whileParser.ParseStatement();
Equal(SyntaxKind.WhileStatement, whileStatement.Kind, "While statement kind");
Equal(0, whileParser.Diagnostics.Count, "While diagnostics");
var whileSyntax = (WhileStatementSyntax)whileStatement;
Equal(SyntaxKind.LiteralExpression, whileSyntax.Condition.Kind, "While condition kind");
Equal(1, whileSyntax.Statements.Count, "While body statement count");
Equal(SyntaxKind.AssignmentStatement, whileSyntax.Statements[0].Kind, "While body assignment kind");
Equal(new TextSpan(0, whileSource.Length), whileSyntax.Span, "While full span");

const string forSource = "For i = 1 To 5 Step 2\ntotal = total + i\nNext i";
var forParser = new StatementParser(forSource);
var forStatement = forParser.ParseStatement();
Equal(SyntaxKind.ForStatement, forStatement.Kind, "For statement kind");
Equal(0, forParser.Diagnostics.Count, "For diagnostics");
var forSyntax = (ForStatementSyntax)forStatement;
Equal("i", forSyntax.IdentifierToken.Text, "For identifier");
Equal(SyntaxKind.LiteralExpression, forSyntax.FromExpression.Kind, "For from expression kind");
Equal(SyntaxKind.LiteralExpression, forSyntax.ToExpression.Kind, "For to expression kind");
Equal(SyntaxKind.LiteralExpression, forSyntax.StepExpression!.Kind, "For Step expression kind");
Equal(1, forSyntax.Statements.Count, "For body statement count");
Equal("i", forSyntax.NextIdentifierToken!.Text, "For Next identifier");
Equal(new TextSpan(0, forSource.Length), forSyntax.Span, "For full span");

const string forWithoutStepSource = "For i = 1 To 5\ni = i + 1\nNext";
var forWithoutStepParser = new StatementParser(forWithoutStepSource);
var forWithoutStep = (ForStatementSyntax)forWithoutStepParser.ParseStatement();
Equal(0, forWithoutStepParser.Diagnostics.Count, "For without Step diagnostics");
Equal(false, forWithoutStep.StepExpression is not null, "For without Step has no Step expression");
Equal(false, forWithoutStep.NextIdentifierToken is not null, "For bare Next has no identifier");

const string forAllSource = "ForAll value In numbers\ncount = count + 1\nEnd ForAll";
var forAllParser = new StatementParser(forAllSource);
var forAllStatement = forAllParser.ParseStatement();
Equal(SyntaxKind.ForAllStatement, forAllStatement.Kind, "ForAll statement kind");
Equal(0, forAllParser.Diagnostics.Count, "ForAll diagnostics");
var forAllSyntax = (ForAllStatementSyntax)forAllStatement;
Equal("value", forAllSyntax.IdentifierToken.Text, "ForAll identifier");
Equal(SyntaxKind.NameExpression, forAllSyntax.CollectionExpression.Kind, "ForAll collection expression kind");
Equal(1, forAllSyntax.Statements.Count, "ForAll body statement count");
Equal(SyntaxKind.AssignmentStatement, forAllSyntax.Statements[0].Kind, "ForAll body assignment kind");
Equal(new TextSpan(0, forAllSource.Length), forAllSyntax.Span, "ForAll full span");

const string doWhileSource = "Do While True\ncount = count + 1\nLoop";
var doWhileParser = new StatementParser(doWhileSource);
var doWhileStatement = doWhileParser.ParseStatement();
Equal(SyntaxKind.DoStatement, doWhileStatement.Kind, "Do While statement kind");
Equal(0, doWhileParser.Diagnostics.Count, "Do While diagnostics");
var doWhileSyntax = (DoStatementSyntax)doWhileStatement;
Equal(SyntaxKind.WhileKeyword, doWhileSyntax.ConditionKeyword!.Kind, "Do While condition keyword");
Equal(false, doWhileSyntax.IsPostTest, "Do While is pre-test");
Equal(SyntaxKind.LiteralExpression, doWhileSyntax.Condition!.Kind, "Do While condition kind");
Equal(1, doWhileSyntax.Statements.Count, "Do While body statement count");
Equal(new TextSpan(0, doWhileSource.Length), doWhileSyntax.Span, "Do While full span");

const string doUntilSource = "Do Until False\ncount = count + 1\nLoop";
var doUntilParser = new StatementParser(doUntilSource);
var doUntilSyntax = (DoStatementSyntax)doUntilParser.ParseStatement();
Equal(0, doUntilParser.Diagnostics.Count, "Do Until diagnostics");
Equal(SyntaxKind.UntilKeyword, doUntilSyntax.ConditionKeyword!.Kind, "Do Until condition keyword");
Equal(false, doUntilSyntax.IsPostTest, "Do Until is pre-test");

const string loopWhileSource = "Do\ncount = count + 1\nLoop While True";
var loopWhileParser = new StatementParser(loopWhileSource);
var loopWhileSyntax = (DoStatementSyntax)loopWhileParser.ParseStatement();
Equal(0, loopWhileParser.Diagnostics.Count, "Loop While diagnostics");
Equal(SyntaxKind.WhileKeyword, loopWhileSyntax.ConditionKeyword!.Kind, "Loop While condition keyword");
Equal(true, loopWhileSyntax.IsPostTest, "Loop While is post-test");
Equal(SyntaxKind.LiteralExpression, loopWhileSyntax.Condition!.Kind, "Loop While condition kind");

const string loopUntilSource = "Do\ncount = count + 1\nLoop Until False";
var loopUntilParser = new StatementParser(loopUntilSource);
var loopUntilSyntax = (DoStatementSyntax)loopUntilParser.ParseStatement();
Equal(0, loopUntilParser.Diagnostics.Count, "Loop Until diagnostics");
Equal(SyntaxKind.UntilKeyword, loopUntilSyntax.ConditionKeyword!.Kind, "Loop Until condition keyword");
Equal(true, loopUntilSyntax.IsPostTest, "Loop Until is post-test");

const string selectSource = "Select Case value\nCase 1\nPrint(\"one\")\nCase 2 To 10\nPrint(\"range\")\nCase Is > 10\nPrint(\"high\")\nCase Else\nPrint(\"other\")\nEnd Select";
var selectParser = new StatementParser(selectSource);
var selectStatement = selectParser.ParseStatement();
Equal(SyntaxKind.SelectStatement, selectStatement.Kind, "Select statement kind");
Equal(0, selectParser.Diagnostics.Count, "Select diagnostics");
var selectSyntax = (SelectStatementSyntax)selectStatement;
Equal(SyntaxKind.NameExpression, selectSyntax.Expression.Kind, "Select expression kind");
Equal(4, selectSyntax.Cases.Count, "Select case count");
Equal(SelectCaseKind.Value, selectSyntax.Cases[0].CaseKind, "Select value case kind");
Equal(SelectCaseKind.Range, selectSyntax.Cases[1].CaseKind, "Select range case kind");
Equal(SelectCaseKind.Relational, selectSyntax.Cases[2].CaseKind, "Select relational case kind");
Equal(SelectCaseKind.Else, selectSyntax.Cases[3].CaseKind, "Select else case kind");
Equal(1, selectSyntax.Cases[0].Statements.Count, "Select case body count");
Equal(new TextSpan(0, selectSource.Length), selectSyntax.Span, "Select full span");

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

var decimalBinder = new ExpressionBinder();
var boundDecimal = decimalBinder.Bind(new ExpressionParser("10.5 + 2.25").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundDecimal.Kind, "bound decimal arithmetic root");
Equal(typeof(double), boundDecimal.Type, "bound decimal arithmetic type");
Equal(0, decimalBinder.Diagnostics.Count, "bound decimal arithmetic diagnostics");

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
Equal("(10.5 + 2.25)", emitter.Emit(boundDecimal), "bound decimal C# emission");

var nestedNewParser = new ExpressionParser("New Person(\"Fredrik\", \"Admin\")");
nestedNewParser.ParseExpression();
Equal(0, nestedNewParser.Diagnostics.Count, "New arguments parser diagnostics");

var nestedParenParser = new ExpressionParser("(1 + 2) * 3");
nestedParenParser.ParseExpression();
Equal(0, nestedParenParser.Diagnostics.Count, "parenthesized parser diagnostics");

var parenthesizedBinder = new ExpressionBinder();
var boundParenthesized = parenthesizedBinder.Bind(new ExpressionParser("(1 + 2) * 3").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundParenthesized.Kind, "bound parenthesized arithmetic root");
Equal(typeof(long), boundParenthesized.Type, "bound parenthesized arithmetic type");
Equal(0, parenthesizedBinder.Diagnostics.Count, "bound parenthesized arithmetic diagnostics");
Equal("((1 + 2) * 3)", emitter.Emit(boundParenthesized), "bound parenthesized arithmetic C# emission");

var comparisonBinder = new ExpressionBinder();
var boundComparison = comparisonBinder.Bind(new ExpressionParser("10.5 >= 2.25").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundComparison.Kind, "bound comparison root");
Equal(typeof(bool), boundComparison.Type, "bound comparison type");
Equal(0, comparisonBinder.Diagnostics.Count, "bound comparison diagnostics");
Equal("(10.5 >= 2.25)", emitter.Emit(boundComparison), "bound comparison C# emission");

var concatBinder = new ExpressionBinder();
var boundConcat = concatBinder.Bind(new ExpressionParser("\"XP\" & \"Script\"").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundConcat.Kind, "bound concat root");
Equal(typeof(string), boundConcat.Type, "bound concat type");
Equal(0, concatBinder.Diagnostics.Count, "bound concat diagnostics");
Equal("(\"XP\" + \"Script\")", emitter.Emit(boundConcat), "bound concat C# emission");

var equalityBinder = new ExpressionBinder();
var boundEquality = equalityBinder.Bind(new ExpressionParser("1 + 2 = 3").ParseExpression());
Equal("((1 + 2) == 3)", emitter.Emit(boundEquality), "bound equality C# emission");
Equal(0, equalityBinder.Diagnostics.Count, "bound equality diagnostics");

var overloadSymbols = new SymbolTable();
overloadSymbols.Declare(new FunctionSymbol("ConvertValue", typeof(string), [typeof(long)]));
overloadSymbols.Declare(new FunctionSymbol("ConvertValue", typeof(string), [typeof(string)]));
var overloadLongBinder = new ExpressionBinder(overloadSymbols);
var boundLongOverload = overloadLongBinder.Bind(new ExpressionParser("ConvertValue(1)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundLongOverload.Kind, "bound long overload root");
Equal(0, overloadLongBinder.Diagnostics.Count, "bound long overload diagnostics");
Equal(typeof(long), ((BoundCallExpression)boundLongOverload).Function.ParameterTypes[0], "selected long overload");

var overloadStringBinder = new ExpressionBinder(overloadSymbols);
var boundStringOverload = overloadStringBinder.Bind(new ExpressionParser("ConvertValue(\"one\")").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundStringOverload.Kind, "bound string overload root");
Equal(0, overloadStringBinder.Diagnostics.Count, "bound string overload diagnostics");
Equal(typeof(string), ((BoundCallExpression)boundStringOverload).Function.ParameterTypes[0], "selected string overload");

var overloadMissingBinder = new ExpressionBinder(overloadSymbols);
overloadMissingBinder.Bind(new ExpressionParser("ConvertValue(True)").ParseExpression());
Equal(1, overloadMissingBinder.Diagnostics.Count, "no matching overload diagnostic count");
Equal("XPS2004", overloadMissingBinder.Diagnostics[0].Code, "no matching overload diagnostic code");

var ambiguousOverloadSymbols = new SymbolTable();
ambiguousOverloadSymbols.Declare(new FunctionSymbol("ConvertValue", typeof(string), [typeof(long)]));
ambiguousOverloadSymbols.Declare(new FunctionSymbol("ConvertValue", typeof(string), [typeof(long)]));
var ambiguousOverloadBinder = new ExpressionBinder(ambiguousOverloadSymbols);
ambiguousOverloadBinder.Bind(new ExpressionParser("ConvertValue(1)").ParseExpression());
Equal(1, ambiguousOverloadBinder.Diagnostics.Count, "ambiguous overload diagnostic count");
Equal("XPS2005", ambiguousOverloadBinder.Diagnostics[0].Code, "ambiguous overload diagnostic code");

var callSymbols = new SymbolTable();
callSymbols.Declare(new FunctionSymbol("Array", typeof(string[]), [typeof(string)]));
callSymbols.Declare(new FunctionSymbol("RunCommand", typeof(bool), [typeof(string), typeof(string[])]));
callSymbols.Declare(new FunctionSymbol("Error", typeof(string), []));
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
memberSymbols.Declare(new VariableSymbol("person", typeof(object)));
memberSymbols.Declare(new FunctionSymbol("String.Substring", typeof(string), [typeof(long)]));
var memberBinder = new ExpressionBinder(memberSymbols);
var boundMemberCall = memberBinder.Bind(new ExpressionParser("text.Substring(1)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMemberCall.Kind, "bound member call root");
Equal(typeof(string), boundMemberCall.Type, "bound member call result type");
Equal(0, memberBinder.Diagnostics.Count, "bound member call diagnostics");
Equal("text.Substring(1)", emitter.Emit(boundMemberCall), "bound member call C# emission");

memberSymbols.Declare(new PropertySymbol("Object.Name", typeof(string)));
memberSymbols.Declare(new FunctionSymbol("Object.Describe", typeof(string), []));

memberSymbols.Declare(new FunctionSymbol("Object.ConvertValue", typeof(string), [typeof(long)]));
memberSymbols.Declare(new FunctionSymbol("Object.ConvertValue", typeof(string), [typeof(string)]));

var memberLongOverloadBinder = new ExpressionBinder(memberSymbols);
var boundMemberLongOverload = memberLongOverloadBinder.Bind(new ExpressionParser("person.ConvertValue(1)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMemberLongOverload.Kind, "bound member long overload root");
Equal(0, memberLongOverloadBinder.Diagnostics.Count, "bound member long overload diagnostics");
Equal(typeof(long), ((BoundCallExpression)boundMemberLongOverload).Function.ParameterTypes[0], "selected member long overload");

var memberStringOverloadBinder = new ExpressionBinder(memberSymbols);
var boundMemberStringOverload = memberStringOverloadBinder.Bind(new ExpressionParser("person.ConvertValue(\"one\")").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMemberStringOverload.Kind, "bound member string overload root");
Equal(0, memberStringOverloadBinder.Diagnostics.Count, "bound member string overload diagnostics");
Equal(typeof(string), ((BoundCallExpression)boundMemberStringOverload).Function.ParameterTypes[0], "selected member string overload");

var memberMissingOverloadBinder = new ExpressionBinder(memberSymbols);
memberMissingOverloadBinder.Bind(new ExpressionParser("person.ConvertValue(True)").ParseExpression());
Equal(1, memberMissingOverloadBinder.Diagnostics.Count, "member no matching overload diagnostic count");
Equal("XPS2004", memberMissingOverloadBinder.Diagnostics[0].Code, "member no matching overload diagnostic code");

memberSymbols.Declare(new FunctionSymbol("Object.Ambiguous", typeof(string), [typeof(long)]));
memberSymbols.Declare(new FunctionSymbol("Object.Ambiguous", typeof(string), [typeof(long)]));
var ambiguousMemberOverloadBinder = new ExpressionBinder(memberSymbols);
ambiguousMemberOverloadBinder.Bind(new ExpressionParser("person.Ambiguous(1)").ParseExpression());
Equal(1, ambiguousMemberOverloadBinder.Diagnostics.Count, "ambiguous member overload diagnostic count");
Equal("XPS2005", ambiguousMemberOverloadBinder.Diagnostics[0].Code, "ambiguous member overload diagnostic code");

var propertyBinder = new ExpressionBinder(memberSymbols);
var boundProperty = propertyBinder.Bind(new ExpressionParser("person.Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundProperty.Kind, "bound XPScript property access root");
Equal(typeof(string), boundProperty.Type, "bound XPScript property access type");
Equal(0, propertyBinder.Diagnostics.Count, "bound XPScript property access diagnostics");
Equal("person.Name", emitter.Emit(boundProperty), "bound XPScript property access C# emission");

var methodBinder = new ExpressionBinder(memberSymbols);
var boundMethod = methodBinder.Bind(new ExpressionParser("person.Describe()").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMethod.Kind, "bound XPScript member function root");
Equal(typeof(string), boundMethod.Type, "bound XPScript member function type");
Equal(0, methodBinder.Diagnostics.Count, "bound XPScript member function diagnostics");
Equal("person.Describe()", emitter.Emit(boundMethod), "bound XPScript member function C# emission");

var indexedSymbols = new SymbolTable();
indexedSymbols.Declare(new VariableSymbol("store", typeof(object)));
indexedSymbols.Declare(new VariableSymbol("people", typeof(object[])));
indexedSymbols.Declare(new PropertySymbol("Object.Name", typeof(string)));
indexedSymbols.Declare(new FunctionSymbol("Object.Describe", typeof(string), []));

indexedSymbols.Declare(new IndexedPropertySymbol("Object.Item", typeof(object), [typeof(long)]));
var indexedPropertyAccessBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedPropertyAccess = indexedPropertyAccessBinder.Bind(new ExpressionParser("store.Item(1)").ParseExpression());
Equal(BoundNodeKind.IndexedPropertyExpression, boundIndexedPropertyAccess.Kind, "bound indexed property root");
Equal(typeof(object), boundIndexedPropertyAccess.Type, "bound indexed property type");
Equal(0, indexedPropertyAccessBinder.Diagnostics.Count, "bound indexed property diagnostics");
Equal("store.Item(1)", emitter.Emit(boundIndexedPropertyAccess), "bound indexed property C# emission");

var chainedIndexedPropertyBinder = new ExpressionBinder(indexedSymbols);
var boundChainedIndexedProperty = chainedIndexedPropertyBinder.Bind(new ExpressionParser("store.Item(1).Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundChainedIndexedProperty.Kind, "bound indexed property member root");
Equal(typeof(string), boundChainedIndexedProperty.Type, "bound indexed property member type");
Equal(0, chainedIndexedPropertyBinder.Diagnostics.Count, "bound indexed property member diagnostics");
Equal("store.Item(1).Name", emitter.Emit(boundChainedIndexedProperty), "bound indexed property member C# emission");

var personSemanticType = XpTypeSymbol.User("Person");
var boxSemanticType = XpTypeSymbol.User("Box");
var semanticTypeSymbols = new SymbolTable();
semanticTypeSymbols.Declare(new VariableSymbol("typedPerson", typeof(object), personSemanticType));
semanticTypeSymbols.Declare(new VariableSymbol("typedBox", typeof(object), boxSemanticType));
semanticTypeSymbols.Declare(new PropertySymbol("Person.Name", typeof(string)));
semanticTypeSymbols.Declare(new PropertySymbol("Box.Value", typeof(string)));

semanticTypeSymbols.Declare(new FunctionSymbol("Handle", typeof(string), [typeof(object)], null, [personSemanticType]));
semanticTypeSymbols.Declare(new FunctionSymbol("Handle", typeof(string), [typeof(object)], null, [boxSemanticType]));

var semanticPersonOverloadBinder = new ExpressionBinder(semanticTypeSymbols);
var boundSemanticPersonOverload = semanticPersonOverloadBinder.Bind(new ExpressionParser("Handle(typedPerson)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundSemanticPersonOverload.Kind, "semantic Person overload root");
Equal(0, semanticPersonOverloadBinder.Diagnostics.Count, "semantic Person overload diagnostics");
Equal("Person", ((BoundCallExpression)boundSemanticPersonOverload).Function.SemanticParameterTypes![0].Name, "selected semantic Person overload");

var semanticBoxOverloadBinder = new ExpressionBinder(semanticTypeSymbols);
var boundSemanticBoxOverload = semanticBoxOverloadBinder.Bind(new ExpressionParser("Handle(typedBox)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundSemanticBoxOverload.Kind, "semantic Box overload root");
Equal(0, semanticBoxOverloadBinder.Diagnostics.Count, "semantic Box overload diagnostics");
Equal("Box", ((BoundCallExpression)boundSemanticBoxOverload).Function.SemanticParameterTypes![0].Name, "selected semantic Box overload");

var typedPersonBinder = new ExpressionBinder(semanticTypeSymbols);
var boundTypedPersonName = typedPersonBinder.Bind(new ExpressionParser("typedPerson.Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundTypedPersonName.Kind, "semantic Person member root");
Equal(0, typedPersonBinder.Diagnostics.Count, "semantic Person member diagnostics");
Equal("typedPerson.Name", emitter.Emit(boundTypedPersonName), "semantic Person member C# emission");

var typedBoxBinder = new ExpressionBinder(semanticTypeSymbols);
var boundTypedBoxValue = typedBoxBinder.Bind(new ExpressionParser("typedBox.Value").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundTypedBoxValue.Kind, "semantic Box member root");
Equal(0, typedBoxBinder.Diagnostics.Count, "semantic Box member diagnostics");
Equal("typedBox.Value", emitter.Emit(boundTypedBoxValue), "semantic Box member C# emission");

var wrongTypedMemberBinder = new ExpressionBinder(semanticTypeSymbols);
wrongTypedMemberBinder.Bind(new ExpressionParser("typedPerson.Value").ParseExpression());
Equal(1, wrongTypedMemberBinder.Diagnostics.Count, "semantic type isolation diagnostics");
Equal("XPS2009", wrongTypedMemberBinder.Diagnostics[0].Code, "unknown property diagnostic code");
Equal(new TextSpan(12, 5), wrongTypedMemberBinder.Diagnostics[0].Span, "unknown property member span");

var missingMethodBinder = new ExpressionBinder(semanticTypeSymbols);
missingMethodBinder.Bind(new ExpressionParser("typedPerson.Missing()").ParseExpression());
Equal(1, missingMethodBinder.Diagnostics.Count, "unknown method diagnostic count");
Equal("XPS2009", missingMethodBinder.Diagnostics[0].Code, "unknown method diagnostic code");
Equal(new TextSpan(12, 7), missingMethodBinder.Diagnostics[0].Span, "unknown method member span");

const string positionedSource = "Print \"first\"\nPrint \"second\"\n    typedPerson.Value";
var positionedExpressionOffset = positionedSource.IndexOf("typedPerson", StringComparison.Ordinal);
var positionedBinder = new ExpressionBinder(semanticTypeSymbols);
positionedBinder.Bind(new ExpressionParser("typedPerson.Value", positionedExpressionOffset).ParseExpression());
Equal(1, positionedBinder.Diagnostics.Count, "absolute member diagnostic count");
Equal(new TextSpan(positionedExpressionOffset + 12, 5), positionedBinder.Diagnostics[0].Span, "absolute member diagnostic span");
var positionedLocation = SourceTextMap.GetPosition(positionedSource, positionedBinder.Diagnostics[0].Span.Start);
Equal(3, positionedLocation.Line, "absolute member diagnostic line");
Equal(17, positionedLocation.Column, "absolute member diagnostic column");

semanticTypeSymbols.Declare(new VariableSymbol("typedStore", typeof(object), XpTypeSymbol.User("IndexedObjectStore")));
semanticTypeSymbols.Declare(new VariableSymbol("typedKey", typeof(object), personSemanticType));
semanticTypeSymbols.Declare(new IndexedPropertySymbol("IndexedObjectStore.ByOwner", typeof(object), [typeof(object)], boxSemanticType, [personSemanticType]));
var semanticIndexedParameterBinder = new ExpressionBinder(semanticTypeSymbols);
var boundSemanticIndexedParameter = semanticIndexedParameterBinder.Bind(new ExpressionParser("typedStore.ByOwner(typedKey).Value").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundSemanticIndexedParameter.Kind, "semantic indexed parameter member root");
Equal(0, semanticIndexedParameterBinder.Diagnostics.Count, "semantic indexed parameter diagnostics");
Equal("typedStore.ByOwner(typedKey).Value", emitter.Emit(boundSemanticIndexedParameter), "semantic indexed parameter C# emission");

semanticTypeSymbols.Declare(new VariableSymbol("typedWrongKey", typeof(object), boxSemanticType));
var semanticIndexedWrongParameterBinder = new ExpressionBinder(semanticTypeSymbols);
semanticIndexedWrongParameterBinder.Bind(new ExpressionParser("typedStore.ByOwner(typedWrongKey)").ParseExpression());
Equal(1, semanticIndexedWrongParameterBinder.Diagnostics.Count, "semantic indexed wrong parameter diagnostic count");
Equal("XPS2003", semanticIndexedWrongParameterBinder.Diagnostics[0].Code, "semantic indexed wrong parameter diagnostic code");

semanticTypeSymbols.Declare(new IndexedPropertySymbol("IndexedObjectStore.Item", typeof(object), [typeof(long)], boxSemanticType));
var typedIndexedPropertyBinder = new ExpressionBinder(semanticTypeSymbols);
var boundTypedIndexedPropertyMember = typedIndexedPropertyBinder.Bind(new ExpressionParser("typedStore.Item(1).Value").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundTypedIndexedPropertyMember.Kind, "semantic indexed property member root");
Equal(typeof(string), boundTypedIndexedPropertyMember.Type, "semantic indexed property member type");
Equal(0, typedIndexedPropertyBinder.Diagnostics.Count, "semantic indexed property member diagnostics");
Equal("typedStore.Item(1).Value", emitter.Emit(boundTypedIndexedPropertyMember), "semantic indexed property member C# emission");

var indexedPropertyBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedProperty = indexedPropertyBinder.Bind(new ExpressionParser("people[1].Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundIndexedProperty.Kind, "bound indexed object property root");
Equal(typeof(string), boundIndexedProperty.Type, "bound indexed object property type");
Equal(0, indexedPropertyBinder.Diagnostics.Count, "bound indexed object property diagnostics");
Equal("people[1].Name", emitter.Emit(boundIndexedProperty), "bound indexed object property C# emission");

var indexedMethodBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedMethod = indexedMethodBinder.Bind(new ExpressionParser("people[1].Describe()").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundIndexedMethod.Kind, "bound indexed object method root");
Equal(typeof(string), boundIndexedMethod.Type, "bound indexed object method type");
Equal(0, indexedMethodBinder.Diagnostics.Count, "bound indexed object method diagnostics");
Equal("people[1].Describe()", emitter.Emit(boundIndexedMethod), "bound indexed object method C# emission");

var indexedChainBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedChain = indexedChainBinder.Bind(new ExpressionParser("people[1].Name").ParseExpression());
Equal(typeof(string), boundIndexedChain.Type, "indexed member chain retains final member type");
Equal(0, indexedChainBinder.Diagnostics.Count, "indexed member chain diagnostics");

var zeroArgBinder = new ExpressionBinder(callSymbols);
var boundZeroArg = zeroArgBinder.Bind(new ExpressionParser("Error()").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundZeroArg.Kind, "bound zero-argument call root");
Equal(typeof(string), boundZeroArg.Type, "bound zero-argument call type");
Equal(0, zeroArgBinder.Diagnostics.Count, "bound zero-argument call diagnostics");
Equal("Error()", emitter.Emit(boundZeroArg), "bound zero-argument call C# emission");

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
EqualLegacyAst("10.5 + 2.25", "decimal arithmetic");
EqualLegacyAst("10.5 >= 2.25", "numeric comparison");
EqualLegacyAst("\"XP\" & \"Script\"", "string concatenation");
EqualLegacyAst("1 + 2 = 3", "comparison precedence");
EqualLegacyAst("True And False Or True", "boolean associativity");

var malformedExpressionParser = new ExpressionParser("1 2");
malformedExpressionParser.ParseExpression();
Equal(1, malformedExpressionParser.Diagnostics.Count, "trailing expression token diagnostic count");
Equal("XPS1012", malformedExpressionParser.Diagnostics[0].Code, "trailing expression token diagnostic code");

var nestedCallParser = new ExpressionParser("RunCommand(\"where.exe\", Array(\"winget\"))");
nestedCallParser.ParseExpression();
Equal(0, nestedCallParser.Diagnostics.Count, "nested call parser diagnostics");

var badCallBinder = new ExpressionBinder(callSymbols);
badCallBinder.Bind(new ExpressionParser("RunCommand(\"where.exe\")").ParseExpression());
Equal(1, badCallBinder.Diagnostics.Count, "RunCommand arity diagnostic count");

Console.WriteLine("AST lexer, syntax-model, expression-parser, binder and emitter focused tests passed.");
