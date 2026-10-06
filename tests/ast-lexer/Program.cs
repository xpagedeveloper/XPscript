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

// Keep the most recently failing regression first so CI fails fast on this area.

var moduleMeParser = new DeclarationParser("Sub Main()\nPrint Me.Name\nEnd Sub");
_ = moduleMeParser.ParseDeclaration();
Equal(1, moduleMeParser.Diagnostics.Count(d => d.Message == "'Me' is only valid inside class instance members."), "module Me context diagnostic");

var noBaseParentParser = new DeclarationParser("Class Child\nPublic Function Describe() As String\nDescribe = Parent.Describe()\nEnd Function\nEnd Class");
_ = noBaseParentParser.ParseDeclaration();
Equal(1, noBaseParentParser.Diagnostics.Count(d => d.Message == "'Parent' requires the current class to Extend a base class."), "Parent without base diagnostic");

var inheritedContextParser = new DeclarationParser("Class Child Extend BaseClass\nPublic Function Describe() As String\nDescribe = Me.Name & Parent.Describe()\nEnd Function\nEnd Class");
_ = inheritedContextParser.ParseDeclaration();
Equal(0, inheritedContextParser.Diagnostics.Count, "Me and Parent inherited class context diagnostics");

var keywordFieldParser = new DeclarationParser("Class KeywordBox\nPrivate If As Integer\nEnd Class");
_ = keywordFieldParser.ParseDeclaration();
Equal("XPS1012", keywordFieldParser.Diagnostics.First().Code, "keyword field diagnostic code");

var keywordMethodParser = new DeclarationParser("Class KeywordBox\nPublic Sub If()\nEnd Sub\nEnd Class");
_ = keywordMethodParser.ParseDeclaration();
Equal("XPS1012", keywordMethodParser.Diagnostics.First().Code, "keyword method diagnostic code");

var scopedRuntimeNameParser = new DeclarationParser("Class RuntimeNames\nJsonParse As String\nPublic Function StrLeftBack() As String\nStrLeftBack = JsonParse\nEnd Function\nEnd Class");
var scopedRuntimeNameClass = (ClassDeclarationSyntax)scopedRuntimeNameParser.ParseDeclaration();
Equal(0, scopedRuntimeNameParser.Diagnostics.Count, "runtime/global names remain legal class members");
Equal("JsonParse", ((FieldDeclarationSyntax)scopedRuntimeNameClass.Members[0]).Identifier.Text, "runtime-named field");
Equal("StrLeftBack", ((FunctionDeclarationSyntax)scopedRuntimeNameClass.Members[1]).Identifier.Text, "runtime-named method");

const string incompatibleIndexedPropertySource = "Class BadIndexed\nPublic Property Get Item(index As Integer) As String\nItem = \"ok\"\nEnd Property\nPublic Property Let Item(key As String, value As String)\nEnd Property\nEnd Class";
var incompatibleIndexedPropertyParser = new DeclarationParser(incompatibleIndexedPropertySource);
_ = incompatibleIndexedPropertyParser.ParseDeclaration();
Equal(1, incompatibleIndexedPropertyParser.Diagnostics.Count(d => d.Message.Contains("compatible index parameter signatures", StringComparison.Ordinal)), "indexed property signature mismatch diagnostic");

const string compatibleIndexedPropertySource = "Class GoodIndexed\nPublic Property Get Item(index As Integer) As String\nItem = \"ok\"\nEnd Property\nPublic Property Let Item(index As Integer, value As String)\nEnd Property\nEnd Class";
var compatibleIndexedPropertyParser = new DeclarationParser(compatibleIndexedPropertySource);
_ = compatibleIndexedPropertyParser.ParseDeclaration();
Equal(0, compatibleIndexedPropertyParser.Diagnostics.Count, "compatible indexed property signatures");

const string moduleLifecycleNamesSource = "Sub New()\nEnd Sub";
var moduleNewParser = new DeclarationParser(moduleLifecycleNamesSource);
var moduleNew = moduleNewParser.ParseDeclaration();
Equal(SyntaxKind.SubDeclaration, moduleNew.Kind, "module Sub New remains ordinary Sub");
Equal(0, moduleNewParser.Diagnostics.Count, "module Sub New diagnostics");

var moduleDeleteParser = new DeclarationParser("Sub Delete()\nEnd Sub");
var moduleDelete = moduleDeleteParser.ParseDeclaration();
Equal(SyntaxKind.SubDeclaration, moduleDelete.Kind, "module Sub Delete remains ordinary Sub");
Equal(0, moduleDeleteParser.Diagnostics.Count, "module Sub Delete diagnostics");

const string classConstantSource = "Class ConstantBox\nConst Limit = 10\nPublic Kept As Integer\nEnd Class";
var classConstantParser = new DeclarationParser(classConstantSource);
var classConstantClass = (ClassDeclarationSyntax)classConstantParser.ParseDeclaration();
Equal("XPS1012", classConstantParser.Diagnostics.First(d => d.Message == "Constants are not supported as class members.").Code, "class constant diagnostic code");
Equal(1, classConstantClass.Members.Count, "members retained after class constant diagnostic");
Equal("Kept", ((FieldDeclarationSyntax)classConstantClass.Members[0]).Identifier.Text, "field after class constant");

const string classFieldContractSource = "Class Node\nPrivate NextNode As Node\nPrivate Left As Node, Right As Node\nPrivate Current As Node = New Node()\nEnd Class";
var classFieldContractParser = new DeclarationParser(classFieldContractSource);
var classFieldContractClass = (ClassDeclarationSyntax)classFieldContractParser.ParseDeclaration();
Equal("Node", ((FieldDeclarationSyntax)classFieldContractClass.Members[0]).Type.Identifier.Text, "self-referential class field type");
Equal(2, classFieldContractParser.Diagnostics.Count(d => d.Message == "Class fields must use one declaration per line and cannot have an initializer."), "invalid class field declaration diagnostics");

const string staticClassMemberSource = "Class StaticBox\nStatic Value As Integer\nStatic Sub Work()\nEnd Sub\nPublic Kept As Integer\nEnd Class";
var staticClassMemberParser = new DeclarationParser(staticClassMemberSource);
var staticClassMemberClass = (ClassDeclarationSyntax)staticClassMemberParser.ParseDeclaration();
Equal(2, staticClassMemberParser.Diagnostics.Count(d => d.Message == "Static class members are not supported."), "static class member diagnostics");
Equal(1, staticClassMemberClass.Members.Count, "members retained after static member diagnostics");
Equal("Kept", ((FieldDeclarationSyntax)staticClassMemberClass.Members[0]).Identifier.Text, "field after static members");

