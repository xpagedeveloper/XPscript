using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class TypeCoercionPreprocessor
{
    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    { "Byte", "Integer", "Long", "Single", "Double", "Currency" };

    public string Transform(string source)
    {
        source = new IncrementCompoundAssignmentPreprocessor().Transform(source);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var output = new string[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = StripComment(line).Trim();

            if (Regex.IsMatch(trimmed, @"^(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) variables.Clear();

            var dim = Regex.Match(trimmed, @"^Dim\s+([A-Za-z_]\w*)\s*(?:\([^)]*\))?\s+As\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (dim.Success) variables[dim.Groups[1].Value] = dim.Groups[2].Value;

            var assign = Regex.Match(trimmed, @"^(?:Let\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (assign.Success && variables.TryGetValue(assign.Groups[1].Value, out var targetType))
            {
                var rhs = assign.Groups[2].Value.Trim();
                var indent = line[..(line.Length - line.TrimStart().Length)];

                if (targetType.Equals("Variant", StringComparison.OrdinalIgnoreCase))
                {
                    var rewritten = RewriteVariantArithmeticExpression(rhs);
                    if (!rewritten.Equals(rhs, StringComparison.Ordinal))
                    {
                        output[i] = RewriteNullSemantics(RewriteBooleanCondition($"{indent}{assign.Groups[1].Value} = {rewritten}"));
                        continue;
                    }
                }
                else
                {
                    var plus = FindTopLevelPlus(rhs);
                    if (plus > 0 && NumericTypes.Contains(targetType))
                    {
                        var left = rhs[..plus].Trim();
                        var right = rhs[(plus + 1)..].Trim();
                        var method = targetType.ToLowerInvariant() switch
                        {
                            "byte" => "AddByte",
                            "integer" => "AddInteger",
                            "long" => "AddLong",
                            "single" => "AddSingle",
                            "double" => "AddDouble",
                            "currency" => "AddCurrency",
                            _ => throw new InvalidOperationException("Unsupported numeric coercion target: " + targetType)
                        };
                        output[i] = RewriteNullSemantics(RewriteBooleanCondition($"{indent}{assign.Groups[1].Value} = XPScriptCoercion.{method}({left}, {right})"));
                        continue;
                    }
                }
            }

            output[i] = RewriteNullSemantics(RewriteBooleanCondition(line));
        }
        return string.Join("\n", output);
    }

    private static string RewriteVariantArithmeticExpression(string value)
    {
        var expression = value.Trim();
        if (expression.Length == 0) return expression;

        if (IsFullyParenthesized(expression))
            return "(" + RewriteVariantArithmeticExpression(expression[1..^1]) + ")";

        // LotusScript precedence, low to high for recursive lowering:
        // +/-, Mod, integer division, */, unary +/- and exponentiation.
        // Operators at the same precedence are evaluated left-to-right, so finding the
        // rightmost operator makes the recursively lowered tree left-associative.
        var additive = FindRightmostTopLevelBinaryOperator(expression, '+', '-');
        if (additive >= 0)
        {
            var op = expression[additive];
            var left = RewriteVariantArithmeticExpression(expression[..additive]);
            var right = RewriteVariantArithmeticExpression(expression[(additive + 1)..]);
            var method = op == '+' ? "AddVariant" : "SubtractVariant";
            return $"XPScriptCoercion.{method}({left}, {right})";
        }

        var modulo = FindRightmostTopLevelWordOperator(expression, "Mod");
        if (modulo >= 0)
        {
            var left = RewriteVariantArithmeticExpression(expression[..modulo]);
            var right = RewriteVariantArithmeticExpression(expression[(modulo + 3)..]);
            return $"XPScriptCoercion.ModVariant({left}, {right})";
        }

        var integerDivision = FindRightmostTopLevelBinaryOperator(expression, '\\');
        if (integerDivision >= 0)
        {
            var left = RewriteVariantArithmeticExpression(expression[..integerDivision]);
            var right = RewriteVariantArithmeticExpression(expression[(integerDivision + 1)..]);
            return $"XPScriptCoercion.IntegerDivideVariant({left}, {right})";
        }

        var multiplicative = FindRightmostTopLevelBinaryOperator(expression, '*', '/');
        if (multiplicative >= 0)
        {
            var op = expression[multiplicative];
            var left = RewriteVariantArithmeticExpression(expression[..multiplicative]);
            var right = RewriteVariantArithmeticExpression(expression[(multiplicative + 1)..]);
            var method = op == '*' ? "MultiplyVariant" : "DivideVariant";
            return $"XPScriptCoercion.{method}({left}, {right})";
        }

        if (expression.Length > 1 && expression[0] is '+' or '-')
        {
            var operand = RewriteVariantArithmeticExpression(expression[1..]);
            var method = expression[0] == '+' ? "UnaryPlusVariant" : "NegateVariant";
            return $"XPScriptCoercion.{method}({operand})";
        }

        var exponentiation = FindRightmostTopLevelBinaryOperator(expression, '^');
        if (exponentiation >= 0)
        {
            var left = RewriteVariantArithmeticExpression(expression[..exponentiation]);
            var right = RewriteVariantArithmeticExpression(expression[(exponentiation + 1)..]);
            return $"XPScriptCoercion.PowerVariant({left}, {right})";
        }

        return expression;
    }

    private static int FindRightmostTopLevelBinaryOperator(string value, params char[] operators)
    {
        var inString = false;
        var depth = 0;
        var candidate = -1;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth != 0 || !operators.Contains(c)) continue;
            if ((c == '+' || c == '-') && IsUnarySign(value, i)) continue;
            candidate = i;
        }
        return candidate;
    }

    private static int FindRightmostTopLevelWordOperator(string value, string operation)
    {
        var inString = false;
        var depth = 0;
        var candidate = -1;
        for (var i = 0; i <= value.Length - operation.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth != 0 || !value.AsSpan(i, operation.Length).Equals(operation, StringComparison.OrdinalIgnoreCase)) continue;

            var beforeOk = i == 0 || !(char.IsLetterOrDigit(value[i - 1]) || value[i - 1] == '_');
            var afterIndex = i + operation.Length;
            var afterOk = afterIndex == value.Length || !(char.IsLetterOrDigit(value[afterIndex]) || value[afterIndex] == '_');
            if (beforeOk && afterOk)
            {
                candidate = i;
                i += operation.Length - 1;
            }
        }
        return candidate;
    }

    private static bool IsUnarySign(string value, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(value[i])) continue;
            return value[i] is '(' or ',' or '+' or '-' or '*' or '/' or '\\' or '^' or '=' or '<' or '>';
        }
        return true;
    }

    private static bool IsFullyParenthesized(string value)
    {
        if (value.Length < 2 || value[0] != '(' || value[^1] != ')') return false;
        var inString = false;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && i != value.Length - 1) return false;
            }
        }
        return depth == 0;
    }

    private static string RewriteBooleanCondition(string line)
    {
        var patterns = new[]
        {
            @"^(?<prefix>\s*If\s+)(?<condition>.+?)(?<suffix>\s+Then\b.*)$",
            @"^(?<prefix>\s*ElseIf\s+)(?<condition>.+?)(?<suffix>\s+Then\b.*)$",
            @"^(?<prefix>\s*While\s+)(?<condition>.+?)(?<suffix>\s*)$",
            @"^(?<prefix>\s*Do\s+While\s+)(?<condition>.+?)(?<suffix>\s*)$",
            @"^(?<prefix>\s*Do\s+Until\s+)(?<condition>.+?)(?<suffix>\s*)$",
            @"^(?<prefix>\s*Loop\s+While\s+)(?<condition>.+?)(?<suffix>\s*)$",
            @"^(?<prefix>\s*Loop\s+Until\s+)(?<condition>.+?)(?<suffix>\s*)$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var condition = match.Groups["condition"].Value.Trim();
            if (condition.StartsWith("XPScriptNullRuntime.ConditionValue(", StringComparison.Ordinal)) return line;

            if (ContainsComparisonOperator(condition)) return line;

            return match.Groups["prefix"].Value + "XPScriptNullRuntime.ConditionValue(" + condition + ")" + match.Groups["suffix"].Value;
        }

        return line;
    }

    private static bool ContainsComparisonOperator(string value)
    {
        var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c is '=' or '<' or '>') return true;
        }
        return false;
    }

    private static string RewriteNullSemantics(string line)
    {
        var output = new StringBuilder(line.Length + 32);
        var code = new StringBuilder();
        var inString = false;

        void FlushCode()
        {
            if (code.Length == 0) return;
            var text = code.ToString();
            text = Regex.Replace(text, @"(?<![\w.])IsNull\s*\(", "XPScriptNullRuntime.IsNull(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])IsEmpty\s*\(", "XPScriptNullRuntime.IsEmpty(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])IsObject\s*\(", "XPScriptNullRuntime.IsObject(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])IsScalar\s*\(", "XPScriptNullRuntime.IsScalar(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])DataType\s*\(", "XPScriptNullRuntime.DataType(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])TypeName\s*\(", "XPScriptNullRuntime.TypeName(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])Null\b", "XPScriptNullRuntime.NullValue", RegexOptions.IgnoreCase);
            output.Append(text);
            code.Clear();
        }

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (!inString && c == '\'')
            {
                FlushCode();
                output.Append(line.AsSpan(i));
                return output.ToString();
            }

            if (c != '"')
            {
                if (inString) output.Append(c); else code.Append(c);
                continue;
            }

            if (!inString)
            {
                FlushCode();
                inString = true;
                output.Append(c);
                continue;
            }

            output.Append(c);
            if (i + 1 < line.Length && line[i + 1] == '"')
            {
                output.Append(line[++i]);
                continue;
            }
            inString = false;
        }

        FlushCode();
        return output.ToString();
    }

    private static int FindTopLevelPlus(string value)
    {
        var inString = false; var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString; continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == '+' && depth == 0) return i;
        }
        return -1;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}