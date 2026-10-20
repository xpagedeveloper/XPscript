using System.Globalization;
using SymbolDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

public sealed class BoundExpressionEmitter
{
    private readonly Dictionary<Symbol, (string Value, string Tag)> _aliases = [];
    internal bool IsListAlias(Symbol symbol) => _aliases.ContainsKey(symbol);

    internal Action CaptureAliasScope(Action emit)
    {
        var scope = _aliases.ToArray();
        return () =>
        {
            var previous = _aliases.ToArray();
            _aliases.Clear();
            foreach (var pair in scope) _aliases.Add(pair.Key, pair.Value);
            try { emit(); }
            finally
            {
                _aliases.Clear();
                foreach (var pair in previous) _aliases.Add(pair.Key, pair.Value);
            }
        };
    }

    // Alias substitution is lexical: nested ForAll blocks restore the outer
    // binding even when body emission fails.
    internal void WithListAlias(Symbol symbol, string value, string tag, Action emitBody)
    {
        var hadPrevious = _aliases.TryGetValue(symbol, out var previous);
        _aliases[symbol] = (value, tag);
        try { emitBody(); }
        finally
        {
            if (hadPrevious) _aliases[symbol] = previous;
            else _aliases.Remove(symbol);
        }
    }

    public string Emit(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression literal => EmitLiteral(literal),
        BoundConversionExpression conversion => EmitConversion(conversion),
        BoundNameExpression name when name.Symbol is VariableSymbol && name.SemanticType.Name == "Application" => "XPScriptApplicationRuntime.Namespace",
        BoundNameExpression name when name.Symbol is VariableSymbol { Name: "Me" } => "this",
        BoundNameExpression name when name.Symbol is VariableSymbol { Name: "Parent" } => "base",
        BoundNameExpression name => _aliases.TryGetValue(name.Symbol, out var alias) ? alias.Value
            : name.Symbol is LocalSymbol { StaticStorageName: { } storage } ? storage : name.Symbol.Name,
        BoundMemberAccessExpression member when member.Receiver is BoundNameExpression receiver && receiver.Symbol.Name.Equals("Console", StringComparison.OrdinalIgnoreCase) && member.Name.Equals("WriteLine", StringComparison.OrdinalIgnoreCase) => "System.Console.WriteLine",
        BoundMemberAccessExpression member when member.Receiver is BoundNameExpression receiver && receiver.Symbol.Name.Equals("XPJsonDocument", StringComparison.OrdinalIgnoreCase) && member.Name.Equals("Parse", StringComparison.OrdinalIgnoreCase) => "XPScriptNativeJson.Parse",
        BoundMemberAccessExpression member when AstApplicationSupport.PropertyTarget(member.Receiver.SemanticType.Name, member.Name) is { } appTarget => appTarget,
        BoundMemberAccessExpression member when AstApplicationSupport.RuntimeType(member.Receiver.SemanticType.Name) is { } appType =>
            $"(({appType})({Emit(member.Receiver)})).{AstApplicationSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when AstImageSupport.IsImage(member.Receiver.SemanticType.Name) =>
            $"{(member.Receiver is BoundNameExpression { Symbol: TypeSymbol } ? "XPImage" : $"((XPImage)({Emit(member.Receiver)}))")}.{AstImageSupport.MemberName(member.Name)}",
        BoundMemberAccessExpression member when AstHttpSupport.RuntimeType(member.Receiver.SemanticType.Name) is { } httpType =>
            $"(({httpType})({Emit(member.Receiver)})).{AstHttpSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when AstSpreadsheetSupport.RuntimeType(member.Receiver.SemanticType.Name) is { } sheetType =>
            $"(({sheetType})({Emit(member.Receiver)})).{AstSpreadsheetSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when AstCsvSupport.RuntimeType(member.Receiver.SemanticType.Name) is { } csvType =>
            $"{(member.Receiver is BoundNameExpression { Symbol: TypeSymbol } ? csvType : $"(({csvType})({Emit(member.Receiver)}))")}.{AstCsvSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when AstXmlSupport.RequiresElement(member.Receiver.SemanticType.Name, member.Name) =>
            $"XPScriptNativeXml.RequireElement({Emit(member.Receiver)}).{AstXmlSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when AstXmlSupport.RuntimeType(member.Receiver.SemanticType.Name) is { } xmlType =>
            $"{(member.Receiver is BoundNameExpression { Symbol: TypeSymbol } ? xmlType : $"(({xmlType})({Emit(member.Receiver)}))")}.{AstXmlSupport.MemberName(member.Receiver.SemanticType.Name, member.Name)}",
        BoundMemberAccessExpression member when RuntimeObjectType(member.Receiver.SemanticType.Name) is { } jsonType => $"(({jsonType})({Emit(member.Receiver)})).{JsonMemberName(member.Name)}",
        BoundMemberAccessExpression member when member.Receiver is BoundNameExpression receiver && receiver.Symbol.Name.Equals("http", StringComparison.OrdinalIgnoreCase) && member.Receiver.SemanticType.Name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) => $"http.{member.Name}",
        BoundMemberAccessExpression member => $"{(member.Receiver.SemanticType.IsVariant ? $"((dynamic)({Emit(member.Receiver)}))" : Emit(member.Receiver))}.{member.Name}",
        BoundIndexExpression index when index.Expression.SemanticType.IsList => $"{Emit(index.Expression)}[{Emit(index.Index)}]",
        BoundIndexExpression index when index.Expression.SemanticType.IsVariant => $"LSArrayRuntime.Index({Emit(index.Expression)}, {Emit(index.Index)})",
        BoundIndexExpression index when AstCsvSupport.RuntimeType(index.Expression.SemanticType.Name) is { } csvType =>
            $"(({csvType})({Emit(index.Expression)}))[{Emit(index.Index)}]",
        BoundIndexExpression index when AstXmlSupport.RuntimeType(index.Expression.SemanticType.Name) is { } xmlType =>
            $"(({xmlType})({Emit(index.Expression)})).Get({Emit(index.Index)})",
        BoundIndexExpression index when index.Expression.SemanticType.IsArray
            => EmitArrayGet(index),
        BoundIndexExpression index when index.Expression is BoundNameExpression { Symbol: LocalSymbol { IsDynamicArray: true } }
            => EmitArrayGet(index),
        BoundIndexExpression index when index.Expression is BoundNameExpression { Symbol: VariableSymbol variable } && variable.SemanticType?.IsArray == true
            => EmitArrayGet(index),
        BoundIndexExpression index when index.Indices.Count > 1 && index.Expression is BoundNameExpression
            => EmitArrayGet(index),
        BoundIndexExpression index => index.Expression.Type == typeof(object)
            ? $"((dynamic){Emit(index.Expression)})[{Emit(index.Index)}]"
            : index.Expression.Type == typeof(Dictionary<string, object?>)
                ? $"{Emit(index.Expression)}[Convert.ToString({Emit(index.Index)}) ?? string.Empty]"
            : $"{Emit(index.Expression)}[{Emit(index.Index)}]",
        BoundNewExpression @new when @new.SemanticType.IsReferenceCell =>
            $"new {BoundMethodEmitter.CSharpType(@new.SemanticType)}({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.Constructor is not null => $"new Xp{@new.SemanticType.Name}({AstClassConstructorSupport.Arguments(@new.Constructor, @new.Arguments, this)})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase) => "XPScriptNativeHttp.CreateClient()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPHttpRequest", StringComparison.OrdinalIgnoreCase) => "XPScriptNativeHttp.CreateRequest()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPSpreadsheet", StringComparison.OrdinalIgnoreCase) =>
            $"XPScriptSpreadsheetFactory.Create({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when AstImageSupport.IsImage(@new.SemanticType.Name) =>
            $"new XPImage({string.Join(", ", @new.Arguments.Select((arg, i) => AstImageSupport.ConvertArgument(Emit(arg), i < 2 ? "int" : "string")))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) => $"new XpPath({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) => @new.Arguments.Count == 0
            ? "XPScriptNativeCsv.CreateDocument()" : $"XPScriptNativeCsv.Parse({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPXmlDocument", StringComparison.OrdinalIgnoreCase) => @new.Arguments.Count == 0
            ? "XPScriptNativeXml.CreateDocument()" : $"XPScriptNativeXml.Parse({Emit(@new.Arguments[0])})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPXmlElement", StringComparison.OrdinalIgnoreCase) =>
            $"XPScriptNativeXml.CreateElement({Emit(@new.Arguments[0])})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("SystemInventory", StringComparison.OrdinalIgnoreCase) =>
            "XPScriptSystemInventoryFactory.Create()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase) =>
            $"new XPScriptDbSqlite({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPDbMySql", StringComparison.OrdinalIgnoreCase) || @new.SemanticType.Name.Equals("XPDBMySQL", StringComparison.OrdinalIgnoreCase) =>
            $"new XPScriptDbMySql({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase) || @new.SemanticType.Name.Equals("XPDBMsSQL", StringComparison.OrdinalIgnoreCase) =>
            $"new XPScriptDbMsSql({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPDbSupabase", StringComparison.OrdinalIgnoreCase) || @new.SemanticType.Name.Equals("XPSupabaseDB", StringComparison.OrdinalIgnoreCase) =>
            $"new XPScriptDbSupabase({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPJsonArray", StringComparison.OrdinalIgnoreCase) => "new XpJsonArray()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPJsonDocument", StringComparison.OrdinalIgnoreCase) => "new XpJsonDocument()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPJSONNavigator", StringComparison.OrdinalIgnoreCase) => $"new XPJSONNavigator({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) => $"new NotesHTTPRequest({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name is "XPJSONObject" => "LSJsonHttpRuntime.CreateJSONObject()",
        BoundNewExpression @new when @new.SemanticType.Name is "XPJSONArray" => "LSJsonHttpRuntime.CreateJSONArray()",
        BoundNewExpression @new when @new.SemanticType.Name is "XPJSONElement" => @new.Arguments.Count switch
        {
            0 => "LSJsonHttpRuntime.CreateJSONElement()",
            1 => $"LSJsonHttpRuntime.CreateJSONElement({Emit(@new.Arguments[0])})",
            _ => $"LSJsonHttpRuntime.CreateJSONElement({Emit(@new.Arguments[1])}, {Emit(@new.Arguments[0])})"
        },
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPJsonObject", StringComparison.OrdinalIgnoreCase) => "new XpJsonObject()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPJsonSchema", StringComparison.OrdinalIgnoreCase) => "new XPScriptJsonSchema()",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPAi", StringComparison.OrdinalIgnoreCase) => $"new XPScriptAi({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.Name.Equals("XPAITool", StringComparison.OrdinalIgnoreCase) => $"new XPScriptAiTool({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new when @new.SemanticType.IsObject && @new.Arguments.Count == 0 => "new System.Dynamic.ExpandoObject()",
        BoundNewExpression @new when @new.Type == typeof(object) && @new.SemanticType.Name.Length > 0 && char.IsUpper(@new.SemanticType.Name[0]) => $"new Xp{@new.SemanticType.Name}({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundNewExpression @new => @new.Type == typeof(object)
            ? "new System.Dynamic.ExpandoObject()"
            : $"new {@new.Type.Name}({string.Join(", ", @new.Arguments.Select(Emit))})",
        BoundIndexedPropertyExpression indexed => $"{Emit(indexed.Receiver)}.{indexed.Property.GetterName ?? indexed.Property.Name.Split('.').Last()}({EmitIndexedArguments(indexed)})",
        BoundCallExpression call => EmitCall(call),
        BoundUnaryExpression unary => unary.OperatorKind == SyntaxKind.NotKeyword && unary.Type == typeof(object)
            ? $"(!Convert.ToBoolean({Emit(unary.Operand)}))"
            : $"({EmitUnaryOperator(unary.OperatorKind, unary.Type)}{Emit(unary.Operand)})",
        BoundBinaryExpression binary => EmitBinary(binary),
        _ => throw new NotSupportedException($"Emission is not implemented for {expression.Kind}.")
    };

    internal static string? RuntimeObjectType(string name) => name.Trim().ToUpperInvariant() switch
    {
        "BYTE" or "INTEGER" or "LONG" => "long",
        "SINGLE" or "DOUBLE" or "CURRENCY" => "double",
        "BOOLEAN" => "bool",
        "STRING" => "string",
        "VARIANT" => "object",
        _ => RuntimeObjectTypeCore(name)
    };

    private static string? RuntimeObjectTypeCore(string name) => AstApplicationSupport.RuntimeType(name) ?? AstSpreadsheetSupport.RuntimeType(name) ?? AstHttpSupport.RuntimeType(name) ?? AstNotesJsonSupport.RuntimeType(name) ?? AstSystemInventorySupport.RuntimeType(name) ?? AstSqliteSupport.RuntimeType(name) ?? AstMySqlSupport.RuntimeType(name) ?? AstMsSqlSupport.RuntimeType(name) ?? AstSupabaseSupport.RuntimeType(name) ?? AstAttachmentSupport.RuntimeType(name) ?? AstXmlSupport.RuntimeType(name) ?? AstCsvSupport.RuntimeType(name) ?? (name.ToUpperInvariant() switch
    {
        "XPIMAGE" => "XPImage",
        "XPJSONDOCUMENT" => "XPScriptJsonDocument",
        "XPJSONOBJECT" => "XPScriptJsonObject",
        "XPJSONARRAY" => "XPScriptJsonArray",
        "XPJSONELEMENT" => "XPScriptJsonElement",
        "XPJSONSCHEMA" => "XPScriptJsonSchema",
        "XPAI" => "XPScriptAi",
        "XPAIRESPONSE" => "XPScriptAiResponse",
        "XPAITOOL" => "XPScriptAiTool",
        "XPAITOOLFUNCTION" => "XPScriptAiToolFunction",
        "XPAITOOLCALL" => "XPScriptAiToolCall",
        _ => null
    });

    internal string IterationValue(BoundNameExpression variable, string value) => RuntimeObjectType(variable.SemanticType.Name) is { } nativeType
        ? $"({nativeType})({value})" : variable.Type == typeof(long) ? $"XPScriptRuntime.CLng({value})"
        : variable.Type == typeof(string) ? $"(string)({value})" : value;

    private static string JsonMemberName(string name) => name.ToUpperInvariant() switch
    {
        "ROOT" => "Root", "TYPE" => "Type", "VALUE" => "Value", "COUNT" => "Count",
        "GET" => "Get", "SET" => "Set", "ADD" => "Add", "REMOVE" => "Remove",
        "REMOVEAT" => "RemoveAt", "CONTAINS" => "Contains", "STRINGIFY" => "Stringify",
        "ASOBJECT" => "AsObject", "ASARRAY" => "AsArray", "TOOBJECT" => "ToObject",
        _ => name
    };

    private string EmitArrayGet(BoundIndexExpression index)
    {
        var access = $"LSArrayRuntime.Get({Emit(index.Expression)}, {EmitArrayIndexArguments(index)})";
        return index.Type == typeof(long) ? $"Convert.ToInt64({access})"
            : index.Type == typeof(int) ? $"Convert.ToInt32({access})"
            : index.Type == typeof(double) ? $"Convert.ToDouble({access})"
            : index.Type == typeof(bool) ? $"Convert.ToBoolean({access})"
            : index.Type == typeof(string) ? $"(Convert.ToString({access}) ?? string.Empty)"
            : $"(({BoundMethodEmitter.CSharpType(index.SemanticType)})({access}))";
    }

    internal string EmitArrayIndexArguments(BoundIndexExpression index) =>
        string.Join(", ", index.Indices.Select(Emit));

    internal string EmitIndexedArguments(BoundIndexedPropertyExpression indexed) =>
        EmitReferenceArguments(indexed.Arguments, indexed.Property.ByRefIndexes);

    internal string EmitReferenceArguments(IReadOnlyList<BoundExpression> arguments, IReadOnlyList<bool>? byRef) =>
        string.Join(", ", arguments.Select((argument, i) => byRef?[i] == true
            ? "ref " + EmitIndexedReference(argument) : Emit(argument)));

    private string EmitIndexedReference(BoundExpression argument) => argument switch
    {
        BoundIndexExpression index when index.Expression.SemanticType.IsList =>
            $"{Emit(index.Expression)}.Reference({Emit(index.Index)})",
        BoundIndexExpression index when index.Expression.SemanticType.IsArray =>
            $"LSArrayRuntime.Reference<{BoundMethodEmitter.CSharpType(index.SemanticType)}>({Emit(index.Expression)}, {EmitArrayIndexArguments(index)})",
        _ => Emit(argument)
    };

    private string EmitCall(BoundCallExpression call)
    {
        if (call.Target is BoundMemberAccessExpression aiMember && aiMember.Receiver.SemanticType.Name.Equals("XPAi", StringComparison.OrdinalIgnoreCase) &&
            call.Function.Name is "AddTool" or "RemoveTool" or "ClearTools" or "HasTool" or "GetTool" or "GetToolNames" or "ToolCount")
        {
            var aiArguments = call.Arguments.Select(Emit).Prepend(Emit(aiMember.Receiver));
            return $"XPScriptAiToolRegistry.{call.Function.Name}({string.Join(", ", aiArguments)})";
        }
        if (call.Function.NativeParameterTypes is { } nativeTypes)
        {
            var nativeArguments = call.Arguments.Select((arg, i) => AstImageSupport.ConvertArgument(Emit(arg), nativeTypes[i]));
            if (call.Function.NativeReceiverArgument && call.Target is BoundMemberAccessExpression extension)
                nativeArguments = nativeArguments.Prepend(Emit(extension.Receiver));
            return $"{(call.Function.NativeTargetName ?? Emit(call.Target!))}({string.Join(", ", nativeArguments)})";
        }
        if (BoundListCallLowering.RequiresWrapper(call))
        {
            var wrapperArguments = new List<string>();
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                var argument = call.Arguments[i];
                var byRef = call.Function.ByRefParameters?[i] == true;
                var parameterType = BoundMethodEmitter.CSharpType(call.Function.ParameterTypes[i]);
                if (byRef && argument is BoundIndexExpression { Expression.SemanticType.IsList: true } element)
                {
                    wrapperArguments.Add($"new LSListByRef<{parameterType}>({Emit(element.Expression)}, {Emit(element.Index)})");
                }
                else if (byRef && argument is BoundNameExpression listAlias && _aliases.TryGetValue(listAlias.Symbol, out var location))
                    wrapperArguments.Add($"new LSListByRef<{parameterType}>({location.Value[..^".Value".Length]})");
                else wrapperArguments.Add((byRef ? "ref " : "") + Emit(argument));
            }
            return $"{BoundListCallLowering.Name(call)}({string.Join(", ", wrapperArguments)})";
        }
        var tagArgument = call.Arguments.Count >= 1 ? call.Arguments[0] : null;
        if (tagArgument is BoundConversionExpression conversion) tagArgument = conversion.Expression;
        // IsElement inspects the tag, not the indexed value: reading a missing
        // element first would throw instead of returning False.
        if (call.Target is null && call.Function.Name.Equals("IsElement", StringComparison.OrdinalIgnoreCase) &&
            tagArgument is BoundIndexExpression index && index.Expression.SemanticType.IsList)
            return $"{Emit(index.Expression)}.ContainsTag({Emit(index.Index)})";
        if (call.Target is null && call.Function.Name.Equals("IsElement", StringComparison.OrdinalIgnoreCase) &&
            tagArgument is BoundIndexExpression dynamicIndex && dynamicIndex.Expression.SemanticType.IsVariant)
            return $"LSListRuntime.ContainsTag({Emit(dynamicIndex.Expression)}, {Emit(dynamicIndex.Index)})";
        if (call.Target is null && call.Function.Name.Equals("ListTag", StringComparison.OrdinalIgnoreCase) &&
            tagArgument is BoundNameExpression name && _aliases.TryGetValue(name.Symbol, out var alias))
            return alias.Tag;
        if (call.Target is null && call.Arguments.Count is 1 or 2 &&
            tagArgument?.SemanticType.IsArray == true)
        {
            if (call.Function.Name.Equals("LBound", StringComparison.OrdinalIgnoreCase))
            {
                return call.Arguments.Count == 2
                    ? $"LSArrayRuntime.LBound({Emit(tagArgument)}, Convert.ToInt32({Emit(call.Arguments[1])}))"
                    : $"LSArrayRuntime.LBound({Emit(tagArgument)})";
            }
            if (call.Function.Name.Equals("UBound", StringComparison.OrdinalIgnoreCase))
            {
                return call.Arguments.Count == 2
                    ? $"LSArrayRuntime.UBound({Emit(tagArgument)}, Convert.ToInt32({Emit(call.Arguments[1])}))"
                    : $"LSArrayRuntime.UBound({Emit(tagArgument)})";
            }
        }
        var arguments = call.Arguments.Select((argument, index) =>
        {
            var byRef = call.Function.ByRefParameters?[index] == true;
            if (byRef && argument is BoundMemberAccessExpression { IsFieldStorage: true, Receiver.SemanticType.IsReferenceCell: true })
                return "ref " + Emit(argument);
            if (byRef && argument is BoundIndexExpression { Expression.SemanticType.IsArray: true })
                return "ref " + EmitIndexedReference(argument);
            if (byRef && argument is not BoundNameExpression)
                throw new NotSupportedException("ByRef conversion requires temporary and copy-back lowering.");
            if (!byRef && call.Function.SemanticParameterTypes is null && call.Function.ParameterTypes[index].IsArray)
                return $"({BoundMethodEmitter.CSharpType(call.Function.ParameterTypes[index])})({Emit(argument)})";
            if (!byRef && call.Function.SemanticParameterTypes is { } parameterTypes &&
                RuntimeObjectType(parameterTypes[index].Name) is { } nativeType)
                return $"({nativeType})({Emit(argument)})";
            return (byRef ? "ref " : "") + Emit(argument);
        });
        var functionName = call.Function.Name.EndsWith("$", StringComparison.Ordinal) ? call.Function.Name.TrimEnd('$') : call.Function.Name;
        if (functionName.Equals("JsonParse", StringComparison.OrdinalIgnoreCase)) functionName = "XPScriptNativeJson.Parse";
        if (call.Target is null && functionName.Equals("CDate", StringComparison.OrdinalIgnoreCase))
            return $"Program.CDate({string.Join(", ", arguments)})";
        if (call.Target is null && functionName.ToUpperInvariant() is "CSTR" or "CINT" or "CLNG" or "CDBL" or "CBOOL" or "CBYTE" or "CCUR" or "CSNG" or "CVAR" or "CDATE" or "LEN" or "LENB" or "TRIM" or "UCASE" or "LCASE" or "ENVIRON" or "ISARRAY" or "ISDATE" or "ISNUMERIC")
        {
            functionName = "XPScriptRuntime." + (functionName.ToUpperInvariant() switch
            {
                "CSTR" => "CStr", "CINT" => "CInt", "CLNG" => "CLng", "CDBL" => "CDbl", "CBOOL" => "CBool",
                "CBYTE" => "CByte", "CCUR" => "CCur", "CSNG" => "CSng", "CVAR" => "CVar", "CDATE" => "CDate",
                "LEN" => "Len", "LENB" => "LenB", "TRIM" => "Trim", "UCASE" => "UCase", "LCASE" => "LCase",
                "ENVIRON" => "Environ", "ISARRAY" => "IsArray", "ISDATE" => "IsDate", "ISNUMERIC" => "IsNumeric",
                _ => functionName
            });
        }
        if (functionName.Equals("Error", StringComparison.OrdinalIgnoreCase) || functionName.Equals("GetTickCount", StringComparison.OrdinalIgnoreCase) || functionName.Equals("Loc", StringComparison.OrdinalIgnoreCase) || functionName.Equals("ShowDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("LoadFileDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("OpenFileDialog", StringComparison.OrdinalIgnoreCase) || functionName.Equals("SaveFileDialog", StringComparison.OrdinalIgnoreCase))
            functionName = "Program.AstCompatibilityHelpers." + functionName;
        return $"{(call.Function.NativeTargetName ?? (call.Target is null ? functionName : Emit(call.Target)))}({string.Join(", ", arguments)})";
    }

    private string EmitBinary(BoundBinaryExpression binary) => binary.OperatorKind == SyntaxKind.IsKeyword
        ? $"object.ReferenceEquals({Emit(binary.Left)}, {Emit(binary.Right)})"
        : binary.Type == typeof(object)
        ? $"((dynamic)({Emit(binary.Left)}) {EmitBinaryOperator(binary.OperatorKind, binary.Type)} (dynamic)({Emit(binary.Right)}))"
        : binary.OperatorKind == SyntaxKind.PlusToken && binary.Type == typeof(string)
        ? $"(Convert.ToString({Emit(binary.Left)}) + Convert.ToString({Emit(binary.Right)}))"
        : binary.OperatorKind == SyntaxKind.LikeKeyword
            ? $"Program.AstCompatibilityHelpers.Like({Emit(binary.Left)}, {Emit(binary.Right)})"
            : $"({Emit(binary.Left)} {EmitBinaryOperator(binary.OperatorKind, binary.Type)} {Emit(binary.Right)})";

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
        if (expression.SemanticType.IsArray && expression.Conversion.Kind == ConversionKind.FromVariant)
            return $"LSArrayRuntime.RequireArray<{BoundMethodEmitter.CSharpType(expression.SemanticType.ElementType!)}>({value}, {expression.SemanticType.ArrayRank})";
        if (expression.SemanticType.IsList && expression.Conversion.Kind == ConversionKind.FromVariant)
            return $"(LSList<{BoundMethodEmitter.CSharpType(expression.SemanticType.ElementType!)}>)({value})";
        if (RuntimeObjectType(expression.SemanticType.Name) is { } jsonType && expression.Conversion.Kind is ConversionKind.FromVariant or ConversionKind.ReferenceUpcast)
            return $"({jsonType})({value})";
        if (expression.Type == typeof(object) && expression.Conversion.Kind == ConversionKind.FromVariant)
            return expression.SemanticType.IsVariant || expression.SemanticType.IsObject ? value
                : $"({BoundMethodEmitter.CSharpType(expression.SemanticType)})({value})";
        if (expression.Expression is BoundNewExpression && expression.Type != typeof(object))
            return value;
        return expression.Conversion.Kind switch
        {
            ConversionKind.Identity or ConversionKind.EmptyToVariant or ConversionKind.NullToVariant or ConversionKind.NothingToObject => value,
            ConversionKind.ReferenceUpcast => $"(({BoundMethodEmitter.CSharpType(expression.SemanticType)})({value}))",
            ConversionKind.NumericWidening => $"((double)({value}))",
            ConversionKind.ToVariant => $"((object)({value}))",
            ConversionKind.ToObject when expression.Type != typeof(object) => value,
            ConversionKind.ToObject => $"((object)({value}))",
            ConversionKind.FromVariant => $"{(ConversionMethod(expression.Type).StartsWith("Program.", StringComparison.Ordinal) ? string.Empty : "XPScriptRuntime.")}{ConversionMethod(expression.Type)}({value})",
            _ => throw new NotSupportedException($"Conversion {expression.Conversion.Kind} is not supported.")
        };
    }

    private static string ConversionMethod(Type type) => type == typeof(string) ? "CStr"
        : type == typeof(long) ? "CLng"
        : type == typeof(double) ? "CDbl"
        : type == typeof(bool) ? "CBool"
        : type == typeof(byte) ? "Program.CByte"
        : type == typeof(object) ? "CObj"
        : throw new NotSupportedException($"Variant conversion to {type} is not supported.");

    private static string EmitUnaryOperator(SyntaxKind kind, Type type) => kind switch
    {
        SyntaxKind.NotKeyword => type == typeof(long) ? "~" : "!",
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        _ => throw new NotSupportedException($"Unary operator {kind} is not supported.")
    };

    private static string EmitBinaryOperator(SyntaxKind kind, Type resultType) => kind switch
    {
        SyntaxKind.IsKeyword => "ReferenceEquals",
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        SyntaxKind.StarToken => "*",
        SyntaxKind.SlashToken => "/",
        SyntaxKind.AmpersandToken => "+",
        SyntaxKind.LessToken => "<",
        SyntaxKind.LessOrEqualsToken => "<=",
        SyntaxKind.GreaterToken => ">",
        SyntaxKind.GreaterOrEqualsToken => ">=",
        SyntaxKind.AndKeyword => resultType == typeof(bool) ? "&&" : "&",
        SyntaxKind.OrKeyword => resultType == typeof(bool) ? "||" : "|",
        SyntaxKind.EqualsToken => "==",
        SyntaxKind.LessGreaterToken => "!=",
        _ => throw new NotSupportedException($"Binary operator {kind} is not supported.")
    };
}