const string localClassSource = "Sub InvalidLocalClass()\nClass LocalBox\nEnd Class\nEnd Sub";
var localClassParser = new DeclarationParser(localClassSource);
_ = localClassParser.ParseDeclaration();
var localClassDiagnostic = localClassParser.Diagnostics.FirstOrDefault(d => d.Message == "Class declarations are not allowed inside procedures.");
Equal("XPS1012", localClassDiagnostic?.Code, "procedure-local class diagnostic code");

const string nestedClassSource = "Class Outer\nClass Inner\nEnd Class\nPublic Value As Integer\nEnd Class";
var nestedClassParser = new DeclarationParser(nestedClassSource);
var nestedClass = (ClassDeclarationSyntax)nestedClassParser.ParseDeclaration();
Equal("XPS1012", nestedClassParser.Diagnostics[0].Code, "nested class diagnostic code");
Equal("Nested class declarations are not allowed.", nestedClassParser.Diagnostics[0].Message, "nested class diagnostic message");
Equal(1, nestedClass.Members.Count, "outer class members after nested class diagnostic");
Equal("Value", ((FieldDeclarationSyntax)nestedClass.Members[0]).Identifier.Text, "outer class field after nested class diagnostic");
Equal(nestedClassSource.Length, nestedClass.Span.End, "outer class closes at its own End Class");

Equal(DeclarationVisibility.Private, DeclarationVisibilityResolver.ResolveClass(null), "default class visibility");
Equal(DeclarationVisibility.Public, DeclarationVisibilityResolver.ResolveClass(null, optionPublic: true), "Option Public class visibility");
Equal(DeclarationVisibility.Private, DeclarationVisibilityResolver.ResolveField(null), "default field visibility");
Equal(DeclarationVisibility.Public, DeclarationVisibilityResolver.ResolveField(null, optionPublic: true), "Option Public field visibility");
Equal(DeclarationVisibility.Public, DeclarationVisibilityResolver.ResolveClassMember(null), "default class member visibility");

var privateVisibilityParser = new DeclarationParser("Private Class VisibilityBox\nPrivate Value As Integer\nPrivate Sub Hidden()\nEnd Sub\nPublic Function Visible() As Integer\nVisible = 1\nEnd Function\nEnd Class");
var privateVisibilityClass = (ClassDeclarationSyntax)privateVisibilityParser.ParseDeclaration();
Equal(DeclarationVisibility.Private, DeclarationVisibilityResolver.ResolveClass(privateVisibilityClass.Visibility), "explicit private class visibility");
Equal(DeclarationVisibility.Private, DeclarationVisibilityResolver.ResolveField(((FieldDeclarationSyntax)privateVisibilityClass.Members[0]).Visibility), "explicit private field visibility");
Equal(DeclarationVisibility.Private, DeclarationVisibilityResolver.ResolveClassMember(((SubDeclarationSyntax)privateVisibilityClass.Members[1]).Visibility), "explicit private method visibility");
Equal(DeclarationVisibility.Public, DeclarationVisibilityResolver.ResolveClassMember(((FunctionDeclarationSyntax)privateVisibilityClass.Members[2]).Visibility), "explicit public method visibility");

const string indexedPropertySource = "Class IndexedBox\nPublic Property Get Item(index As Integer) As String\nItem = \"ok\"\nEnd Property\nPublic Property Let Item(index As Integer, value As String)\nEnd Property\nPublic Property Set Owner(index As Integer, value As Person)\nEnd Property\nEnd Class";
var indexedPropertyParser = new DeclarationParser(indexedPropertySource);
var indexedPropertyClass = (ClassDeclarationSyntax)indexedPropertyParser.ParseDeclaration();
Equal(0, indexedPropertyParser.Diagnostics.Count, "indexed property diagnostics");
var indexedGet = (PropertyDeclarationSyntax)indexedPropertyClass.Members[0];
Equal(1, indexedGet.Parameters.Count, "indexed Property Get parameter count");
Equal("String", indexedGet.Type!.Identifier.Text, "indexed Property Get return type");
var indexedLet = (PropertyDeclarationSyntax)indexedPropertyClass.Members[1];
Equal(2, indexedLet.Parameters.Count, "indexed Property Let parameter count");
Equal("value", indexedLet.Parameters[1].Identifier.Text, "indexed Property Let value parameter");
var indexedSet = (PropertyDeclarationSyntax)indexedPropertyClass.Members[2];
Equal(2, indexedSet.Parameters.Count, "indexed Property Set parameter count");

const string propertyClassSource = "Class PropertyBox\nPrivate mName As String\nPublic Property Get Name As String\nName = mName\nEnd Property\nPublic Property Let Name As String\nmName = Name\nEnd Property\nPublic Property Set Owner As Person\nEnd Property\nEnd Class";
var propertyClassParser = new DeclarationParser(propertyClassSource);
var propertyClass = (ClassDeclarationSyntax)propertyClassParser.ParseDeclaration();
if (propertyClassParser.Diagnostics.Count != 0) throw new Exception("property class diagnostics: " + string.Join(" | ", propertyClassParser.Diagnostics.Select(d => $"{d.Code}:{d.Message}@{d.Span.Start}")));
var propertyGet = (PropertyDeclarationSyntax)propertyClass.Members[1];
Equal(SyntaxKind.GetKeyword, propertyGet.AccessorKeyword.Kind, "Property Get accessor");
Equal("String", propertyGet.Type!.Identifier.Text, "Property Get type");
var propertyLet = (PropertyDeclarationSyntax)propertyClass.Members[2];
Equal(SyntaxKind.LetKeyword, propertyLet.AccessorKeyword.Kind, "Property Let accessor");
var propertySet = (PropertyDeclarationSyntax)propertyClass.Members[3];
Equal(SyntaxKind.SetKeyword, propertySet.AccessorKeyword.Kind, "Property Set accessor");


const string lifecycleClassSource = "Class Lifecycle\nSub New(ByVal value As Integer)\nMe.Value = value\nEnd Sub\nSub Delete()\nEnd Sub\nValue As Integer\nEnd Class";
var lifecycleClassParser = new DeclarationParser(lifecycleClassSource);
var lifecycleClass = (ClassDeclarationSyntax)lifecycleClassParser.ParseDeclaration();
Equal(0, lifecycleClassParser.Diagnostics.Count, "class lifecycle diagnostics");
Equal(SyntaxKind.ConstructorDeclaration, lifecycleClass.Members[0].Kind, "Sub New declaration kind");
Equal(1, ((ConstructorDeclarationSyntax)lifecycleClass.Members[0]).Parameters.Count, "Sub New parameter count");
Equal(SyntaxKind.DestructorDeclaration, lifecycleClass.Members[1].Kind, "Sub Delete declaration kind");

