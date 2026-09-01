using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class XPScriptTranspiler
{
    public string Transpile(string source, string sourceName) =>
        Transpile(source, sourceName, CompilerDriver.CurrentRuntimeIdentifier());

    public string Transpile(string source, string sourceName, string runtimeIdentifier)
    {
        var serviceDefinition = XpsServiceScriptParser.Parse(source, sourceName);
        var includeResult = new IncludeSourcePreprocessor().Transform(serviceDefinition.Source, sourceName);
        try
        {
            var generated = TranspileExpanded(includeResult.Source, sourceName, runtimeIdentifier, includeResult.Map);
            if (!serviceDefinition.IsService) return generated;

            generated = XpsServiceGeneratedCodePostProcessor.Transform(generated, serviceDefinition);
            generated += "\n\n" + XpsServiceRuntimeSource.Build(serviceDefinition) + "\n";
            generated = XpsWindowsServiceHostPostProcessor.Transform(generated, serviceDefinition);
            return generated;
        }
        catch (CompilerException ex)
        {
            var remapped = SourceMapDiagnostics.Remap(ex.Message, sourceName, includeResult.Map);
            if (string.Equals(remapped, ex.Message, StringComparison.Ordinal)) throw;
            throw new CompilerException(remapped);
        }
    }

    public string TranspileRestricted(
        string source,
        string sourceName,
        string runtimeIdentifier,
        IEnumerable<string> allowedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceRoots);
        using var scope = IncludeSecurityContext.Push(allowedSourceRoots);
        return Transpile(source, sourceName, runtimeIdentifier);
    }

    private static string TranspileExpanded(string source, string sourceName, string runtimeIdentifier, SourceMap sourceMap)
    {
        source = new MultilineStringPreprocessor().Transform(source, sourceName);
        source = new EscapedQuotePreprocessor().Transform(source);
        source = new EvaluateByValSyntaxPreprocessor().Transform(source);
        source = new ReservedIdentifierPreprocessor().Transform(source);
        new DateComparisonValidator().Validate(source, sourceName);
        new ClassOverloadValidator().Validate(source, sourceName);
        new SourceTypeValidator().Validate(source, sourceName);
        source = new IfLayoutPreprocessor().Transform(source);
        source = new ParameterlessProcedureHeaderPreprocessor().Transform(source);
        source = new SourceLineContinuationPreprocessor().Transform(source);
        source = new ParameterPassingPreprocessor().Transform(source);
        source = new SourceLineMarkerPreprocessor().Transform(source, sourceMap, sourceName);
        source = new HclPrintFormattingPreprocessor().Transform(source);
        source = new StatementSeparatorPreprocessor().Transform(source);
        source = new NativeLibraryPlatformPreprocessor(runtimeIdentifier).Transform(source);
        source = new NativeInteropSafetyPreprocessor().Transform(source);

        var udtValues = new UdtValueSemanticsPreprocessor();
        source = udtValues.Transform(source);
        source = new TypeDeclarationPreprocessor().Transform(source);
        source = new LanguageExtensionsPreprocessor().Transform(source);
        source = new PropertyLetCompatibilityPreprocessor().Transform(source);
        source = new IndexedPropertyPreprocessor().Transform(source);
        source = new ObjectFunctionSetPreprocessor().Transform(source);
        source = new NativeHttpJsonPreprocessor().Transform(source);
        source = source.Replace("XPScriptDatabaseAttachmentRuntime.ForSqlite(", "XPScriptDatabaseAttachmentApi.ForSqlite(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForMsSql(", "XPScriptDatabaseAttachmentApi.ForMsSql(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForSupabase(", "XPScriptDatabaseAttachmentApi.ForSupabase(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForDomino(", "XPScriptDatabaseAttachmentApi.ForDomino(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.SetSupabaseBucket(", "XPScriptDatabaseAttachmentApi.SetSupabaseBucket(", StringComparison.Ordinal);
        var usesSqlite = source.Contains("XPScriptDbSqlite", StringComparison.Ordinal);
        var usesMsSql = source.Contains("XPScriptDbMsSql", StringComparison.Ordinal);
        var usesAi = source.Contains("XPScriptAi", StringComparison.Ordinal);
        if (usesSqlite && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPDBSQLite is not available for browser-wasm targets.");
        if (usesMsSql && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPDbMsSql is not available for browser-wasm targets.");
        if (usesAi && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPAi is not available for browser-wasm targets. Keep AI credentials and requests on the server.");
        var moduleObjects = new ModuleObjectGlobalsPreprocessor(udtValues.TypeNames);
        source = moduleObjects.Transform(source);
        var moduleGlobals = new ModuleGlobalsPreprocessor(udtValues.TypeNames);
        source = moduleGlobals.Transform(source);
        source = new DateObjectPreprocessor().Transform(source);
        source = new TypeCoercionPreprocessor().Transform(source);
        source = new StringConcatenationPreprocessor().Transform(source);
        source = new FileIoExtensionsPreprocessor().Transform(source);

        var operatorArray = new OperatorArrayCompatibilityPreprocessor();
        source = operatorArray.NormalizeSource(source);
        var protectedSource = ProtectStringLiterals(source, out var protectedStrings);
        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new CrossPlatformPreprocessor().Transform(protectedSource);
        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);
        protectedSource = new ApplicationObjectPreprocessor().Transform(protectedSource);
        protectedSource = RewriteListPresenceChecks(protectedSource);
        protectedSource = operatorArray.TransformProtectedSource(protectedSource);
        protectedSource = new TextIoCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ReferenceRuntimeExtensionsPreprocessor().Transform(protectedSource);
        protectedSource = new XPScriptEvaluatePreprocessor().Transform(protectedSource);
        protectedSource = new JsonHttpCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ExtendedCompatibilityTranspiler().Transform(protectedSource);
        var generated = new CoreCompatibilityTranspiler().Transpile(protectedSource, sourceName);
        generated = new ParameterPassingPostProcessor().Transform(generated);
        generated = new NativeInteropDiagnosticsPostProcessor().Transform(generated);
        generated = moduleGlobals.Inject(generated);

        generated = Regex.Replace(generated, @"(?<=\S)\s+\+\+\s+(?=\S)", " && ");

        generated += "\n\n" + CoreControlRuntimeSource.Code + "\n";
        generated += "\n\n" + SourceLineRuntimeSource.Code + "\n";
        generated += "\n\n" + NativeInteropRuntimeSource.Code + "\n";
        generated += "\n\n" + FileSystemPortabilityRuntimeSource.Code + "\n";
        generated += "\n\n" + ApplicationRuntimeSource.Code + "\n";
        generated += "\n\n" + CallbackRuntimeSource.Code + "\n";
        generated += "\n\n" + ExtendedCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + CrossPlatformRuntimeSource.Code + "\n";
        generated += "\n\n" + EvaluateArgumentRuntimeSource.Code + "\n";
        generated += "\n\n" + NormalizeEvaluateRuntime(XPScriptEvaluateRuntimeSource.Code) + "\n";
        generated += "\n\n" + DateObjectRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonHttpCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonNodesSerializerShimSource.Code + "\n";
        generated += "\n\n" + TextIoCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + FileIoExtensionsRuntimeSource.Code + "\n";
        generated += "\n\n" + ReferenceRuntimeExtensionsSource.Code + "\n";
        generated += "\n\n" + NativeHttpRuntimeSource.Code + "\n";
        generated += "\n\n" + AsyncHttpRuntimeSource.Code + "\n";
        generated += "\n\n" + NativeJsonRuntimeSource.Code + "\n";
        if (usesSqlite) generated += "\n\n" + SqliteDbRuntimeSource.Code + "\n";
        if (usesMsSql) generated += "\n\n" + MsSqlDbRuntimeSource.Code + "\n";
        if (usesAi) generated += "\n\n" + AiRuntimeSource.Code + "\n";
        generated += "\n\n" + HttpDbRuntimeSource.Code + "\n";
        generated += "\n\n" + DatabaseUiDataSourceRuntimeSource.Build(usesSqlite, usesMsSql) + "\n";
        generated += "\n\n" + DatabaseAttachmentRuntimeV2Source.Build(usesSqlite, usesMsSql) + "\n";
        generated += "\n\n" + DatabaseAttachmentRuntimeV3Source.Code + "\n";
        generated += "\n\n" + ModuleArrayRuntimeSource.Code + "\n";
        generated += "\n\n" + UdtArrayRuntimeSource.Code + "\n";
        generated += "\n\n" + ModuleObjectRuntimeSource.Code + "\n";
        generated += "\n\n" + OperatorArrayCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + TypeCoercionRuntimeSource.Code + "\n";
        generated += "\n\n" + VariantIndexRuntimeSource.Code + "\n";
        generated += "\n\n" + HclSelectedCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + HclArrayReplaceRuntimeSource.Code + "\n";
        generated += "\n\n" + HclPlatformStringRuntimeSource.Code + "\n";
        generated += "\n\n" + HclPrintFormattingRuntimeSource.Code + "\n";
        generated += "\n\n" + HclIsDefinedCompatibilityRuntimeSource.Code + "\n";

        if (usesAi) generated = new AiSessionRuntimePostProcessor().Transform(generated);
        generated = new UIExtensionDesktopPostProcessor().Transform(generated);
        generated = new BrowserWasmHttpCsrfPostProcessor(runtimeIdentifier).Transform(generated);
        generated = new FileSystemPortabilityPostProcessor().Transform(generated);

        generated = generated.Replace(
            "XPScriptRuntime.SetArgs(args);",
            $"XPScriptRuntime.SetArgs(args);\n        XPScriptFileSystemRuntime.SetScriptDirectory(\"{EscapeCSharpString(GetSourceDirectory(sourceName))}\");\n        XPNativeInteropRuntime.Initialize();\n        XPScriptApplicationRuntime.SetArgs(args);\n        LSOperatorArrayRuntime.SetCompareNoCase({operatorArray.CompareNoCase.ToString().ToLowerInvariant()});",
            StringComparison.Ordinal);

        generated = generated.Replace("text.StartsWith('/', StringComparison.Ordinal)", "text.StartsWith(\"/\", StringComparison.Ordinal)", StringComparison.Ordinal);
        generated = generated.Replace("byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),", "byte[] requestBytes => System.Text.Encoding.UTF8.GetString(requestBytes),", StringComparison.Ordinal);
        generated = generated.Replace("using System.Text.RegularExpressions;", "using System.Text.RegularExpressions;\nusing System.Runtime.InteropServices;", StringComparison.Ordinal);
        generated = Regex.Replace(generated, @"(?m)^\s*__lsErrCtx\.Statement\s*=\s*\d+;\s*\r?$\n?", "");
        generated = ScopeErrorProtection(generated);

        foreach (var item in protectedStrings) generated = generated.Replace(item.Key, item.Value, StringComparison.Ordinal);
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    private static string NormalizeEvaluateRuntime(string code) => code
        .Replace("\"isobject\" when args.Count == 1 => XPScriptRuntime.IsObject(Arg(0)),",
            "\"isobject\" when args.Count == 1 => XPScriptNullRuntime.IsObject(Arg(0)),", StringComparison.Ordinal)
        .Replace("\"isscalar\" when args.Count == 1 => Arg(0) is not LSArray && XPScriptRuntime.IsScalar(Arg(0)),",
            "\"isscalar\" when args.Count == 1 => Arg(0) is not LSArray && XPScriptNullRuntime.IsScalar(Arg(0)),", StringComparison.Ordinal);

    private static string GetSourceDirectory(string sourceName)
    {
        var fullSourcePath = Path.GetFullPath(sourceName);
        return Path.GetDirectoryName(fullSourcePath) ?? Environment.CurrentDirectory;
    }

    private static string EscapeCSharpString(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    public string Transpile(string source) => Transpile(source, "input.xps");

    private static string RewriteListPresenceChecks(string source)
    {
        var listNames = Regex.Matches(source, @"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+List\s+As\s+[A-Za-z_]\w*\s*$")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToArray();

        foreach (var listName in listNames)
        {
            source = Regex.Replace(
                source,
                $@"\bIsElement\s*\(\s*{Regex.Escape(listName)}\s*\((?<key>[^()]*)\)\s*\)",
                m => $"{listName}.ContainsTag({m.Groups["key"].Value})",
                RegexOptions.IgnoreCase);
        }
        return source;
    }

    private static string ProtectStringLiterals(string source, out Dictionary<string, string> replacements)
    {
        replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '"') { output.Append(source[i]); continue; }
            output.Append('"');
            var inner = new StringBuilder(); i++;
            for (; i < source.Length; i++)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { inner.Append("\"\""); i++; continue; }
                    break;
                }
                inner.Append(source[i]);
            }
            if (i >= source.Length) throw new CompilerException("Unterminated string literal.");
            var marker = $"__XPSCRIPT_STRING_{replacements.Count:D6}__";
            replacements[marker] = EscapeForGeneratedCSharpString(inner.ToString());
            output.Append(marker).Append('"');
        }
        return output.ToString();
    }

    private static string EscapeForGeneratedCSharpString(string sourceInner)
    {
        var decoded = sourceInner.Replace("\"\"", "\"", StringComparison.Ordinal);
        return decoded.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string ScopeErrorProtection(string generated)
    {
        var activationIndexes = new[]
        {
            generated.IndexOf("LSControlRuntime.SetGoto(__lsErrCtx", StringComparison.Ordinal),
            generated.IndexOf("LSControlRuntime.SetResumeNext(__lsErrCtx", StringComparison.Ordinal)
        }.Where(x => x >= 0).ToArray();
        if (activationIndexes.Length == 0) return generated;

        var activation = activationIndexes.Min(); var prefix = generated[..activation]; var suffix = generated[activation..]; var removedIds = new HashSet<int>();
        var wrapperPattern = new Regex(@"(?m)^(?<indent>[ \t]*)__ls_stmt_before_(?<id>\d+):;\r?\n[ \t]*try \{ (?<statement>.*) \}\r?\n[ \t]*catch \(Exception __lsEx\) \{.*\}\r?\n[ \t]*__ls_stmt_after_\d+:;\r?\n?", RegexOptions.CultureInvariant);
        prefix = wrapperPattern.Replace(prefix, match =>
        {
            removedIds.Add(int.Parse(match.Groups["id"].Value));
            return match.Groups["indent"].Value + match.Groups["statement"].Value + Environment.NewLine;
        });
        generated = prefix + suffix;
        foreach (var id in removedIds)
            generated = Regex.Replace(generated, $@"case\s+{id}:\s+goto\s+__ls_stmt_(?:before|after)_{id};\s*", "", RegexOptions.CultureInvariant);
        return generated;
    }
}
