using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed partial class XPScriptTranspiler
{
    public string Transpile(string source, string sourceName) =>
        Transpile(source, sourceName, CompilerDriver.CurrentRuntimeIdentifier());

    public string Transpile(string source) => Transpile(source, "input.xps");

    public string Transpile(string source, string sourceName, string runtimeIdentifier)
    {
        var serviceDefinition = XpsServiceScriptParser.Parse(source, sourceName);
        var prepared = ExpandedSourceContext.Current;
        var includeResult = prepared is not null && prepared.Matches(serviceDefinition.Source, sourceName)
            ? new IncludeSourcePreprocessor.Result(serviceDefinition.Source, prepared.Map, [Path.GetFullPath(sourceName)])
            : new IncludeSourcePreprocessor().Transform(serviceDefinition.Source, sourceName);
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
            throw new CompilerException(remapped, ex.DiagnosticCode, ex.Category, ex.GeneratedDiagnostics);
        }
    }

    public string TranspileRestricted(string source, string sourceName, string runtimeIdentifier, IEnumerable<string> allowedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceRoots);
        using var scope = IncludeSecurityContext.Push(allowedSourceRoots);
        return Transpile(source, sourceName, runtimeIdentifier);
    }

    private static CompilerException TargetUnavailable(string symbol, string target, string allowedTargets, string? detail = null)
    {
        var message = $"'{symbol}' is not available for target '{target}'." + (string.IsNullOrWhiteSpace(detail) ? "" : " " + detail);
        var diagnostic = new CompileDiagnostic
        {
            Description = message,
            DiagnosticCode = CompilerDiagnosticCodes.TargetApiUnavailable,
            Category = "target",
            Properties =
            [
                new() { Name = "symbol", Value = symbol },
                new() { Name = "target", Value = target },
                new() { Name = "allowedTargets", Value = allowedTargets }
            ]
        };
        return new CompilerException(message, CompilerDiagnosticCodes.TargetApiUnavailable, "target", [diagnostic]);
    }

    private static string TranspileExpanded(string source, string sourceName, string runtimeIdentifier, SourceMap sourceMap)
    {
        var originalFeatures = RuntimeFeatures.Detect(source);
        if (runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase) && originalFeatures.Ai)
            throw TargetUnavailable("XPAi", runtimeIdentifier, "server target", "Keep AI credentials and requests on the server.");

        // Semantic validators must run against the unmodified expanded source so their
        // line numbers still index sourceMap. Source markers insert physical lines and
        // would otherwise shift validator diagnostics away from the include map.
        try
        {
            new DateComparisonValidator().Validate(source, sourceName);
            new ClassOverloadValidator().Validate(source, sourceName);
            new SourceTypeValidator().Validate(source, sourceName, sourceMap);
        }
        catch (CompilerException ex)
        {
            // Semantic validators operate on the flattened include source. Remap their
            // coordinates immediately while the exact include map is still available.
            var remapped = SourceMapDiagnostics.Remap(ex.Message, sourceName, sourceMap);
            if (string.Equals(remapped, ex.Message, StringComparison.Ordinal)) throw;
            throw new CompilerException(remapped, ex.DiagnosticCode, ex.Category, ex.GeneratedDiagnostics);
        }

        // Expand multiline strings before the ordinary string scanner. Multiline
        // delimiters are language syntax and must not be misclassified as XPS1006 by
        // the compatibility string scan.
        source = new MultilineStringPreprocessor().Transform(source, sourceName);

        // Validate/normalize ordinary string delimiters while the source still has the
        // physical line layout represented by sourceMap. Running this after marker and
        // compatibility preprocessors would report transformed coordinates.
        var operatorArray = new OperatorArrayCompatibilityPreprocessor();
        source = operatorArray.NormalizeSource(source);

        // Resolve and validate physical line continuations before generated source
        // markers are inserted so dangling continuations retain original coordinates.
        source = new SourceLineContinuationPreprocessor().Transform(source, sourceName);

        // Attach runtime/#line markers only after semantic validation and syntax scanning.
        source = new SourceLineMarkerPreprocessor().Transform(source, sourceMap, sourceName);
        source = new EscapedQuotePreprocessor().Transform(source);
        source = new ReservedIdentifierPreprocessor().Transform(source);
        source = new IfLayoutPreprocessor().Transform(source);
        source = new ParameterlessProcedureHeaderPreprocessor().Transform(source);
        source = new ParameterPassingPreprocessor().Transform(source);
        source = new HclPrintFormattingPreprocessor().Transform(source);
        source = new StatementSeparatorPreprocessor().Transform(source, sourceName);
        source = new NativeLibraryPlatformPreprocessor(runtimeIdentifier).Transform(source);
        source = new NativeInteropSafetyPreprocessor().Transform(source);
        var udtValues = new UdtValueSemanticsPreprocessor();
        source = udtValues.Transform(source);
        source = new TypeDeclarationPreprocessor().Transform(source);
        source = new LanguageExtensionsPreprocessor().Transform(source);
        source = new PropertyLetCompatibilityPreprocessor().Transform(source);
        source = new IndexedPropertyPreprocessor().Transform(source);
        source = new ObjectFunctionSetPreprocessor().Transform(source);
        var runtimeFeatures = RuntimeFeatures.Detect(source);
        var usesAi = originalFeatures.Ai || runtimeFeatures.Ai;
        var notesRuntimeFeatures = NotesRuntimeFeatures.Detect(source);
        source = new NativeHttpJsonPreprocessor().Transform(source);
        var archiveRequested = PreprocessorFeatureGate.ContainsTypeReference(PreprocessorFeatureGate.CodeOnly(source), "Archive", "ArchiveEntry");
        source = new ArchiveObjectPreprocessor().Transform(source);
        var spreadsheetRequested = PreprocessorFeatureGate.ContainsTypeReference(PreprocessorFeatureGate.CodeOnly(source), "XPSpreadsheet", "XPWorksheet", "XPCell");
        source = new SpreadsheetObjectPreprocessor().Transform(source);
        var networkToolsRequested = PreprocessorFeatureGate.ContainsTypeReference(PreprocessorFeatureGate.CodeOnly(source), "NetworkTools", "NetworkPingResult", "NetworkTraceHop", "NetworkDnsResult", "NetworkPortResult", "NetworkUdpResult", "NetworkHttpResult", "NetworkTlsResult", "NetworkInterfaceInfo", "NetworkEndpointInfo");
        source = new NetworkToolsObjectPreprocessor().Transform(source);
        source = source.Replace("XPScriptDatabaseAttachmentRuntime.ForSqlite(", "XPScriptDatabaseAttachmentApi.ForSqlite(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForMsSql(", "XPScriptDatabaseAttachmentApi.ForMsSql(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForSupabase(", "XPScriptDatabaseAttachmentApi.ForSupabase(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.ForDomino(", "XPScriptDatabaseAttachmentApi.ForDomino(", StringComparison.Ordinal)
            .Replace("XPScriptDatabaseAttachmentRuntime.SetSupabaseBucket(", "XPScriptDatabaseAttachmentApi.SetSupabaseBucket(", StringComparison.Ordinal);
        var usesSqlite = runtimeFeatures.Sqlite || source.Contains("XPScriptDbSqlite", StringComparison.Ordinal);
        var usesMsSql = runtimeFeatures.MsSql || source.Contains("XPScriptDbMsSql", StringComparison.Ordinal);
        var usesExtendedArchive = source.Contains("XPScriptExtendedArchive", StringComparison.Ordinal);
        var usesArchive = archiveRequested || usesExtendedArchive || source.Contains("XPScriptArchive", StringComparison.Ordinal);
        var usesSpreadsheet = spreadsheetRequested || source.Contains("XPScriptSpreadsheet", StringComparison.Ordinal);
        var usesNetworkTools = networkToolsRequested || source.Contains("XPScriptNetworkTools", StringComparison.Ordinal);
        if (runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
        {
            if (usesSqlite) throw TargetUnavailable("XPDBSQLite", runtimeIdentifier, "server or desktop target");
            if (usesMsSql) throw TargetUnavailable("XPDbMsSql", runtimeIdentifier, "server or desktop target");
            if (usesArchive) throw TargetUnavailable("Archive", runtimeIdentifier, "server or desktop target", "Archive file-path operations are not available for browser-wasm targets yet.");
            if (usesSpreadsheet) throw TargetUnavailable("XPSpreadsheet", runtimeIdentifier, "server or desktop target");
            if (usesNetworkTools) throw TargetUnavailable("NetworkTools", runtimeIdentifier, "server or desktop target", "Browser sandboxes do not expose native ICMP, sockets, TLS streams, or local network interface APIs.");
        }
        var moduleObjects = new ModuleObjectGlobalsPreprocessor(udtValues.TypeNames);
        source = moduleObjects.Transform(source);
        var moduleGlobals = new ModuleGlobalsPreprocessor(udtValues.TypeNames);
        source = moduleGlobals.Transform(source);
        source = new DateObjectPreprocessor().Transform(source);
        source = new TypeCoercionPreprocessor().Transform(source);
        source = new StringConcatenationPreprocessor().Transform(source);
        source = new FileIoExtensionsPreprocessor().Transform(source);
        var protectedSource = ProtectStringLiterals(source, out var protectedStrings);
        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new CrossPlatformPreprocessor().Transform(protectedSource);
        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);
        protectedSource = new ApplicationObjectPreprocessor().Transform(protectedSource);
        protectedSource = RewriteListPresenceChecks(protectedSource);
        protectedSource = operatorArray.TransformProtectedSource(protectedSource);
        protectedSource = new TextIoCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ReferenceRuntimeExtensionsPreprocessor().Transform(protectedSource);
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
        generated += "\n\n" + DateObjectRuntimeSource.Code + "\n";
        if (usesSpreadsheet) generated += "\n\n" + SpreadsheetRuntimeSource.Code + "\n";
        if (usesNetworkTools) generated += "\n\n" + NetworkToolsRuntimeSource.Code + "\n";
        if (usesArchive) { generated += "\n\n" + ArchiveRuntimeSource.Code + "\n"; generated += "\n\n" + ArchiveMemoryRuntimeSource.Code + "\n"; generated += "\n\n" + ArchiveIteratorRuntimeSource.Code + "\n"; }
        if (usesExtendedArchive) { generated += "\n\n" + ArchiveExtendedReaderRuntimeSource.Code + "\n"; generated += "\n\n" + ArchiveExtendedWriterRuntimeSource.Code + "\n"; generated += "\n\n" + ArchiveExtendedWriterFactoryRuntimeSource.Code + "\n"; }
        if (runtimeFeatures.RequiresJson || usesAi) { generated += "\n\n" + JsonHttpCompatibilityRuntimeSource.Code + "\n"; generated += "\n\n" + JsonNodesSerializerShimSource.ShimCode + "\n"; generated += "\n\n" + NativeJsonRuntimeSource.Code + "\n"; }
        if (runtimeFeatures.JsonSchema) generated += "\n\n" + JsonSchemaRuntimeSource.Code + "\n";
        if (runtimeFeatures.Xml) generated += "\n\n" + NativeXmlRuntimeSource.Code + "\n";
        if (runtimeFeatures.Csv) generated += "\n\n" + NativeCsvRuntimeSource.Code + "\n";
        generated += "\n\n" + TextIoCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + FileIoExtensionsRuntimeSource.Code + "\n";
        generated += "\n\n" + ReferenceRuntimeExtensionsSource.Code + "\n";
        if (runtimeFeatures.RequiresHttp) { generated += "\n\n" + NativeHttpRuntimeSource.Code + "\n"; generated += "\n\n" + HttpCoreRuntimeSource.Code + "\n"; generated += "\n\n" + AsyncHttpRuntimeSource.Code + "\n"; }
        if (runtimeFeatures.Ui) generated += "\n\n" + UIExtensionRuntimeSource.Code + "\n";
        if (runtimeFeatures.RequiresHttp && runtimeFeatures.Ui) generated += "\n\n" + HttpUiFormRuntimeSource.Code + "\n";
        if (runtimeFeatures.Database) generated += "\n\n" + CaseInsensitiveDynamicObjectRuntimeSource.Code + "\n";
        if (usesSqlite) generated += "\n\n" + SqliteDbRuntimeSource.Code + "\n";
        if (usesMsSql) generated += "\n\n" + MsSqlDbRuntimeSource.Code + "\n";
        if (usesAi) generated += "\n\n" + AiRuntimeSource.Code + "\n";
        if (runtimeFeatures.RequiresHttpDatabaseTypes) generated += "\n\n" + HttpDbRuntimeSource.Code + "\n";
        if (runtimeFeatures.Database) generated += "\n\n" + DatabaseUiDataSourceRuntimeSource.Build(usesSqlite, usesMsSql, runtimeFeatures.RequiresHttpDatabaseTypes) + "\n";
        if (runtimeFeatures.Attachments) { generated += "\n\n" + DatabaseAttachmentRuntimeV2Source.Build(usesSqlite, usesMsSql) + "\n"; generated += "\n\n" + DatabaseAttachmentRuntimeV3Source.Code + "\n"; }
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
        if (usesAi) { generated = new AiSessionRuntimePostProcessor().Transform(generated); generated = new AiPromptSchemaRuntimePostProcessor().Transform(generated); }
        generated = new UIExtensionDesktopPostProcessor(notesRuntimeFeatures).Transform(generated);
        generated = new BrowserWasmHttpCsrfPostProcessor(runtimeIdentifier).Transform(generated);
        generated = new FileSystemPortabilityPostProcessor().Transform(generated);
        generated = generated.Replace("XPScriptRuntime.SetArgs(args);", $"XPScriptRuntime.SetArgs(args);\n        XPScriptFileSystemRuntime.SetScriptDirectory(\"{EscapeCSharpString(GetSourceDirectory(sourceName))}\");\n        XPNativeInteropRuntime.Initialize();\n        XPScriptApplicationRuntime.SetArgs(args);\n        LSOperatorArrayRuntime.SetCompareNoCase({operatorArray.CompareNoCase.ToString().ToLowerInvariant()});", StringComparison.Ordinal);
        generated = generated.Replace("text.StartsWith('/', StringComparison.Ordinal)", "text.StartsWith(\"/\", StringComparison.Ordinal)", StringComparison.Ordinal);
        generated = generated.Replace("byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),", "byte[] requestBytes => System.Text.Encoding.UTF8.GetString(requestBytes),", StringComparison.Ordinal);
        generated = generated.Replace("using System.Text.RegularExpressions;", "using System.Text.RegularExpressions;\nusing System.Runtime.InteropServices;", StringComparison.Ordinal);
        generated = Regex.Replace(generated, @"(?m)^\s*__lsErrCtx\.Statement\s*=\s*\d+;\s*\r?$\n?", "");
        generated = ScopeErrorProtection(generated);
        foreach (var item in protectedStrings) generated = generated.Replace(item.Key, item.Value, StringComparison.Ordinal);
        generated = generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
        return new CompilerSourceLineDirectivePostProcessor().Transform(generated);
    }
}