var invalidDeleteParser = new DeclarationParser("Class InvalidLifecycle\nSub Delete(value As Integer)\nEnd Sub\nEnd Class");
invalidDeleteParser.ParseDeclaration();
Equal("XPS1012", invalidDeleteParser.Diagnostics[^1].Code, "Sub Delete parameter diagnostic");


var meRegressionParser = new ExpressionParser("Me.Name");
var meRegression = (MemberAccessExpressionSyntax)meRegressionParser.ParseExpression();
Equal(0, meRegressionParser.Diagnostics.Count, "Me member diagnostics");
Equal("Me", ((NameExpressionSyntax)meRegression.Expression).IdentifierToken.Text, "Me receiver");
Equal("Name", meRegression.NameToken.Text, "Me member name");

var parentCallParser = new ExpressionParser("Parent.Describe()");
var parentCall = (CallExpressionSyntax)parentCallParser.ParseExpression();
Equal(0, parentCallParser.Diagnostics.Count, "Parent method call diagnostics");
var parentMember = (MemberAccessExpressionSyntax)parentCall.Target;
Equal("Parent", ((NameExpressionSyntax)parentMember.Expression).IdentifierToken.Text, "Parent receiver");
Equal("Describe", parentMember.NameToken.Text, "Parent method name");


var printIdentifierRegression = Lex("'comment\nPrint value");
Equal(SyntaxKind.IdentifierToken, printIdentifierRegression[1].Kind, "Print remains an identifier in lexer output");

var errorCallRegression = new ExpressionParser("Error()").ParseExpression();
Equal(SyntaxKind.CallExpression, errorCallRegression.Kind, "Error() remains an expression call after Error statement keyword support");


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

var dump = SyntaxTreeDumper.Dump(manualAst);
if (!dump.Contains("UnaryExpression", StringComparison.Ordinal) ||
    !dump.Contains("NameExpression", StringComparison.Ordinal) ||
    !dump.Contains("Start = 0", StringComparison.Ordinal))
    throw new InvalidOperationException("Syntax tree dump must include node kinds and source spans.");

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

var fileKeywordMemberRegression = new ExpressionParser("obj.Open").ParseExpression();
Equal(SyntaxKind.MemberAccessExpression, fileKeywordMemberRegression.Kind, "file statement keywords remain valid member names");
var fileKeywordCallRegression = new ExpressionParser("Open()").ParseExpression();
Equal(SyntaxKind.CallExpression, fileKeywordCallRegression.Kind, "Open remains a valid expression call");

var hashTokenRegression = Lex("#1");
Equal(SyntaxKind.HashToken, hashTokenRegression[0].Kind, "file number hash token");

var openHashParser = new StatementParser("Open fileName For Random As #1 Len = 128");
var openHash = (OpenStatementSyntax)openHashParser.ParseStatement();
Equal(0, openHashParser.Diagnostics.Count, "Open hash Len diagnostics");
Equal(SyntaxKind.HashToken, openHash.HashToken!.Kind, "Open hash token");
Equal(SyntaxKind.LenKeyword, openHash.LenKeyword!.Kind, "Open Len keyword");
Equal(SyntaxKind.LiteralExpression, openHash.RecordLengthExpression!.Kind, "Open record length expression");

var closeHashParser = new StatementParser("Close #1");
var closeHash = (CloseStatementSyntax)closeHashParser.ParseStatement();
Equal(0, closeHashParser.Diagnostics.Count, "Close hash diagnostics");
Equal(1, closeHash.FileNumbers.Count, "Close hash file count");

var closeMultipleHashParser = new StatementParser("Close #1, #2");
var closeMultipleHash = (CloseStatementSyntax)closeMultipleHashParser.ParseStatement();
Equal(0, closeMultipleHashParser.Diagnostics.Count, "Close multiple hash diagnostics");
Equal(2, closeMultipleHash.FileNumbers.Count, "Close multiple hash file count");

var lenFunctionParser = new ExpressionParser("Len(value)");
var lenFunction = lenFunctionParser.ParseExpression();
Equal(SyntaxKind.CallExpression, lenFunction.Kind, "Len remains a valid expression call");
Equal(0, lenFunctionParser.Diagnostics.Count, "Len function diagnostics");

var printHashParser = new StatementParser("Print #1, \"value\"");
var printHash = (FileOutputStatementSyntax)printHashParser.ParseStatement();
Equal(0, printHashParser.Diagnostics.Count, "Print hash diagnostics");
Equal(1, printHash.Values.Count, "Print hash value count");

var writeHashParser = new StatementParser("Write #1, value");
var writeHash = (FileOutputStatementSyntax)writeHashParser.ParseStatement();
Equal(0, writeHashParser.Diagnostics.Count, "Write hash diagnostics");

var lineInputHashParser = new StatementParser("Line Input #1, line");
var lineInputHash = (FileInputStatementSyntax)lineInputHashParser.ParseStatement();
Equal(0, lineInputHashParser.Diagnostics.Count, "Line Input hash diagnostics");

var inputHashParser = new StatementParser("Input #1, value");
var inputHash = (FileInputStatementSyntax)inputHashParser.ParseStatement();
Equal(0, inputHashParser.Diagnostics.Count, "Input hash diagnostics");

foreach (var (source, argumentCount) in new[]
{
    ("Reset", 0),
    ("WriteFile path, content, charset", 3),
    ("AppendFile path, content, charset", 3),
    ("WriteLines path, values, charset", 3),
    ("WriteBytes path, bytes", 2),
})
{
    var fileConvenienceParser = new StatementParser(source);
    var fileConvenienceStatement = (RuntimeFileStatementSyntax)fileConvenienceParser.ParseStatement();
    Equal(SyntaxKind.RuntimeFileStatement, fileConvenienceStatement.Kind, source + " statement kind");
    Equal(argumentCount, fileConvenienceStatement.Arguments.Count, source + " argument count");
    Equal(0, fileConvenienceParser.Diagnostics.Count, source + " diagnostics");
}

var inputExpressionParser = new StatementParser("value = Input(1)");
var inputExpressionStatement = (AssignmentStatementSyntax)inputExpressionParser.ParseStatement();
Equal(SyntaxKind.CallExpression, inputExpressionStatement.Expression.Kind, "Input expression remains call expression");
Equal(0, inputExpressionParser.Diagnostics.Count, "Input expression diagnostics");

var seekHashParser = new StatementParser("Seek #1, 42");
var seekHash = (SeekStatementSyntax)seekHashParser.ParseStatement();
Equal(0, seekHashParser.Diagnostics.Count, "Seek hash diagnostics");

var consolePrintParser = new StatementParser("Print \"hello\"");
var consolePrint = (RuntimeFileStatementSyntax)consolePrintParser.ParseStatement();
Equal(0, consolePrintParser.Diagnostics.Count, "console Print diagnostics");
Equal(1, consolePrint.Arguments.Count, "console Print argument count");

