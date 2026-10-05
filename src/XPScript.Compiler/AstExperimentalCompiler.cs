using System.Text;
using System.Collections;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Experimental CLI bridge for the currently supported AST compilation slice.</summary>
internal static class AstExperimentalCompiler
{
    public static async Task<string> CompileAsync(string sourcePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var declarationStart = System.Text.RegularExpressions.Regex.Match(source, @"(?im)^\s*(Sub|Function|Class)\b");
        if (declarationStart.Success)
            source = source[declarationStart.Index..].TrimStart();
        var parser = new DeclarationParser(source);
        var declaration = parser.ParseDeclaration();
        if (parser.Diagnostics.Count > 0)
        {
            var diagnostic = parser.Diagnostics[0];
            throw new CompilerException(
                string.Join(Environment.NewLine, parser.Diagnostics.Select(item => item.Message)),
                diagnostic.Code,
                "syntax");
        }
        if (declaration is not SubDeclarationSyntax sub || !sub.Identifier.Text.Equals("Main", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("AST experimental compilation currently requires a top-level Sub Main().", "XPS3001", "ast");

        var symbols = SymbolTable.CreateWithCompilerCatalog();
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Array", typeof(long[]), [typeof(long), typeof(long)]));
        symbols.Declare(new VariableSymbol("Application", typeof(object), XpTypeSymbol.Variant));
        var binder = new StatementBinder(symbols, allowDynamicMembers: true);
        var bound = sub.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
        if (binder.Diagnostics.Count > 0)
            throw new CompilerException(string.Join(Environment.NewLine, binder.Diagnostics.Select(d => d.Message)), binder.Diagnostics[0].Code, "semantic");

        var body = new BoundMethodEmitter().Emit("Main", typeof(void), bound);
        var generated = $$"""
using System;
using System.Collections.Generic;
using System.Collections;
using System.Dynamic;
internal static class LSCoreCompare
{
    public static bool Equal(object? left, object? right) => string.Equals(left?.ToString(), right?.ToString(), StringComparison.Ordinal);
    public static bool Between(object? value, object? low, object? high) => Convert.ToDouble(value) >= Convert.ToDouble(low) && Convert.ToDouble(value) <= Convert.ToDouble(high);
    public static bool Rel(object? value, string op, object? other) => op switch
    {
        "=" => Equal(value, other), "<>" => !Equal(value, other),
        ">" => Convert.ToDouble(value) > Convert.ToDouble(other), ">=" => Convert.ToDouble(value) >= Convert.ToDouble(other),
        "<" => Convert.ToDouble(value) < Convert.ToDouble(other), "<=" => Convert.ToDouble(value) <= Convert.ToDouble(other), _ => false
    };
}
internal static class LSForAllRuntime
{
    public static IEnumerable Enumerate(object? value) => value as IEnumerable ?? Array.Empty<object>();
}
internal static class Program
{
    public static dynamic Application = new ExpandoObject();
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
    public static long[] Array(long first, long second) => [first, second];
    public static class XPScriptNullRuntime
    {
        public static bool ConditionValue(object? value) => value is bool boolean ? boolean : Convert.ToBoolean(value ?? false);
    }
    public static class XPScriptRuntime
    {
        public static string PrintText(object? value) => value?.ToString() ?? "Variable is null";
        public static IEnumerable<long> Range(long from, long to, long step)
        {
            if (step == 0) throw new ArgumentOutOfRangeException(nameof(step));
            if (step > 0) for (var value = from; value <= to; value += step) yield return value;
            else for (var value = from; value >= to; value += step) yield return value;
        }
        public static long CLng(object value) => Convert.ToInt64(value);
        public static string CStr(object? value) => Convert.ToString(value) ?? string.Empty;
    }
{{body}}
}
""";
        return await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
