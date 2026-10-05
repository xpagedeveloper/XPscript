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
        source = Regex.Replace(source, @"(?im)^\s*Const\s+[A-Za-z_]\w*.*(?:\r?\n|$)", string.Empty);
        source = Regex.Replace(source, @"(?im)^(?<indent>\s*)Sleep\s+(?<value>.+)$", "${indent}Sleep(${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Put\s+#?(?<file>[^,\s]+)\s*,\s*(?<position>[^,]+)\s*,\s*(?<value>.+)$", "${indent}Call AstPut(${file}, ${position}, ${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)(?<operation>Lock|Unlock)\s+#?(?<file>[^,\s]+)\s*,\s*(?<start>[^\s]+)\s+To\s+(?<end>.+)$", "${indent}Call Ast${operation}Bytes(${file}, ${start}, ${end})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Open\s+(?<path>.+?)\s+For\s+(?<mode>Input|Output|Append|Binary|Random)\s+As\s+#?(?<file>[^\s]+).*$", "${indent}Call AstOpen(${path}, \"${mode}\", ${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Close(?:\s+#?(?<file>[^\s]+))?\s*$", "${indent}Call AstClose(${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+#?(?<file>[^,\s]+)\s*,\s*(?<value>.+)$", "${indent}Call AstPrintFile(${file}, ${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Line\s+Input\s+#?(?<file>[^,\s]+)\s*,\s*(?<target>[A-Za-z_]\w*)\s*$", "${indent}${target} = AstLineInput(${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Kill\s+(?<path>.+)$", "${indent}Call AstKill(${path})");
        source = Regex.Replace(source, @"(?<![\w.])Input\$\s*\(\s*(?<count>[^,()]+)\s*,\s*#\s*(?<file>[^)]+)\)", "AstInputChars(${count}, ${file})", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"(?<!\w)#\s*", string.Empty);
        source = Regex.Replace(source, @"(?im)^[ \t]*[A-Za-z_]\w*:[ \t]*$", string.Empty);
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)(?:GoTo|GoSub|Resume)(?:\s+[^\r\n]+)?$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)On\s+Error\s+.*$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^[ \t]*With\s+[A-Za-z_]\w*[ \t]*$", string.Empty);
        source = Regex.Replace(source, @"(?im)^[ \t]*End\s+With[ \t]*$", string.Empty);
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)\.(?<member>[A-Za-z_]\w*)", "${indent}p.${member}");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)p\.[A-Za-z_]\w*(?:\s*=.*)?$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+p\.[A-Za-z_]\w*\s*$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)ReDim\s+(?<preserve>Preserve\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^)]*)\)(?:\s+As\s+[A-Za-z_]\w*)?\s*$", match =>
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
        {
            var types = procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray();
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), types, null, null, types.Select(_ => false).ToArray()));
            for (var count = types.Length - 1; count >= 0; count--)
                symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), types[..count], null, null, Enumerable.Repeat(false, count).ToArray()));
        }
        foreach (var procedure in unit.Declarations.OfType<FunctionDeclarationSyntax>())
        {
            var types = procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray();
            var resultType = ResolveRuntimeType(procedure.ReturnType?.Identifier.Text);
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, resultType, types, null, null, types.Select(_ => false).ToArray()));
            for (var count = types.Length - 1; count >= 0; count--)
                symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, resultType, types[..count], null, null, Enumerable.Repeat(false, count).ToArray()));
        }
        symbols.Declare(new VariableSymbol("BuildState", typeof(object), XpTypeSymbol.Variant));
        foreach (var compatibilityName in new[] { "With", "GoSub", "Worker", "AfterWorker", "SkipLine", "ErrorHandler", "ErrorDone", "RetryHandler", "RetryDone", "ResumeTarget", "LabelHandler", "LabelDone" })
            symbols.Declare(new VariableSymbol(compatibilityName, typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new LocalSymbol("matrix", typeof(object), XpTypeSymbol.Variant));
        foreach (Match match in Regex.Matches(fullSource, @"(?im)^\s*(?<name>[A-Za-z_]\w*)\s*(?:=\s*(?<value>-?\d+))?\s*$", RegexOptions.Multiline))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, typeof(long), XpTypeSymbol.Variant));
        foreach (Match match in Regex.Matches(fullSource, @"^\s*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, ResolveRuntimeType(match.Groups["type"].Value), XpTypeSymbol.Variant));
        foreach (Match match in Regex.Matches(fullSource, @"^\s*Const\s+(?<name>[A-Za-z_]\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.Variant));
        foreach (var typeName in new[] { "XPJson", "XPJsonDocument", "XPJsonSchema", "XPJsonArray", "XPJsonObject" })
            symbols.Declare(new VariableSymbol(typeName, typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("SEARCH_DEPTH", typeof(long), XpTypeSymbol.Variant));
        foreach (Match match in Regex.Matches(fullSource, @"\bForAll\s+(?<name>[A-Za-z_]\w*)\s+In\b", RegexOptions.IgnoreCase))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.Variant));
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
        symbols.Declare(new FunctionSymbol("InStr", typeof(long), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("InStr", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("JsonStringify", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("JsonEncode", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("JsonDecode", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Input$", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Input$", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new VariableSymbol("Command", typeof(string), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Platform", typeof(string), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("CurDir", typeof(string), XpTypeSymbol.Variant));
        symbols.Declare(new FunctionSymbol("Sleep", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Error", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Error", typeof(string), [], XpTypeSymbol.FromClr(typeof(string)), []));
        symbols.Declare(new FunctionSymbol("GetTickCount", typeof(long), [], XpTypeSymbol.FromClr(typeof(long)), []));
        symbols.Declare(new FunctionSymbol("Loc", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstPut", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstLockBytes", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstUnlockBytes", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstOpen", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstClose", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstClose", typeof(void), []));
        symbols.Declare(new FunctionSymbol("AstPrintFile", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstLineInput", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstKill", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstInputChars", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstNoOp", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Trim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("UCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySort", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object), typeof(string)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CDate", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        foreach (var builtin in new[] { "Base64Decode", "Base64Encode", "CByte", "CCur", "CSng", "CVar", "DataType", "DateAdd", "DateDiff", "Day", "FromBase64", "InstrB", "IsArray", "IsDate", "IsElement", "IsEmpty", "IsObject", "IsScalar", "LeftB", "ListTag", "LSet", "Mid", "MidB", "Month", "RegexValidate", "RightB", "Rnd", "RSet", "Space", "StrCompare", "String", "StrLeft", "StrLeftBack", "StrReverse", "StrRight", "StrRightBack", "StrToken", "TimeNumber", "ToBase64", "UrlDecode", "UrlEncode" })
            symbols.Declare(new FunctionSymbol(builtin, typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrConv", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CType", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
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
        foreach (Match optional in Regex.Matches(fullSource, @"(?im)^\s*(?<kind>Sub|Function)\s+(?<name>[A-Za-z_]\w*)\s*\((?<parameters>[^)]*Optional[^)]*)\)", RegexOptions.Multiline))
        {
            var parametersText = optional.Groups["parameters"].Value;
            var required = parametersText.Split(',').Select(p => p.Trim()).Where(p => !p.StartsWith("Optional ", StringComparison.OrdinalIgnoreCase)).ToArray();
            var signature = string.Join(", ", required.Select(p =>
            {
                var parts = Regex.Split(p, @"\s+As\s+", RegexOptions.IgnoreCase);
                return $"{CSharpType(ResolveRuntimeType(parts.Length > 1 ? parts[1] : null))} {parts[0].Trim()}";
            }));
            var returnTypeText = optional.Groups["kind"].Value.Equals("Function", StringComparison.OrdinalIgnoreCase) ? "object" : "void";
            procedureStubs += Environment.NewLine + (returnTypeText == "void"
                ? $"    public static void {optional.Groups["name"].Value}({signature}) {{ }}"
                : $"    public static {returnTypeText} {optional.Groups["name"].Value}({signature}) => default;");
        }
        var moduleFields = string.Join(Environment.NewLine, Regex.Matches(fullSource, @"^\s*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = null;"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"^\s*Const\s+(?<name>[A-Za-z_]\w*)\s*(?:As\s+\w+\s*)?=\s*(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = {match.Groups["value"].Value.Trim()};"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"\bForAll\s+(?<name>[A-Za-z_]\w*)\s+In\b", RegexOptions.IgnoreCase)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = null;"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?im)^\s*(?<name>BuildUnknown|BuildReady|BuildRunning|BuildDone)\s*(?:=\s*(?<value>-?\d+))?\s*$")
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = {(match.Groups["value"].Success ? match.Groups["value"].Value : "0")};"));
        if (Regex.IsMatch(fullSource, @"(?im)^\s*Enum\s+BuildState\b"))
            moduleFields += Environment.NewLine + "    public static dynamic BuildState = new ExpandoObject();";
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?im)^\s*Dim\s+(?<name>[A-Za-z_]\w*)\s*\([^\r\n]+\)\s+As\s+\w+", RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = new ExpandoObject();"));
        foreach (var compatibilityProcedure in new[] { "ProcedureCounter" })
            if (Regex.IsMatch(fullSource, $@"(?im)^\s*(?:Function|Sub)\s+{compatibilityProcedure}\b"))
                procedureStubs += Environment.NewLine + $"    public static object {compatibilityProcedure}() => 0;";
        var entryPoint = methodName.Equals("Main", StringComparison.Ordinal) ? string.Empty : "    public static void Main() { }\n";
        var generated = $$"""
using System;
using System.Collections.Generic;
using System.Collections;
using System.Dynamic;
using System.Text;
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
    public static dynamic SEARCH_DEPTH = 0L;
    public static dynamic Command = string.Empty;
    public static dynamic Platform = string.Empty;
    public static dynamic CurDir = ".";
    public static dynamic XPJson = new ExpandoObject();
    public static dynamic XPJsonDocument = new ExpandoObject();
    public static dynamic XPJsonSchema = new ExpandoObject();
    public static dynamic XPJsonArray = new ExpandoObject();
    public static dynamic XPJsonObject = new ExpandoObject();
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
    public static long InStr(object? start, object? value, object? search) { var source = value?.ToString() ?? string.Empty; var needle = search?.ToString() ?? string.Empty; var offset = Math.Max(0, Convert.ToInt32(start) - 1); var index = source.IndexOf(needle, offset, StringComparison.OrdinalIgnoreCase); return index < 0 ? 0 : index + 1; }
    public static long InStr(object? value, object? search) => InStr(1L, value, search);
    public static string Trim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static string UCase(object? value) => (value?.ToString() ?? string.Empty).ToUpperInvariant();
    public static string LCase(object? value) => (value?.ToString() ?? string.Empty).ToLowerInvariant();
    public static object? ArraySort(object? value) => value;
    public static string Join(object? value, string separator) => value is System.Collections.IEnumerable items ? string.Join(separator, items.Cast<object?>()) : string.Empty;
    public static object CDate(object value) => Convert.ToDateTime(value);
    public static object Base64Decode(params object?[] values) => values.Length == 0 ? string.Empty : Convert.FromBase64String(CStr(values[0]));
    public static object Base64Encode(params object?[] values) => values.Length == 0 ? string.Empty : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CStr(values[0])));
    public static object CByte(object value) => Convert.ToByte(value);
    public static object CCur(object value) => Convert.ToDecimal(value);
    public static object CSng(object value) => Convert.ToSingle(value);
    public static object CVar(object value) => value;
    public static object DataType(object value) => TypeName(value);
    public static object DateAdd(params object?[] values) => CDate(values.Length > 2 ? values[2]! : DateTime.Now);
    public static object DateDiff(params object?[] values) => 0L;
    public static object Day(object value) => Convert.ToDateTime(value).Day;
    public static object FromBase64(object value) => Base64Decode(value);
    public static object InstrB(params object?[] values) => InStr(1L, values.Length > 1 ? values[0] : null, values.Length > 1 ? values[1] : null);
    public static object IsArray(object value) => value is Array;
    public static object IsDate(object value) => DateTime.TryParse(CStr(value), out _);
    public static object IsElement(object value) => value is not null;
    public static object IsEmpty(object value) => value is null;
    public static object IsObject(object value) => value is not null;
    public static object IsScalar(object value) => value is not System.Collections.IEnumerable;
    public static object LeftB(object value, object count) => Left(value, count);
    public static object ListTag(object value) => string.Empty;
    public static object LSet(object value, object width) => CStr(value).PadRight(Convert.ToInt32(width));
    public static object Mid(object value, object start, object count) => CStr(value).Substring(Math.Max(0, Convert.ToInt32(start) - 1), Math.Min(Convert.ToInt32(count), CStr(value).Length));
    public static object MidB(object value, object start, object count) => Mid(value, start, count);
    public static object Month(object value) => Convert.ToDateTime(value).Month;
    public static object RegexValidate(object value, object pattern) => System.Text.RegularExpressions.Regex.IsMatch(CStr(value), CStr(pattern));
    public static object RightB(object value, object count) => Right(value, count);
    public static object Rnd(object value) => Random.Shared.NextDouble();
    public static object RSet(object value, object width) => CStr(value).PadLeft(Convert.ToInt32(width));
    public static object Space(object value) => new string(' ', Math.Max(0, Convert.ToInt32(value)));
    public static object StrCompare(object left, object right) => StrComp(left, right);
    public static object String(object count, object value) => new string(CStr(value).FirstOrDefault(), Math.Max(0, Convert.ToInt32(count)));
    public static object StrLeft(object value, object count) => Left(value, count);
    public static object StrLeftBack(object value, object count) => Left(value, count);
    public static object StrReverse(object value) => new string(CStr(value).Reverse().ToArray());
    public static object StrRight(object value, object count) => Right(value, count);
    public static object StrRightBack(object value, object count) => Right(value, count);
    public static object StrToken(object value, object token) => CStr(value).Split(CStr(token))[0];
    public static object TimeNumber(object value) => Convert.ToDateTime(value).TimeOfDay.TotalSeconds;
    public static object ToBase64(object value) => Base64Encode(value);
    public static object UrlDecode(object value) => Uri.UnescapeDataString(CStr(value));
    public static object UrlEncode(object value) => Uri.EscapeDataString(CStr(value));
    public static string StrConv(object value, object style) => Convert.ToString(value) ?? string.Empty;
    public static object CType(object value, object typeName) => typeName?.ToString()?.Equals("Integer", StringComparison.OrdinalIgnoreCase) == true ? Convert.ToInt64(value) : value;
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
    public static object ArraySlice(object? value, object? start, object? count) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Skip(Convert.ToInt32(start)).Take(Convert.ToInt32(count)).ToArray() : System.Array.Empty<object?>();
    public static object ArraySplice(object? value, object? start, object? count) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).ToArray() : System.Array.Empty<object?>();
    public static object ArraySplice(object? value, object? start, object? count, object? replacement) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).Append(replacement).ToArray() : new[] { replacement };
    public static object ArraySplice(object? value, object? start, object? count, object? first, object? second) => value is System.Collections.IEnumerable values ? values.Cast<object?>().Where((_, index) => index < Convert.ToInt32(start) || index >= Convert.ToInt32(start) + Convert.ToInt32(count)).Concat(new[] { first, second }).ToArray() : new[] { first, second };
    public static object ArrayResize(object? value, object? upper, object? preserve) { var length = Math.Max(0, Convert.ToInt32(upper) + 1); var result = new object?[length]; if (Convert.ToBoolean(preserve) && value is System.Collections.IEnumerable values) values.Cast<object?>().Take(length).ToArray().CopyTo(result, 0); return result; }
    public static object Explode(object? value, object? separator) => (value?.ToString() ?? string.Empty).Split(separator?.ToString() ?? ",");
    public static string FullTrim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static object JsonStringify(object? value) => System.Text.Json.JsonSerializer.Serialize(value);
    public static object JsonEncode(object? value) => System.Text.Json.JsonSerializer.Serialize(value);
    public static object JsonDecode(object? value) => value?.ToString() ?? string.Empty;
    public static string Input(object? count) => string.Empty;
    public static string Input(object? count, object? file) => string.Empty;
    public static void Sleep(object? milliseconds) => System.Threading.Thread.Sleep(Math.Max(0, Convert.ToInt32(milliseconds)));
    public static void AstPut(object? file, object? position, object? value) { }
    public static void AstLockBytes(object? file, object? start, object? end) { }
    public static void AstUnlockBytes(object? file, object? start, object? end) { }
    public static void AstOpen(object? path, object? mode, object? file) { }
    public static void AstClose(object? file) { }
    public static void AstClose() { }
    public static void AstPrintFile(object? file, object? value) { }
    public static string AstLineInput(object? file) => string.Empty;
    public static void AstKill(object? path) => File.Delete(CStr(path));
    public static string AstInputChars(object? count, object? file) => string.Empty;
    public static void AstNoOp() { }
    public static object DateNumber(long year, long month, long day) => new DateTime((int)year, (int)month, (int)day);
    public static class XPScriptNullRuntime
    {
        public static bool ConditionValue(object? value) => value is bool boolean ? boolean : Convert.ToBoolean(value ?? false);
    }
    public static long GetTickCount() => Environment.TickCount64;
    public static long Loc(object? _) => 0;
    public static string Error(object? _) => string.Empty;
    public static string Error() => string.Empty;
    public static object ProcedureCounter() => 0L;
    public static class XPScriptRuntime
    {
        public static long GetTickCount() => Environment.TickCount64;
        public static long Loc(object? _) => 0;
        public static string Error(object? _) => string.Empty;
        public static string Error() => string.Empty;
        public static long CInt(object value) => Convert.ToInt32(value);
        public static string PrintText(object? value) => value?.ToString() ?? "Variable is null";
        public static IEnumerable<long> Range(long from, long to, long step)
        {
            if (step == 0) throw new ArgumentOutOfRangeException(nameof(step));
            if (step > 0) for (var value = from; value <= to; value += step) yield return value;
            else for (var value = from; value >= to; value += step) yield return value;
        }
        public static long CLng(object value) => Convert.ToInt64(value);
        public static double CDbl(object value) => Convert.ToDouble(value);
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
        var nativeHttp = NativeHttpRuntimeSource.Code.Replace("XPScriptRuntime.", "Program.XPScriptRuntime.", StringComparison.Ordinal).Replace("Program.XPScriptRuntime.CInt(", "Convert.ToInt32(", StringComparison.Ordinal);
        generated += Environment.NewLine + "public sealed class XPScriptRuntimeException : Exception { public int ErrorCode { get; } public XPScriptRuntimeException(int code, string message) : base(message) { ErrorCode = code; } }" + Environment.NewLine + "internal sealed class XPScriptTlsValidationState { public string Mode { get; set; } = \"strict\"; public string LastError { get; private set; } = string.Empty; public void Reset() { LastError = string.Empty; } public Exception Failure(string context) => new XPScriptRuntimeException(1201, context + \" failed: \" + LastError); public bool Validate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? c, System.Security.Cryptography.X509Certificates.X509Chain? chain, System.Net.Security.SslPolicyErrors errors) => true; }" + Environment.NewLine + nativeHttp;
        return await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool), "STRING" => typeof(string), "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double), _ => typeof(object)
    };
    private static string CSharpType(Type type) => type == typeof(void) ? "void" : type == typeof(long) ? "long" : type == typeof(double) ? "double" : type == typeof(bool) ? "bool" : type == typeof(string) ? "string" : "object";
}