var randomizeParser = new StatementParser("Randomize 123");
var randomizeStatement = (RuntimeFileStatementSyntax)randomizeParser.ParseStatement();
Equal(0, randomizeParser.Diagnostics.Count, "Randomize seed diagnostics");
Equal(1, randomizeStatement.Arguments.Count, "Randomize seed argument count");

var randomizeEmptyParser = new StatementParser("Randomize");
var randomizeEmpty = (RuntimeFileStatementSyntax)randomizeEmptyParser.ParseStatement();
Equal(0, randomizeEmptyParser.Diagnostics.Count, "Randomize diagnostics");
Equal(0, randomizeEmpty.Arguments.Count, "Randomize argument count");

var beepParser = new StatementParser("Beep");
var beepStatement = (RuntimeFileStatementSyntax)beepParser.ParseStatement();
Equal(0, beepParser.Diagnostics.Count, "Beep diagnostics");
Equal(0, beepStatement.Arguments.Count, "Beep argument count");

var seekFunctionParser = new StatementParser("Seek(f)");
var seekFunction = (ExpressionStatementSyntax)seekFunctionParser.ParseStatement();
Equal(0, seekFunctionParser.Diagnostics.Count, "Seek function diagnostics");

var putParser = new StatementParser("Put #f, 1, binaryValue");
var putStatement = (RuntimeFileStatementSyntax)putParser.ParseStatement();
Equal(0, putParser.Diagnostics.Count, "Put diagnostics");
Equal(3, putStatement.Arguments.Count, "Put argument count");

var getParser = new StatementParser("Get #f, 1, binaryValue");
var getStatement = (RuntimeFileStatementSyntax)getParser.ParseStatement();
Equal(0, getParser.Diagnostics.Count, "Get diagnostics");
Equal(3, getStatement.Arguments.Count, "Get argument count");

var malformedPutParser = new StatementParser("Put #f, 1");
malformedPutParser.ParseStatement();
Equal("XPS1012", malformedPutParser.Diagnostics[0].Code, "Put missing value diagnostic");

foreach (var source in new[] { "Lock #f, 1 To 64", "Unlock #f, 1 To 64" })
{
    var parser = new StatementParser(source);
    var statement = (RuntimeFileStatementSyntax)parser.ParseStatement();
    Equal(0, parser.Diagnostics.Count, source + " diagnostics");
    Equal(3, statement.Arguments.Count, source + " argument count");
}

var lockWholeFileParser = new StatementParser("Lock #f");
var lockWholeFile = (RuntimeFileStatementSyntax)lockWholeFileParser.ParseStatement();
Equal(0, lockWholeFileParser.Diagnostics.Count, "Lock whole file diagnostics");
Equal(1, lockWholeFile.Arguments.Count, "Lock whole file argument count");

var chDriveParser = new StatementParser("ChDrive \"C\"");
var chDrive = (RuntimeFileStatementSyntax)chDriveParser.ParseStatement();
Equal(0, chDriveParser.Diagnostics.Count, "ChDrive diagnostics");
Equal(1, chDrive.Arguments.Count, "ChDrive argument count");

var runtimeFileIdentifierRegression = Lex("FileCopy Kill MkDir RmDir ChDir ChDrive SetFileAttr Name");
Equal(string.Join(",", Enumerable.Repeat(SyntaxKind.IdentifierToken, 8)), string.Join(",", runtimeFileIdentifierRegression.Take(8).Select(t => t.Kind)), "runtime file commands remain identifiers");

var fileCopyParser = new StatementParser("FileCopy sourcePath, destinationPath");
var fileCopy = (RuntimeFileStatementSyntax)fileCopyParser.ParseStatement();
Equal(0, fileCopyParser.Diagnostics.Count, "FileCopy diagnostics");
Equal(2, fileCopy.Arguments.Count, "FileCopy argument count");

foreach (var command in new[] { "Kill filePath", "MkDir directoryPath", "RmDir directoryPath", "ChDir directoryPath" })
{
    var parser = new StatementParser(command);
    var statement = (RuntimeFileStatementSyntax)parser.ParseStatement();
    Equal(0, parser.Diagnostics.Count, command + " diagnostics");
    Equal(1, statement.Arguments.Count, command + " argument count");
}

var setFileAttrParser = new StatementParser("SetFileAttr filePath, attributes");
var setFileAttr = (RuntimeFileStatementSyntax)setFileAttrParser.ParseStatement();
Equal(0, setFileAttrParser.Diagnostics.Count, "SetFileAttr diagnostics");
Equal(2, setFileAttr.Arguments.Count, "SetFileAttr argument count");

var renameFileParser = new StatementParser("Name oldPath As newPath");
var renameFile = (RenameFileStatementSyntax)renameFileParser.ParseStatement();
Equal(0, renameFileParser.Diagnostics.Count, "Name file diagnostics");
Equal(SyntaxKind.AsKeyword, renameFile.AsKeyword.Kind, "Name file As keyword");

var fileIoKeywordRegression = new ExpressionParser("obj.Print").ParseExpression();
Equal(SyntaxKind.MemberAccessExpression, fileIoKeywordRegression.Kind, "file I/O keywords remain valid member names");
var printCallRegression = new ExpressionParser("Print()").ParseExpression();
Equal(SyntaxKind.CallExpression, printCallRegression.Kind, "Print remains a valid expression call");

var printFileParser = new StatementParser("Print #fileNo, value, \"text\"");
var printFile = (FileOutputStatementSyntax)printFileParser.ParseStatement();
Equal(0, printFileParser.Diagnostics.Count, "Print file diagnostics");
Equal(SyntaxKind.PrintKeyword, printFile.Keyword.Kind, "Print file keyword");
Equal(2, printFile.Values.Count, "Print file value count");

var writeFileParser = new StatementParser("Write #1, value");
var writeFile = (FileOutputStatementSyntax)writeFileParser.ParseStatement();
Equal(0, writeFileParser.Diagnostics.Count, "Write file diagnostics");
Equal(SyntaxKind.WriteKeyword, writeFile.Keyword.Kind, "Write file keyword");

var inputFileParser = new StatementParser("Input #fileNo, first, second");
var inputFile = (FileInputStatementSyntax)inputFileParser.ParseStatement();
Equal(0, inputFileParser.Diagnostics.Count, "Input file diagnostics");
Equal(2, inputFile.Targets.Count, "Input target count");

var lineInputParser = new StatementParser("Line Input #fileNo, line");
var lineInput = (FileInputStatementSyntax)lineInputParser.ParseStatement();
Equal(0, lineInputParser.Diagnostics.Count, "Line Input diagnostics");
Equal(SyntaxKind.LineKeyword, lineInput.LineKeyword!.Kind, "Line Input keyword");
Equal(1, lineInput.Targets.Count, "Line Input target count");

