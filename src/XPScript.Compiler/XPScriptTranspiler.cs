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

    public string TranspileRestricted(string source, string sourceName, string runtimeIdentifier, IEnumerable<string> allowedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceRoots);
        using var scope = IncludeSecurityContext.Push(allowedSourceRoots);
        return Transpile(source, sourceName, runtimeIdentifier);
    }

    private static string TranspileExpanded(string source, string sourceName, string runtimeIdentifier, SourceMap sourceMap)
    {
        source = new MultilineStringPreprocessor().Transform(source, sourceName);
        source = new EscapedQuotePreprocessor().Transform(source);
        var jsonNames = new JsonNameMetadataPreprocessor();
        source = jsonNames.Transform(source);
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
        var runtimeFeatures = RuntimeFeatures.Detect(source);
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
        var usesAi = source.Contains("XPScriptAi", StringComparison.Ordinal);
        var usesExtendedArchive = source.Contains("XPScriptExtendedArchive", StringComparison.Ordinal);
        var usesArchive = archiveRequested || usesExtendedArchive || source.Contains("XPScriptArchive", StringComparison.Ordinal);
        var usesSpreadsheet = spreadsheetRequested || source.Contains("XPScriptSpreadsheet", StringComparison.Ordinal);
        var usesNetworkTools = networkToolsRequested || source.Contains("XPScriptNetworkTools", StringComparison.Ordinal);
        var usesApplicationCrypto = Regex.IsMatch(PreprocessorFeatureGate.CodeOnly(source), @"\bApplication\.Crypto\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (usesSqlite && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("XPDBSQLite is not available for browser-wasm targets.");
        if (usesMsSql && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("XPDbMsSql is not available for browser-wasm targets.");
        if (usesAi && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("XPAi is not available for browser-wasm targets. Keep AI credentials and requests on the server.");
        if (usesArchive && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("Archive file-path operations are not available for browser-wasm targets yet. Run archive filesystem work on the server until in-memory Archive support is implemented.");
        if (usesSpreadsheet && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("XPSpreadsheet file operations are not available for browser-wasm targets in the basic implementation.");
        if (usesNetworkTools && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("NetworkTools is not available for browser-wasm targets because browser sandboxes do not expose native ICMP, sockets, TLS streams, or local network interface APIs.");
        if (usesApplicationCrypto && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new CompilerException("Application.Crypto is not available in browser-wasm client code because .NET AES-GCM is unsupported there. Keep cryptographic operations in server-side code.");
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
        protectedSource = new JsonHttpCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ExtendedCompatibilityTranspiler().Transform(protectedSource);
        var generated = new CoreCompatibilityTranspiler().Transpile(protectedSource, sourceName);
        generated = jsonNames.ApplyToGeneratedCode(generated);
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
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }
}
