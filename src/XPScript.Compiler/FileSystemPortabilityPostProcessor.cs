namespace XPScript.Compiler;

internal sealed class FileSystemPortabilityPostProcessor
{
    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        generated = System.Text.RegularExpressions.Regex.Replace(
            generated,
            @"\.(StartsWith|EndsWith)\('([^']{1})',\s*(StringComparison\.[A-Za-z]+)\)",
            m => $".{m.Groups[1].Value}(\"{m.Groups[2].Value}\", {m.Groups[3].Value})",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        generated = generated.Replace("Encoding.Default", "XPScriptFileSystemRuntime.LegacyEncoding", StringComparison.Ordinal);
        generated = generated.Replace("Path.GetFullPath(XPScriptRuntime.CStr(pathValue))", "XPScriptFileSystemRuntime.ResolvePath(pathValue)", StringComparison.Ordinal);
        generated = generated.Replace("Path.GetFullPath(CStr(pathValue))", "XPScriptFileSystemRuntime.ResolvePath(pathValue)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)", "XPScriptFileSystemRuntime.OpenInputStream(path)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read)", "XPScriptFileSystemRuntime.OpenOutputStream(path, append)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)", "XPScriptFileSystemRuntime.OpenBinaryStream(path)", StringComparison.Ordinal);
        generated = generated.Replace("new StreamReader(path, charset, detectEncodingFromByteOrderMarks: true)", "new StreamReader(XPScriptFileSystemRuntime.OpenInputStream(path), charset, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false)", StringComparison.Ordinal);
        generated = generated.Replace("public static long FileLen(object? fileName) => new FileInfo(CStr(fileName)).Length;", "public static long FileLen(object? fileName) => XPScriptFileSystemRuntime.FileLen(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static DateTime FileDateTime(object? fileName) => File.GetLastWriteTime(CStr(fileName));", "public static DateTime FileDateTime(object? fileName) => XPScriptFileSystemRuntime.FileDateTime(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static int GetFileAttr(object? fileName) => (int)File.GetAttributes(CStr(fileName));", "public static int GetFileAttr(object? fileName) => XPScriptFileSystemRuntime.GetFileAttr(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static void SetFileAttr(object? fileName, int attributes) => File.SetAttributes(CStr(fileName), (FileAttributes)attributes);", "public static void SetFileAttr(object? fileName, int attributes) => XPScriptFileSystemRuntime.SetFileAttr(fileName, attributes);", StringComparison.Ordinal);
        generated = generated.Replace("public static void FileCopy(object? source, object? destination) => File.Copy(CStr(source), CStr(destination), true);", "public static void FileCopy(object? source, object? destination) => XPScriptFileSystemRuntime.CopyFile(source, destination);", StringComparison.Ordinal);
        generated = generated.Replace("public static void Kill(object? path) => File.Delete(CStr(path));", "public static void Kill(object? path) => XPScriptFileSystemRuntime.DeleteFile(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void NameFile(object? oldPath, object? newPath) => File.Move(CStr(oldPath), CStr(newPath), true);", "public static void NameFile(object? oldPath, object? newPath) => XPScriptFileSystemRuntime.MoveFile(oldPath, newPath);", StringComparison.Ordinal);
        generated = generated.Replace("public static void MkDir(object? path) => Directory.CreateDirectory(CStr(path));", "public static void MkDir(object? path) => XPScriptFileSystemRuntime.MakeDirectory(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void RmDir(object? path) => Directory.Delete(CStr(path), false);", "public static void RmDir(object? path) => XPScriptFileSystemRuntime.RemoveDirectory(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void ChDir(object? path) => Environment.CurrentDirectory = Path.GetFullPath(CStr(path));", "public static void ChDir(object? path) => XPScriptFileSystemRuntime.ChangeDirectory(path);", StringComparison.Ordinal);

        generated = generated.Replace(
            "DirEnumerator = Directory.EnumerateFileSystemEntries(directory, mask)\n                .Select(Path.GetFileName)\n                .Where(x => x is not null)\n                .Cast<string>()\n                .GetEnumerator();",
            "DirEnumerator = XPScriptFileSystemRuntime.Enumerate(raw).GetEnumerator();",
            StringComparison.Ordinal);

        generated = generated.Replace(
            "public static object? Evaluate(object? sourceText) => Evaluate(sourceText, null);",
            """
    public static object? Evaluate(object? sourceText) => Evaluate(sourceText, null);

    public static object? Evaluate(object? sourceText, object? callvar0, object? callvar1, params object?[] additionalCallvars)
    {
        var values = new object?[additionalCallvars.Length + 2];
        values[0] = callvar0;
        values[1] = callvar1;
        Array.Copy(additionalCallvars, 0, values, 2, additionalCallvars.Length);
        return Evaluate(sourceText, PackCallvars(values));
    }

    public static object? EvaluateArguments(object? sourceText, params XPScriptEvaluateArgument[] arguments)
    {
        var source = XPScriptRuntime.CStr(sourceText);
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            if (arguments.Length == 0)
                return new Evaluator(source, null).Run();

            object? callvar;
            if (arguments.Length == 1)
            {
                callvar = arguments[0].IsByRef
                    ? arguments[0].Value
                    : XPScriptEvaluateCollectionRuntime.Snapshot(arguments[0].Value);
            }
            else
            {
                var values = new object?[arguments.Length];
                var byVal = new bool[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    values[i] = arguments[i].Value;
                    byVal[i] = !arguments[i].IsByRef;
                }
                callvar = XPScriptEvaluateCollectionRuntime.PackArguments(values, byVal);
            }

            var evaluator = new Evaluator(source, callvar);
            var result = evaluator.Run();
            if (arguments.Length == 1)
            {
                arguments[0].WriteBack(evaluator.CurrentCallvar);
            }
            else if (evaluator.CurrentCallvar is LSArray packed && packed.IsAllocated)
            {
                for (var i = 0; i < arguments.Length; i++)
                    arguments[i].WriteBack(packed.Get(new object?[] { i }));
            }
            return result;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Evaluate failed: " + ex.Message);
        }
    }

    private static LSArray PackCallvars(params object?[] values)
    {
        var packed = new LSArray("Variant", true, new[] { 0 }, new[] { values.Length - 1 });
        for (var i = 0; i < values.Length; i++)
            packed.Set(values[i], new object?[] { i });
        return packed;
    }
""",
            StringComparison.Ordinal);

        generated = generated.Replace(
            "return new Evaluator(source, Snapshot(callvar)).Run();",
            "return new Evaluator(source, XPScriptEvaluateCollectionRuntime.Snapshot(callvar)).Run();",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "return Snapshot(ParseExpression());",
            "return XPScriptEvaluateCollectionRuntime.SnapshotReturn(ParseExpression());",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "private readonly object? _callvar;",
            "private object? _callvar;\n        public object? CurrentCallvar => _callvar;",
            StringComparison.Ordinal);

        const string oldAssignment = """
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
""";

        const string newAssignment = """
                if (Check(TokenKind.Identifier)
                    && Current.Text.Equals("callvar", StringComparison.OrdinalIgnoreCase)
                    && Peek(1).Kind == TokenKind.LeftParen)
                {
                    var savedPosition = _position;
                    Advance();
                    Advance();
                    var indexes = new List<object?>();
                    if (!Check(TokenKind.RightParen))
                        do { indexes.Add(ParseExpression()); } while (Match(TokenKind.Comma));
                    Consume(TokenKind.RightParen, "Expected ')' after callvar indexes.");
                    if (Match(TokenKind.Equal))
                    {
                        WriteCallvar(indexes, ParseExpression());
                        SkipStatementTail();
                        continue;
                    }
                    _position = savedPosition;
                }

                if (Check(TokenKind.Identifier) && Peek(1).Kind == TokenKind.Equal)
                {
                    var name = Advance().Text;
                    Advance();
                    if (name.Equals("callvar", StringComparison.OrdinalIgnoreCase))
                    {
                        _callvar = ParseExpression();
                        SkipStatementTail();
                        continue;
                    }
                    if (!_variables.ContainsKey(name))
                        throw Error("Assignment requires a local variable declared with Dim: " + name);
                    _variables[name] = ParseExpression();
                    SkipStatementTail();
                    continue;
                }
""";
        generated = generated.Replace(oldAssignment, newAssignment, StringComparison.Ordinal);

        const string oldReadCallvar = """
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
""";

        const string newReadCallvar = """
        private object? ReadCallvar(IReadOnlyList<object?> args)
        {
            if (_callvar is LSArray outerArray && outerArray.Rank == 1 && args.Count > 1)
            {
                var value = outerArray.Get(new object?[] { args[0] });
                return XPScriptEvaluateCollectionRuntime.ReadIndexed(value, args.Skip(1).ToArray());
            }
            if (_callvar is Array outer && outer.Rank == 1 && args.Count > 1)
            {
                var value = outer.GetValue(XPScriptRuntime.CInt(args[0]));
                return XPScriptEvaluateCollectionRuntime.ReadIndexed(value, args.Skip(1).ToArray());
            }
            return XPScriptEvaluateCollectionRuntime.ReadIndexed(_callvar, args);
        }

        private void WriteCallvar(IReadOnlyList<object?> args, object? value)
        {
            if (_callvar is LSArray outerArray && outerArray.Rank == 1 && args.Count > 1)
            {
                var nested = outerArray.Get(new object?[] { args[0] });
                XPScriptEvaluateCollectionRuntime.WriteIndexed(nested, args.Skip(1).ToArray(), value);
                return;
            }
            if (_callvar is Array outer && outer.Rank == 1 && args.Count > 1)
            {
                var nested = outer.GetValue(XPScriptRuntime.CInt(args[0]));
                XPScriptEvaluateCollectionRuntime.WriteIndexed(nested, args.Skip(1).ToArray(), value);
                return;
            }
            XPScriptEvaluateCollectionRuntime.WriteIndexed(_callvar, args, value);
        }
""";
        generated = generated.Replace(oldReadCallvar, newReadCallvar, StringComparison.Ordinal);

        generated = generated.Replace(
            "if (Match(TokenKind.Plus)) value = Add(value, ParseMultiplicative());",
            "if (Match(TokenKind.Plus)) value = XPScriptCoercion.AddVariant(value, ParseMultiplicative());",
            StringComparison.Ordinal);

        const string oldCompare = """
        private static bool Compare(object? left, object? right, TokenKind op)
        {
            int comparison;
            if (left is DateTime || right is DateTime) comparison = DateTime.Compare(XPScriptRuntime.CDate(left), XPScriptRuntime.CDate(right));
            else if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right)) comparison = XPScriptRuntime.CDbl(left).CompareTo(XPScriptRuntime.CDbl(right));
            else comparison = string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
            return op switch { TokenKind.Equal => comparison == 0, TokenKind.NotEqual => comparison != 0, TokenKind.Less => comparison < 0, TokenKind.LessEqual => comparison <= 0, TokenKind.Greater => comparison > 0, TokenKind.GreaterEqual => comparison >= 0, _ => false };
        }
""";

        const string newCompare = """
        private static bool Compare(object? left, object? right, TokenKind op)
        {
            var operation = op switch
            {
                TokenKind.Equal => "=",
                TokenKind.NotEqual => "<>",
                TokenKind.Less => "<",
                TokenKind.LessEqual => "<=",
                TokenKind.Greater => ">",
                TokenKind.GreaterEqual => ">=",
                _ => throw new XPScriptRuntimeException(5, "Unsupported Evaluate comparison operator.")
            };
            return XPScriptEvaluateSemanticsRuntime.Compare(left, right, operation);
        }
""";
        generated = generated.Replace(oldCompare, newCompare, StringComparison.Ordinal);

        generated = generated.Replace(
            "catch (XPScriptRuntimeException) { throw; }",
            "catch (XPScriptRuntimeException ex) { throw XPScriptEvaluateSemanticsRuntime.Sanitize(ex); }",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "throw new XPScriptRuntimeException(5, \"Evaluate failed: \" + ex.Message);",
            "throw XPScriptEvaluateSemanticsRuntime.Sanitize(ex);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "throw new XPScriptRuntimeException(5, \"Invalid numeric literal in Evaluate: \" + text);",
            "throw new XPScriptRuntimeException(5, \"Invalid numeric literal in Evaluate.\");",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "_ => throw new XPScriptRuntimeException(5, \"Function is not available inside Evaluate: \" + name)",
            "_ => XPScriptEvaluateFunctionArityRuntime.Throw(name, args.Count)",
            StringComparison.Ordinal);

        generated += "\n\n" + EvaluateCollectionIsolationRuntimeSource.Code + "\n";
        generated += "\n\n" + EvaluateSemanticsRuntimeSource.Code + "\n";
        generated += "\n\n" + EvaluateFunctionArityRuntimeSource.Code + "\n";
        return generated;
    }
}