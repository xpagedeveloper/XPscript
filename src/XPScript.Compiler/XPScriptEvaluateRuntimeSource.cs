using System.Text;

namespace XPScript.Compiler;

internal static class XPScriptEvaluateRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateRuntime
{
    public static object? Evaluate(object? sourceText) => Evaluate(sourceText, null);

    public static object? Evaluate(object? sourceText, object? callvar)
    {
        var source = XPScriptRuntime.CStr(sourceText);
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            return new Evaluator(source, Snapshot(callvar)).Run();
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Evaluate failed: " + ex.Message);
        }
    }

    private static object? Snapshot(object? value)
    {
        if (value is null) return null;
        if (value is LSArray array)
        {
            var lower = array.LowerBounds.ToArray();
            var upper = array.UpperBounds.ToArray();
            var copy = new LSArray(array.ElementType, true, lower, upper);
            if (!array.IsAllocated) return new LSArray(array.ElementType, true);
            var current = new int[array.Rank];
            CopyDimension(0);
            return copy;

            void CopyDimension(int dimension)
            {
                for (var i = lower[dimension]; i <= upper[dimension]; i++)
                {
                    current[dimension] = i;
                    if (dimension + 1 < current.Length) CopyDimension(dimension + 1);
                    else copy.Set(Snapshot(array.Get(current.Cast<object?>().ToArray())), current.Cast<object?>().ToArray());
                }
            }
        }
        if (value is Array clrArray) return clrArray.Clone();
        return value;
    }

    private sealed class Evaluator
    {
        private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Token> _tokens;
        private readonly object? _callvar;
        private int _position;

        public Evaluator(string source, object? callvar)
        {
            _tokens = Tokenize(source);
            _callvar = callvar;
        }

        public object? Run()
        {
            while (!Check(TokenKind.End))
            {
                SkipSeparators();
                if (Check(TokenKind.End)) break;

                if (MatchKeyword("Dim"))
                {
                    ParseDim();
                    SkipStatementTail();
                    continue;
                }

                if (MatchKeyword("Return"))
                    return Snapshot(ParseExpression());

                if (Check(TokenKind.Identifier) && Peek(1).Kind == TokenKind.Equal)
                {
                    var name = Advance().Text;
                    Advance();
                    if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                        throw Error("callvar is read-only inside Evaluate.");
                    if (!_variables.ContainsKey(name))
                        throw Error("Assignment requires a local variable declared with Dim: " + name);
                    _variables[name] = ParseExpression();
                    SkipStatementTail();
                    continue;
                }

                _ = ParseExpression();
                SkipStatementTail();
            }
            return null;
        }

        private void ParseDim()
        {
            var name = Consume(TokenKind.Identifier, "Expected variable name after Dim.").Text;
            if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                throw Error("callvar is reserved and cannot be declared inside Evaluate.");
            object? initial = null;
            if (MatchKeyword("As"))
            {
                var type = Consume(TokenKind.Identifier, "Expected type name after As.").Text;
                initial = DefaultValue(type);
            }
            if (Match(TokenKind.Equal)) initial = ParseExpression();
            if (_variables.ContainsKey(name)) throw Error("Variable already declared in Evaluate scope: " + name);
            _variables[name] = initial;
        }

        private static object? DefaultValue(string type) => type.ToLowerInvariant() switch
        {
            "string" => "",
            "boolean" => false,
            "byte" => (byte)0,
            "integer" => 0,
            "long" => 0L,
            "single" => 0f,
            "double" => 0d,
            "currency" => 0m,
            "date" => DateTime.MinValue,
            "variant" or "object" => null,
            _ => throw new XPScriptRuntimeException(5, "Unsupported Evaluate local type: " + type)
        };

        private object? ParseExpression() => ParseImp();
        private object? ParseImp() { var value = ParseEqv(); while (MatchKeyword("Imp")) value = LSOperatorArrayRuntime.Imp(value, ParseEqv()); return value; }
        private object? ParseEqv() { var value = ParseXor(); while (MatchKeyword("Eqv")) value = LSOperatorArrayRuntime.Eqv(value, ParseXor()); return value; }
        private object? ParseXor() { var value = ParseOr(); while (MatchKeyword("Xor")) value = LSOperatorArrayRuntime.Xor(value, ParseOr()); return value; }
        private object? ParseOr() { var value = ParseAnd(); while (MatchKeyword("Or")) value = LSOperatorArrayRuntime.LogicalOr(value, ParseAnd()); return value; }
        private object? ParseAnd() { var value = ParseComparison(); while (MatchKeyword("And")) value = LSOperatorArrayRuntime.LogicalAnd(value, ParseComparison()); return value; }

        private object? ParseComparison()
        {
            var left = ParseConcat();
            while (true)
            {
                var kind = Current.Kind;
                if (kind is not (TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual)) return left;
                Advance();
                left = Compare(left, ParseConcat(), kind);
            }
        }

        private object? ParseConcat() { var value = ParseAdditive(); while (Match(TokenKind.Ampersand)) value = XPScriptRuntime.CStr(value) + XPScriptRuntime.CStr(ParseAdditive()); return value; }
        private object? ParseAdditive() { var value = ParseMultiplicative(); while (true) { if (Match(TokenKind.Plus)) value = Add(value, ParseMultiplicative()); else if (Match(TokenKind.Minus)) value = Numeric(value) - Numeric(ParseMultiplicative()); else return value; } }
        private object? ParseMultiplicative() { var value = ParsePower(); while (true) { if (Match(TokenKind.Star)) value = Numeric(value) * Numeric(ParsePower()); else if (Match(TokenKind.Slash)) value = Numeric(value) / Numeric(ParsePower()); else if (Match(TokenKind.Backslash)) value = LSOperatorArrayRuntime.IntDiv(value, ParsePower()); else if (MatchKeyword("Mod")) value = XPScriptRuntime.CLng(value) % XPScriptRuntime.CLng(ParsePower()); else return value; } }
        private object? ParsePower() { var value = ParseUnary(); if (Match(TokenKind.Caret)) value = LSOperatorArrayRuntime.Pow(value, ParsePower()); return value; }
        private object? ParseUnary() { if (MatchKeyword("Not")) return LSOperatorArrayRuntime.LogicalNot(ParseUnary()); if (Match(TokenKind.Plus)) return Numeric(ParseUnary()); if (Match(TokenKind.Minus)) return -Numeric(ParseUnary()); return ParsePrimary(); }

        private object? ParsePrimary()
        {
            if (Match(TokenKind.Number)) return Previous.NumberValue;
            if (Match(TokenKind.String)) return Previous.Text;
            if (Match(TokenKind.DateLiteral)) return XPScriptRuntime.CDate(Previous.Text);
            if (MatchKeyword("True")) return true;
            if (MatchKeyword("False")) return false;
            if (MatchKeyword("Null")) return null;
            if (MatchKeyword("Nothing")) return null;
            if (Match(TokenKind.LeftParen)) { var value = ParseExpression(); Consume(TokenKind.RightParen, "Expected ')' after expression."); return value; }

            if (Match(TokenKind.Identifier))
            {
                var name = Previous.Text;
                if (Match(TokenKind.LeftParen))
                {
                    var args = new List<object?>();
                    if (!Check(TokenKind.RightParen)) do { args.Add(ParseExpression()); } while (Match(TokenKind.Comma));
                    Consume(TokenKind.RightParen, "Expected ')' after function arguments.");
                    if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase)) return ReadCallvar(args);
                    return InvokeFunction(name, args);
                }
                if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase)) return _callvar;
                if (_variables.TryGetValue(name, out var value)) return value;
                throw Error("Unknown identifier in Evaluate scope: " + name);
            }
            throw Error("Expected an XPScript expression.");
        }

        private object? ReadCallvar(IReadOnlyList<object?> args)
        {
            if (_callvar is LSArray array) return array.Get(args.ToArray());
            if (_callvar is Array clrArray)
            {
                if (args.Count != 1) throw Error("CLR array callvar requires one index.");
                return clrArray.GetValue(XPScriptRuntime.CInt(args[0]));
            }
            throw Error("callvar is not an indexed value.");
        }

        private static object? InvokeFunction(string name, IReadOnlyList<object?> args)
        {
            object? Arg(int index) => index < args.Count ? args[index] : null;
            var function = name.ToLowerInvariant();
            return function switch
            {
                "cstr" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)),
                "cint" when args.Count == 1 => XPScriptRuntime.CInt(Arg(0)),
                "clng" when args.Count == 1 => XPScriptRuntime.CLng(Arg(0)),
                "cdbl" when args.Count == 1 => XPScriptRuntime.CDbl(Arg(0)),
                "csng" when args.Count == 1 => XPScriptRuntime.CSng(Arg(0)),
                "ccur" when args.Count == 1 => XPScriptRuntime.CCur(Arg(0)),
                "cbyte" when args.Count == 1 => XPScriptRuntime.CByte(Arg(0)),
                "cbool" when args.Count == 1 => XPScriptRuntime.CBool(Arg(0)),
                "cdate" or "cdat" when args.Count == 1 => XPScriptRuntime.CDate(Arg(0)),
                "cvar" when args.Count == 1 => XPScriptRuntime.CVar(Arg(0)),

                "typename" when args.Count == 1 => Arg(0) is LSArray ? "ARRAY" : XPScriptRuntime.TypeName(Arg(0)),
                "datatype" when args.Count == 1 => Arg(0) is LSArray ? 8192 : XPScriptRuntime.DataType(Arg(0)),
                "isarray" when args.Count == 1 => Arg(0) is LSArray or Array,
                "isdate" when args.Count == 1 => XPScriptRuntime.IsDate(Arg(0)),
                "isempty" when args.Count == 1 => XPScriptRuntime.IsEmpty(Arg(0)),
                "isnull" when args.Count == 1 => XPScriptRuntime.IsNull(Arg(0)),
                "isobject" when args.Count == 1 => XPScriptRuntime.IsObject(Arg(0)),
                "isscalar" when args.Count == 1 => Arg(0) is not LSArray && XPScriptRuntime.IsScalar(Arg(0)),
                "isnumeric" when args.Count == 1 => XPScriptRuntime.IsNumeric(Arg(0)),
                "lbound" when args.Count is 1 or 2 => LSArrayRuntime.LBound(Arg(0), args.Count == 2 ? XPScriptRuntime.CInt(Arg(1)) : 1),
                "ubound" when args.Count is 1 or 2 => LSArrayRuntime.UBound(Arg(0), args.Count == 2 ? XPScriptRuntime.CInt(Arg(1)) : 1),

                "len" when args.Count == 1 => XPScriptRuntime.Len(Arg(0)),
                "left" when args.Count == 2 => XPScriptRuntime.Left(Arg(0), XPScriptRuntime.CInt(Arg(1))),
                "right" when args.Count == 2 => XPScriptRuntime.Right(Arg(0), XPScriptRuntime.CInt(Arg(1))),
                "mid" when args.Count == 2 => XPScriptRuntime.Mid(Arg(0), XPScriptRuntime.CInt(Arg(1))),
                "mid" when args.Count == 3 => XPScriptRuntime.Mid(Arg(0), XPScriptRuntime.CInt(Arg(1)), XPScriptRuntime.CInt(Arg(2))),
                "lcase" when args.Count == 1 => XPScriptRuntime.LCase(Arg(0)),
                "ucase" when args.Count == 1 => XPScriptRuntime.UCase(Arg(0)),
                "trim" when args.Count == 1 => XPScriptRuntime.Trim(Arg(0)),
                "ltrim" when args.Count == 1 => XPScriptRuntime.LTrim(Arg(0)),
                "rtrim" when args.Count == 1 => XPScriptRuntime.RTrim(Arg(0)),
                "fulltrim" when args.Count == 1 => XPScriptRuntime.FullTrim(Arg(0)),
                "strreverse" when args.Count == 1 => XPScriptRuntime.StrReverse(Arg(0)),
                "instr" when args.Count == 2 => XPScriptRuntime.Instr(Arg(0), Arg(1)),
                "instr" when args.Count == 3 => XPScriptRuntime.Instr(XPScriptRuntime.CInt(Arg(0)), Arg(1), Arg(2)),
                "instr" when args.Count == 4 => XPScriptRuntime.Instr(XPScriptRuntime.CInt(Arg(0)), Arg(1), Arg(2), XPScriptRuntime.CInt(Arg(3))),
                "replace" when args.Count == 3 => XPScriptRuntime.Replace(Arg(0), Arg(1), Arg(2)),
                "replace" when args.Count == 4 => XPScriptRuntime.Replace(Arg(0), Arg(1), Arg(2), XPScriptRuntime.CInt(Arg(3))),
                "replace" when args.Count == 5 => XPScriptRuntime.Replace(Arg(0), Arg(1), Arg(2), XPScriptRuntime.CInt(Arg(3)), XPScriptRuntime.CInt(Arg(4))),
                "replace" when args.Count == 6 => XPScriptRuntime.Replace(Arg(0), Arg(1), Arg(2), XPScriptRuntime.CInt(Arg(3)), XPScriptRuntime.CInt(Arg(4)), XPScriptRuntime.CInt(Arg(5))),
                "space" when args.Count == 1 => XPScriptRuntime.Space(XPScriptRuntime.CInt(Arg(0))),
                "string" when args.Count == 2 => XPScriptRuntime.String(XPScriptRuntime.CInt(Arg(0)), Arg(1)),
                "chr" when args.Count == 1 => XPScriptRuntime.Chr(XPScriptRuntime.CInt(Arg(0))),
                "asc" when args.Count == 1 => XPScriptRuntime.Asc(Arg(0)),

                "abs" when args.Count == 1 => XPScriptRuntime.Abs(XPScriptRuntime.CDbl(Arg(0))),
                "int" when args.Count == 1 => XPScriptRuntime.Int(XPScriptRuntime.CDbl(Arg(0))),
                "fix" when args.Count == 1 => XPScriptRuntime.Fix(XPScriptRuntime.CDbl(Arg(0))),
                "round" when args.Count == 1 => XPScriptRuntime.Round(XPScriptRuntime.CDbl(Arg(0))),
                "round" when args.Count == 2 => XPScriptRuntime.Round(XPScriptRuntime.CDbl(Arg(0)), XPScriptRuntime.CInt(Arg(1))),
                "sqr" when args.Count == 1 => XPScriptRuntime.Sqr(XPScriptRuntime.CDbl(Arg(0))),
                "sgn" when args.Count == 1 => XPScriptRuntime.Sgn(XPScriptRuntime.CDbl(Arg(0))),
                "sin" when args.Count == 1 => XPScriptRuntime.Sin(XPScriptRuntime.CDbl(Arg(0))),
                "cos" when args.Count == 1 => XPScriptRuntime.Cos(XPScriptRuntime.CDbl(Arg(0))),
                "tan" when args.Count == 1 => XPScriptRuntime.Tan(XPScriptRuntime.CDbl(Arg(0))),
                "atn" when args.Count == 1 => XPScriptRuntime.ATn(XPScriptRuntime.CDbl(Arg(0))),
                "atn2" when args.Count == 2 => XPScriptRuntime.ATn2(XPScriptRuntime.CDbl(Arg(0)), XPScriptRuntime.CDbl(Arg(1))),
                "asin" when args.Count == 1 => XPScriptRuntime.ASin(XPScriptRuntime.CDbl(Arg(0))),
                "acos" when args.Count == 1 => XPScriptRuntime.ACos(XPScriptRuntime.CDbl(Arg(0))),
                "exp" when args.Count == 1 => XPScriptRuntime.Exp(XPScriptRuntime.CDbl(Arg(0))),
                "log" when args.Count == 1 => XPScriptRuntime.Log(XPScriptRuntime.CDbl(Arg(0))),
                "fraction" when args.Count == 1 => XPScriptRuntime.Fraction(XPScriptRuntime.CDbl(Arg(0))),
                "val" when args.Count == 1 => XPScriptRuntime.Val(Arg(0)),
                "str" when args.Count == 1 => XPScriptRuntime.Str(Arg(0)),
                "bin" when args.Count == 1 => XPScriptRuntime.Bin(XPScriptRuntime.CLng(Arg(0))),
                "hex" when args.Count == 1 => XPScriptRuntime.Hex(XPScriptRuntime.CLng(Arg(0))),
                "oct" when args.Count == 1 => XPScriptRuntime.Oct(XPScriptRuntime.CLng(Arg(0))),

                "year" when args.Count == 1 => XPScriptRuntime.Year(XPScriptRuntime.CDate(Arg(0))),
                "month" when args.Count == 1 => XPScriptRuntime.Month(XPScriptRuntime.CDate(Arg(0))),
                "day" when args.Count == 1 => XPScriptRuntime.Day(XPScriptRuntime.CDate(Arg(0))),
                "hour" when args.Count == 1 => XPScriptRuntime.Hour(XPScriptRuntime.CDate(Arg(0))),
                "minute" when args.Count == 1 => XPScriptRuntime.Minute(XPScriptRuntime.CDate(Arg(0))),
                "second" when args.Count == 1 => XPScriptRuntime.Second(XPScriptRuntime.CDate(Arg(0))),
                "datevalue" when args.Count == 1 => XPScriptRuntime.DateValue(Arg(0)),
                "timevalue" when args.Count == 1 => XPScriptRuntime.TimeValue(Arg(0)),
                "datenumber" when args.Count == 3 => XPScriptRuntime.DateNumber(XPScriptRuntime.CInt(Arg(0)), XPScriptRuntime.CInt(Arg(1)), XPScriptRuntime.CInt(Arg(2))),
                "timenumber" when args.Count == 3 => XPScriptRuntime.TimeNumber(XPScriptRuntime.CInt(Arg(0)), XPScriptRuntime.CInt(Arg(1)), XPScriptRuntime.CInt(Arg(2))),
                "dateadd" when args.Count == 3 => XPScriptRuntime.DateAdd(XPScriptRuntime.CStr(Arg(0)), XPScriptRuntime.CDbl(Arg(1)), XPScriptRuntime.CDate(Arg(2))),
                "datediff" when args.Count == 3 => XPScriptRuntime.DateDiff(XPScriptRuntime.CStr(Arg(0)), XPScriptRuntime.CDate(Arg(1)), XPScriptRuntime.CDate(Arg(2))),
                "datepart" when args.Count == 2 => XPScriptRuntime.DatePart(XPScriptRuntime.CStr(Arg(0)), XPScriptRuntime.CDate(Arg(1))),

                _ => throw new XPScriptRuntimeException(5, "Function is not available inside Evaluate: " + name)
            };
        }

        private static object? Add(object? left, object? right)
        {
            if (left is null || right is null) return null;
            if (left is string leftText) return leftText + XPScriptRuntime.CStr(right);
            if (right is string rightText)
            {
                if (double.TryParse(rightText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var parsed)) return Numeric(left) + parsed;
                return XPScriptRuntime.CStr(left) + rightText;
            }
            return Numeric(left) + Numeric(right);
        }

        private static double Numeric(object? value) => XPScriptRuntime.CDbl(value);
        private static bool Compare(object? left, object? right, TokenKind op)
        {
            int comparison;
            if (left is DateTime || right is DateTime) comparison = DateTime.Compare(XPScriptRuntime.CDate(left), XPScriptRuntime.CDate(right));
            else if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right)) comparison = XPScriptRuntime.CDbl(left).CompareTo(XPScriptRuntime.CDbl(right));
            else comparison = string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
            return op switch { TokenKind.Equal => comparison == 0, TokenKind.NotEqual => comparison != 0, TokenKind.Less => comparison < 0, TokenKind.LessEqual => comparison <= 0, TokenKind.Greater => comparison > 0, TokenKind.GreaterEqual => comparison >= 0, _ => false };
        }

        private void SkipStatementTail() { if (Check(TokenKind.NewLine) || Check(TokenKind.Colon)) SkipSeparators(); else if (!Check(TokenKind.End)) throw Error("Expected end of statement."); }
        private void SkipSeparators() { while (Match(TokenKind.NewLine) || Match(TokenKind.Colon)) { } }
        private bool Match(TokenKind kind) { if (!Check(kind)) return false; Advance(); return true; }
        private bool MatchKeyword(string keyword) { if (!Check(TokenKind.Identifier) || !Current.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return false; Advance(); return true; }
        private Token Consume(TokenKind kind, string message) { if (Check(kind)) return Advance(); throw Error(message); }
        private bool Check(TokenKind kind) => Current.Kind == kind;
        private Token Advance() { if (_position < _tokens.Count - 1) _position++; return Previous; }
        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
        private Token Previous => _tokens[Math.Max(0, _position - 1)];
        private Token Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];
        private XPScriptRuntimeException Error(string message) => new(5, message + " (Evaluate token " + (_position + 1) + ")");
    }

    private enum TokenKind { End, NewLine, Colon, Identifier, Number, String, DateLiteral, LeftParen, RightParen, Comma, Plus, Minus, Star, Slash, Backslash, Caret, Ampersand, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual }
    private readonly record struct Token(TokenKind Kind, string Text, object? NumberValue = null);

    private static List<Token> Tokenize(string source)
    {
        var result = new List<Token>();
        for (var i = 0; i < source.Length;)
        {
            var c = source[i];
            if (c is ' ' or '\t' or '\r') { i++; continue; }
            if (c == '\n') { result.Add(new(TokenKind.NewLine, "\n")); i++; continue; }
            if (c == ':') { result.Add(new(TokenKind.Colon, ":")); i++; continue; }
            if (c == '(') { result.Add(new(TokenKind.LeftParen, "(")); i++; continue; }
            if (c == ')') { result.Add(new(TokenKind.RightParen, ")")); i++; continue; }
            if (c == ',') { result.Add(new(TokenKind.Comma, ",")); i++; continue; }
            if (c == '+') { result.Add(new(TokenKind.Plus, "+")); i++; continue; }
            if (c == '-') { result.Add(new(TokenKind.Minus, "-")); i++; continue; }
            if (c == '*') { result.Add(new(TokenKind.Star, "*")); i++; continue; }
            if (c == '/') { result.Add(new(TokenKind.Slash, "/")); i++; continue; }
            if (c == '\\') { result.Add(new(TokenKind.Backslash, "\\")); i++; continue; }
            if (c == '^') { result.Add(new(TokenKind.Caret, "^")); i++; continue; }
            if (c == '&') { result.Add(new(TokenKind.Ampersand, "&")); i++; continue; }
            if (c == '=') { result.Add(new(TokenKind.Equal, "=")); i++; continue; }
            if (c == '<') { if (i + 1 < source.Length && source[i + 1] == '>') { result.Add(new(TokenKind.NotEqual, "<>")); i += 2; continue; } if (i + 1 < source.Length && source[i + 1] == '=') { result.Add(new(TokenKind.LessEqual, "<=")); i += 2; continue; } result.Add(new(TokenKind.Less, "<")); i++; continue; }
            if (c == '>') { if (i + 1 < source.Length && source[i + 1] == '=') { result.Add(new(TokenKind.GreaterEqual, ">=")); i += 2; continue; } result.Add(new(TokenKind.Greater, ">")); i++; continue; }
            if (c == '"') { var sb = new StringBuilder(); i++; var closed = false; while (i < source.Length) { if (source[i] == '"') { if (i + 1 < source.Length && source[i + 1] == '"') { sb.Append('"'); i += 2; continue; } i++; closed = true; break; } sb.Append(source[i++]); } if (!closed) throw new XPScriptRuntimeException(5, "Unterminated string literal in Evaluate."); result.Add(new(TokenKind.String, sb.ToString())); continue; }
            if (c == '#') { var end = source.IndexOf('#', i + 1); if (end < 0) throw new XPScriptRuntimeException(5, "Unterminated date literal in Evaluate."); result.Add(new(TokenKind.DateLiteral, source[(i + 1)..end])); i = end + 1; continue; }
            if (char.IsDigit(c) || (c == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1]))) { var start = i++; while (i < source.Length && (char.IsDigit(source[i]) || source[i] is '.' or 'e' or 'E' or '+' or '-')) { if ((source[i] == '+' || source[i] == '-') && source[i - 1] is not ('e' or 'E')) break; i++; } var text = source[start..i]; if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) throw new XPScriptRuntimeException(5, "Invalid numeric literal in Evaluate: " + text); object numeric = !text.Contains('.') && !text.Contains('e', StringComparison.OrdinalIgnoreCase) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : number; result.Add(new(TokenKind.Number, text, numeric)); continue; }
            if (char.IsLetter(c) || c == '_') { var start = i++; while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++; result.Add(new(TokenKind.Identifier, source[start..i])); continue; }
            if (c == '\'') { while (i < source.Length && source[i] != '\n') i++; continue; }
            throw new XPScriptRuntimeException(5, "Unsupported character in Evaluate source: " + c);
        }
        result.Add(new(TokenKind.End, ""));
        return result;
    }
}
""";
}
