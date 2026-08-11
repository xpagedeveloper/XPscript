using System.Text;

namespace XPScript.Compiler;

internal static class XPScriptEvaluateRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateRuntime
{
    public static object? Evaluate(object? sourceText) => Evaluate(sourceText, null, false);

    public static object? Evaluate(object? sourceText, object? callvar) => Evaluate(sourceText, callvar, true);

    private static object? Evaluate(object? sourceText, object? callvar, bool hasCallVar)
    {
        var source = XPScriptRuntime.CStr(sourceText);
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            var input = hasCallVar ? RestrictedCallVar.Create(callvar) : null;
            return new Evaluator(source, input).Run();
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Evaluate failed: " + ex.Message);
        }
    }

    private sealed class RestrictedCallVar
    {
        private readonly object? _snapshot;

        private RestrictedCallVar(object? snapshot) => _snapshot = snapshot;

        public static RestrictedCallVar Create(object? value) => new(CloneValue(value));

        public object? Read() => CloneValue(_snapshot);

        public object? Read(object? key)
        {
            if (_snapshot is LSArray array)
                return CloneValue(array.Get(key));

            if (_snapshot is Array clrArray)
            {
                var index = XPScriptRuntime.CInt(key);
                if (index < 0 || index >= clrArray.Length)
                    throw new IndexOutOfRangeException("callvar array index out of range: " + index);
                return CloneValue(clrArray.GetValue(index));
            }

            if (_snapshot is ILSList)
            {
                var property = _snapshot.GetType().GetProperty("Item");
                if (property is null)
                    throw new XPScriptRuntimeException(5, "callvar list does not expose an indexed value accessor.");
                try { return CloneValue(property.GetValue(_snapshot, [key])); }
                catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
            }

            throw new XPScriptRuntimeException(5, "callvar(index) requires an Array or List input.");
        }

        private static object? CloneValue(object? value)
        {
            if (value is null || value is string || value.GetType().IsValueType)
                return value;

            if (value is LSArray array)
                return CloneXpsArray(array);

            if (value is Array clrArray)
                return clrArray.Clone();

            if (value is ILSList)
                return CloneXpsList(value);

            // Objects are intentionally passed as opaque references only when the caller explicitly supplies one.
            // Evaluate cannot discover caller locals/globals and cannot assign to callvar itself.
            return value;
        }

        private static LSArray CloneXpsArray(LSArray source)
        {
            if (!source.IsAllocated)
                return new LSArray(source.ElementType, source.IsDynamic);

            var lower = source.LowerBounds.ToArray();
            var upper = source.UpperBounds.ToArray();
            var copy = new LSArray(source.ElementType, source.IsDynamic, lower, upper);
            var indices = new int[source.Rank];
            CopyDimension(0);
            return copy;

            void CopyDimension(int dimension)
            {
                for (var i = lower[dimension]; i <= upper[dimension]; i++)
                {
                    indices[dimension] = i;
                    if (dimension + 1 < indices.Length)
                    {
                        CopyDimension(dimension + 1);
                        continue;
                    }
                    var boxed = indices.Cast<object?>().ToArray();
                    copy.Set(CloneValue(source.Get(boxed)), boxed);
                }
            }
        }

        private static object CloneXpsList(object source)
        {
            var type = source.GetType();
            var copy = Activator.CreateInstance(type)
                ?? throw new XPScriptRuntimeException(5, "Unable to clone callvar List.");
            var aliases = type.GetMethod("Aliases")?.Invoke(source, null) as System.Collections.IEnumerable
                ?? throw new XPScriptRuntimeException(5, "Unable to enumerate callvar List.");
            var item = type.GetProperty("Item")
                ?? throw new XPScriptRuntimeException(5, "Unable to access callvar List values.");

            foreach (var alias in aliases)
            {
                if (alias is null) continue;
                var aliasType = alias.GetType();
                var tag = aliasType.GetProperty("Tag")?.GetValue(alias);
                var value = aliasType.GetProperty("Value")?.GetValue(alias);
                item.SetValue(copy, CloneValue(value), [tag]);
            }
            return copy;
        }
    }

    private sealed class Evaluator
    {
        private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Token> _tokens;
        private readonly RestrictedCallVar? _callvar;
        private int _position;

        public Evaluator(string source, RestrictedCallVar? callvar)
        {
            _tokens = Tokenize(source);
            _callvar = callvar;
        }

        public object? Run()
        {
            object? last = null;
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
                    return RestrictedReturn(ParseExpression());

                if (Check(TokenKind.Identifier) && Peek(1).Kind == TokenKind.Equal)
                {
                    var name = Advance().Text;
                    Advance();
                    if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                        throw Error("callvar is read-only inside Evaluate.");
                    if (!_variables.ContainsKey(name))
                        throw Error("Assignment requires a local variable declared with Dim: " + name);
                    last = ParseExpression();
                    _variables[name] = last;
                    SkipStatementTail();
                    continue;
                }

                last = ParseExpression();
                SkipStatementTail();
            }
            return RestrictedReturn(last);
        }

        private static object? RestrictedReturn(object? value) => RestrictedCallVar.Create(value).Read();

        private void ParseDim()
        {
            var name = Consume(TokenKind.Identifier, "Expected variable name after Dim.").Text;
            if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                throw Error("callvar is reserved and cannot be redeclared inside Evaluate.");
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

        private object? ParseImp()
        {
            var value = ParseEqv();
            while (MatchKeyword("Imp")) value = LSOperatorArrayRuntime.Imp(value, ParseEqv());
            return value;
        }

        private object? ParseEqv()
        {
            var value = ParseXor();
            while (MatchKeyword("Eqv")) value = LSOperatorArrayRuntime.Eqv(value, ParseXor());
            return value;
        }

        private object? ParseXor()
        {
            var value = ParseOr();
            while (MatchKeyword("Xor")) value = LSOperatorArrayRuntime.Xor(value, ParseOr());
            return value;
        }

        private object? ParseOr()
        {
            var value = ParseAnd();
            while (MatchKeyword("Or")) value = LSOperatorArrayRuntime.LogicalOr(value, ParseAnd());
            return value;
        }

        private object? ParseAnd()
        {
            var value = ParseComparison();
            while (MatchKeyword("And")) value = LSOperatorArrayRuntime.LogicalAnd(value, ParseComparison());
            return value;
        }

        private object? ParseComparison()
        {
            var left = ParseConcat();
            while (true)
            {
                var kind = Current.Kind;
                if (kind is not (TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual))
                    return left;
                Advance();
                var right = ParseConcat();
                left = Compare(left, right, kind);
            }
        }

        private object? ParseConcat()
        {
            var value = ParseAdditive();
            while (Match(TokenKind.Ampersand)) value = XPScriptRuntime.CStr(value) + XPScriptRuntime.CStr(ParseAdditive());
            return value;
        }

        private object? ParseAdditive()
        {
            var value = ParseMultiplicative();
            while (true)
            {
                if (Match(TokenKind.Plus)) value = Add(value, ParseMultiplicative());
                else if (Match(TokenKind.Minus)) value = Numeric(value) - Numeric(ParseMultiplicative());
                else return value;
            }
        }

        private object? ParseMultiplicative()
        {
            var value = ParsePower();
            while (true)
            {
                if (Match(TokenKind.Star)) value = Numeric(value) * Numeric(ParsePower());
                else if (Match(TokenKind.Slash)) value = Numeric(value) / Numeric(ParsePower());
                else if (Match(TokenKind.Backslash)) value = LSOperatorArrayRuntime.IntDiv(value, ParsePower());
                else if (MatchKeyword("Mod")) value = XPScriptRuntime.CLng(value) % XPScriptRuntime.CLng(ParsePower());
                else return value;
            }
        }

        private object? ParsePower()
        {
            var value = ParseUnary();
            if (Match(TokenKind.Caret)) value = LSOperatorArrayRuntime.Pow(value, ParsePower());
            return value;
        }

        private object? ParseUnary()
        {
            if (MatchKeyword("Not")) return LSOperatorArrayRuntime.LogicalNot(ParseUnary());
            if (Match(TokenKind.Plus)) return Numeric(ParseUnary());
            if (Match(TokenKind.Minus)) return -Numeric(ParseUnary());
            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            if (Match(TokenKind.Number)) return Previous.NumberValue;
            if (Match(TokenKind.String)) return Previous.Text;
            if (Match(TokenKind.DateLiteral)) return XPScriptRuntime.CDate(Previous.Text);
            if (MatchKeyword("True")) return true;
            if (MatchKeyword("False")) return false;
            if (MatchKeyword("Null")) return null;
            if (MatchKeyword("Nothing")) return null;

            if (Match(TokenKind.LeftParen))
            {
                var value = ParseExpression();
                Consume(TokenKind.RightParen, "Expected ')' after expression.");
                return value;
            }

            if (Match(TokenKind.Identifier))
            {
                var name = Previous.Text;
                if (Match(TokenKind.LeftParen))
                {
                    var args = new List<object?>();
                    if (!Check(TokenKind.RightParen))
                    {
                        do { args.Add(ParseExpression()); } while (Match(TokenKind.Comma));
                    }
                    Consume(TokenKind.RightParen, "Expected ')' after arguments.");
                    if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_callvar is null) throw Error("callvar was not supplied to Evaluate.");
                        if (args.Count != 1) throw Error("callvar(index) requires exactly one index or list tag.");
                        return _callvar.Read(args[0]);
                    }
                    return InvokeFunction(name, args);
                }
                if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                {
                    if (_callvar is null) throw Error("callvar was not supplied to Evaluate.");
                    return _callvar.Read();
                }
                if (_variables.TryGetValue(name, out var value)) return value;
                throw Error("Unknown identifier in Evaluate scope: " + name);
            }

            throw Error("Expected an XPScript expression.");
        }

        private static object? InvokeFunction(string name, IReadOnlyList<object?> args)
        {
            object? Arg(int index) => index < args.Count ? args[index] : null;
            return name.ToLowerInvariant() switch
            {
                "cstr" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)),
                "cint" when args.Count == 1 => XPScriptRuntime.CInt(Arg(0)),
                "clng" when args.Count == 1 => XPScriptRuntime.CLng(Arg(0)),
                "cdbl" when args.Count == 1 => XPScriptRuntime.CDbl(Arg(0)),
                "csng" when args.Count == 1 => XPScriptRuntime.CSng(Arg(0)),
                "ccur" when args.Count == 1 => XPScriptRuntime.CCur(Arg(0)),
                "cbyte" when args.Count == 1 => XPScriptRuntime.CByte(Arg(0)),
                "cbool" when args.Count == 1 => XPScriptRuntime.CBool(Arg(0)),
                "cdate" when args.Count == 1 => XPScriptRuntime.CDate(Arg(0)),
                "len" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)).Length,
                "lcase" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)).ToLower(CultureInfo.CurrentCulture),
                "ucase" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)).ToUpper(CultureInfo.CurrentCulture),
                "trim" when args.Count == 1 => XPScriptRuntime.CStr(Arg(0)).Trim(),
                "abs" when args.Count == 1 => Math.Abs(XPScriptRuntime.CDbl(Arg(0))),
                "round" when args.Count is 1 or 2 => Math.Round(XPScriptRuntime.CDbl(Arg(0)), args.Count == 2 ? XPScriptRuntime.CInt(Arg(1)) : 0, MidpointRounding.ToEven),
                _ => throw new XPScriptRuntimeException(5, "Function is not available inside Evaluate: " + name)
            };
        }

        private static object? Add(object? left, object? right)
        {
            if (left is null || right is null) return null;
            if (left is string leftText)
                return leftText + XPScriptRuntime.CStr(right);
            if (right is string rightText)
            {
                if (double.TryParse(rightText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var parsed))
                    return Numeric(left) + parsed;
                return XPScriptRuntime.CStr(left) + rightText;
            }
            return Numeric(left) + Numeric(right);
        }

        private static double Numeric(object? value) => XPScriptRuntime.CDbl(value);

        private static bool Compare(object? left, object? right, TokenKind op)
        {
            int comparison;
            if (left is DateTime || right is DateTime)
                comparison = DateTime.Compare(XPScriptRuntime.CDate(left), XPScriptRuntime.CDate(right));
            else if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right))
                comparison = XPScriptRuntime.CDbl(left).CompareTo(XPScriptRuntime.CDbl(right));
            else
                comparison = string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);

            return op switch
            {
                TokenKind.Equal => comparison == 0,
                TokenKind.NotEqual => comparison != 0,
                TokenKind.Less => comparison < 0,
                TokenKind.LessEqual => comparison <= 0,
                TokenKind.Greater => comparison > 0,
                TokenKind.GreaterEqual => comparison >= 0,
                _ => false
            };
        }

        private void SkipStatementTail()
        {
            if (Check(TokenKind.NewLine) || Check(TokenKind.Colon)) SkipSeparators();
            else if (!Check(TokenKind.End)) throw Error("Expected end of statement.");
        }

        private void SkipSeparators()
        {
            while (Match(TokenKind.NewLine) || Match(TokenKind.Colon)) { }
        }

        private bool Match(TokenKind kind)
        {
            if (!Check(kind)) return false;
            Advance();
            return true;
        }

        private bool MatchKeyword(string keyword)
        {
            if (!Check(TokenKind.Identifier) || !Current.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return false;
            Advance();
            return true;
        }

        private Token Consume(TokenKind kind, string message)
        {
            if (Check(kind)) return Advance();
            throw Error(message);
        }

        private bool Check(TokenKind kind) => Current.Kind == kind;
        private Token Advance() { if (_position < _tokens.Count - 1) _position++; return Previous; }
        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
        private Token Previous => _tokens[Math.Max(0, _position - 1)];
        private Token Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];
        private XPScriptRuntimeException Error(string message) => new(5, message + " (Evaluate token " + (_position + 1) + ")");
    }

    private enum TokenKind
    {
        End, NewLine, Colon, Identifier, Number, String, DateLiteral,
        LeftParen, RightParen, Comma,
        Plus, Minus, Star, Slash, Backslash, Caret, Ampersand,
        Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual
    }

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
            if (c == '<')
            {
                if (i + 1 < source.Length && source[i + 1] == '>') { result.Add(new(TokenKind.NotEqual, "<>")); i += 2; continue; }
                if (i + 1 < source.Length && source[i + 1] == '=') { result.Add(new(TokenKind.LessEqual, "<=")); i += 2; continue; }
                result.Add(new(TokenKind.Less, "<")); i++; continue;
            }
            if (c == '>')
            {
                if (i + 1 < source.Length && source[i + 1] == '=') { result.Add(new(TokenKind.GreaterEqual, ">=")); i += 2; continue; }
                result.Add(new(TokenKind.Greater, ">")); i++; continue;
            }
            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                var closed = false;
                while (i < source.Length)
                {
                    if (source[i] == '"')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        i++; closed = true; break;
                    }
                    sb.Append(source[i++]);
                }
                if (!closed) throw new XPScriptRuntimeException(5, "Unterminated string literal in Evaluate.");
                result.Add(new(TokenKind.String, sb.ToString()));
                continue;
            }
            if (c == '#')
            {
                var end = source.IndexOf('#', i + 1);
                if (end < 0) throw new XPScriptRuntimeException(5, "Unterminated date literal in Evaluate.");
                result.Add(new(TokenKind.DateLiteral, source[(i + 1)..end]));
                i = end + 1;
                continue;
            }
            if (char.IsDigit(c) || (c == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
            {
                var start = i++;
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    if ((source[i] == '+' || source[i] == '-') && source[i - 1] is not ('e' or 'E')) break;
                    i++;
                }
                var text = source[start..i];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    throw new XPScriptRuntimeException(5, "Invalid numeric literal in Evaluate: " + text);
                object numeric = !text.Contains('.') && !text.Contains('e', StringComparison.OrdinalIgnoreCase) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    ? integer
                    : number;
                result.Add(new(TokenKind.Number, text, numeric));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                result.Add(new(TokenKind.Identifier, source[start..i]));
                continue;
            }
            if (c == '\'')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            throw new XPScriptRuntimeException(5, "Unsupported character in Evaluate source: " + c);
        }
        result.Add(new(TokenKind.End, ""));
        return result;
    }
}
""";
}
