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
        var runtimeFeatures = RuntimeFeatures.Detect(source);
        var notesRuntimeFeatures = NotesRuntimeFeatures.Detect(source);
        source = new NativeHttpJsonPreprocessor().Transform(source);
        var archiveRequested = PreprocessorFeatureGate.ContainsTypeReference(PreprocessorFeatureGate.CodeOnly(source), "Archive", "ArchiveEntry");
        source = new ArchiveObjectPreprocessor().Transform(source);
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
        if (usesSqlite && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPDBSQLite is not available for browser-wasm targets.");
        if (usesMsSql && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPDbMsSql is not available for browser-wasm targets.");
        if (usesAi && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("XPAi is not available for browser-wasm targets. Keep AI credentials and requests on the server.");
        if (usesArchive && runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("Archive file-path operations are not available for browser-wasm targets yet. Run archive filesystem work on the server until in-memory Archive support is implemented.");
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
        if (usesArchive)
        {
            generated += "\n\n" + ArchiveRuntimeSource.Code + "\n";
            generated += "\n\n" + ArchiveMemoryRuntimeSource.Code + "\n";
            generated += "\n\n" + ArchiveIteratorRuntimeSource.Code + "\n";
        }
        if (usesExtendedArchive)
        {
            generated += "\n\n" + ArchiveExtendedReaderRuntimeSource.Code + "\n";
            generated += "\n\n" + ArchiveExtendedWriterRuntimeSource.Code + "\n";
            generated += "\n\n" + ArchiveExtendedWriterFactoryRuntimeSource.Code + "\n";
        }
        if (runtimeFeatures.RequiresJson || usesAi)
        {
            generated += "\n\n" + JsonHttpCompatibilityRuntimeSource.Code + "\n";
            generated += "\n\n" + JsonNodesSerializerShimSource.ShimCode + "\n";
            generated += "\n\n" + NativeJsonRuntimeSource.Code + "\n";
        }
        if (runtimeFeatures.Xml) generated += "\n\n" + NativeXmlRuntimeSource.Code + "\n";
        if (runtimeFeatures.Csv) generated += "\n\n" + NativeCsvRuntimeSource.Code + "\n";
        generated += "\n\n" + TextIoCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + FileIoExtensionsRuntimeSource.Code + "\n";
        generated += "\n\n" + ReferenceRuntimeExtensionsSource.Code + "\n";
        if (runtimeFeatures.RequiresHttp)
        {
            generated += "\n\n" + NativeHttpRuntimeSource.Code + "\n";
            generated += "\n\n" + AsyncHttpRuntimeSource.Code + "\n";
        }
        if (runtimeFeatures.Ui) generated += "\n\n" + UIExtensionRuntimeSource.Code + "\n";
        if (runtimeFeatures.RequiresHttp && runtimeFeatures.Ui)
            generated += "\n\n" + HttpUiFormRuntimeSource.Code + "\n";
        if (runtimeFeatures.Database) generated += "\n\n" + CaseInsensitiveDynamicObjectRuntimeSource.Code + "\n";
        if (usesSqlite) generated += "\n\n" + SqliteDbRuntimeSource.Code + "\n";
        if (usesMsSql) generated += "\n\n" + MsSqlDbRuntimeSource.Code + "\n";
        if (usesAi) generated += "\n\n" + AiRuntimeSource.Code + "\n";
        if (runtimeFeatures.RequiresHttpDatabaseTypes) generated += "\n\n" + HttpDbRuntimeSource.Code + "\n";
        if (runtimeFeatures.Database)
            generated += "\n\n" + DatabaseUiDataSourceRuntimeSource.Build(
                usesSqlite,
                usesMsSql,
                runtimeFeatures.RequiresHttpDatabaseTypes) + "\n";
        if (runtimeFeatures.Attachments)
        {
            generated += "\n\n" + DatabaseAttachmentRuntimeV2Source.Build(usesSqlite, usesMsSql) + "\n";
            generated += "\n\n" + DatabaseAttachmentRuntimeV3Source.Code + "\n";
        }
        generated += "\n\n" + ModuleArrayRuntimeSource.Code + "\n";
        generated += "\n\n" + UdtArrayRuntimeSource.Code + "\n";

        if (runtimeFeatures.NativeHttpJson)
            generated += "\n\n" + NativeHttpJsonRuntimeSource.Code + "\n";
        if (runtimeFeatures.DominoJson)
            generated += "\n\n" + DominoJsonRuntimeSource.Code + "\n";
        if (runtimeFeatures.NotesSession)
            generated += "\n\n" + NotesSessionRuntimeSource.Code + "\n";
        if (runtimeFeatures.NotesDocument)
            generated += "\n\n" + NotesDocumentRuntimeSource.Code + "\n";
        if (runtimeFeatures.NotesMIMEEntity)
            generated += "\n\n" + NotesMimeEntityRuntimeSource.Code + "\n";
        if (runtimeFeatures.NotesDatabase)
            generated += "\n\n" + NotesDatabaseRuntimeSource.Code + "\n";
        if (runtimeFeatures.NotesDesignElement)
            generated += "\n\n" + NotesDesignElementRuntimeSource.Code + "\n";
        if (runtimeFeatures.Ui)
            generated += "\n\n" + UiRuntimeSource.Code + "\n";

        return RestoreStringLiterals(generated, protectedStrings);
    }

    private static string NormalizeEvaluateRuntime(string runtime) => runtime;

    private static string ProtectStringLiterals(string source, out List<string> literals)
    {
        literals = [];
        return Regex.Replace(source, @"\"(?:\"\"|[^\"])*\"", match =>
        {
            var index = literals.Count;
            literals.Add(match.Value);
            return $"__XPS_STRING_LITERAL_{index}__";
        });
    }

    private static string RestoreStringLiterals(string source, IReadOnlyList<string> literals)
    {
        for (var i = 0; i < literals.Count; i++)
            source = source.Replace($"__XPS_STRING_LITERAL_{i}__", literals[i], StringComparison.Ordinal);
        return source;
    }

    private static string RewriteListPresenceChecks(string source) => source;
}
