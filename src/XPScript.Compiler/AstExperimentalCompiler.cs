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
        source = Regex.Replace(source, @"_\s*(?:\r?\n)", " ");
        source = Regex.Replace(source, @"(?im)^(?<indent>\s*)ReDim\s+(?<preserve>Preserve\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^)]*)\)\s*$", match =>
        {
            var bounds = match.Groups["bounds"].Value.Trim();
            var upper = Regex.Match(bounds, @"(?i)\bTo\s+(?<upper>.+)$").Groups["upper"].Value;
            if (string.IsNullOrWhiteSpace(upper)) upper = bounds;
            return $"{match.Groups["indent"].Value}{match.Groups["name"].Value} = ArrayResize({match.Groups["name"].Value}, {upper}, {(match.Groups["preserve"].Success ? "True" : "False")})";
        });
        var declarationStart = System.Text.RegularExpressions.Regex.Match(source, @"(?im)^\s*(Sub|Function|Class)\b");
        if (declarationStart.Success)
            source = source[declarationStart.Index..].TrimStart();
        var parser = new DeclarationParser(source);
        var unit = parser.ParseCompilationUnit();
        // The experimental path keeps going when a non-selected declaration
        // contains syntax outside the current AST slice. Binding diagnostics
        // for the selected procedure remain authoritative below.
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
        foreach (Match match in Regex.Matches(fullSource, @"^\s*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, ResolveRuntimeType(match.Groups["type"].Value), XpTypeSymbol.Variant));
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Array", typeof(long[]), [typeof(long), typeof(long)]));
        symbols.Declare(new FunctionSymbol("CStr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("UBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Base64DecodeBinary", typeof(byte[]), [typeof(string)]));
        symbols.Declare(new FunctionSymbol("Len", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LenB", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("TypeName", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("FileLen", typeof(long), [typeof(string)]));
        symbols.Declare(new FunctionSymbol("FreeFile", typeof(long), []));
        symbols.Declare(new FunctionSymbol("CInt", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("CLng", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("CDbl", typeof(double), [typeof(object)], XpTypeSymbol.FromClr(typeof(double)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CBool", typeof(bool), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Evaluate", typeof(object), [typeof(string), typeof(object)],
            XpTypeSymbol.Variant, [XpTypeSymbol.FromClr(typeof(string)), XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Chr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Asc", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Replace", typeof(string), [typeof(string), typeof(string), typeof(string)]));
        symbols.Declare(new FunctionSymbol("Trim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("UCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySort", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object), typeof(string)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CDate", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrComp", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Abs", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Int", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Fix", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Round", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Sqr", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Sgn", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Sin", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Cos", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Tan", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Hex", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Bin", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Left", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Right", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LTrim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("RTrim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Val", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Str", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CVDate", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("UChr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Uni", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Year", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("IsList", typeof(bool), [typeof(object)], XpTypeSymbol.FromClr(typeof(bool)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("IsUnknown", typeof(bool), [typeof(object)], XpTypeSymbol.FromClr(typeof(bool)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("IsNumeric", typeof(bool), [typeof(object)], XpTypeSymbol.FromClr(typeof(bool)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("IsNull", typeof(bool), [typeof(object)], XpTypeSymbol.FromClr(typeof(bool)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("RegexMatch", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArrayAppend", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArrayGetIndex", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArrayUnique", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySlice", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySlice", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArrayResize", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Explode", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("FullTrim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("DateNumber", typeof(object), [typeof(long), typeof(long), typeof(long)]));
        symbols.Declare(new VariableSymbol("Application", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Debugger", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Process", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Session", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Request", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("NotesSession", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("UIForm", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Database", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Document", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Err", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Response", typeof(object), XpTypeSymbol.Variant));
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
        var procedureStubs = string.Join(Environment.NewLine, unit.Declarations
            .Where(item => item is SubDeclarationSyntax or FunctionDeclarationSyntax)
            .Where(item => !string.Equals(item switch { SubDeclarationSyntax subDeclaration => subDeclaration.Identifier.Text, FunctionDeclarationSyntax functionDeclaration => functionDeclaration.Identifier.Text, _ => string.Empty }, declarationName, StringComparison.OrdinalIgnoreCase))
            .Select(item => item switch
            {
                SubDeclarationSyntax procedure => $"    public static void {procedure.Identifier.Text}({string.Join(", ", procedure.Parameters.Select(p => $"{CSharpType(ResolveRuntimeType(p.Type?.Identifier.Text))} {p.Identifier.Text}"))}) {{ }}",
                FunctionDeclarationSyntax procedure => $"    public static {CSharpType(ResolveRuntimeType(procedure.ReturnType?.Identifier.Text))} {procedure.Identifier.Text}({string.Join(", ", procedure.Parameters.Select(p => $"{CSharpType(ResolveRuntimeType(p.Type?.Identifier.Text))} {p.Identifier.Text}"))}) => default;",
                _ => string.Empty
            }));
        var moduleFields = string.Join(Environment.NewLine, Regex.Matches(fullSource, @"^\s*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = null;"));
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
    public static IEnumerable Enumerate(object? value) => value as IEnumerable ?? System.Array.Empty<object>();
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
    public static dynamic Err = new ExpandoObject();
    public static dynamic Response = new ExpandoObject();
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
    public static double CDbl(object value) => Convert.ToDouble(value);
    public static bool CBool(object value) => Convert.ToBoolean(value);
    public static object? Evaluate(string expression, object? value) => value;
    public static string Chr(object value) => Convert.ToChar(value).ToString();
    public static long Asc(object value) => Convert.ToChar(value);
    public static string Replace(string value, string oldValue, string newValue) => value.Replace(oldValue, newValue, StringComparison.Ordinal);
    public static string Trim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static string UCase(object? value) => (value?.ToString() ?? string.Empty).ToUpperInvariant();
    public static string LCase(object? value) => (value?.ToString() ?? string.Empty).ToLowerInvariant();
    public static object? ArraySort(object? value) => value;
    public static string Join(object? value, string separator) => value is System.Collections.IEnumerable items ? string.Join(separator, items.Cast<object?>()) : string.Empty;
    public static object CDate(object value) => Convert.ToDateTime(value);
    public static long StrComp(object? left, object? right) => string.Compare(left?.ToString(), right?.ToString(), StringComparison.Ordinal);
    public static object Abs(object value) => Math.Abs(Convert.ToDouble(value));
    public static object Int(object value) => Math.Floor(Convert.ToDouble(value));
    public static object Fix(object value) => Math.Truncate(Convert.ToDouble(value));
    public static object Round(object value) => Math.Round(Convert.ToDouble(value));
    public static object Sqr(object value) => Math.Sqrt(Convert.ToDouble(value));
    public static object Sgn(object value) => Math.Sign(Convert.ToDouble(value));
    public static object Sin(object value) => Math.Sin(Convert.ToDouble(value));
    public static object Cos(object value) => Math.Cos(Convert.ToDouble(value));
    public static object Tan(object value) => Math.Tan(Convert.ToDouble(value));
    public static string Hex(object value) => Convert.ToInt64(value).ToString("X");
    public static string Bin(object value) => Convert.ToString(Convert.ToInt64(value), 2) ?? "0";
    public static string Left(object value, object count) => (value?.ToString() ?? string.Empty)[..Math.Clamp(Convert.ToInt32(count), 0, value?.ToString()?.Length ?? 0)];
    public static string Right(object value, object count) { var text = value?.ToString() ?? string.Empty; var length = Math.Clamp(Convert.ToInt32(count), 0, text.Length); return text[^length..]; }
    public static string LTrim(object? value) => value?.ToString()?.TrimStart() ?? string.Empty;
    public static string RTrim(object? value) => value?.ToString()?.TrimEnd() ?? string.Empty;
    public static object Val(object value) => double.TryParse(value?.ToString(), out var result) ? result : 0d;
    public static string Str(object value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    public static object CVDate(object value) => Convert.ToDateTime(value);
    public static string UChr(object value) => char.ConvertFromUtf32(Convert.ToInt32(value));
    public static long Uni(object value) => char.ConvertToUtf32((value?.ToString() ?? "\0"), 0);
    public static long Year(object value) => Convert.ToDateTime(value).Year;
    public static bool IsList(object? value) => value is System.Collections.IDictionary;
    public static bool IsUnknown(object? value) => value is null;
    public static bool IsNumeric(object? value) => double.TryParse(value?.ToString(), out _);
    public static bool IsNull(object? value) => value is null;
    public static object RegexMatch(object? value, object? pattern) => System.Text.RegularExpressions.Regex.Matches(value?.ToString() ?? "", pattern?.ToString() ?? "").Select(match => match.Value).ToArray();
    public static object ArrayAppend(object? value, object? item) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Append(item).ToArray() : new object?[] { item };
    public static long ArrayGetIndex(object? value, object? item) => value is System.Collections.IEnumerable values ? values.Cast<object?>().ToList().FindIndex(x => Equals(x, item)) : -1;
    public static object ArrayUnique(object? value) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Distinct().ToArray() : System.Array.Empty<object?>();
    public static object ArraySlice(object? value, object? start) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Skip(Convert.ToInt32(start)).ToArray() : System.Array.Empty<object?>();
    public static object ArraySplice(object? value, object? start, object? count) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).ToArray() : System.Array.Empty<object?>();
    public static object ArraySplice(object? value, object? start, object? count, object? replacement) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).Append(replacement).ToArray() : new[] { replacement };
    public static object ArraySplice(object? value, object? start, object? count, object? first, object? second) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).Concat(new[] { first, second }).ToArray() : new[] { first, second };
    public static object ArrayResize(object? value, object? upper, object? preserve) { var length = Math.Max(0, Convert.ToInt32(upper) + 1); var result = new object?[length]; if (Convert.ToBoolean(preserve) && value is System.Collections.IEnumerable values) values.Cast<object?>().Take(length).ToArray().CopyTo(result, 0); return result; }
    public static object Explode(object? value, object? separator) => (value?.ToString() ?? string.Empty).Split(separator?.ToString() ?? ",");
    public static string FullTrim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static object DateNumber(long year, long month, long day) => new DateTime((int)year, (int)month, (int)day);
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
        public static bool Like(string value, string pattern) => System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, "^" + System.Text.RegularExpressions.Regex.Escape(pattern ?? string.Empty).Replace("\\\\*", ".*").Replace("\\\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
{{procedureStubs}}
{{moduleFields}}
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
    private static string CSharpType(Type type) => type == typeof(void) ? "void" : type == typeof(long) ? "long" : type == typeof(double) ? "double" : type == typeof(bool) ? "bool" : type == typeof(string) ? "string" : "object";
}

