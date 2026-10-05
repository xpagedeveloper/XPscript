using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
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
        var fullSource = source;
        var declarationStart = System.Text.RegularExpressions.Regex.Match(source, @"(?im)^\s*(Sub|Function|Class)\b");
        if (declarationStart.Success)
            source = source[declarationStart.Index..].TrimStart();
        var parser = new DeclarationParser(source);
        var unit = parser.ParseCompilationUnit();
        if (parser.Diagnostics.Count > 0)
        {
            var diagnostic = parser.Diagnostics[0];
            throw new CompilerException(
                string.Join(Environment.NewLine, parser.Diagnostics.Select(item => item.Message)),
                diagnostic.Code,
                "syntax");
        }
        var declaration = unit.Declarations.FirstOrDefault(item => item is SubDeclarationSyntax candidate && candidate.Identifier.Text.Equals("Main", StringComparison.OrdinalIgnoreCase))
            ?? unit.Declarations.FirstOrDefault(item => item is SubDeclarationSyntax or FunctionDeclarationSyntax);
        var sub = declaration as SubDeclarationSyntax;
        var function = declaration as FunctionDeclarationSyntax;
        if (sub is null && function is null)
            throw new CompilerException("AST experimental compilation currently requires a Sub declaration.", "XPS3001", "ast");

        var symbols = SymbolTable.CreateWithCompilerCatalog();
        foreach (var classDeclaration in unit.Declarations.OfType<ClassDeclarationSyntax>())
            symbols.Declare(new TypeSymbol(classDeclaration.Identifier.Text, typeof(object), XpTypeSymbol.User(classDeclaration.Identifier.Text)));
        foreach (Match match in Regex.Matches(fullSource, @"^\s*(?:Public\s+|Private\s+)?Class\s+(?<name>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new TypeSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.User(match.Groups["name"].Value)));
        foreach (var procedure in unit.Declarations.OfType<SubDeclarationSyntax>())
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray()));
        foreach (var procedure in unit.Declarations.OfType<FunctionDeclarationSyntax>())
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, ResolveRuntimeType(procedure.ReturnType?.Identifier.Text), procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray()));
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Array", typeof(long[]), [typeof(long), typeof(long)]));
        symbols.Declare(new FunctionSymbol("CStr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("UBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Base64DecodeBinary", typeof(byte[]), [typeof(string)]));
        symbols.Declare(new FunctionSymbol("Len", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("LenB", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("TypeName", typeof(string), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("FileLen", typeof(long), [typeof(string)]));
        symbols.Declare(new FunctionSymbol("FreeFile", typeof(long), []));
        symbols.Declare(new FunctionSymbol("CInt", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("CLng", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("CBool", typeof(bool), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Evaluate", typeof(object), [typeof(string), typeof(object)]));
        symbols.Declare(new FunctionSymbol("Evaluate", typeof(object), [typeof(string), typeof(long)]));
        symbols.Declare(new FunctionSymbol("Chr", typeof(string), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Asc", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Replace", typeof(string), [typeof(string), typeof(string), typeof(string)]));
        symbols.Declare(new FunctionSymbol("Trim", typeof(string), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("UCase", typeof(string), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("LCase", typeof(string), [typeof(object)]));
        symbols.Declare(new VariableSymbol("Application", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Debugger", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Process", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Session", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Request", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("NotesSession", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("UIForm", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Database", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Document", typeof(object), XpTypeSymbol.Variant));
        var declarationParameters = sub?.Parameters ?? function!.Parameters;
        var parameters = declarationParameters.Select(parameter => new ParameterSymbol(parameter.Identifier.Text,
            ResolveRuntimeType(parameter.Type?.Identifier.Text), parameter.IsByRef, XpTypeSymbol.FromClr(ResolveRuntimeType(parameter.Type?.Identifier.Text)))).ToArray();
        foreach (var parameter in parameters) symbols.Declare(parameter);
        var returnType = function is null ? typeof(void) : ResolveRuntimeType(function.ReturnType?.Identifier.Text);
        if (function is not null) symbols.Declare(new LocalSymbol(function.Identifier.Text, returnType, XpTypeSymbol.FromClr(returnType)));
        var binder = new StatementBinder(symbols, function is null ? null : XpTypeSymbol.FromClr(returnType), function is not null, true);
        var statements = sub?.Statements ?? function!.Statements;
        var bound = statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
        if (binder.Diagnostics.Count > 0)
            throw new CompilerException(string.Join(Environment.NewLine, binder.Diagnostics.Select(d => d.Message)), binder.Diagnostics[0].Code, "semantic");

        var declarationName = sub?.Identifier.Text ?? function!.Identifier.Text;
        var methodName = declarationName.Equals("Main", StringComparison.OrdinalIgnoreCase) && parameters.Length == 0 ? "Main" : declarationName;
        var body = new BoundMethodEmitter().Emit(methodName, returnType, parameters, bound);
        var entryPoint = methodName.Equals("Main", StringComparison.Ordinal) ? string.Empty : "    public static void Main() { }\n";
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
    public static dynamic Debugger = new ExpandoObject();
    public static dynamic Process = new ExpandoObject();
    public static dynamic Session = new ExpandoObject();
    public static dynamic Request = new ExpandoObject();
    public static dynamic NotesSession = new ExpandoObject();
    public static dynamic UIForm = new ExpandoObject();
    public static dynamic Database = new ExpandoObject();
    public static dynamic Document = new ExpandoObject();
    public static string CStr(object? value) => Convert.ToString(value) ?? string.Empty;
    public static long LBound(object value) => 0;
    public static long UBound(object value) => value is Array array ? array.Length - 1 : -1;
    public static byte[] Base64DecodeBinary(string value) => Convert.FromBase64String(value);
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
    public static long[] Array(long first, long second) => [first, second];
    public static long Len(object? value) => value is Array array ? array.Length : (value?.ToString()?.Length ?? 0);
    public static long LenB(object? value) => Len(value);
    public static string TypeName(object? value) => value?.GetType().Name ?? "Nothing";
    public static long FileLen(string path) => new FileInfo(path).Length;
    public static long FreeFile() => 1;
    public static long CInt(object value) => Convert.ToInt64(value);
    public static long CLng(object value) => Convert.ToInt64(value);
    public static bool CBool(object value) => Convert.ToBoolean(value);
    public static object? Evaluate(string expression, object? value) => value;
    public static string Chr(object value) => Convert.ToChar(value).ToString();
    public static long Asc(object value) => Convert.ToChar(value);
    public static string Replace(string value, string oldValue, string newValue) => value.Replace(oldValue, newValue, StringComparison.Ordinal);
    public static string Trim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static string UCase(object? value) => (value?.ToString() ?? string.Empty).ToUpperInvariant();
    public static string LCase(object? value) => (value?.ToString() ?? string.Empty).ToLowerInvariant();
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
        public static object? CObj(object? value) => value;
    }
{{body}}
{{entryPoint}}
}
""";
        return await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool), "STRING" => typeof(string), "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double), _ => typeof(object)
    };
}