var seekFileParser = new StatementParser("Seek #fileNo, 42");
var seekFile = (SeekStatementSyntax)seekFileParser.ParseStatement();
Equal(0, seekFileParser.Diagnostics.Count, "Seek diagnostics");
Equal(SyntaxKind.LiteralExpression, seekFile.Position.Kind, "Seek position expression");

var openInputParser = new StatementParser("Open fileName For Input As fileNo");
var openInput = (OpenStatementSyntax)openInputParser.ParseStatement();
Equal(0, openInputParser.Diagnostics.Count, "Open Input diagnostics");
Equal(SyntaxKind.NameExpression, openInput.PathExpression.Kind, "Open path expression");
Equal(SyntaxKind.InputKeyword, openInput.ModeKeyword.Kind, "Open Input mode");
Equal(SyntaxKind.NameExpression, openInput.FileNumberExpression.Kind, "Open file number expression");

var openOutputParser = new StatementParser("Open \"output.txt\" For Output As 1");
var openOutput = (OpenStatementSyntax)openOutputParser.ParseStatement();
Equal(0, openOutputParser.Diagnostics.Count, "Open Output diagnostics");
Equal(SyntaxKind.OutputKeyword, openOutput.ModeKeyword.Kind, "Open Output mode");
Equal(SyntaxKind.LiteralExpression, openOutput.PathExpression.Kind, "Open literal path");

var openBinaryParser = new StatementParser("Open fileName For Binary As fileNo");
var openBinary = (OpenStatementSyntax)openBinaryParser.ParseStatement();
Equal(SyntaxKind.BinaryKeyword, openBinary.ModeKeyword.Kind, "Open Binary mode");
Equal(0, openBinaryParser.Diagnostics.Count, "Open Binary diagnostics");

var closeAllParser = new StatementParser("Close");
var closeAll = (CloseStatementSyntax)closeAllParser.ParseStatement();
Equal(0, closeAllParser.Diagnostics.Count, "bare Close diagnostics");
Equal(0, closeAll.FileNumbers.Count, "bare Close file count");

var closeFilesParser = new StatementParser("Close 1, fileNo");
var closeFiles = (CloseStatementSyntax)closeFilesParser.ParseStatement();
Equal(0, closeFilesParser.Diagnostics.Count, "Close files diagnostics");
Equal(2, closeFiles.FileNumbers.Count, "Close files count");
Equal(SyntaxKind.LiteralExpression, closeFiles.FileNumbers[0].Kind, "Close literal file number");
Equal(SyntaxKind.NameExpression, closeFiles.FileNumbers[1].Kind, "Close named file number");

var onEventCallParser = new StatementParser("On Event SAX_StartElement From parser Call SAXStartElement");
var onEventCall = (OnEventStatementSyntax)onEventCallParser.ParseStatement();
Equal(0, onEventCallParser.Diagnostics.Count, "On Event Call diagnostics");
Equal("SAX_StartElement", onEventCall.EventName.Text, "On Event event name");
Equal(SyntaxKind.NameExpression, onEventCall.SourceExpression.Kind, "On Event source expression");
Equal(SyntaxKind.CallKeyword, onEventCall.ActionKeyword.Kind, "On Event Call action");
Equal("SAXStartElement", onEventCall.HandlerToken!.Text, "On Event Call handler");

var onEventMemberSourceParser = new StatementParser("On Event Changed From holder.Parser Call HandleChanged");
var onEventMemberSource = (OnEventStatementSyntax)onEventMemberSourceParser.ParseStatement();
Equal(0, onEventMemberSourceParser.Diagnostics.Count, "On Event member source diagnostics");
Equal(SyntaxKind.MemberAccessExpression, onEventMemberSource.SourceExpression.Kind, "On Event member source expression");

var onEventRemoveParser = new StatementParser("On Event SAX_Characters From parser Remove SAXCharacters");
var onEventRemove = (OnEventStatementSyntax)onEventRemoveParser.ParseStatement();
Equal(0, onEventRemoveParser.Diagnostics.Count, "On Event Remove diagnostics");
Equal(SyntaxKind.RemoveKeyword, onEventRemove.ActionKeyword.Kind, "On Event Remove action");
Equal("SAXCharacters", onEventRemove.HandlerToken!.Text, "On Event Remove handler");

var onEventRemoveAllParser = new StatementParser("On Event SAX_Characters From parser Remove");
var onEventRemoveAll = (OnEventStatementSyntax)onEventRemoveAllParser.ParseStatement();
Equal(0, onEventRemoveAllParser.Diagnostics.Count, "On Event Remove-all diagnostics");
Equal(false, onEventRemoveAll.HandlerToken is not null, "On Event Remove-all has no handler");

var onErrorGotoParser = new StatementParser("On Error GoTo ErrorHandler");
var onErrorGoto = (OnErrorStatementSyntax)onErrorGotoParser.ParseStatement();
Equal(0, onErrorGotoParser.Diagnostics.Count, "On Error GoTo diagnostics");
Equal(SyntaxKind.GoToKeyword, onErrorGoto.ActionKeyword.Kind, "On Error GoTo action");
Equal("ErrorHandler", onErrorGoto.TargetToken!.Text, "On Error GoTo target");

var onErrorResumeNextParser = new StatementParser("On Error Resume Next");
var onErrorResumeNext = (OnErrorStatementSyntax)onErrorResumeNextParser.ParseStatement();
Equal(0, onErrorResumeNextParser.Diagnostics.Count, "On Error Resume Next diagnostics");
Equal(SyntaxKind.ResumeKeyword, onErrorResumeNext.ActionKeyword.Kind, "On Error Resume action");
Equal(SyntaxKind.NextKeyword, onErrorResumeNext.TargetToken!.Kind, "On Error Resume Next target");

var onErrorDisableParser = new StatementParser("On Error GoTo 0");
var onErrorDisable = (OnErrorStatementSyntax)onErrorDisableParser.ParseStatement();
Equal(0, onErrorDisableParser.Diagnostics.Count, "On Error GoTo 0 diagnostics");
Equal("0", onErrorDisable.TargetToken!.Text, "On Error GoTo 0 target");

var resumeParser = new StatementParser("Resume");
var resume = (ResumeStatementSyntax)resumeParser.ParseStatement();
Equal(0, resumeParser.Diagnostics.Count, "Resume diagnostics");
Equal(false, resume.TargetToken is not null, "bare Resume target");

var resumeNextParser = new StatementParser("Resume Next");
var resumeNext = (ResumeStatementSyntax)resumeNextParser.ParseStatement();
Equal(SyntaxKind.NextKeyword, resumeNext.TargetToken!.Kind, "Resume Next target");
Equal(0, resumeNextParser.Diagnostics.Count, "Resume Next diagnostics");

