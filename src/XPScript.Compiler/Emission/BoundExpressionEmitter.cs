using System.Globalization;
using SymbolDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

public sealed class BoundExpressionEmitter
{
    public string Emit(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression literal => EmitLiteral(literal),
        BoundConversionExpression conversion => EmitConversion(conversion),
        BoundNameExpression name => name.Symbol.Name,
        BoundMemberAccessExpression member when member.Receiver is BoundNameExpression receiver && receiver.Symbol.Name.Equals("Console", StringComparison.OrdinalIgnoreCase) && member.Name.Equals("WriteLine", StringComparison.OrdinalIgnoreCase) => "System.Console.WriteLine",
        BoundMemberAccessExpression member => $"{(member.Receiver.SemanticType.IsVariant ? $"((dynamic)({Emit(member.Receiver)}))" : Emit(member.Receiver))}.{member.Name}",
        BoundIndexExpression index => index.Expression.Type == typeof(object)
            ? $"((dynamic){Emit(index.Expression)})[{Emit(index.Index)}]"
            : $"{Emit(index.Expression)}[{Emit(index.Index)}]",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase) => "XPScriptNativeHttp.CreateClient()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPHttpRequest", StringComparison.OrdinalIgnoreCase) => "XPScriptNativeHttp.CreateRequest()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) => $"new XpPath({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("Person", StringComparison.OrdinalIgnoreCase) => $"new XpPerson({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new => @new.Type == typeof(object)
            ? "new System.Dynamic.ExpandoObject()"
            : $"new {@new.Type.Name}({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundIndexedPropertyExpression indexed => $"{Emit(indexed.Receiver)}.{indexed.Property.Name.Split('.').Last()}({string.Join(", ", indexed.Arguments.Select(Emit))})",
        BoundCallExpression call => EmitCall(call),
        BoundUnaryExpression unary => unary.OperatorKind == SyntaxKind.NotKeyword && unary.Type == typeof(object)
            ? $"(!Convert.ToBoolean({Emit(unary.Operand)}))"
            : $"({EmitUnaryOperator(unary.OperatorKind, unary.Type)}{Emit(unary.Operand)})",
        BoundBinaryExpression binary => EmitBinary(binary),
        _ => throw new NotSupportedException($"Emission is not implemented for {expression.Kind}.")
    };

    private string EmitCall(BoundCallExpression call)
    {
        var arguments = call.Arguments.Select((argument, index) =>
        {
            var byRef = call.Function.ByRefParameters?[index] == true;
            if (byRef && argument is not BoundNameExpression)
                throw new NotSupportedException("ByRef conversion requires temporary and copy-back lowering.");
            return (byRef ? "ref " : "") + Emit(argument);
        });
        var functionName = call.Function.Name.EndsWith("$", StringComparison.Ordinal) ? call.Function.Name.TrimEnd('$') : call.Function.Name;
        if (functionName.Equals("Error", StringComparison.OrdinalIgnoreCase) || functionName.Equals("GetTickCount", StringComparison.OrdinalIgnoreCase) || functionName.Equals("Loc", StringComparison.OrdinalIgnoreCase) || functionName.Equals("ProcedureCounter", StringComparison.OrdinalIgnoreCase) || functionName.Equals("ShowDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("LoadFileDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("OpenFileDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("SaveFileDialog", StringComparison.OrdinalIgnoreCase))
            functionName = "XPScriptRuntime." + functionName;
        return $"{(call.Target is null ? functionName : Emit(call.Target))}({string.Join(", ", arguments)})";
    }

    private string EmitBinary(BoundBinaryExpression binary) => binary.Type == typeof(object)
        ? $"((dynamic)({Emit(binary.Left)}) {EmitBinaryOperator(binary.OperatorKind)} (dynamic)({Emit(binary.Right)}))"
        : binary.OperatorKind == SyntaxKind.PlusToken && binary.Type == typeof(string)
        ? $"(Convert.ToString({Emit(binary.Left)}) + Convert.ToString({Emit(binary.Right)}))"
        : binary.OperatorKind == SyntaxKind.LikeKeyword
            ? $"XPScriptRuntime.Like({Emit(binary.Left)}, {Emit(binary.Right)})"
            : $"({Emit(binary.Left)} {EmitBinaryOperator(binary.OperatorKind)} {Emit(binary.Right)})";

    private static string EmitLiteral(BoundLiteralExpression expression) => expression.Value switch
    {
        null => "null",
        bool value => value ? "true" : "false",
        string value => SymbolDisplay.FormatLiteral(value, quote: true),
        DBNull => "global::System.DBNull.Value",
        long value => value == long.MinValue ? "global::System.Int64.MinValue" : value.ToString(CultureInfo.InvariantCulture) + "L",
        double value => double.IsNaN(value) ? "global::System.Double.NaN" : double.IsPositiveInfinity(value) ? "global::System.Double.PositiveInfinity" : double.IsNegativeInfinity(value) ? "global::System.Double.NegativeInfinity" : value.ToString("R", CultureInfo.InvariantCulture) + "D",
        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
        _ => expression.Value.ToString() ?? "null"
    };

    private string EmitConversion(BoundConversionExpression expression)
    {
        var value = Emit(expression.Expression);
        return expression.Conversion.Kind switch
        {
            ConversionKind.Identity or ConversionKind.EmptyToVariant or ConversionKind.NullToVariant or ConversionKind.NothingToObject => value,
            ConversionKind.NumericWidening => $"((double)({value}))",
            ConversionKind.ToVariant or ConversionKind.ToObject => $"((object)({value}))",
            ConversionKind.FromVariant when expression.Type.IsArray => $"({expression.Type.Name})({value})",
            ConversionKind.FromVariant => $"XPScriptRuntime.{ConversionMethod(expression.Type)}({value})",
            _ => throw new NotSupportedException($"Conversion {expression.Conversion.Kind} is not supported.")
        };
    }

    private static string ConversionMethod(Type type) => type == typeof(string) ? "CStr"
        : type == typeof(long) ? "CLng"
        : type == typeof(double) ? "CDbl"
        : type == typeof(bool) ? "CBool"
        : type == typeof(object) ? "CObj"
        : throw new NotSupportedException($"Variant conversion to {type} is not supported.");

    private static string EmitUnaryOperator(SyntaxKind kind, Type type) => kind switch
    {
        SyntaxKind.NotKeyword => type == typeof(long) ? "~" : "!",
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        _ => throw new NotSupportedException($"Unary operator {kind} is not supported.")
    };

    private static string EmitBinaryOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        SyntaxKind.StarToken => "*",
        SyntaxKind.SlashToken => "/",
        SyntaxKind.AmpersandToken => "+",
        SyntaxKind.LessToken => "<",
        SyntaxKind.LessOrEqualsToken => "<=",
        SyntaxKind.GreaterToken => ">",
        SyntaxKind.GreaterOrEqualsToken => ">=",
        SyntaxKind.AndKeyword => "&",
        SyntaxKind.OrKeyword => "|",
        SyntaxKind.EqualsToken => "==",
        SyntaxKind.LessGreaterToken => "!=",
        _ => throw new NotSupportedException($"Binary operator {kind} is not supported.")
    };
}
