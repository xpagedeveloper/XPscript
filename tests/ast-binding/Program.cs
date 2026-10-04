using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

var parser = new ExpressionParser("missingName");
var syntax = parser.ParseExpression();
if (parser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Focused unknown-symbol input must parse without diagnostics.");

var binder = new ExpressionBinder();
_ = binder.Bind(syntax);

if (binder.Diagnostics.Count != 1)
    throw new InvalidOperationException($"Expected one binder diagnostic, got {binder.Diagnostics.Count}.");

var diagnostic = binder.Diagnostics[0];
if (!string.Equals(diagnostic.Code, "XPS2008", StringComparison.Ordinal))
    throw new InvalidOperationException($"Expected XPS2008, got {diagnostic.Code}.");
if (!diagnostic.Message.Contains("missingName", StringComparison.Ordinal))
    throw new InvalidOperationException($"Unknown-symbol diagnostic did not identify missingName: {diagnostic.Message}");

Console.WriteLine("AST_BINDING_UNKNOWN_SYMBOL_OK");