var resumeLabelParser = new StatementParser("Resume RetryLabel");
var resumeLabel = (ResumeStatementSyntax)resumeLabelParser.ParseStatement();
Equal("RetryLabel", resumeLabel.TargetToken!.Text, "Resume label target");
Equal(0, resumeLabelParser.Diagnostics.Count, "Resume label diagnostics");

var errorStatementParser = new StatementParser("Error 123, \"expected-error\"");
var errorStatement = (ErrorStatementSyntax)errorStatementParser.ParseStatement();
Equal(0, errorStatementParser.Diagnostics.Count, "Error statement diagnostics");
Equal(SyntaxKind.LiteralExpression, errorStatement.NumberExpression.Kind, "Error number expression");
Equal(SyntaxKind.LiteralExpression, errorStatement.DescriptionExpression!.Kind, "Error description expression");

var callStatementParser = new StatementParser("Call Sleep(1)");
var callStatement = callStatementParser.ParseStatement();
Equal(SyntaxKind.CallStatement, callStatement.Kind, "Call statement kind");
Equal(0, callStatementParser.Diagnostics.Count, "Call statement diagnostics");
var callStatementSyntax = (CallStatementSyntax)callStatement;
Equal(SyntaxKind.CallExpression, callStatementSyntax.Expression.Kind, "Call statement expression kind");
Equal(new TextSpan(0, 13), callStatementSyntax.Span, "Call statement full span");

var meMemberParser = new ExpressionParser("Me.Name");
var meMember = (MemberAccessExpressionSyntax)meMemberParser.ParseExpression();
Equal(0, meMemberParser.Diagnostics.Count, "Me member access diagnostics");
Equal("Me", ((NameExpressionSyntax)meMember.Expression).IdentifierToken.Text, "Me self-reference");
Equal("Name", meMember.NameToken.Text, "Me member name");

var meCallParser = new ExpressionParser("Me.Update()");
var meCall = (CallExpressionSyntax)meCallParser.ParseExpression();
Equal(0, meCallParser.Diagnostics.Count, "Me method call diagnostics");
Equal("Update", ((MemberAccessExpressionSyntax)meCall.Target).NameToken.Text, "Me method call target");

const string extendedClassSource = "Public Class Employee Extend Person\nPublic EmployeeId As Integer\nEnd Class";
var extendedClassParser = new DeclarationParser(extendedClassSource);
var extendedClass = (ClassDeclarationSyntax)extendedClassParser.ParseDeclaration();
Equal(0, extendedClassParser.Diagnostics.Count, "Extend class diagnostics");
Equal(SyntaxKind.ExtendKeyword, extendedClass.ExtendKeyword!.Kind, "Extend keyword");
Equal("Person", extendedClass.BaseType!.Identifier.Text, "Extend base type");
Equal(1, extendedClass.Members.Count, "Extend class member count");
Equal(new TextSpan(0, extendedClassSource.Length), extendedClass.Span, "Extend class span");

var missingExtendBaseParser = new DeclarationParser("Class Broken Extend\nEnd Class");
var missingExtendBase = (ClassDeclarationSyntax)missingExtendBaseParser.ParseDeclaration();
Equal(null, missingExtendBase.BaseType, "missing Extend base type");
Equal("XPS1012", missingExtendBaseParser.Diagnostics[^1].Code, "missing Extend base diagnostic");

const string classDeclarationSource = "Public Class Person\nPrivate mName As String\nPublic Name As String\nPublic Function Describe() As String\nDescribe = mName\nEnd Function\nEnd Class";
var classDeclarationParser = new DeclarationParser(classDeclarationSource);
var classDeclaration = (ClassDeclarationSyntax)classDeclarationParser.ParseDeclaration();
Equal(0, classDeclarationParser.Diagnostics.Count, "Class declaration diagnostics");
Equal(SyntaxKind.PublicKeyword, classDeclaration.Visibility!.Kind, "Class visibility");
Equal("Person", classDeclaration.Identifier.Text, "Class identifier");
Equal(3, classDeclaration.Members.Count, "Class member count");
Equal(SyntaxKind.FieldDeclaration, classDeclaration.Members[0].Kind, "private field kind");
Equal(SyntaxKind.PrivateKeyword, ((FieldDeclarationSyntax)classDeclaration.Members[0]).Visibility!.Kind, "private field visibility");
Equal("String", ((FieldDeclarationSyntax)classDeclaration.Members[1]).Type.Identifier.Text, "public field type");
Equal(SyntaxKind.FunctionDeclaration, classDeclaration.Members[2].Kind, "class function member kind");
Equal(SyntaxKind.PublicKeyword, ((FunctionDeclarationSyntax)classDeclaration.Members[2]).Visibility!.Kind, "class function visibility");
Equal(new TextSpan(0, classDeclarationSource.Length), classDeclaration.Span, "Class declaration span");

var missingEndClassParser = new DeclarationParser("Class Missing\nvalue As Integer");
missingEndClassParser.ParseDeclaration();
Equal("XPS1012", missingEndClassParser.Diagnostics[^1].Code, "missing End Class diagnostic");

const string multilineSubDeclarationSource = "Sub Check(ByVal value As Integer)\nIf value > 0 Then\nPrint \"positive\"\nElse\nPrint \"zero\"\nEnd If\nEnd Sub";
var multilineSubDeclarationParser = new DeclarationParser(multilineSubDeclarationSource);
var multilineSubDeclaration = (SubDeclarationSyntax)multilineSubDeclarationParser.ParseDeclaration();
Equal(0, multilineSubDeclarationParser.Diagnostics.Count, "multiline Sub declaration diagnostics");
Equal(1, multilineSubDeclaration.Statements.Count, "multiline Sub top-level statement count");
Equal(SyntaxKind.IfStatement, multilineSubDeclaration.Statements[0].Kind, "multiline Sub preserves block If");
Equal(1, ((IfStatementSyntax)multilineSubDeclaration.Statements[0]).ThenStatements.Count, "multiline Sub If body count");
Equal(1, ((IfStatementSyntax)multilineSubDeclaration.Statements[0]).ElseStatements.Count, "multiline Sub Else body count");

const string defaultByRefFunctionSource = "Function AddNumbers(a As Long, b As Long) As Long\nAddNumbers = a + b\nEnd Function";
var defaultByRefFunctionParser = new DeclarationParser(defaultByRefFunctionSource);
var defaultByRefFunction = (FunctionDeclarationSyntax)defaultByRefFunctionParser.ParseDeclaration();
Equal(0, defaultByRefFunctionParser.Diagnostics.Count, "default ByRef Function diagnostics");
Equal(false, defaultByRefFunction.Parameters[0].IsByVal, "unmodified parameter is not ByVal");
Equal(true, defaultByRefFunction.Parameters[0].IsByRef, "unmodified parameter defaults to ByRef");
Equal(false, defaultByRefFunction.Parameters[0].IsExplicitByRef, "default ByRef has no explicit modifier token");
Equal("Long", defaultByRefFunction.ReturnType!.Identifier.Text, "Function Long return type");

