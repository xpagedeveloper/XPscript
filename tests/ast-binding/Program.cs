using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

static ExpressionSyntax Parse(string text)
{
    var parser = new ExpressionParser(text);
    var syntax = parser.ParseExpression();
    if (parser.Diagnostics.Count != 0)
        throw new InvalidOperationException($"Focused binder input '{text}' must parse without diagnostics.");
    return syntax;
}

var unknownBinder = new ExpressionBinder();
_ = unknownBinder.Bind(Parse("missingName"));
if (unknownBinder.Diagnostics.Count != 1)
    throw new InvalidOperationException($"Expected one binder diagnostic, got {unknownBinder.Diagnostics.Count}.");

var unknownDiagnostic = unknownBinder.Diagnostics[0];
if (!string.Equals(unknownDiagnostic.Code, "XPS2008", StringComparison.Ordinal))
    throw new InvalidOperationException($"Expected XPS2008, got {unknownDiagnostic.Code}.");
if (!unknownDiagnostic.Message.Contains("missingName", StringComparison.Ordinal))
    throw new InvalidOperationException($"Unknown-symbol diagnostic did not identify missingName: {unknownDiagnostic.Message}");

Console.WriteLine("AST_BINDING_UNKNOWN_SYMBOL_OK");

var symbols = new SymbolTable();
var widgetType = XpTypeSymbol.User("Widget");
symbols.Declare(new VariableSymbol("widget", typeof(object), widgetType));
symbols.Declare(new PropertySymbol("Widget.Title", typeof(string), XpTypeSymbol.FromClr(typeof(string))));

var memberBinder = new ExpressionBinder(symbols);
var member = memberBinder.Bind(Parse("widget.Title"));
if (memberBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Expected member lookup to succeed, got: {string.Join("; ", memberBinder.Diagnostics.Select(d => d.Message))}.");
if (member is not BoundMemberAccessExpression || member.Type != typeof(string))
    throw new InvalidOperationException("Member lookup did not bind Widget.Title as a string property.");

Console.WriteLine("AST_BINDING_MEMBER_LOOKUP_OK");

var missingMemberBinder = new ExpressionBinder(symbols);
_ = missingMemberBinder.Bind(Parse("widget.Missing"));
if (missingMemberBinder.Diagnostics.Count != 1 || missingMemberBinder.Diagnostics[0].Code != "XPS2009")
    throw new InvalidOperationException("Missing member must produce exactly one XPS2009 diagnostic.");

Console.WriteLine("AST_BINDING_UNKNOWN_MEMBER_OK");

var overloadSymbols = new SymbolTable();
overloadSymbols.Declare(new FunctionSymbol("Pick", typeof(string), [typeof(long)]));
overloadSymbols.Declare(new FunctionSymbol("Pick", typeof(long), [typeof(string)]));

var overloadBinder = new ExpressionBinder(overloadSymbols);
var overloadCall = overloadBinder.Bind(Parse("Pick(1)"));
if (overloadBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Expected exact overload binding to succeed, got: {string.Join("; ", overloadBinder.Diagnostics.Select(d => d.Message))}.");
if (overloadCall is not BoundCallExpression boundCall || boundCall.Function.ParameterTypes.Count != 1 || boundCall.Function.ParameterTypes[0] != typeof(long))
    throw new InvalidOperationException("Call binding did not select the exact Integer/Long overload.");

Console.WriteLine("AST_BINDING_OVERLOAD_OK");

var noMatchBinder = new ExpressionBinder(overloadSymbols);
_ = noMatchBinder.Bind(Parse("Pick(True)"));
if (noMatchBinder.Diagnostics.Count != 1 || noMatchBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("No matching overload must produce exactly one XPS2004 diagnostic.");

Console.WriteLine("AST_BINDING_NO_MATCHING_OVERLOAD_OK");

var ambiguousSymbols = new SymbolTable();
ambiguousSymbols.Declare(new FunctionSymbol("Choose", typeof(long), [typeof(long)]));
ambiguousSymbols.Declare(new FunctionSymbol("Choose", typeof(string), [typeof(long)]));

var ambiguousBinder = new ExpressionBinder(ambiguousSymbols);
_ = ambiguousBinder.Bind(Parse("Choose(1)"));
if (ambiguousBinder.Diagnostics.Count != 1 || ambiguousBinder.Diagnostics[0].Code != "XPS2005")
    throw new InvalidOperationException("Ambiguous overload must produce exactly one XPS2005 diagnostic.");

Console.WriteLine("AST_BINDING_AMBIGUOUS_OVERLOAD_OK");

var declarationSymbols = new SymbolTable();
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(long)])))
    throw new InvalidOperationException("First overload declaration must succeed.");
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(string)])))
    throw new InvalidOperationException("Same-name overload with a different parameter type must succeed.");
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(long), typeof(string)])))
    throw new InvalidOperationException("Same-name overload with a different parameter count must succeed.");
if (declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(long), [typeof(long)]), out var duplicateCode, out _))
    throw new InvalidOperationException("Return type alone must not create a distinct overload.");
if (duplicateCode != "XPS2006")
    throw new InvalidOperationException($"Duplicate parameter signature must produce XPS2006, got {duplicateCode}.");

Console.WriteLine("AST_BINDING_OVERLOAD_DECLARATION_RULES_OK");

var byRefSymbols = new SymbolTable();
byRefSymbols.Declare(new VariableSymbol("value", typeof(long)));
byRefSymbols.Declare(new FunctionSymbol("Touch", typeof(long), [typeof(long)], ByRefParameters: [true]));
byRefSymbols.Declare(new FunctionSymbol("Read", typeof(long), [typeof(long)], ByRefParameters: [false]));

var byRefVariableBinder = new ExpressionBinder(byRefSymbols);
_ = byRefVariableBinder.Bind(Parse("Touch(value)"));
if (byRefVariableBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("A variable argument must be valid for a ByRef parameter.");

var byRefLiteralBinder = new ExpressionBinder(byRefSymbols);
_ = byRefLiteralBinder.Bind(Parse("Touch(1)"));
if (byRefLiteralBinder.Diagnostics.Count != 1 || byRefLiteralBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("A literal argument must not match a ByRef overload.");

var byValLiteralBinder = new ExpressionBinder(byRefSymbols);
_ = byValLiteralBinder.Bind(Parse("Read(1)"));
if (byValLiteralBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("A literal argument must be valid for a ByVal parameter.");

var modeOverloads = new SymbolTable();
if (!modeOverloads.TryDeclare(new FunctionSymbol("Mode", typeof(long), [typeof(long)], ByRefParameters: [true])))
    throw new InvalidOperationException("ByRef overload declaration must succeed.");
if (!modeOverloads.TryDeclare(new FunctionSymbol("Mode", typeof(long), [typeof(long)], ByRefParameters: [false])))
    throw new InvalidOperationException("ByVal overload with the same type signature must remain distinct.");

Console.WriteLine("AST_BINDING_BYREF_BYVAL_OK");
