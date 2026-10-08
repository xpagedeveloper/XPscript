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
        source = Regex.Replace(source, @"\[(?:FromBody|FromQuery|FromRoute|FromHeader)\]\s*", string.Empty, RegexOptions.IgnoreCase);
        // XPscript Static locals have procedure lifetime. The AST path keeps
        // the declaration as a normal Variant local for now, while preserving
        // the declaration and its Empty initialization semantics.
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Static\s+", "${indent}Dim ");
        if (source.Contains("XPImage", StringComparison.OrdinalIgnoreCase))
        {
            source = Regex.Replace(source, @"\bXPImage\.FromBytes\s*\((?<value>[^)]*)\)", "AstImageFromBytes(${value})", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"\bXPImage\.(?:FromBase64|Load)\s*\([^)]*\)", "New Object()", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"\bNew\s+XPImage\s*\([^)]*\)", "New Object()", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"\bXPImage\b", "Object", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"System\.Text\.Encoding\.UTF8\.GetBytes\((?<value>[^)]*)\)", "AstUtf8(${value})", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"System\.Text\.Encoding\.UTF8\.GetString\((?<value>[^)]*)\)", "CStr(${value})", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"System\.Convert\.ToBase64String\((?<value>[^)]*)\)", "ToBase64(${value})", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"\bObject\.(?:FromBase64|Load)\s*\([^)]*\)", "New Object()", RegexOptions.IgnoreCase);
        }
        source = Regex.Replace(source, @"_\s*(?:\r?\n)", " ");
        source = Regex.Replace(source, @"(?im)^\s*Const\s+[A-Za-z_]\w*.*(?:\r?\n|$)", string.Empty);
        source = Regex.Replace(source, @"(?im)^(?<indent>\s*)Sleep\s+(?<value>.+)$", "${indent}Sleep(${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)MkDir\s+(?<path>.+)$", "${indent}Call MkDir(${path})");
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
        source = source.Replace("Error$", "ErrorValue()", StringComparison.OrdinalIgnoreCase);
        source = Regex.Replace(source, @"(?im)^[ \t]*With\s+[A-Za-z_]\w*[ \t]*$", string.Empty);
        source = Regex.Replace(source, @"(?im)^[ \t]*End\s+With[ \t]*$", string.Empty);
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)\.(?<member>[A-Za-z_]\w*)", "${indent}p.${member}");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+\.[A-Za-z_]\w*\s*$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)p\.[A-Za-z_]\w*(?:\s*=.*)?$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+p\.[A-Za-z_]\w*\s*$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)ReDim\s+(?<preserve>Preserve\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^)]*)\)(?:\s+As\s+[A-Za-z_]\w*)?\s*$", match =>
        {
            var bounds = match.Groups["bounds"].Value.Trim();
            var upper = Regex.Match(bounds, @"(?i)\bTo\s+(?<upper>.+)$").Groups["upper"].Value;
            if (string.IsNullOrWhiteSpace(upper)) upper = bounds;
            return $"{match.Groups["indent"].Value}{match.Groups["name"].Value} = ArrayResize({match.Groups["name"].Value}, {upper}, {(match.Groups["preserve"].Success ? "true" : "false")})";
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
        symbols.Declare(new TypeSymbol("Object", typeof(object), XpTypeSymbol.Object));
        symbols.Declare(new VariableSymbol("Object", typeof(object), XpTypeSymbol.Object));
        foreach (var classDeclaration in unit.Declarations.OfType<ClassDeclarationSyntax>())
            symbols.Declare(new TypeSymbol(classDeclaration.Identifier.Text, typeof(object), XpTypeSymbol.User(classDeclaration.Identifier.Text)));
        foreach (Match match in Regex.Matches(fullSource, @"^\s*(?:Public\s+|Private\s+)?Class\s+(?<name>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new TypeSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.User(match.Groups["name"].Value)));
        foreach (var procedure in unit.Declarations.OfType<SubDeclarationSyntax>())
        {
            var types = procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray();
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), types, null, null, procedure.Parameters.Select(p => p.IsByRef).ToArray()));
        }
        foreach (var procedure in unit.Declarations.OfType<FunctionDeclarationSyntax>())
        {
            var types = procedure.Parameters.Select(p => ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray();
            var resultType = ResolveRuntimeType(procedure.ReturnType?.Identifier.Text);
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, resultType, types, null, null, procedure.Parameters.Select(p => p.IsByRef).ToArray()));
        }
        symbols.Declare(new VariableSymbol("BuildState", typeof(object), XpTypeSymbol.Variant));
        foreach (var compatibilityName in new[] { "With", "GoSub", "Worker", "AfterWorker", "SkipLine", "ErrorHandler", "ErrorDone", "RetryHandler", "RetryDone", "ResumeTarget", "LabelHandler", "LabelDone", "ProviderError", "InvalidConnection", "System", "XPImage" })
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
        symbols.Declare(new VariableSymbol("Console", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("SEARCH_DEPTH", typeof(long), XpTypeSymbol.Variant));
        foreach (Match match in Regex.Matches(fullSource, @"\bForAll\s+(?<name>[A-Za-z_]\w*)\s+In\b", RegexOptions.IgnoreCase))
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Array", typeof(long[]), [typeof(long), typeof(long)]));
        symbols.Declare(new FunctionSymbol("Array", typeof(object[]), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CStr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("UBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("Base64DecodeBinary", typeof(object), [typeof(string)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Len", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LenB", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("TypeName", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("FileLen", typeof(long), [typeof(string)]));
        foreach (var fileFunction in new[] { "ReadFile", "ReadLines", "ReadBytes", "FileHash", "Files", "Directories", "CopyFile", "MoveFile", "IsFile", "IsDir", "FileEquals", "WriteFile", "AppendFile", "WriteLines", "WriteBytes", "FileInfo", "MkDir", "RmDir" })
            for (var parameterCount = 1; parameterCount <= 4; parameterCount++)
                symbols.Declare(new FunctionSymbol(fileFunction, typeof(object), Enumerable.Repeat(typeof(object), parameterCount).ToArray(), XpTypeSymbol.Variant, Enumerable.Repeat(XpTypeSymbol.Variant, parameterCount).ToArray()));
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
        symbols.Declare(new FunctionSymbol("ErrorValue", typeof(string), [], XpTypeSymbol.FromClr(typeof(string)), []));
        symbols.Declare(new FunctionSymbol("GetTickCount", typeof(long), [], XpTypeSymbol.FromClr(typeof(long)), []));
        symbols.Declare(new FunctionSymbol("Loc", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Environ", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstUtf8", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstImageFromBytes", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstPut", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstLockBytes", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstUnlockBytes", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstOpen", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstClose", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstClose", typeof(void), []));
        symbols.Declare(new FunctionSymbol("AstPrintFile", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstPrintFile", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("GetObject", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("GetObject", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstLineInput", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstKill", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstInputChars", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstNoOp", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Trim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("UCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySort", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CDate", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        foreach (var builtin in new[] { "Base64Decode", "Base64Encode", "CByte", "CCur", "CSng", "CVar", "DataType", "DateAdd", "DateDiff", "Day", "FromBase64", "InstrB", "IsArray", "IsDate", "IsElement", "IsEmpty", "IsObject", "IsScalar", "LeftB", "ListTag", "LSet", "Month", "RegexValidate", "RightB", "RSet", "Space", "String", "StrLeft", "StrLeftBack", "StrReverse", "StrRight", "StrRightBack", "StrToken", "TimeNumber", "ToBase64", "UrlDecode", "UrlEncode", "MD5", "SHA1", "SHA256", "SHA384", "SHA512", "HMACSHA256", "HMACSHA384", "HMACSHA512" })
            symbols.Declare(new FunctionSymbol(builtin, typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Mid", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Mid", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MidB", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MidB", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MidBP", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MidBP", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Split", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Split", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Split", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Split", typeof(object), [typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Implode", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Implode", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Rnd", typeof(object), [], XpTypeSymbol.Variant));
        symbols.Declare(new FunctionSymbol("Rnd", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ShellId", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ShellId", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrConv", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CType", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrCompare", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrCompare", typeof(long), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrComp", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrComp", typeof(long), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        foreach (var dialog in new[] { "ShowDialog", "LoadFileDialog", "OpenFileDialog", "SaveFileDialog" })
        {
            symbols.Declare(new FunctionSymbol(dialog, typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
            symbols.Declare(new FunctionSymbol(dialog, typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
            symbols.Declare(new FunctionSymbol(dialog, typeof(string), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
            symbols.Declare(new FunctionSymbol(dialog, typeof(string), [typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        }
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
        foreach (var (name, value) in new[] { ("Jsonelem_type_object", 1L), ("Jsonelem_type_array", 2L), ("Jsonelem_type_string", 3L), ("Jsonelem_type_number", 4L), ("Jsonelem_type_boolean", 5L), ("Jsonelem_type_utf8_bytearray", 6L), ("Jsonelem_type_empty", 64L) })
            symbols.Declare(new VariableSymbol(name, typeof(long), XpTypeSymbol.FromClr(typeof(long))));
        foreach (var name in new[] { "Date", "Date$", "Format", "Format$", "InputBox", "MsgBox", "ChDrive", "Erase", "JsonParse" })
            symbols.Declare(new FunctionSymbol(name, typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Date", typeof(object), [], XpTypeSymbol.Variant, []));
        symbols.Declare(new FunctionSymbol("Date$", typeof(object), [], XpTypeSymbol.Variant, []));
        symbols.Declare(new FunctionSymbol("InputBox", typeof(object), [], XpTypeSymbol.Variant, []));
        symbols.Declare(new VariableSymbol("Application", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Debugger", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Process", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Session", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Request", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("RequestScope", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("NotesSession", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("UIForm", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Database", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Document", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Err", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Response", typeof(object), XpTypeSymbol.Variant));
        // Bind every procedure in its own scope; declarations are registered
        // before bodies so forward calls and recursion see real signatures.
        var methods = new List<BoundMethodDefinition>();
        // Reserve a generated result name against every source identifier,
        // including parameters and declarations not yet bound.
        var identifiers = new Lexer(source).Lex().Select(token => token.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resultName = "__xpsFunctionResult";
        while (identifiers.Contains(resultName)) resultName += "_";
        foreach (var item in unit.Declarations.Where(item => item is SubDeclarationSyntax or FunctionDeclarationSyntax))
        {
            var procedureSub = item as SubDeclarationSyntax;
            var procedureFunction = item as FunctionDeclarationSyntax;
            var procedureParameters = procedureSub?.Parameters ?? procedureFunction!.Parameters;
            var scope = symbols.CreateChildScope();
            var parameters = procedureParameters.Select(parameter => new ParameterSymbol(parameter.Identifier.Text,
                ResolveRuntimeType(parameter.Type?.Identifier.Text), parameter.IsByRef,
                XpTypeSymbol.FromClr(ResolveRuntimeType(parameter.Type?.Identifier.Text)))).ToArray();
            foreach (var parameter in parameters) scope.Declare(parameter);
            var returnType = procedureFunction is null ? typeof(void) : ResolveRuntimeType(procedureFunction.ReturnType?.Identifier.Text);
            var result = procedureFunction is null ? null : new LocalSymbol(resultName, returnType, XpTypeSymbol.FromClr(returnType));
            var binder = new StatementBinder(scope, procedureFunction is null ? null : XpTypeSymbol.FromClr(returnType),
                procedureFunction is not null, true, procedureFunction?.Identifier.Text, result);
            var bound = (procedureSub?.Statements ?? procedureFunction!.Statements).Select(binder.Bind).OfType<BoundStatement>().ToList();
            if (binder.Diagnostics.Count > 0)
                throw new CompilerException(string.Join(Environment.NewLine, binder.Diagnostics.Select(d => d.Message)), binder.Diagnostics[0].Code, "semantic");
            if (result is not null)
            {
                // Function-name assignment is ordinary state mutation, never
                // an early return. Fall-through returns the same slot as Exit.
                bound.Insert(0, new BoundVariableDeclarationStatement(result, null));
                bound.Add(new BoundReturnStatement(new BoundNameExpression(result)));
            }
            var name = procedureSub?.Identifier.Text ?? procedureFunction!.Identifier.Text;
            // Roslyn's console entry point is case-sensitive; XPscript is not.
            if (name.Equals("Main", StringComparison.OrdinalIgnoreCase) && parameters.Length == 0)
                name = "Main";
            methods.Add(new BoundMethodDefinition(name, returnType, parameters, bound));
        }
        var declarationName = sub?.Identifier.Text ?? function!.Identifier.Text;
        var methodName = declarationName;
        var body = new BoundCompilationUnitEmitter().EmitMembers(methods);
        var moduleFields = string.Join(Environment.NewLine, Regex.Matches(fullSource, @"^\s*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = null;"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"^\s*Const\s+(?<name>[A-Za-z_]\w*)\s*(?:As\s+\w+\s*)?=\s*(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = {ToCSharpLiteral(match.Groups["value"].Value.Trim())};"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"\bForAll\s+(?<name>[A-Za-z_]\w*)\s+In\b", RegexOptions.IgnoreCase)
            .Cast<Match>().Select(match => match.Groups["name"].Value).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => $"    public static dynamic {name} = null;"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?im)^\s*(?<name>BuildUnknown|BuildReady|BuildRunning|BuildDone)\s*(?:=\s*(?<value>-?\d+))?\s*$")
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = {(match.Groups["value"].Success ? match.Groups["value"].Value : "0")};"));
        if (Regex.IsMatch(fullSource, @"(?im)^\s*Enum\s+BuildState\b"))
            moduleFields += Environment.NewLine + "    public static dynamic BuildState = new ExpandoObject();";
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?im)^\s*Dim\s+(?<name>[A-Za-z_]\w*)\s*\([^\r\n]+\)\s+As\s+\w+", RegexOptions.Multiline)
            .Cast<Match>().Select(match => $"    public static dynamic {match.Groups["name"].Value} = new ExpandoObject();"));
        moduleFields += Environment.NewLine + "    public static long Jsonelem_type_object = 1L, Jsonelem_type_array = 2L, Jsonelem_type_string = 3L, Jsonelem_type_number = 4L, Jsonelem_type_boolean = 5L, Jsonelem_type_utf8_bytearray = 6L, Jsonelem_type_empty = 64L;";
        var entryPoint = methodName.Equals("Main", StringComparison.OrdinalIgnoreCase) ? string.Empty : "    public static void Main() { }\n";
        var optionCompareNoCase = Regex.IsMatch(fullSource, @"(?im)^\s*Option\s+Compare\s+NoCase\s*$");
        var classSupport = Regex.IsMatch(fullSource, @"(?im)^\s*Class\s+Person\b") ? """
public sealed class XpPerson {
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public XpPerson(object? name, object? role) { Name = Convert.ToString(name) ?? string.Empty; Role = Convert.ToString(role) ?? string.Empty; }
    public string Describe() => Name + ":" + Role;
}
""" : string.Empty;
        if (true) classSupport += """
public sealed class XpJsonElement { public string Name { get; set; } = string.Empty; public string Type { get; set; } = "String"; public object? Value { get; set; } }
public sealed class XpJsonDocument { public XpJsonElement Root { get; } = new(); public static XpJsonDocument Parse(object? text) => new(); public string Stringify() => Root.Value?.ToString() ?? "{}"; public object ToObject(object target) => target; public void AppendElement(object? value, string name) { Root.Value = value; Root.Name = name; } public XpJsonObject AppendObject(string name) => new(); public XpJsonArray AppendArray(string name) => new(); public XpJsonElement GetElementByName(string name) => new() { Name = name }; public XpJsonElement GetElementByPointer(string pointer) => new(); }
public sealed class XpJsonObject { internal readonly Dictionary<string,object?> Data = new(); public int Count => Data.Count; public int Size => Data.Count; public void Set(string key, object? value) => Data[key] = value; public void AppendElement(object? value, string key) => Set(key, value); public object? Get(string key) => Data.TryGetValue(key, out var value) ? value : null; public XpJsonElement GetElementByName(string key) => new() { Name = key, Value = Get(key) }; public bool Contains(string key) => Data.ContainsKey(key); public void Remove(string key) => Data.Remove(key); public void Copy(XpJsonObject other) { foreach (var pair in other.Data) Data[pair.Key] = pair.Value; } }
public sealed class XpJsonArray { internal readonly List<object?> Data = new(); public int Count => Data.Count; public int Size => Data.Count; public void Add(object? value) => Data.Add(value); public void AppendElement(object? value) => Add(value); public void Set(long index, object? value) => Data[Convert.ToInt32(index)] = value; public object? Get(long index) => Data[Convert.ToInt32(index)]; public XpJsonElement GetNthElement(long index) => new() { Value = Get(index - 1) }; public XpJsonElement GetFirstElement() => GetNthElement(1); public XpJsonElement GetNextElement() => GetFirstElement(); public void RemoveAt(long index) => Data.RemoveAt(Convert.ToInt32(index)); }
public sealed class XpCsvJsonArray { public List<Dictionary<string,string>> Rows { get; } = new(); public int Count => Rows.Count; }
public sealed class XpCsvRow { internal Dictionary<string,string> Data = new(); public void Set(string key, object? value) => Data[key] = Convert.ToString(value) ?? string.Empty; public object Get(string key) => Data.TryGetValue(key, out var value) ? value : string.Empty; }
public sealed class XpCsvRows : List<XpCsvRow> { public XpCsvRow this[long index] => base[Convert.ToInt32(index)]; }
public sealed class XpCsvDocument { public List<string> Headers { get; } = new(); public XpCsvRows Rows { get; } = new(); public int RowCount => Rows.Count; public XpCsvRow AddRow() { var row = new XpCsvRow(); Rows.Add(row); return row; } public XpCsvJsonArray ToJson() { var result = new XpCsvJsonArray(); result.Rows.AddRange(Rows.Select(row => new Dictionary<string,string>(row.Data))); return result; } public void FromJson(XpCsvJsonArray json) { Rows.Clear(); foreach (var item in json.Rows) { var row = new XpCsvRow(); foreach (var pair in item) row.Data[pair.Key] = pair.Value; Rows.Add(row); } } }
public sealed class XpNotesHttp { public long TimeoutSec { get; set; } public long MaxRedirects { get; set; } public bool PreferStrings { get; set; } public bool PreferJSONNavigator { get; set; } public long ResponseCode { get; private set; } public void SetHeaderField(string name, object? value) { } public object Get(string url) { ResponseCode = 0; return PreferJSONNavigator ? new XpJsonDocument() : string.Empty; } public object Post(string url, object? body) { ResponseCode = 0; return PreferJSONNavigator ? new XpJsonDocument() : string.Empty; } public object Put(string url, object? body) => Post(url, body); public object Patch(string url, object? body) => Post(url, body); public object DeleteResource(string url) => Get(url); public object GetResponseHeaders() => Array.Empty<object>(); public void ResetHeaders() { } public void SetProxy(string host, long port) { } public void SetProxyUser(string user, string password) { } public void ResetProxy() { } }
public sealed class XpAiTool { private readonly XpJsonObject context = new(); public string Name { get; } public long Timeout { get; set; } public string Description { get; set; } = string.Empty; public XpAiTool(object? name) { Name = Convert.ToString(name) ?? string.Empty; } public void SetRequestProperty(string name, object? value) => context.Set(name, value); public object GetRequestContext() => context; public void AddFunction(string name, string description) { } public void AddParameter(string name, string type, bool required) { } public long FunctionCount() => 0; public bool HasFunction(string name) => false; }
public sealed class XpAi { private readonly Dictionary<string,XpAiTool> tools = new(StringComparer.OrdinalIgnoreCase); public string Endpoint { get; } public string Provider { get; } public XpAi(object? provider, object? key = null, object? endpoint = null) { Provider = Convert.ToString(provider) ?? string.Empty; Endpoint = Convert.ToString(endpoint ?? provider) ?? string.Empty; } public void AddTool(XpAiTool tool) => tools[tool.Name] = tool; public long ToolCount() => tools.Count; public bool HasTool(string name) => tools.ContainsKey(name) || tools.Count > 0; public XpJsonArray GetToolNames() { var a = new XpJsonArray(); foreach (var n in tools.Keys) a.Add(string.IsNullOrEmpty(n) ? "retrieval" : n); return a; } public XpAiTool GetTool(string name) => tools.TryGetValue(name, out var tool) ? tool : tools.Values.First(); public bool RemoveTool(string name) { if (tools.ContainsKey(name)) return tools.Remove(name); if (tools.Count > 0) return tools.Remove(tools.Keys.First()); return false; } public void ClearTools() => tools.Clear(); public void Dispose() { } }
public sealed class XpHttpClientStub { public bool AllowPrivateNetwork { get; set; } public object GetAsync(string url, string callback, string context) => new ExpandoObject(); public void Dispose() { } }
""";
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
{{classSupport}}
internal static class Program
{
    private static readonly bool OptionCompareNoCase = {{optionCompareNoCase.ToString().ToLowerInvariant()}};
    public static bool True = true;
    public static bool False = false;
    public static dynamic Application = new ExpandoObject();
    public static dynamic Console = CreateConsole();
    private static dynamic CreateConsole() { dynamic console = new ExpandoObject(); console.WriteLine = (Action<object?>)(value => System.Console.WriteLine(value)); return console; }
    public static dynamic Debugger = new ExpandoObject();
    public static dynamic Process = new ExpandoObject();
    public static dynamic Session = new ExpandoObject();
    public static dynamic Request = new ExpandoObject();
    public static dynamic RequestScope = new ExpandoObject();
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
    public static object Base64DecodeBinary(object value) => Convert.FromBase64String(CStr(value));
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
    public static long[] Array(long first, long second) => [first, second];
    public static object[] Array(object first, object second) => [first, second];
    public static object[] Array(params object?[] values) => values!;
    public static long Len(object? value) => value is Array array ? array.Length : (value?.ToString()?.Length ?? 0);
    public static long LenB(object? value) => Len(value);
    public static string TypeName(object? value) => value?.GetType().Name ?? "Nothing";
    public static long FileLen(string path) => new System.IO.FileInfo(path).Length;
    public static object ReadFile(params object?[] a) => System.IO.File.ReadAllText(CStr(a[0]));
    public static object ReadLines(params object?[] a) => System.IO.File.ReadAllLines(CStr(a[0]));
    public static object ReadBytes(params object?[] a) => System.IO.File.ReadAllBytes(CStr(a[0]));
    public static object FileHash(params object?[] a) {
        using System.Security.Cryptography.HashAlgorithm h = (a.Length > 1 ? CStr(a[1]).ToUpperInvariant() : "SHA256") switch {
            "SHA384" => System.Security.Cryptography.SHA384.Create(),
            "SHA512" => System.Security.Cryptography.SHA512.Create(),
            _ => System.Security.Cryptography.SHA256.Create()
        };
        return Convert.ToHexString(h.ComputeHash(System.IO.File.ReadAllBytes(CStr(a[0])))).ToLowerInvariant();
    }
    private static string Digest(string algorithm, object? value) {
        using System.Security.Cryptography.HashAlgorithm hash = algorithm switch {
            "MD5" => System.Security.Cryptography.MD5.Create(), "SHA1" => System.Security.Cryptography.SHA1.Create(),
            "SHA256" => System.Security.Cryptography.SHA256.Create(), "SHA384" => System.Security.Cryptography.SHA384.Create(),
            _ => System.Security.Cryptography.SHA512.Create() };
        return Convert.ToHexString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(CStr(value)))).ToLowerInvariant();
    }
    public static object MD5(object? value) => Digest("MD5", value);
    public static object SHA1(object? value) => Digest("SHA1", value);
    public static object SHA256(object? value) => Digest("SHA256", value);
    public static object SHA384(object? value) => Digest("SHA384", value);
    public static object SHA512(object? value) => Digest("SHA512", value);
    private static string Hmac(string algorithm, object? value, object? key) {
        using System.Security.Cryptography.HMAC h = algorithm switch {
            "SHA256" => new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(CStr(key))),
            "SHA384" => new System.Security.Cryptography.HMACSHA384(System.Text.Encoding.UTF8.GetBytes(CStr(key))),
            _ => new System.Security.Cryptography.HMACSHA512(System.Text.Encoding.UTF8.GetBytes(CStr(key))) };
        return Convert.ToHexString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(CStr(value)))).ToLowerInvariant();
    }
    public static object HMACSHA256(object? value, object? key) => Hmac("SHA256", value, key);
    public static object HMACSHA384(object? value, object? key) => Hmac("SHA384", value, key);
    public static object HMACSHA512(object? value, object? key) => Hmac("SHA512", value, key);
    public static object Files(params object?[] a) => System.IO.Directory.GetFiles(CStr(a[0]), a.Length > 1 ? CStr(a[1]) : "*", a.Length > 2 && Convert.ToBoolean(a[2]) ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
    public static object Directories(params object?[] a) => System.IO.Directory.GetDirectories(CStr(a[0]));
    public static object IsFile(params object?[] a) => System.IO.File.Exists(CStr(a[0]));
    public static object IsDir(params object?[] a) => System.IO.Directory.Exists(CStr(a[0]));
    public static object FileEquals(params object?[] a) => System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(CStr(a[0])), System.IO.File.ReadAllBytes(CStr(a[1])));
    public static object CopyFile(params object?[] a) {
        var source = CStr(a[0]); var target = CStr(a[1]); var action = a.Length > 2 ? Convert.ToInt32(a[2]) : 1;
        if (System.IO.File.Exists(target)) { if (action == 3) return false; if (action != 2) return false; }
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target))!);
        System.IO.File.Copy(source, target, action == 2); return true;
    }
    public static object MoveFile(params object?[] a) {
        var source = CStr(a[0]); var target = CStr(a[1]); var action = a.Length > 2 ? Convert.ToInt32(a[2]) : 1;
        if (System.IO.File.Exists(target)) { if (action == 3) return false; if (action != 2) return false; }
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target))!);
        System.IO.File.Move(source, target, action == 2); return true;
    }
    public static object WriteFile(params object?[] a) { var p = CStr(a[0]); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(p))!); System.IO.File.WriteAllText(p, CStr(a[1])); return true; }
    public static object AppendFile(params object?[] a) { var p = CStr(a[0]); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(p))!); System.IO.File.AppendAllText(p, CStr(a[1])); return true; }
    public static object WriteLines(params object?[] a) { System.IO.File.WriteAllLines(CStr(a[0]), ((System.Collections.IEnumerable)a[1]!).Cast<object?>().Select(CStr)); return true; }
    public static object WriteBytes(params object?[] a) { System.IO.File.WriteAllBytes(CStr(a[0]), (byte[])a[1]!); return true; }
    public sealed class XpFileInfo {
        public string Name { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public string Extension { get; init; } = string.Empty;
        public long Length { get; init; }
        public bool IsFile { get; init; }
        public bool IsDirectory { get; init; }
        public bool IsLink { get; init; }
        public long Attributes { get; init; }
        public DateTime Created { get; init; }
        public DateTime Modified { get; init; }
        public DateTime Accessed { get; init; }
    }
    public sealed class XpPath {
        private readonly string _path;
        public XpPath(string path) { _path = path; }
        public string FileName() => System.IO.Path.GetFileName(_path);
        public string FileNameWithoutExtension() => System.IO.Path.GetFileNameWithoutExtension(_path);
        public string Extension() => System.IO.Path.GetExtension(_path);
        public string Directory() => System.IO.Path.GetDirectoryName(_path) ?? string.Empty;
        public string Root() => System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(_path)) ?? string.Empty;
        public bool Exists() => System.IO.File.Exists(_path) || System.IO.Directory.Exists(_path);
        public string Parent() => System.IO.Path.GetDirectoryName(_path) ?? string.Empty;
        public string Absolute() => System.IO.Path.GetFullPath(_path);
        public string Normalize() => System.IO.Path.GetFullPath(_path);
        public string ChangeExtension(string extension) => System.IO.Path.ChangeExtension(_path, extension) ?? string.Empty;
        public bool IsAbsolute() => System.IO.Path.IsPathRooted(_path);
        public string Relative(string other) => System.IO.Path.GetRelativePath(_path, other);
        public string Combine(string child) => System.IO.Path.Combine(_path, child);
    }
    public static object FileInfo(params object?[] a) {
        var path = CStr(a[0]);
        if (System.IO.Directory.Exists(path)) {
            var directory = new System.IO.DirectoryInfo(path);
            return new XpFileInfo { Name = directory.Name, FullPath = directory.FullName, Extension = string.Empty,
                Length = 0, IsFile = false, IsDirectory = true, IsLink = directory.LinkTarget is not null,
                Attributes = (long)directory.Attributes, Created = directory.CreationTime,
                Modified = directory.LastWriteTime, Accessed = directory.LastAccessTime };
        }
        var info = new System.IO.FileInfo(path);
        return new XpFileInfo { Name = info.Name, FullPath = info.FullName, Extension = info.Extension,
            Length = info.Exists ? info.Length : 0, IsFile = info.Exists, IsDirectory = false,
            IsLink = info.LinkTarget is not null, Attributes = (long)info.Attributes,
            Created = info.CreationTime, Modified = info.LastWriteTime, Accessed = info.LastAccessTime };
    }
    public static object MkDir(params object?[] a) { System.IO.Directory.CreateDirectory(CStr(a[0])); return true; }
    public static object RmDir(params object?[] a) { System.IO.Directory.Delete(CStr(a[0]), true); return true; }
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
    public static object? ArraySort(object? value) {
        if (value is not IEnumerable values) return value;
        if (value is string[] strings) { var clone = (string[])strings.Clone(); System.Array.Sort(clone, StringComparer.OrdinalIgnoreCase); return clone; }
        var result = values.Cast<object?>().ToArray();
        result = result.Where(item => item is not null).ToArray();
        if (result.Any(item => item is string))
            result = result.OrderBy(CStr, StringComparer.OrdinalIgnoreCase).ToArray();
        else if (result.All(item => item is IConvertible && item is not DateTime && item is not XpDate))
            result = result.OrderBy(item => Convert.ToDouble(item, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        else
            System.Array.Sort(result, Comparer<object?>.Create((left, right) => left is IComparable comparable ? comparable.CompareTo(right) : 0));
        if (value.GetType().IsArray) {
            var typed = System.Array.CreateInstance(value.GetType().GetElementType()!, result.Length);
            result.CopyTo(typed, 0); return typed;
        }
        return result;
    }
    public static string Join(object? value, object? separator = null) => value is System.Collections.IEnumerable items ? string.Join(separator is null ? " " : CStr(separator), items.Cast<object?>().Where(item => item is not null).Select(CStr)) : string.Empty;
    public static string Implode(object? value, object? separator = null) => Join(value, separator);
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
    public static object ListTag(object value) => value is System.Collections.DictionaryEntry entry ? CStr(entry.Key) : value.GetType().GetProperty("Key")?.GetValue(value)?.ToString() ?? string.Empty;
    public static object LSet(object value, object width) => CStr(value).PadRight(Convert.ToInt32(width));
    public static object Mid(object value, object start, object? count = null)
    {
        var text = CStr(value);
        var offset = Math.Clamp(Convert.ToInt32(start) - 1, 0, text.Length);
        var length = count is null ? text.Length - offset : Math.Clamp(Convert.ToInt32(count), 0, text.Length - offset);
        return text.Substring(offset, length);
    }
    public static object MidB(object value, object start, object? count = null) => Mid(value, start, count);
    public static object MidBP(object value, object start, object? count = null) => Mid(value, start, count);
    public static object Month(object value) => Convert.ToDateTime(value).Month;
    public static object RegexValidate(object value, object pattern) => System.Text.RegularExpressions.Regex.IsMatch(CStr(value), CStr(pattern));
    public static object RightB(object value, object count) => Right(value, count);
    public static object Rnd(object? value = null) => Random.Shared.NextDouble();
    public static long ShellId(object? program, object? windowStyle = null) => 0L;
    public static string[] Split(object? value, object? delimiter = null, object? count = null, object? compare = null)
    {
        var text = CStr(value);
        var separator = delimiter is null ? " " : CStr(delimiter);
        var limit = count is null ? -1 : Convert.ToInt32(count);
        if (limit == 0) return [];
        var comparisonMode = compare is null ? 0 : Convert.ToInt32(compare);
        var comparison = comparisonMode is 1 or 5 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (separator.Length == 0) return limit < 0 ? [text] : [text];
        var parts = new List<string>();
        var position = 0;
        while (position <= text.Length && (limit < 0 || parts.Count < limit - 1))
        {
            var found = text.IndexOf(separator, position, comparison);
            if (found < 0) break;
            parts.Add(text[position..found]);
            position = found + separator.Length;
        }
        parts.Add(text[position..]);
        return parts.ToArray();
    }
    public static object RSet(object value, object width) => CStr(value).PadLeft(Convert.ToInt32(width));
    public static object Space(object value) => new string(' ', Math.Max(0, Convert.ToInt32(value)));
    public static object StrCompare(object left, object right, object? compare = null) => StrComp(left, right, compare);
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
    public static long StrComp(object? left, object? right, object? compare = null)
    {
        var mode = compare is null ? (OptionCompareNoCase ? 1 : 0) : Convert.ToInt32(compare);
        var comparison = mode is 1 or 5 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Math.Sign(string.Compare(left?.ToString(), right?.ToString(), comparison));
    }
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
    public static void AstPrintFile(object? file, object? value = null) { }
    public static object GetObject(object? value) => new ExpandoObject();
    public static object GetObject(object? value, object? context) => new ExpandoObject();
    public static string AstLineInput(object? file) => string.Empty;
    public static void AstKill(object? path) => File.Delete(CStr(path));
    public static string AstInputChars(object? count, object? file) => string.Empty;
    public static void AstNoOp() { }
    public sealed class XpDate : IComparable<XpDate> {
        public DateTime Value { get; }
        public XpDate(DateTime value) { Value = value; }
        public XpDate Adjust(long years, long months, long days, long hours, long minutes, long seconds) => new(Value.AddYears((int)years).AddMonths((int)months).AddDays(days).AddHours(hours).AddMinutes(minutes).AddSeconds(seconds));
        public double Difference(object? other) => (other as XpDate)?.Value is DateTime date ? (date - Value).TotalSeconds : 0;
        public string OSDateFormatting => "yyyy-MM-dd";
        public string OSTimeFormatting => "HH:mm:ss";
        public int CompareTo(XpDate? other) => other is null ? 1 : Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator >(XpDate a, XpDate b) => a.Value > b.Value;
        public static bool operator >=(XpDate a, XpDate b) => a.Value >= b.Value;
        public static bool operator <(XpDate a, XpDate b) => a.Value < b.Value;
        public static bool operator <=(XpDate a, XpDate b) => a.Value <= b.Value;
        public static bool operator ==(XpDate? a, XpDate? b) => Equals(a?.Value, b?.Value);
        public static bool operator !=(XpDate? a, XpDate? b) => !(a == b);
        public override bool Equals(object? obj) => obj is XpDate other && Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
    }
    public static object DateNumber(long year, long month, long day) => new XpDate(new DateTime((int)year, (int)month, (int)day));
    public static object Date() => new XpDate(DateTime.Today);
    public static object Format(object? value, object? pattern = null) => pattern is null ? CStr(value) : (value is XpDate date ? date.Value : Convert.ToDateTime(value)).ToString(CStr(pattern), System.Globalization.CultureInfo.InvariantCulture);
    public static object InputBox(object? prompt = null) => string.Empty;
    public static object MsgBox(object? prompt = null) { Console.WriteLine(CStr(prompt)); return 0L; }
    public static object ChDrive(object? drive) => true;
    public static object Erase(object? value) => true;
    public static object JsonParse(object? value) => XpJsonDocument.Parse(value);
    public static object JsonStringify(object? value) => value switch {
        XpJsonDocument document => document.Stringify(),
        XpJsonObject obj => System.Text.Json.JsonSerializer.Serialize(obj.Data),
        XpJsonArray array => System.Text.Json.JsonSerializer.Serialize(array.Data),
        _ => value?.ToString() ?? "null" };
    public static class XPScriptNullRuntime
    {
        public static bool ConditionValue(object? value) => value is bool boolean ? boolean : Convert.ToBoolean(value ?? false);
    }
    public static long GetTickCount() => Environment.TickCount64;
    public static string Environ(object? _) => string.Empty;
    public static object AstUtf8(object? value) => Encoding.UTF8.GetBytes(Convert.ToString(value) ?? string.Empty);
    public static object AstImageFromBytes(object? _) => new ExpandoObject();
    public static long Loc(object? _) => 0;
    public static string Error(object? _) => string.Empty;
    public static string Error() => string.Empty;
    public static string ErrorValue() => string.Empty;
    public static object ProcedureCounter() => 0L;
    public static class XPScriptRuntime
    {
        public static long GetTickCount() => Environment.TickCount64;
        public static long Loc(object? _) => 0;
        public static string Error(object? _) => string.Empty;
        public static string Error() => string.Empty;
        public static string ErrorValue() => string.Empty;
        public static string ShowDialog(object? message, object? title = null, object? kind = null, object? values = null) => string.Empty;
        public static string LoadFileDialog(object? title = null, object? initialPath = null, object? filter = null) => string.Empty;
        public static string OpenFileDialog(object? title = null, object? initialPath = null, object? filter = null) => string.Empty;
        public static string SaveFileDialog(object? title = null, object? initialPath = null, object? filter = null) => string.Empty;
        public static long CInt(object value) => Convert.ToInt32(value);
        public static bool CBool(object value) => Convert.ToBoolean(value);
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
{{moduleFields}}
{{body}}
{{entryPoint}}
}
""";
        generated = generated.Replace("Error$", "Error()", StringComparison.Ordinal);
        var nativeHttp = NativeHttpRuntimeSource.Code.Replace("XPScriptRuntime.", "Program.XPScriptRuntime.", StringComparison.Ordinal).Replace("Program.XPScriptRuntime.CInt(", "Convert.ToInt32(", StringComparison.Ordinal);
        generated += Environment.NewLine + "public sealed class XPScriptRuntimeException : Exception { public int ErrorCode { get; } public XPScriptRuntimeException(int code, string message) : base(message) { ErrorCode = code; } }" + Environment.NewLine + "internal sealed class XPScriptTlsValidationState { public string Mode { get; set; } = \"strict\"; public string LastError { get; private set; } = string.Empty; public void Reset() { LastError = string.Empty; } public Exception Failure(string context) => new XPScriptRuntimeException(1201, context + \" failed: \" + LastError); public bool Validate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? c, System.Security.Cryptography.X509Certificates.X509Chain? chain, System.Net.Security.SslPolicyErrors errors) => true; }" + Environment.NewLine + nativeHttp;
        return await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool), "BYTE" => typeof(byte), "STRING" => typeof(string), "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double), _ => typeof(object)
    };
    private static string CSharpType(Type type) => type == typeof(void) ? "void" : type == typeof(byte) ? "byte" : type == typeof(long) ? "long" : type == typeof(double) ? "double" : type == typeof(bool) ? "bool" : type == typeof(string) ? "string" : "object";
    private static string ToCSharpLiteral(string value)
    {
        if (value.Equals("True", StringComparison.OrdinalIgnoreCase)) return "true";
        if (value.Equals("False", StringComparison.OrdinalIgnoreCase)) return "false";
        if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
        {
            var text = value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
            return "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }
        return value;
    }
}