const string subDeclarationSource = "Public Sub Add(ByVal left As Integer, ByRef right As Integer)\nright = left + right\nEnd Sub";
var subDeclarationParser = new DeclarationParser(subDeclarationSource);
var subDeclaration = (SubDeclarationSyntax)subDeclarationParser.ParseDeclaration();
Equal(0, subDeclarationParser.Diagnostics.Count, "Sub declaration diagnostics");
Equal(SyntaxKind.PublicKeyword, subDeclaration.Visibility!.Kind, "Sub visibility");
Equal("Add", subDeclaration.Identifier.Text, "Sub identifier");
Equal(2, subDeclaration.Parameters.Count, "Sub parameter count");
Equal(true, subDeclaration.Parameters[0].IsByVal, "Sub ByVal parameter");
Equal(true, subDeclaration.Parameters[1].IsByRef, "Sub ByRef parameter");
Equal(true, subDeclaration.Parameters[1].IsExplicitByRef, "Sub explicit ByRef parameter");
Equal("Integer", subDeclaration.Parameters[0].Type!.Identifier.Text, "Sub parameter type");
Equal(1, subDeclaration.Statements.Count, "Sub body statement count");
Equal(SyntaxKind.AssignmentStatement, subDeclaration.Statements[0].Kind, "Sub body assignment");
Equal(new TextSpan(0, subDeclarationSource.Length), subDeclaration.Span, "Sub declaration span");

const string functionDeclarationSource = "Private Function Increment(ByVal value As Integer) As Integer\nReturn value + 1\nEnd Function";
var functionDeclarationParser = new DeclarationParser(functionDeclarationSource);
var functionDeclaration = (FunctionDeclarationSyntax)functionDeclarationParser.ParseDeclaration();
Equal(0, functionDeclarationParser.Diagnostics.Count, "Function declaration diagnostics");
Equal(SyntaxKind.PrivateKeyword, functionDeclaration.Visibility!.Kind, "Function visibility");
Equal("Increment", functionDeclaration.Identifier.Text, "Function identifier");
Equal(1, functionDeclaration.Parameters.Count, "Function parameter count");
Equal(true, functionDeclaration.Parameters[0].IsByVal, "Function ByVal parameter");
Equal("Integer", functionDeclaration.ReturnType!.Identifier.Text, "Function return type");
Equal(1, functionDeclaration.Statements.Count, "Function body statement count");
Equal(SyntaxKind.ReturnStatement, functionDeclaration.Statements[0].Kind, "Function Return statement");
Equal(new TextSpan(0, functionDeclarationSource.Length), functionDeclaration.Span, "Function declaration span");

var missingEndFunctionParser = new DeclarationParser("Function Missing() As Integer\nReturn 1");
missingEndFunctionParser.ParseDeclaration();
Equal("XPS1012", missingEndFunctionParser.Diagnostics[^1].Code, "missing End Function diagnostic");

var returnEmptyParser = new StatementParser("Return");
var returnEmpty = (ReturnStatementSyntax)returnEmptyParser.ParseStatement();
Equal(0, returnEmptyParser.Diagnostics.Count, "Return empty diagnostics");
Equal(SyntaxKind.ReturnStatement, returnEmpty.Kind, "Return empty statement kind");
Equal<ExpressionSyntax?>(null, returnEmpty.Expression, "Return empty expression");

var returnValueParser = new StatementParser("Return value + 1");
var returnValue = (ReturnStatementSyntax)returnValueParser.ParseStatement();
Equal(0, returnValueParser.Diagnostics.Count, "Return value diagnostics");
Equal(SyntaxKind.BinaryExpression, returnValue.Expression!.Kind, "Return value expression kind");

var returnCallParser = new StatementParser("Return BuildResult(1)");
var returnCall = (ReturnStatementSyntax)returnCallParser.ParseStatement();
Equal(0, returnCallParser.Diagnostics.Count, "Return call diagnostics");
Equal(SyntaxKind.CallExpression, returnCall.Expression!.Kind, "Return call expression kind");

foreach (var (source, targetKind) in new[]
{
    ("Exit Sub", SyntaxKind.SubKeyword),
    ("Exit Function", SyntaxKind.FunctionKeyword),
    ("Exit For", SyntaxKind.ForKeyword),
    ("Exit ForAll", SyntaxKind.ForAllKeyword),
    ("Exit Do", SyntaxKind.DoKeyword),
    ("Exit While", SyntaxKind.WhileKeyword)
})
{
    var exitParser = new StatementParser(source);
    var exitStatement = (ExitStatementSyntax)exitParser.ParseStatement();
    Equal(0, exitParser.Diagnostics.Count, source + " diagnostics");
    Equal(targetKind, exitStatement.TargetKeyword.Kind, source + " target kind");
}

var malformedExitParser = new StatementParser("Exit Unknown");
malformedExitParser.ParseStatement();
Equal(2, malformedExitParser.Diagnostics.Count, "malformed Exit diagnostics");

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
Equal(SyntaxKind.ArrayExpression, runCommandCall.Arguments[1].Kind, "nested Array expression");
var nestedArray = (ArrayExpressionSyntax)runCommandCall.Arguments[1];
Equal("Array", nestedArray.ArrayIdentifier.Text, "Array identifier");
Equal(1, nestedArray.Elements.Count, "nested Array element count");
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
Equal("XPS2001", invalidBinder.Diagnostics[0].Code, "invalid unary diagnostic code");

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
Equal("(1L + (2L * 3L))", emitter.Emit(boundArithmetic), "bound arithmetic C# emission");
Equal("(10.5D + 2.25D)", emitter.Emit(boundDecimal), "bound decimal C# emission");

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
Equal("((1L + 2L) * 3L)", emitter.Emit(boundParenthesized), "bound parenthesized arithmetic C# emission");

var comparisonBinder = new ExpressionBinder();
var boundComparison = comparisonBinder.Bind(new ExpressionParser("10.5 >= 2.25").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundComparison.Kind, "bound comparison root");
Equal(typeof(bool), boundComparison.Type, "bound comparison type");
Equal(0, comparisonBinder.Diagnostics.Count, "bound comparison diagnostics");
Equal("(10.5D >= 2.25D)", emitter.Emit(boundComparison), "bound comparison C# emission");

var concatBinder = new ExpressionBinder();
var boundConcat = concatBinder.Bind(new ExpressionParser("\"XP\" & \"Script\"").ParseExpression());
Equal(BoundNodeKind.BinaryExpression, boundConcat.Kind, "bound concat root");
Equal(typeof(string), boundConcat.Type, "bound concat type");
Equal(0, concatBinder.Diagnostics.Count, "bound concat diagnostics");
Equal("(\"XP\" + \"Script\")", emitter.Emit(boundConcat), "bound concat C# emission");

var equalityBinder = new ExpressionBinder();
var boundEquality = equalityBinder.Bind(new ExpressionParser("1 + 2 = 3").ParseExpression());
Equal("((1L + 2L) == 3L)", emitter.Emit(boundEquality), "bound equality C# emission");
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
Equal("items[1L]", emitter.Emit(boundIndex), "bound index C# emission");

var memberSymbols = new SymbolTable();
memberSymbols.Declare(new VariableSymbol("text", typeof(string)));
memberSymbols.Declare(new VariableSymbol("person", typeof(object)));
memberSymbols.Declare(new FunctionSymbol("String.Substring", typeof(string), [typeof(long)]));
var memberBinder = new ExpressionBinder(memberSymbols);
var boundMemberCall = memberBinder.Bind(new ExpressionParser("text.Substring(1)").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundMemberCall.Kind, "bound member call root");
Equal(typeof(string), boundMemberCall.Type, "bound member call result type");
Equal(0, memberBinder.Diagnostics.Count, "bound member call diagnostics");
Equal("text.Substring(1L)", emitter.Emit(boundMemberCall), "bound member call C# emission");

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
Equal("store.Item(1L)", emitter.Emit(boundIndexedPropertyAccess), "bound indexed property C# emission");

var chainedIndexedPropertyBinder = new ExpressionBinder(indexedSymbols);
var boundChainedIndexedProperty = chainedIndexedPropertyBinder.Bind(new ExpressionParser("store.Item(1).Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundChainedIndexedProperty.Kind, "bound indexed property member root");
Equal(typeof(string), boundChainedIndexedProperty.Type, "bound indexed property member type");
Equal(0, chainedIndexedPropertyBinder.Diagnostics.Count, "bound indexed property member diagnostics");
Equal("store.Item(1L).Name", emitter.Emit(boundChainedIndexedProperty), "bound indexed property member C# emission");

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
Equal("typedStore.Item(1L).Value", emitter.Emit(boundTypedIndexedPropertyMember), "semantic indexed property member C# emission");

var indexedPropertyBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedProperty = indexedPropertyBinder.Bind(new ExpressionParser("people[1].Name").ParseExpression());
Equal(BoundNodeKind.MemberAccessExpression, boundIndexedProperty.Kind, "bound indexed object property root");
Equal(typeof(string), boundIndexedProperty.Type, "bound indexed object property type");
Equal(0, indexedPropertyBinder.Diagnostics.Count, "bound indexed object property diagnostics");
Equal("people[1L].Name", emitter.Emit(boundIndexedProperty), "bound indexed object property C# emission");

var indexedMethodBinder = new ExpressionBinder(indexedSymbols);
var boundIndexedMethod = indexedMethodBinder.Bind(new ExpressionParser("people[1].Describe()").ParseExpression());
Equal(BoundNodeKind.CallExpression, boundIndexedMethod.Kind, "bound indexed object method root");
Equal(typeof(string), boundIndexedMethod.Type, "bound indexed object method type");
Equal(0, indexedMethodBinder.Diagnostics.Count, "bound indexed object method diagnostics");
Equal("people[1L].Describe()", emitter.Emit(boundIndexedMethod), "bound indexed object method C# emission");

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
            Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal when literal.Token.Value is long or double =>
                CSharpSyntaxFactory.ParseExpression(Convert.ToString(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture)!),
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

var recoveryParser = new StatementParser("If True Then\nvalue = 1 extra junk\nnextValue = 2\nEnd If");
var recoveryIf = (IfStatementSyntax)recoveryParser.ParseStatement();
Equal(2, recoveryIf.ThenStatements.Count, "statement recovery preserves following statement");
Equal(SyntaxKind.AssignmentStatement, recoveryIf.ThenStatements[0].Kind, "statement recovery malformed statement kind");
Equal(SyntaxKind.AssignmentStatement, recoveryIf.ThenStatements[1].Kind, "statement recovery following statement kind");
Equal(1, recoveryParser.Diagnostics.Count, "statement recovery diagnostic count");
Equal("XPS1012", recoveryParser.Diagnostics[0].Code, "statement recovery diagnostic code");

var loopRecoveryParser = new StatementParser("While True\nvalue = 1 junk\nvalue = 2\nWend");
var recoveryWhile = (WhileStatementSyntax)loopRecoveryParser.ParseStatement();
Equal(2, recoveryWhile.Statements.Count, "loop recovery preserves following statement");
Equal(1, loopRecoveryParser.Diagnostics.Count, "loop recovery diagnostic count");

var malformedExpressionParser = new ExpressionParser("1 2");
malformedExpressionParser.ParseExpression();
Equal(1, malformedExpressionParser.Diagnostics.Count, "trailing expression token diagnostic count");
Equal("XPS1012", malformedExpressionParser.Diagnostics[0].Code, "trailing expression token diagnostic code");

var arrayExpressionParser = new ExpressionParser("Array(1, \"two\", True)");
var arrayExpression = (ArrayExpressionSyntax)arrayExpressionParser.ParseExpression();
Equal(SyntaxKind.ArrayExpression, arrayExpression.Kind, "array expression kind");
Equal(3, arrayExpression.Elements.Count, "array expression element count");
Equal(2, arrayExpression.CommaTokens.Count, "array expression comma count");
Equal(0, arrayExpressionParser.Diagnostics.Count, "array expression diagnostics");

var emptyArrayExpressionParser = new ExpressionParser("Array()");
var emptyArrayExpression = (ArrayExpressionSyntax)emptyArrayExpressionParser.ParseExpression();
Equal(0, emptyArrayExpression.Elements.Count, "empty array expression element count");
Equal(0, emptyArrayExpressionParser.Diagnostics.Count, "empty array expression diagnostics");

var nestedCallParser = new ExpressionParser("RunCommand(\"where.exe\", Array(\"winget\"))");
nestedCallParser.ParseExpression();
Equal(0, nestedCallParser.Diagnostics.Count, "nested call parser diagnostics");

var badCallBinder = new ExpressionBinder(callSymbols);
badCallBinder.Bind(new ExpressionParser("RunCommand(\"where.exe\")").ParseExpression());
Equal(1, badCallBinder.Diagnostics.Count, "RunCommand arity diagnostic count");

Console.WriteLine("AST lexer, syntax-model, expression-parser, binder and emitter focused tests passed.");
