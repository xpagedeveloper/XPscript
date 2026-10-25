using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Experimental CLI bridge for the currently supported AST compilation slice.</summary>
public static class AstExperimentalCompiler
{
    public static string EnsureAttachmentRuntime(string generated, bool includeNativeDatabaseProviders = true)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var usesAttachmentApi = generated.Contains("XPScriptDatabaseAttachmentApi.", StringComparison.Ordinal);
        if (!usesAttachmentApi && !generated.Contains("XPScriptAttachmentCollectionV3", StringComparison.Ordinal))
            return generated;
        if (generated.Contains("internal static class XPScriptDatabaseAttachmentApi", StringComparison.Ordinal))
            return generated;
        if (generated.Contains("internal sealed class XPScriptAttachmentCollectionV3", StringComparison.Ordinal))
            return generated + Environment.NewLine + DatabaseAttachmentRuntimeV3Source.ApiCode;
        var runtime = generated;
        if (!generated.Contains("XPScriptHttpDbSupabase", StringComparison.Ordinal))
            runtime += Environment.NewLine + HttpCoreRuntimeSource.Code + NativeHttpRuntimeSource.Code + HttpJsonRuntimeSource.Code + HttpDbRuntimeSource.Code +
                (generated.Contains("XPScriptTlsValidationState", StringComparison.Ordinal) ? string.Empty : AstTlsRuntimeCode());
        if (!runtime.Contains("class XPScriptHttpDatabaseDataSourceExtensions", StringComparison.Ordinal))
            runtime += Environment.NewLine + DatabaseUiDataSourceRuntimeSource.BuildHttpDatabaseExtensions();
        if (includeNativeDatabaseProviders && !runtime.Contains("class XPScriptDatabaseDataSourceRuntime", StringComparison.Ordinal))
            runtime += Environment.NewLine + DatabaseUiDataSourceRuntimeSource.Build(true, true, false);
        if (includeNativeDatabaseProviders && !runtime.Contains("class XPScriptDbSqlite", StringComparison.Ordinal))
            runtime += Environment.NewLine + SqliteDbRuntimeSource.Code;
        if (includeNativeDatabaseProviders && !runtime.Contains("class XPScriptDbMsSql", StringComparison.Ordinal))
            runtime += Environment.NewLine + MsSqlDbRuntimeSource.Code;
        if (!runtime.Contains("class XPScriptCaseInsensitiveDynamicObject", StringComparison.Ordinal))
            runtime += Environment.NewLine + CaseInsensitiveDynamicObjectRuntimeSource.Code;
        return runtime + Environment.NewLine + DatabaseAttachmentRuntimeV2Source.Build(
            generated.Contains("XPScriptDbSqlite", StringComparison.Ordinal),
            generated.Contains("XPScriptDbMsSql", StringComparison.Ordinal)) +
            Environment.NewLine + DatabaseAttachmentRuntimeV3Source.Code;
    }

    public static Task<string> CompileAsync(string sourcePath, string outputDirectory, CancellationToken cancellationToken = default)
        => CompileAsync(sourcePath, outputDirectory, null, cancellationToken: cancellationToken);

    /// <summary>
    /// Compiles through the AST pipeline after applying an optional transform to
    /// the generated C# source. This is used by hosts that need to add a host
    /// surface (for example web request/response objects) while keeping parsing,
    /// binding and emission in the AST compiler.
    /// </summary>
    public static Task<string> CompileWithGeneratedSourceTransformAsync(
        string sourcePath,
        string outputDirectory,
        string? runtimeIdentifier,
        Func<string, string>? generatedSourceTransform,
        CancellationToken cancellationToken = default)
        => CompileAsync(sourcePath, outputDirectory, runtimeIdentifier, generatedSourceTransform,
            ApplicationSecurityMode.Off, false, null, null, false, cancellationToken);

    public static async Task<string> CompileAsync(string sourcePath, string outputDirectory, string? runtimeIdentifier,
        ApplicationSecurityMode securityMode = ApplicationSecurityMode.Off, bool restricted = false,
        IReadOnlyList<string>? sourceRoots = null, IReadOnlyList<string>? sourcePreprocessors = null,
        bool embedAssets = false,
        CancellationToken cancellationToken = default)
        => await CompileAsync(sourcePath, outputDirectory, runtimeIdentifier, null, securityMode, restricted,
            sourceRoots, sourcePreprocessors, embedAssets, cancellationToken).ConfigureAwait(false);

    private static async Task<string> CompileAsync(string sourcePath, string outputDirectory, string? runtimeIdentifier,
        Func<string, string>? generatedSourceTransform,
        ApplicationSecurityMode securityMode, bool restricted,
        IReadOnlyList<string>? sourceRoots, IReadOnlyList<string>? sourcePreprocessors,
        bool embedAssets, CancellationToken cancellationToken)
    {
        if (runtimeIdentifier is not null)
            TargetPrerequisiteValidator.Validate(runtimeIdentifier.Trim().ToLowerInvariant());
        using var securityScope = ApplicationSecurityModeContext.Push(securityMode);
        using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors ?? []);
        using var includeScope = restricted
            ? IncludeSecurityContext.Push(sourceRoots is { Count: > 0 } ? sourceRoots : [Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory])
            : null;
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        // Expand Include directives before AST feature detection and parsing so the
        // AST compiler has the same multi-file source contract as the legacy path.
        var includeResult = new IncludeSourcePreprocessor().Transform(source, sourcePath);
        source = includeResult.Source;
        try
        {
            // Keep declaration-level overload diagnostics identical to the
            // legacy path before AST binding emits generated C#.
            new ClassOverloadValidator().Validate(source, sourcePath);
        }
        catch (CompilerException ex)
        {
            var remapped = SourceMapDiagnostics.Remap(ex.Message, sourcePath, includeResult.Map);
            if (string.Equals(remapped, ex.Message, StringComparison.Ordinal)) throw;
            throw new CompilerException(remapped, ex.DiagnosticCode, ex.Category, ex.GeneratedDiagnostics);
        }
        var managedReferences = new ManagedAssemblyReferencePreprocessor(runtimeIdentifier ?? CompilerDriver.CurrentRuntimeIdentifier())
            .Transform(source, includeResult.Map, sourcePath);
        source = managedReferences.Source;
        var nativeSource = source;
        var jsonNameMetadata = new JsonNameMetadataPreprocessor();
        source = jsonNameMetadata.Transform(source);
        var nativeDeclarations = new List<(string Kind, string Name, string Library, string Alias, string ReturnType, string[] Parameters, bool Unicode)>();
        var nativeDependencies = new NativeDependencyPackager(runtimeIdentifier ?? CompilerDriver.CurrentRuntimeIdentifier()).Collect(nativeSource, sourceName: sourcePath);
        source = Regex.Replace(source,
            @"(?ims)^\s*Declare\s+(?<kind>Function|Sub)\s+(?<name>[A-Za-z_]\w*)\s+Lib\s+""(?<library>[^""]+)""(?:\s+Alias\s+""(?<alias>[^""]+)"")?(?<options>.*?)\((?<parameters>[^)]*)\)\s*(?:As\s+(?<returnType>[A-Za-z_]\w*))?\s*(?:_|\r?$)",
            match =>
            {
                var parameters = match.Groups["parameters"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var parameter in parameters)
                    if (Regex.IsMatch(parameter, @"(?i)\bByRef\b"))
                        throw MappedDiagnostic(new TextSpan(0, 0), sourcePath, source,
                            "Native Declare parameters must be passed ByVal because native ByRef/out marshalling is not supported.",
                            CompilerDiagnosticCodes.NativeByRefUnsupported, "interop");
                var rid = runtimeIdentifier?.Trim().ToLowerInvariant();
                var platformPrefix = rid switch
                {
                    "win-x64" => "WindowsX64",
                    "win-arm64" => "WindowsArm64",
                    "linux-x64" => "LinuxX64",
                    "linux-arm64" => "LinuxArm64",
                    "osx-x64" => "MacOSX64",
                    "osx-arm64" => "MacOSArm64",
                    _ => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : OperatingSystem.IsMacOS() ? "MacOS" : string.Empty
                };
                var options = match.Groups["options"].Value;
                var unicode = Regex.IsMatch(options, @"(?i)\bUnicode\b");
                var selectedLibrary = Regex.Match(options, $@"(?i)\b{platformPrefix}Lib\s+""(?<value>[^""]+)""").Groups["value"].Value;
                var selectedAlias = Regex.Match(options, $@"(?i)\b{platformPrefix}Alias\s+""(?<value>[^""]+)""").Groups["value"].Value;
                nativeDeclarations.Add((match.Groups["kind"].Value, match.Groups["name"].Value, string.IsNullOrWhiteSpace(selectedLibrary) ? match.Groups["library"].Value : selectedLibrary,
                    string.IsNullOrWhiteSpace(selectedAlias) ? (string.IsNullOrWhiteSpace(match.Groups["alias"].Value) ? match.Groups["name"].Value : match.Groups["alias"].Value) : selectedAlias,
                    match.Groups["kind"].Value.Equals("Sub", StringComparison.OrdinalIgnoreCase) ? "Void" : (string.IsNullOrWhiteSpace(match.Groups["returnType"].Value) ? "Variant" : match.Groups["returnType"].Value), parameters, unicode));
                return new string(match.Value.Select(c => c is '\r' or '\n' ? c : ' ').ToArray());
            });
        var usesArchive = PreprocessorFeatureGate.ContainsTypeReference(source, "Archive", "ArchiveEntry");
        if (usesArchive)
            source = new ArchiveObjectPreprocessor().Transform(source, sourcePath);
        var usesNetworkTools = Regex.IsMatch(source,
            @"(?i)\bNetwork(?:Tools|PingResult|TraceHop|DnsResult|PortResult|UdpResult|HttpResult|TlsResult|InterfaceInfo|EndpointInfo|HostResult|SubnetInfo|TlsVersionResult|CertificateResult|HttpHeadersResult|HttpSecurityHeadersResult|RedirectResult|BannerResult|PortExposureInfo)\b");
        if (usesNetworkTools)
            source = new NetworkToolsObjectPreprocessor().Transform(source);
        var fullSource = source;
        var usesNotesSession = AstObjectSupportRegistry.UsesObject(fullSource, "NotesSession");
        if (usesNotesSession)
        {
            source = new NotesRuntimePreprocessor().Transform(source, preserveAstConstants: true);
            fullSource = source;
        }
        ValidateAstAiCallbacks(fullSource);
        var runtimeFeatures = RuntimeFeatures.Detect(fullSource);
        var usesAi = runtimeFeatures.Ai;
        if (AstObjectSupportRegistry.UsesUnsupportedObject(source, out var definition))
        {
            if (definition!.Route == AstObjectSupportRegistry.CompilationRoute.LegacyFallback)
                return await CompileWithLegacyFallbackAsync(sourcePath, outputDirectory, cancellationToken).ConfigureAwait(false);

            RejectUnsupported(sourcePath, source, $@"(?i)\b{Regex.Escape(definition.Name)}\b", definition.Message ?? "The XP object is not implemented by the AST compiler.");
        }
        var optionBase = 0;
        var optionBaseMatch = Regex.Match(source, @"(?im)^\s*Option\s+Base\s+(?<base>[01])\s*(?:\r?$)");
        if (optionBaseMatch.Success)
        {
            optionBase = int.Parse(optionBaseMatch.Groups["base"].Value);
            source = Regex.Replace(source, @"(?im)^\s*Option\s+Base\s+[01]\s*(?:\r?$)", match =>
                new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()));
            if (optionBase == 1)
                source = Regex.Replace(source, @"(?im)^(?<indent>\s*Dim\s+(?<name>[A-Za-z_]\w*)\s*)\((?<bounds>[^)]*)\)", match =>
                {
                    var bounds = string.Join(", ", match.Groups["bounds"].Value.Split(',').Select(bound =>
                        Regex.IsMatch(bound.Trim(), @"^\d+$") ? $"1 To {bound.Trim()}" : bound.Trim()));
                    return $"{match.Groups["indent"].Value}({bounds})";
                });
        }
        source = Regex.Replace(source, @"\[(?:FromBody|FromQuery|FromRoute|FromHeader)\]\s*", match =>
            new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()), RegexOptions.IgnoreCase);
        source = new IncrementCompoundAssignmentPreprocessor().Transform(source);
        source = Regex.Replace(source, @"(?im)^(?<indent>\s*)Sleep\s+(?<value>[^\r\n]+)\r?$", "${indent}Sleep(${value})");
        // Keep the argument at its original column: replace the original
        // separator with the opening parenthesis and avoid a longer prefix.
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)MkDir\s+(?<path>[^\r\n]+)\r?$", "${indent}MkDir(${path})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Put\s+#?(?<file>[^,\s]+)\s*,\s*(?<position>[^,]+)\s*,\s*(?<value>[^\r\n]+)\r?$", "${indent}AstPut(${file}, ${position}, ${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Get\s+#?(?<file>[^,\s]+)\s*,\s*(?<position>[^,]+)\s*,\s*(?<value>[A-Za-z_]\w*)\s*$", "${indent}${value} = AstGet(${file}, ${position}, ${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Seek\s+#?(?<file>[^,\s]+)\s*,\s*(?<position>[^\r\n]+)\r?$", "${indent}Call AstSeek(${file}, ${position})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Reset\s*$", "${indent}Call AstReset()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Stop\s*$", "${indent}Call AstStop()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)ChDrive\s+(?<drive>[^\r\n]+)\r?$", "${indent}Call AstChDrive(${drive})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)ChDir\s+(?<path>[^\r\n]+)\r?$", "${indent}Call AstChDir(${path})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)(?<operation>Lock|Unlock)\s+#?(?<file>[^,\s]+)\s*,\s*(?<start>[^\s]+)\s+To\s+(?<end>[^\r\n]+)\r?$", "${indent}Call Ast${operation}Bytes(${file}, ${start}, ${end})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Open\s+(?<path>.+?)\s+For\s+(?<mode>Input|Output|Append|Binary|Random)\s+As\s+#?(?<file>[^\s]+).*$", "${indent}Call AstOpen(${path}, \"${mode}\", ${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Close(?:\s+#?(?<file>[^\s]+))?\s*$", "${indent}Call AstClose(${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+#(?<file>[^,\s]+)\s*,\s*(?<value>[^\r\n]+)\r?$", "${indent}Call AstPrintFile(${file}, ${value})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Line\s+Input\s+#?(?<file>[^,\s]+)\s*,\s*(?<target>[A-Za-z_]\w*)\s*$", "${indent}${target} = AstLineInput(${file})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Kill\s+(?<path>[^\r\n]+)\r?$", "${indent}Call AstKill(${path})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)FileCopy\s+(?<source>.+?)\s*,\s*(?<destination>[^\r\n]+)\r?$", "${indent}Call AstFileCopy(${source}, ${destination})");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Name\s+(?<source>.+?)\s+As\s+(?<destination>[^\r\n]+)\r?$", "${indent}Call AstNameFile(${source}, ${destination})");
        source = Regex.Replace(source, @"(?<![\w.])Input\$\s*\(\s*(?<count>[^,()]+)\s*,\s*#\s*(?<file>[^)]+)\)", "AstInputChars(${count}, ${file})", RegexOptions.IgnoreCase);
        // Error$ is the string-valued spelling of Error(...), not a
        // parameterless function followed by an argument list.
        source = source.Replace("Error$", "Error", StringComparison.OrdinalIgnoreCase);
        source = source.Replace("Format$", "Format", StringComparison.OrdinalIgnoreCase);
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)p\.[A-Za-z_]\w*(?:\s*=.*)?$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)Print\s+p\.[A-Za-z_]\w*\s*$", "${indent}Call AstNoOp()");
        source = Regex.Replace(source, @"(?im)^(?<indent>[ \t]*)ReDim\s+(?<preserve>Preserve\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^)]*)\)(?:\s+As\s+[A-Za-z_]\w*)?\s*$", match =>
        {
            var dimensions = match.Groups["bounds"].Value.Split(',').Select(item => item.Trim()).ToArray();
            if (dimensions.Length is < 1 or > 8)
                throw new InvalidOperationException("AST dynamic arrays support between one and eight dimensions.");
            var arguments = new List<string> { match.Groups["name"].Value };
            var parsed = dimensions.Select(dimension =>
            {
                var range = Regex.Match(dimension, @"(?i)^(?<lower>.+?)\s+To\s+(?<upper>.+)$");
                return (Lower: range.Success ? range.Groups["lower"].Value.Trim() : optionBase.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Upper: range.Success ? range.Groups["upper"].Value.Trim() : dimension);
            }).ToArray();
            arguments.AddRange(parsed.SelectMany(pair => new[] { pair.Upper, pair.Lower }));
            arguments.Insert(2, match.Groups["preserve"].Success ? "true" : "false");
            return $"{match.Groups["indent"].Value}{match.Groups["name"].Value} = ArrayResize({string.Join(", ", arguments)})";
        });
        foreach (Match redim in Regex.Matches(fullSource, @"(?im)^\s*ReDim\s+(?:Preserve\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^)]*,[^)]*)\)"))
        {
            var name = Regex.Escape(redim.Groups["name"].Value);
            source = Regex.Replace(source, $@"(?im)(?<prefix>\bDim\s+{name})\s*\(\s*\)\s+As\s+", "${prefix}(,) As ");
        }
        var declarationStart = System.Text.RegularExpressions.Regex.Match(source, @"(?im)^\s*(?:(?:Public|Private|Static)\s+)*(Sub|Function|Class)\b");
        if (declarationStart.Success)
            // Ignore module headers without moving declaration/body spans away from the source.
            source = new string(source[..declarationStart.Index].Select(c => c is '\r' or '\n' ? c : ' ').ToArray())
                + source[declarationStart.Index..];
        var parser = new DeclarationParser(source);
        var unit = parser.ParseCompilationUnit();
        if (parser.Diagnostics.Count > 0)
            throw SyntaxDiagnosticException(parser.Diagnostics[0], sourcePath, source, fullSource);
        foreach (var item in unit.Declarations)
        {
            var parameters = item is SubDeclarationSyntax procedure ? procedure.Parameters
                : item is FunctionDeclarationSyntax routine ? routine.Parameters : [];
        }
        var moduleSource = AstClassMethodSupport.ModuleSource(fullSource);
        var applicationConstants = unit.Declarations.OfType<ApplicationConstDeclarationSyntax>()
            .Select(declaration => Regex.Match(declaration.Text, @"^\s*Const\s+(?<name>[A-Za-z_]\w*)\s*(?:As\s+\w+\s*)?=\s*(?<value>.+?)\s*$", RegexOptions.IgnoreCase))
            .Where(match => match.Success)
            .ToArray();
        var moduleConstants = Regex.Matches(moduleSource, @"^\s*Const\s+(?<name>[A-Za-z_]\w*)\s*(?:As\s+\w+\s*)?=\s*(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>()
            .ToArray();
        // Module-level constants are emitted as fields. Procedure-level Const
        // declarations are bound structurally by StatementBinder and remain
        // local to the procedure.
        var allConstants = applicationConstants
            .Concat(moduleConstants)
            .GroupBy(match => match.Groups["name"].Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var moduleArrayDeclarations = Regex.Matches(AstModuleFieldSupport.DeclarationSource(fullSource), @"(?im)^\s*(?:(?:Public|Private)\s+)?(?:Dim\s+)?(?<name>[A-Za-z_]\w*)\s*\((?<bounds>[^\r\n]*)\)\s+As\s+(?<type>[A-Za-z_]\w*)")
            .Cast<Match>()
            .ToArray();
        // The experimental path keeps going when a non-selected declaration
        // contains syntax outside the current AST slice. Binding diagnostics
        // for the selected procedure remain authoritative below.
        var declaration = unit.Declarations.FirstOrDefault(item => item is SubDeclarationSyntax candidate && candidate.Identifier.Text.Equals("Main", StringComparison.OrdinalIgnoreCase))
            ?? unit.Declarations.FirstOrDefault(item => item is SubDeclarationSyntax or FunctionDeclarationSyntax);
        var sub = declaration as SubDeclarationSyntax;
        var function = declaration as FunctionDeclarationSyntax;
        if (sub is null && function is null)
            throw MappedDiagnostic(new TextSpan(0, 0), sourcePath, fullSource,
                "AST experimental compilation currently requires a Sub declaration.", "XPS3001", "ast");

        var symbols = SymbolTable.CreateWithCompilerCatalog();
        foreach (var native in nativeDeclarations)
        {
            var parameterTypes = native.Parameters.Select(_ => typeof(object)).ToArray();
            var nativeIsSub = native.Kind.Equals("Sub", StringComparison.OrdinalIgnoreCase);
            var nativeRuntimeReturn = nativeIsSub ? typeof(void) : NativeClrType(native.ReturnType);
            symbols.Declare(new FunctionSymbol(native.Name, nativeRuntimeReturn, parameterTypes,
                nativeIsSub ? null : XpTypeSymbol.FromClr(nativeRuntimeReturn), parameterTypes.Select(_ => XpTypeSymbol.Variant).ToArray())
            { NativeTargetName = $"XPNativeDeclarations.{native.Name}" });
        }
        symbols.Declare(new TypeSymbol("Object", typeof(object), XpTypeSymbol.Object));
        symbols.Declare(new VariableSymbol("Object", typeof(object), XpTypeSymbol.Object));
        foreach (var classDeclaration in unit.Declarations.OfType<ClassDeclarationSyntax>())
            symbols.Declare(new TypeSymbol(classDeclaration.Identifier.Text, typeof(object), XpTypeSymbol.User(classDeclaration.Identifier.Text)));
        var classes = unit.Declarations.OfType<ClassDeclarationSyntax>()
            .ToDictionary(item => item.Identifier.Text, StringComparer.OrdinalIgnoreCase);
        // Validate all edges before any inherited-member walk. This metadata is
        // owned by this compilation's scopes, never shared with another MCP call.
        foreach (var item in classes.Values)
        {
            if (item.BaseType is { } parent && !classes.ContainsKey(parent.Identifier.Text))
                throw MappedDiagnostic(parent.Span, sourcePath, fullSource,
                    $"Unknown base class '{parent.Identifier.Text}'.", CompilerDiagnosticCodes.UnknownSymbol, "semantic");
        }
        foreach (var item in classes.Values)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var current = item; current is not null; current = current.BaseType is { } parent ? classes[parent.Identifier.Text] : null)
                if (!seen.Add(current.Identifier.Text))
                    throw MappedDiagnostic(item.BaseType?.Span ?? item.Identifier.Span, sourcePath, fullSource,
                        $"Inheritance cycle in base chain for class '{item.Identifier.Text}'.", CompilerDiagnosticCodes.InvalidSyntax, "semantic");
            symbols.DeclareUserClassInheritance(item.Identifier.Text, item.BaseType?.Identifier.Text);
        }
        AstClassConstructorSupport.Register(classes, symbols, sourcePath, fullSource);
        foreach (var classDeclaration in classes.Values)
        {
            for (var current = classDeclaration; current is not null; current = current.BaseType is not null && classes.TryGetValue(current.BaseType.Identifier.Text, out var baseClass) ? baseClass : null)
            {
                foreach (var field in current.Members.OfType<FieldDeclarationSyntax>())
                    if (field.Visibility?.Kind != SyntaxKind.PrivateKeyword)
                    symbols.Declare(new PropertySymbol($"{classDeclaration.Identifier.Text}.{field.Identifier.Text}", ResolveRuntimeType(field.Type.Identifier.Text), AstClassMethodSupport.DeclaredType(field.Type, symbols), IsFieldStorage: true));
                foreach (var property in AstClassMethodSupport.Properties(current, true, symbols))
                    symbols.Declare(property with { Name = classDeclaration.Identifier.Text + "." + property.Name.Split('.').Last() });
                foreach (var method in current.Members.OfType<SubDeclarationSyntax>())
                    if (method.Visibility?.Kind != SyntaxKind.PrivateKeyword)
                    {
                        if (current != classDeclaration && classDeclaration.Members.OfType<SubDeclarationSyntax>().Any(own => SameClassMethodSignature(own, method)))
                            continue;
                        var parameterTypes = method.Parameters.Select(parameter => ResolveParameterType(parameter, symbols)).ToArray();
                        var qualifiedMethodName = $"{classDeclaration.Identifier.Text}.{method.Identifier.Text}";
                        symbols.Declare(new FunctionSymbol(qualifiedMethodName, typeof(void), parameterTypes.Select(type => type.RuntimeType).ToArray(), null, parameterTypes, method.Parameters.Select(parameter => parameter.IsByRef).ToArray()));
                        DeclareOptionalSignatures(symbols, qualifiedMethodName, typeof(void), method.Parameters);
                    }
                foreach (var method in current.Members.OfType<FunctionDeclarationSyntax>())
                {
                    if (method.Visibility?.Kind == SyntaxKind.PrivateKeyword) continue;
                    if (current != classDeclaration && classDeclaration.Members.OfType<FunctionDeclarationSyntax>().Any(own => SameClassMethodSignature(own, method)))
                        continue;
                    var returnName = method.ReturnType?.Identifier.Text;
                    var returnType = ResolveRuntimeType(returnName);
                    var parameterTypes = method.Parameters.Select(parameter => ResolveParameterType(parameter, symbols)).ToArray();
                    var qualifiedMethodName = $"{classDeclaration.Identifier.Text}.{method.Identifier.Text}";
                    var semanticReturnType = returnName is not null && classes.ContainsKey(returnName)
                        ? XpTypeSymbol.User(returnName) : XpTypeSymbol.FromClr(returnType);
                    symbols.Declare(new FunctionSymbol(qualifiedMethodName, returnType, parameterTypes.Select(type => type.RuntimeType).ToArray(), semanticReturnType, parameterTypes, method.Parameters.Select(parameter => parameter.IsByRef).ToArray()));
                    DeclareOptionalSignatures(symbols, qualifiedMethodName, returnType, method.Parameters);
                }
            }
        }
        foreach (Match match in Regex.Matches(fullSource, @"^\s*(?:Public\s+|Private\s+)?Class\s+(?<name>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            symbols.Declare(new TypeSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.User(match.Groups["name"].Value)));
        foreach (Match typeMatch in Regex.Matches(fullSource, @"(?ims)^\s*Type\s+(?<name>[A-Za-z_]\w*)\s*(?<body>.*?)^\s*End\s+Type\b"))
        {
            var typeName = typeMatch.Groups["name"].Value;
            symbols.Declare(new TypeSymbol(typeName, typeof(object), XpTypeSymbol.User(typeName)));
            foreach (Match fieldMatch in Regex.Matches(typeMatch.Groups["body"].Value, @"(?im)^\s*(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)\s*$"))
            {
                var fieldType = ResolveRuntimeType(fieldMatch.Groups["type"].Value);
                symbols.Declare(new PropertySymbol($"{typeName}.{fieldMatch.Groups["name"].Value}", fieldType, XpTypeSymbol.FromClr(fieldType)));
            }
        }
        foreach (var procedure in unit.Declarations.OfType<SubDeclarationSyntax>())
        {
            var parameterTypes = procedure.Parameters.Select(parameter => ResolveParameterType(parameter, symbols)).ToArray();
            var types = parameterTypes.Select(type => type.RuntimeType).ToArray();
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), types, null, parameterTypes, procedure.Parameters.Select(p => p.IsByRef).ToArray()));
            DeclareOptionalSignatures(symbols, procedure.Identifier.Text, typeof(void), procedure.Parameters);
        }
        foreach (var procedure in unit.Declarations.OfType<FunctionDeclarationSyntax>())
        {
            var parameterTypes = procedure.Parameters.Select(parameter => ResolveParameterType(parameter, symbols)).ToArray();
            var types = parameterTypes.Select(type => type.RuntimeType).ToArray();
            var resultType = ResolveRuntimeType(procedure.ReturnType?.Identifier.Text);
            symbols.Declare(new FunctionSymbol(procedure.Identifier.Text, resultType, types, AstClassMethodSupport.DeclaredType(procedure.ReturnType, symbols), parameterTypes, procedure.Parameters.Select(p => p.IsByRef).ToArray()));
            DeclareOptionalSignatures(symbols, procedure.Identifier.Text, resultType, procedure.Parameters);
        }
        var enumNames = Regex.Matches(fullSource, @"(?ims)^\s*Enum\s+(?<name>[A-Za-z_]\w*)\s*(?<body>.*?)^\s*End\s+Enum\b")
            .Cast<Match>().ToArray();
        foreach (var enumMatch in enumNames)
        {
            symbols.Declare(new VariableSymbol(enumMatch.Groups["name"].Value, typeof(object), XpTypeSymbol.User(enumMatch.Groups["name"].Value)));
            foreach (Match member in Regex.Matches(enumMatch.Groups["body"].Value, @"(?im)^\s*(?<name>[A-Za-z_]\w*)\s*(?:=\s*-?\d+)?\s*$"))
            {
                symbols.Declare(new VariableSymbol(member.Groups["name"].Value, typeof(object), XpTypeSymbol.User(enumMatch.Groups["name"].Value)));
                symbols.Declare(new PropertySymbol($"{enumMatch.Groups["name"].Value}.{member.Groups["name"].Value}", typeof(object), XpTypeSymbol.User(enumMatch.Groups["name"].Value)));
            }
        }
        foreach (var compatibilityName in AstObjectSupportRegistry.CompatibilityNames)
            symbols.Declare(new VariableSymbol(compatibilityName, typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new TypeSymbol("XPJsonSchema", typeof(object), XpTypeSymbol.User("XPJsonSchema")));
        if (usesNotesSession)
        {
            symbols.Declare(new TypeSymbol("XPScriptNotes", typeof(object), XpTypeSymbol.User("XPScriptNotes")));
            symbols.Declare(new VariableSymbol("XPScriptNotes", typeof(object), XpTypeSymbol.User("XPScriptNotes")));
            DeclareNativeStatic(symbols, "XPScriptNotes.CreateSession", [], "XPScriptNotes.CreateSession");
            DeclareNativeStatic(symbols, "XPScriptNotes.CreateSession", [typeof(object)], "XPScriptNotes.CreateSession");
            DeclareNativeStatic(symbols, "XPScriptNotes.RecycleValue", [typeof(object)], "XPScriptNotes.RecycleValue");
            DeclareNativeStatic(symbols, "XPScriptNotes.RecycleForReplacement", [typeof(object), typeof(object)], "XPScriptNotes.RecycleForReplacement");
            DeclareNativeStatic(symbols, "XPScriptNotes.NormalizeObjectResult", [typeof(object)], "XPScriptNotes.NormalizeObjectResult");
            DeclareNativeStatic(symbols, "XPScriptNotes.NothingValue", [], "XPScriptNotes.NothingValue");
            symbols.Declare(new TypeSymbol("XPScriptNotesValueApi", typeof(object), XpTypeSymbol.User("XPScriptNotesValueApi")));
            symbols.Declare(new VariableSymbol("XPScriptNotesValueApi", typeof(object), XpTypeSymbol.User("XPScriptNotesValueApi")));
            DeclareNativeStatic(symbols, "XPScriptNotesValueApi.GetItemValues", [typeof(object)], "XPScriptNotesValueApi.GetItemValues");
            DeclareNativeStatic(symbols, "XPScriptNotesValueApi.GetItemValueAt", [typeof(object), typeof(object)], "XPScriptNotesValueApi.GetItemValueAt");
            DeclareNativeStatic(symbols, "XPScriptNotesValueApi.GetDocumentItemValues", [typeof(object), typeof(object)], "XPScriptNotesValueApi.GetDocumentItemValues");
            DeclareNativeStatic(symbols, "XPScriptNotesValueApi.GetDocumentItemValueAt", [typeof(object), typeof(object), typeof(object)], "XPScriptNotesValueApi.GetDocumentItemValueAt");
        }
        symbols.Declare(new TypeSymbol("XPScriptIncrementRuntime", typeof(object), XpTypeSymbol.User("XPScriptIncrementRuntime")));
        symbols.Declare(new VariableSymbol("XPScriptIncrementRuntime", typeof(object), XpTypeSymbol.User("XPScriptIncrementRuntime")));
        DeclareNativeStatic(symbols, "XPScriptIncrementRuntime.Increment", [typeof(object)], "XPScriptIncrementRuntime.Increment");
        DeclareNativeStatic(symbols, "XPScriptIncrementRuntime.Decrement", [typeof(object)], "XPScriptIncrementRuntime.Decrement");
        DeclareNativeStatic(symbols, "XPScriptIncrementRuntime.Increment", [typeof(byte)], "XPScriptIncrementRuntime.Increment");
        DeclareNativeStatic(symbols, "XPScriptIncrementRuntime.Decrement", [typeof(byte)], "XPScriptIncrementRuntime.Decrement");
        DeclareNativeStatic(symbols, "XPJsonSchema.Parse", [typeof(object)], "XPScriptJsonSchema.Parse");
        DeclareNativeStatic(symbols, "XPJsonSchema.FromJson", [typeof(object)], "XPScriptJsonSchema.FromJson");
        if (usesArchive)
        {
            symbols.Declare(new TypeSymbol("XPScriptArchiveFactory", typeof(object), XpTypeSymbol.User("XPScriptArchiveFactory")));
            DeclareNativeStatic(symbols, "XPScriptArchiveFactory.Create", [], "XPScriptArchiveFactory.Create");
            DeclareNativeStatic(symbols, "XPScriptArchiveFactory.Create", [typeof(object)], "XPScriptArchiveFactory.Create");
            DeclareNativeStatic(symbols, "XPScriptArchiveFactory.Create", [typeof(object), typeof(bool)], "XPScriptArchiveFactory.Create");
            symbols.Declare(new TypeSymbol("XPScriptExtendedArchiveWriterFactory", typeof(object), XpTypeSymbol.User("XPScriptExtendedArchiveWriterFactory")));
            DeclareNativeStatic(symbols, "XPScriptExtendedArchiveWriterFactory.Create", [typeof(object)], "XPScriptExtendedArchiveWriterFactory.Create");
            symbols.Declare(new TypeSymbol("XPScriptArchiveIteratorRuntime", typeof(object), XpTypeSymbol.User("XPScriptArchiveIteratorRuntime")));
            DeclareNativeStatic(symbols, "XPScriptArchiveIteratorRuntime.GetFirstEntry", [typeof(object)], "XPScriptArchiveIteratorRuntime.GetFirstEntry");
            DeclareNativeStatic(symbols, "XPScriptArchiveIteratorRuntime.GetNextEntry", [typeof(object), typeof(object)], "XPScriptArchiveIteratorRuntime.GetNextEntry");
            DeclareNativeStatic(symbols, "XPScriptArchiveIteratorRuntime.EnumerateEntries", [typeof(object)], "XPScriptArchiveIteratorRuntime.EnumerateEntries");
            DeclareNativeStatic(symbols, "XPScriptArchiveIteratorRuntime.EnumerateFiles", [typeof(object)], "XPScriptArchiveIteratorRuntime.EnumerateFiles");
            DeclareNativeStatic(symbols, "XPScriptArchiveIteratorRuntime.EnumerateFolders", [typeof(object)], "XPScriptArchiveIteratorRuntime.EnumerateFolders");
        }
        var moduleFieldStorage = AstModuleFieldSupport.Find(moduleSource, symbols);
        foreach (var field in moduleFieldStorage)
            symbols.Declare(field.Symbol);
        foreach (var array in moduleArrayDeclarations)
            if (!symbols.LookupAll(array.Groups["name"].Value).Any())
            {
                var elementRuntimeType = ResolveRuntimeType(array.Groups["type"].Value) ?? typeof(object);
                var dimensions = array.Groups["bounds"].Value.Split(',').Length;
                var elementSemanticType = XpTypeSymbol.FromClr(elementRuntimeType);
                symbols.Declare(new VariableSymbol(
                    array.Groups["name"].Value,
                    dimensions == 1 ? elementRuntimeType.MakeArrayType() : elementRuntimeType.MakeArrayType(dimensions),
                    XpTypeSymbol.ArrayOf(elementSemanticType, dimensions)));
            }
        foreach (var match in allConstants)
            symbols.Declare(new VariableSymbol(match.Groups["name"].Value, typeof(object), XpTypeSymbol.Variant));
        foreach (var typeName in AstObjectSupportRegistry.VariantTypeNames)
            symbols.Declare(new VariableSymbol(typeName, typeof(object), BoundExpressionEmitter.RuntimeObjectType(typeName) is null ? XpTypeSymbol.Variant : XpTypeSymbol.User(typeName)));
        symbols.Declare(new VariableSymbol("Console", typeof(object), XpTypeSymbol.Variant));
        // Host compilers may inject these globals into the generated module
        // after AST binding (the web compiler is one such host). Declaring them
        // here keeps binding in the AST path instead of requiring a legacy
        // transpiler pass merely to recognize the host surface. Application,
        // Session, and Request also need their typed catalog identities so
        // members such as Request.State bind through the AST catalog.
        foreach (var hostGlobal in new[] { "Request", "Response", "Body", "Server", "RequestScope", "Session", "Application" })
            symbols.Declare(new VariableSymbol(hostGlobal, typeof(object),
                hostGlobal is "Application" or "Session" or "Request"
                    ? XpTypeSymbol.User(hostGlobal)
                    : XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("SEARCH_DEPTH", typeof(long), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("SEARCH_BREADTH", typeof(long), XpTypeSymbol.Variant));
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        symbols.Declare(new FunctionSymbol("Array", typeof(long[]), [typeof(long), typeof(long)]));
        symbols.Declare(new FunctionSymbol("Array", typeof(object[]), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CStr", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("UBound", typeof(long), [typeof(object)]));
        symbols.Declare(new FunctionSymbol("LBound", typeof(long), [typeof(object), typeof(long)]));
        symbols.Declare(new FunctionSymbol("UBound", typeof(long), [typeof(object), typeof(long)]));
        symbols.Declare(new FunctionSymbol("Base64DecodeBinary", typeof(object), [typeof(string)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Len", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LenB", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("TypeName", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("FileLen", typeof(long), [typeof(string)]));
        symbols.Declare(new FunctionSymbol("FileDateTime", typeof(DateTime), [typeof(string)], XpTypeSymbol.FromClr(typeof(DateTime)), [XpTypeSymbol.FromClr(typeof(string))]));
        symbols.Declare(new FunctionSymbol("GetFileAttr", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("SetFileAttr", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstFileCopy", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstNameFile", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Dir", typeof(string), [], XpTypeSymbol.FromClr(typeof(string)), []));
        for (var dirArgumentCount = 1; dirArgumentCount <= 3; dirArgumentCount++)
            symbols.Declare(new FunctionSymbol("Dir", typeof(string), Enumerable.Repeat(typeof(object), dirArgumentCount).ToArray(), XpTypeSymbol.FromClr(typeof(string)), Enumerable.Repeat(XpTypeSymbol.Variant, dirArgumentCount).ToArray()));
        foreach (var fileFunction in new[] { "ReadFile", "ReadLines", "ReadBytes", "FileHash", "Files", "Directories", "CopyFile", "MoveFile", "FileExists", "DirExists", "IsFile", "IsDir", "FileEquals", "WriteFile", "AppendFile", "WriteLines", "WriteBytes", "FileInfo", "MkDir", "RmDir" })
            for (var parameterCount = 1; parameterCount <= 4; parameterCount++)
                symbols.Declare(new FunctionSymbol(fileFunction, typeof(object), Enumerable.Repeat(typeof(object), parameterCount).ToArray(), XpTypeSymbol.Variant, Enumerable.Repeat(XpTypeSymbol.Variant, parameterCount).ToArray()));
        symbols.Declare(new FunctionSymbol("FreeFile", typeof(long), []));
        symbols.Declare(new FunctionSymbol("Seek", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CInt", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CLng", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CDbl", typeof(double), [typeof(object)], XpTypeSymbol.FromClr(typeof(double)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CBool", typeof(bool), [typeof(object)], XpTypeSymbol.FromClr(typeof(bool)), [XpTypeSymbol.Variant]));
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
        // A native Declare Sub/Function may intentionally use the built-in
        // name (for example Win32 Sleep). In that case the native declaration
        // is the authoritative overload; registering the Variant helper too
        // makes a strongly typed native call ambiguous.
        if (!nativeDeclarations.Any(native => native.Name.Equals("Sleep", StringComparison.OrdinalIgnoreCase)))
            symbols.Declare(new FunctionSymbol("Sleep", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Error", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Error", typeof(string), [], XpTypeSymbol.FromClr(typeof(string)), []));
        symbols.Declare(new FunctionSymbol("ErrorValue", typeof(string), [], XpTypeSymbol.FromClr(typeof(string)), []));
        if (!nativeDeclarations.Any(native => native.Name.Equals("GetTickCount", StringComparison.OrdinalIgnoreCase)))
            symbols.Declare(new FunctionSymbol("GetTickCount", typeof(long), [], XpTypeSymbol.FromClr(typeof(long)), []));
        symbols.Declare(new FunctionSymbol("MsgBox", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MsgBox", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MsgBox", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Loc", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LOF", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Environ", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstUtf8", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstPut", typeof(void), [typeof(object), typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstGet", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstSeek", typeof(void), [typeof(object), typeof(object)], null, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstReset", typeof(void), []));
        symbols.Declare(new FunctionSymbol("AstChDrive", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("AstChDir", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
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
        symbols.Declare(new FunctionSymbol("Delete", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Trim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("UCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("LCase", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySort", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Join", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("CDate", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        foreach (var builtin in new[] { "Base64Decode", "Base64Encode", "CByte", "CCur", "CSng", "CVar", "DataType", "DateAdd", "DateDiff", "Day", "FromBase64", "InstrB", "IsArray", "IsDate", "IsElement", "IsEmpty", "IsObject", "IsScalar", "LeftB", "ListTag", "LSet", "Month", "RegexValidate", "RightB", "RSet", "Space", "String", "StrLeft", "StrLeftBack", "StrReverse", "StrRight", "StrRightBack", "StrToken", "TimeNumber", "ToBase64", "UrlDecode", "UrlEncode", "MD5", "SHA1", "SHA256", "SHA384", "SHA512", "HMACSHA256", "HMACSHA384", "HMACSHA512" })
            symbols.Declare(new FunctionSymbol(builtin, typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("String", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("DateAdd", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("DateDiff", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("TimeNumber", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("DateValue", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("TimeValue", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Hour", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Minute", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Second", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("DatePart", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Weekday", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Weekday", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MonthName", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("MonthName", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("WeekdayName", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("WeekdayName", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
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
        symbols.Declare(new FunctionSymbol("ShellExecute", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.User("XPShellResult"), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ShellExecute", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.User("XPShellResult"), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Shell", typeof(long), [typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Shell", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Shell", typeof(long), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ShellArgs", typeof(long), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ShellArgs", typeof(long), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(long)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("StrTemplate", typeof(string), [typeof(object), typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
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
        symbols.Declare(new FunctionSymbol("ArrayGetIndex", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArrayUnique", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySlice", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySlice", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("ArraySplice", typeof(object), [typeof(object), typeof(object), typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        for (var argumentCount = 4; argumentCount <= 16; argumentCount += 2)
            symbols.Declare(new FunctionSymbol("ArrayResize", typeof(object), Enumerable.Repeat(typeof(object), argumentCount).ToArray(), XpTypeSymbol.Variant, Enumerable.Repeat(XpTypeSymbol.Variant, argumentCount).ToArray()));
        symbols.Declare(new FunctionSymbol("Explode", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("FullTrim", typeof(string), [typeof(object)], XpTypeSymbol.FromClr(typeof(string)), [XpTypeSymbol.Variant]));
        // DateNumber returns the boxed XpDate runtime carrier. Keep the source
        // expression Variant so assignments/ByRef arguments to the language
        // Date type insert the normal runtime carrier conversion.
        symbols.Declare(new FunctionSymbol("DateNumber", typeof(object), [typeof(long), typeof(long), typeof(long)],
            XpTypeSymbol.Variant, [XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long))]));
        symbols.Declare(new VariableSymbol("Debugger", typeof(object), XpTypeSymbol.User("Debugger")));
        symbols.Declare(new FunctionSymbol("Debugger.Print", typeof(void), [typeof(object)], null, [XpTypeSymbol.Variant])
        { NativeTargetName = "XPScriptDebugRuntime.Print" });
        symbols.Declare(new FunctionSymbol("Debugger.UpdateVar", typeof(void), [typeof(string), typeof(object)], null,
            [XpTypeSymbol.FromClr(typeof(string)), XpTypeSymbol.Variant]) { NativeTargetName = "XPScriptDebugRuntime.UpdateDebuggerVar" });
        symbols.Declare(new PropertySymbol("Date.OSDateFormatting", typeof(string), XpTypeSymbol.FromClr(typeof(string))));
        symbols.Declare(new PropertySymbol("Date.OSTimeFormatting", typeof(string), XpTypeSymbol.FromClr(typeof(string))));
        symbols.Declare(new FunctionSymbol("Date.Adjust", typeof(object),
            [typeof(long), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)],
            XpTypeSymbol.User("Date"),
            [XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long)),
             XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long)), XpTypeSymbol.FromClr(typeof(long))]));
        symbols.Declare(new FunctionSymbol("Date.Difference", typeof(double), [typeof(object)],
            XpTypeSymbol.FromClr(typeof(double)), [XpTypeSymbol.User("Date")]));
        symbols.Declare(new FunctionSymbol("Format", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        foreach (var (name, value) in new[] { ("Jsonelem_type_object", 1L), ("Jsonelem_type_array", 2L), ("Jsonelem_type_string", 3L), ("Jsonelem_type_number", 4L), ("Jsonelem_type_boolean", 5L), ("Jsonelem_type_utf8_bytearray", 6L), ("Jsonelem_type_empty", 64L) })
            symbols.Declare(new VariableSymbol(name, typeof(long), XpTypeSymbol.FromClr(typeof(long))));
        foreach (var name in new[] { "Date", "Date$", "Format", "Format$", "ChDrive", "Erase", "JsonParse" })
            symbols.Declare(new FunctionSymbol(name, typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("Date", typeof(object), [], XpTypeSymbol.Variant, []));
        symbols.Declare(new FunctionSymbol("Date$", typeof(object), [], XpTypeSymbol.Variant, []));
        symbols.Declare(new FunctionSymbol("InputBox", typeof(object), [typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("InputBox", typeof(object), [typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        symbols.Declare(new FunctionSymbol("InputBox", typeof(object), [typeof(object), typeof(object), typeof(object)], XpTypeSymbol.Variant, [XpTypeSymbol.Variant, XpTypeSymbol.Variant, XpTypeSymbol.Variant]));
        var usesApplication = AstApplicationSupport.UsesApplication(fullSource);
        var usesDatabaseAttachments = Regex.IsMatch(source, @"(?i)\.\s*Attachments\s*\(");
        var usesDatabaseDynamicRuntime = AstSqliteSupport.Uses(fullSource) || AstMySqlSupport.Uses(fullSource) || AstHttpDatabaseSupport.Uses(fullSource) ||
            AstMsSqlSupport.Uses(fullSource) || AstSupabaseSupport.Uses(fullSource) ||
            usesDatabaseAttachments;
        if (usesApplication)
            foreach (var mainSymbol in symbols.LookupAll("Main").OfType<FunctionSymbol>())
                mainSymbol.NativeTargetName = "__xps_application_main";
        symbols.Declare(new VariableSymbol("RequestScope", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Err", typeof(object), XpTypeSymbol.Variant));
        symbols.Declare(new VariableSymbol("Erl", typeof(object), XpTypeSymbol.Variant));
        // Bind class bodies before module entry points so body diagnostics are
        // not masked by a later overload error in Main. Cache the emitted
        // members so constructors and accessors are not emitted twice.
        var emittedClassMembers = classes.Values.ToDictionary(
            classDeclaration => classDeclaration.Identifier.Text,
            classDeclaration => AstClassMethodSupport.Emit(classDeclaration, symbols, sourcePath, fullSource, classes),
            StringComparer.OrdinalIgnoreCase);
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
            ValidateOptionalDeclarations(procedureParameters, sourcePath, fullSource);
            var scope = symbols.CreateChildScope();
            var parameters = procedureParameters.Select(parameter =>
            {
                var semanticType = ResolveParameterType(parameter, symbols);
                var runtimeType = parameter.IsArray ? typeof(Array) : semanticType.RuntimeType;
                return new ParameterSymbol(parameter.Identifier.Text, runtimeType, parameter.IsByRef, semanticType);
            }).ToArray();
            foreach (var parameter in parameters) scope.Declare(parameter);
            var semanticReturnType = procedureFunction is null ? null : AstClassMethodSupport.DeclaredType(procedureFunction.ReturnType, symbols);
            var returnType = semanticReturnType?.RuntimeType ?? typeof(void);
            var result = procedureFunction is null ? null : new LocalSymbol(resultName, returnType, semanticReturnType);
            var procedureStatements = procedureSub?.Statements ?? procedureFunction!.Statements;
            var bound = BindProcedure(procedureStatements, scope, parameters,
                semanticReturnType, procedureFunction?.Identifier.Text, result,
                (procedureSub?.Identifier.Text ?? procedureFunction!.Identifier.Text).ToUpperInvariant() +
                "(" + string.Join(",", parameters.Select(parameter => (parameter.IsByRef ? "ref:" : "value:") + parameter.Type.FullName + ":" + parameter.SemanticType?.Name)) + ")",
                sourcePath, source, fullSource);
            var name = procedureSub?.Identifier.Text ?? procedureFunction!.Identifier.Text;
            // Roslyn's console entry point is case-sensitive; XPscript is not.
            if (name.Equals("Main", StringComparison.OrdinalIgnoreCase) && parameters.Length == 0)
                name = "Main";
            if (usesApplication && name.Equals("Main", StringComparison.OrdinalIgnoreCase))
                name = "__xps_application_main";
            methods.Add(new BoundMethodDefinition(name, returnType, parameters, bound, false, semanticReturnType));
            var defaults = new BoundExpression?[parameters.Length];
            // Defaults are expressions in the procedure's declaration scope:
            // an earlier parameter may be referenced, while locals and later
            // parameters remain unavailable at declaration time.
            var defaultBinder = new ExpressionBinder(scope);
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!procedureParameters[i].IsOptional) continue;
                var type = parameters[i].SemanticType ?? XpTypeSymbol.FromClr(parameters[i].Type);
                var value = procedureParameters[i].DefaultValue is { } syntax
                    ? defaultBinder.Bind(syntax)
                    : new BoundLiteralExpression(type.IsArray ? null : parameters[i].Type == typeof(string) ? "" :
                        parameters[i].Type.IsValueType ? Activator.CreateInstance(parameters[i].Type) : null, parameters[i].Type, type);
                if (defaultBinder.Diagnostics.Count > 0)
                    throw SyntaxDiagnosticException(defaultBinder.Diagnostics[0], sourcePath, source, fullSource);
                var conversion = scope.ClassifyConversion(value.SemanticType, type);
                if (!conversion.IsImplicit)
                    throw MappedDiagnostic(procedureParameters[i].Span, sourcePath, fullSource,
                        $"Default value for Optional parameter '{parameters[i].Name}' is incompatible with its type.",
                        CompilerDiagnosticCodes.TypeMismatch);
                defaults[i] = conversion.IsIdentity ? value : new BoundConversionExpression(value, type, conversion) { Span = value.Span };
            }
            // Forwarding methods have real bodies and call the full signature;
            // they never manufacture a default return or skip procedure work.
            for (var count = parameters.Length - 1; count >= 0 && procedureParameters[count].IsOptional; count--)
                methods.Add(OptionalCallLowering.Forward(name, returnType, parameters, defaults, count));
        }
        var declarationName = sub?.Identifier.Text ?? function!.Identifier.Text;
        var methodName = declarationName;
        string body;
        try
        {
            body = new BoundCompilationUnitEmitter().EmitMembers(methods, source, sourcePath);
        }
        catch (BoundEmissionException ex)
        {
            var span = ex.Span ?? sub?.Span ?? function?.Span ?? new TextSpan(0, 0);
            throw MappedDiagnosticFromTransformed(span, sourcePath, source, fullSource, ex.Message, CompilerDiagnosticCodes.InvalidSyntax, "emission");
        }
        var moduleFields = string.Join(Environment.NewLine, moduleFieldStorage.Select(field => field.Declaration));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, allConstants
            .Select(match => $"    public static dynamic {match.Groups["name"].Value} = {ToCSharpLiteral(match.Groups["value"].Value.Trim())};"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, enumNames
            .Cast<Match>()
            .SelectMany(enumMatch => EnumMembers(enumMatch.Groups["body"].Value)
                .Select(member => (member.Name, member.Value)))
            .GroupBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(member => $"    public static dynamic {member.Name} = {member.Value};"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine,
            enumNames.Select(enumMatch => $"    public static Xp{enumMatch.Groups["name"].Value} {enumMatch.Groups["name"].Value} = new Xp{enumMatch.Groups["name"].Value}();"));
        moduleFields += Environment.NewLine + string.Join(Environment.NewLine, moduleArrayDeclarations
            .Select(match => $"    public static System.Array {match.Groups["name"].Value} = {ModuleArrayInitializer(match.Groups["bounds"].Value, match.Groups["type"].Value)};"));
        moduleFields += Environment.NewLine + "    public static long Jsonelem_type_object = 1L, Jsonelem_type_array = 2L, Jsonelem_type_string = 3L, Jsonelem_type_number = 4L, Jsonelem_type_boolean = 5L, Jsonelem_type_utf8_bytearray = 6L, Jsonelem_type_empty = 64L;";
        var entryPoint = usesApplication
            ? "public static void Main(string[] args) { XPScriptRuntime.SetArgs(args); XPScriptApplicationRuntime.SetArgs(args); " +
                (methods.Any(m => m.Name == "__xps_application_main" && m.Parameters.Count == 0 && m.ReturnType == typeof(void)) ? "__xps_application_main();" : "") + " }"
            : methodName.Equals("Main", StringComparison.OrdinalIgnoreCase) ? string.Empty : "    public static void Main() { }\n";
        var androidEntryPoint = runtimeIdentifier is "android-arm64" or "android-x64"
            ? usesApplication
                ? "public static void __xps_android_entry(string[] args) { XPScriptRuntime.SetArgs(args); XPScriptApplicationRuntime.SetArgs(args); " +
                    (methods.Any(m => m.Name == "__xps_application_main" && m.Parameters.Count == 0 && m.ReturnType == typeof(void)) ? "__xps_application_main();" : "") + " }"
                : $"public static void __xps_android_entry(string[] args) {{ XPScriptRuntime.SetArgs(args); {methodName}(); }}"
            : string.Empty;
        var optionCompareNoCase = Regex.IsMatch(fullSource, @"(?im)^\s*Option\s+Compare\s+NoCase\s*$");
        var userClassSupport = string.Join(Environment.NewLine, unit.Declarations.OfType<ClassDeclarationSyntax>().Select(classDeclaration =>
        {
            var fields = string.Join(" ", classDeclaration.Members.OfType<FieldDeclarationSyntax>().Select(field =>
                $"{(field.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private" : "public")} {BoundMethodEmitter.CSharpType(AstClassMethodSupport.DeclaredType(field.Type, symbols))} {field.Identifier.Text} = {(field.Type.Identifier.Text.Equals("String", StringComparison.OrdinalIgnoreCase) ? "string.Empty" : "default!")};"));
            var properties = string.Empty; // Accessor bodies are emitted with class procedures, never fabricated auto-properties.
            var baseClause = classDeclaration.BaseType is null
                ? string.Empty
                : $" : Xp{classes[classDeclaration.BaseType.Identifier.Text].Identifier.Text}";
            var imageCleanup = string.Join(" ", classDeclaration.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => AstImageSupport.IsImage(field.Type.Identifier.Text))
                .Select(field => $"AstImageScope.StoreField(ref this.{field.Identifier.Text}, null);"));
            var gcCleanup = imageCleanup.Length == 0
                ? string.Empty
                : $"~Xp{classDeclaration.Identifier.Text}() {{ {imageCleanup} }}";
            var explicitCleanup = imageCleanup.Length == 0
                ? string.Empty
                : imageCleanup + " System.GC.SuppressFinalize(this);";
            var delete = classDeclaration.BaseType is null
                ? "private bool __xps_delete_started; " + $"public virtual void Delete() {{ if (__xps_delete_started) return; __xps_delete_started = true; {explicitCleanup} }}"
                : "private bool __xps_delete_started; " + $"public override void Delete() {{ if (__xps_delete_started) return; __xps_delete_started = true; {explicitCleanup} base.Delete(); }}";
            var memberBodies = emittedClassMembers[classDeclaration.Identifier.Text];
            if (classDeclaration.Members.OfType<DestructorDeclarationSyntax>().Any())
            {
                var cleanup = classDeclaration.BaseType is null ? explicitCleanup
                    : $"try {{ {explicitCleanup} }} finally {{ base.Delete(); }}";
                delete = "private bool __xps_destructor_started; public " + (classDeclaration.BaseType is null ? "virtual" : "override") + " void Delete() { " +
                    "if (__xps_destructor_started) return; __xps_destructor_started = true; " +
                    $"try {{ __xps_destructor_body(); }} finally {{ {cleanup} }} }}";
            }
            // Script classes are assembly-local implementation types, like the native
            // runtime types exposed by their fields and accessor signatures.
            var constructorBody = AstClassConstructorSupport.Emit(classDeclaration, symbols, sourcePath, fullSource);
            // Non-virtual cleanup is bound to the constructor's completed ancestry;
            // never dispatch Delete or a not-yet-initialized derived override here.
            var constructionCleanup = classDeclaration.BaseType is null
                ? $"protected void __xps_cleanup_failed_construction() {{ {imageCleanup} }}"
                : $"protected new void __xps_cleanup_failed_construction() {{ try {{ {imageCleanup} }} finally {{ base.__xps_cleanup_failed_construction(); }} }}";
            return $"internal class Xp{classDeclaration.Identifier.Text}{baseClause} {{ {fields} {properties} {gcCleanup} {constructorBody} {constructionCleanup} {delete} {memberBodies} }}";
        }));
        if (userClassSupport.Length == 0)
        {
            userClassSupport = string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?is)\bClass\s+(?<name>[A-Za-z_]\w*)\b(?<body>.*?)\bEnd\s+Class\b")
                .Cast<Match>().Select(match =>
                {
                    var fields = string.Join(" ", Regex.Matches(match.Groups["body"].Value, @"(?im)^\s*(?:Public|Private)?\s*(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)")
                        .Cast<Match>().Select(field => $"public object? {field.Groups["name"].Value} {{ get; set; }}"));
                    return $"public sealed class Xp{match.Groups["name"].Value} {{ {fields} }}";
                }));
        }
        var classSupport = "namespace __xps_user_classes { using static global::Program; " + userClassSupport + " }" + Environment.NewLine;
        classSupport += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?ims)^\s*Type\s+(?<name>[A-Za-z_]\w*)\s*(?<body>.*?)^\s*End\s+Type\b")
            .Cast<Match>().Select(match =>
            {
                var fields = string.Join(" ", Regex.Matches(match.Groups["body"].Value, @"(?im)^\s*(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)\s*$")
                    .Cast<Match>().Select(field => $"public {field.Groups["type"].Value.Trim().ToUpperInvariant() switch { "INTEGER" or "LONG" => "long", "DOUBLE" or "SINGLE" or "CURRENCY" => "double", "BYTE" => "byte", "BOOLEAN" => "bool", "STRING" => "string", _ => "object?" }} {field.Groups["name"].Value} {{ get; set; }}"));
                return $"public sealed class Xp{match.Groups["name"].Value} {{ {fields} }}";
            }));
        classSupport += Environment.NewLine + string.Join(Environment.NewLine, Regex.Matches(fullSource, @"(?ims)^\s*Enum\s+(?<name>[A-Za-z_]\w*)\s*(?<body>.*?)^\s*End\s+Enum\b")
            .Cast<Match>().Select(match =>
            {
                    var values = EnumMembers(match.Groups["body"].Value)
                    .Select(value => $"public Xp{match.Groups["name"].Value} {value.Name} => new({value.Value});");
                return $"public sealed class Xp{match.Groups["name"].Value} {{ public long Value {{ get; }} public Xp{match.Groups["name"].Value}() {{ }} private Xp{match.Groups["name"].Value}(long value) => Value = value; public static implicit operator Xp{match.Groups["name"].Value}(int value) => new(value); public static implicit operator Xp{match.Groups["name"].Value}(long value) => new(value); {string.Join(" ", values)} public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture); }}";
            }));
        var nativeCode = nativeDeclarations.Count == 0 ? string.Empty : "internal static class XPNativeDeclarations {" + Environment.NewLine + string.Join(Environment.NewLine, nativeDeclarations.Select(native =>
        {
            var nativeReturn = NativeCSharpType(native.ReturnType);
            var rawParameters = string.Join(", ", native.Parameters.Select((parameter, index) =>
            {
                var match = Regex.Match(parameter, @"(?i)(?:ByVal|ByRef)?\s*(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)");
                var type = NativeCSharpType(match.Groups["type"].Value);
                return $"{type} p{index}";
            }));
            var wrapperParameters = string.Join(", ", native.Parameters.Select((parameter, index) => $"object? p{index}"));
            var rawArguments = string.Join(", ", native.Parameters.Select((parameter, index) =>
            {
                var match = Regex.Match(parameter, @"(?i)(?:ByVal|ByRef)?\s*(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)");
                return NativeConversion(match.Groups["type"].Value, $"p{index}");
            }));
            var library = native.Library.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var alias = native.Alias.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var wrapperReturn = nativeReturn;
            var invocation = $"__{native.Name}({rawArguments})";
            var charset = native.Unicode ? "Unicode" : "Ansi";
            return $"[System.Runtime.InteropServices.DllImport(\"{library}\", EntryPoint = \"{alias}\", CharSet = System.Runtime.InteropServices.CharSet.{charset})] private static extern {nativeReturn} __{native.Name}({rawParameters}); public static {wrapperReturn} {native.Name}({wrapperParameters}) {{ {(wrapperReturn == "void" ? invocation + ";" : "return " + invocation + ";")} }}";
        })) + Environment.NewLine + "}";
        var generated = $$"""
using System;
using __xps_user_classes;
using System.Collections.Generic;
using System.Collections;
using System.Dynamic;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using XpJsonDocument = XPScriptJsonDocument;
using XpJsonObject = XPScriptJsonObject;
using XpJsonArray = XPScriptJsonArray;
using XpJsonElement = XPScriptJsonElement;
{{AstJsonRuntimeSource.Code}}
{{(Regex.IsMatch(fullSource, @"(?i)\b(ShowDialog|LoadFileDialog|OpenFileDialog|SaveFileDialog)\b") ? UIDialogRuntimeSource.Code : "")}}
{{(usesArchive ? ArchiveRuntimeSource.Code + ArchiveMemoryRuntimeSource.Code + ArchiveIteratorRuntimeSource.Code : "")}}
{{(usesArchive && source.Contains("XPScriptExtendedArchive", StringComparison.Ordinal) ? ArchiveExtendedReaderRuntimeSource.Code + ArchiveExtendedWriterRuntimeSource.Code + ArchiveExtendedWriterFactoryRuntimeSource.Code : "")}}
{{(usesNetworkTools ? NetworkToolsRuntimeSource.Code : "")}}
{{((usesAi || AstObjectSupportRegistry.UsesObject(fullSource, "XPJsonSchema") || AstObjectSupportRegistry.UsesObject(fullSource, "XPJsonValidationResult")) ? JsonSchemaRuntimeSource.Code : "")}}
{{AstRuntimeSource.Code}}
{{(usesNotesSession ? NotesRuntimeSourceBuilder.Build() : "")}}
{{OperatorArrayCompatibilityRuntimeSource.Code}}
{{VariantIndexRuntimeSource.Code}}
{{AstImageSupport.RuntimeCode(fullSource)}}
{{AstCsvSupport.RuntimeCode(fullSource)}}
{{AstXmlSupport.RuntimeCode(fullSource)}}
{{AstSpreadsheetSupport.RuntimeCode(fullSource)}}
{{AstApplicationSupport.RuntimeCode(fullSource)}}
{{(AstSystemInventorySupport.Uses(fullSource) ? SystemInventoryRuntimeSource.Code : "")}}
{{(usesDatabaseDynamicRuntime ? CaseInsensitiveDynamicObjectRuntimeSource.Code : "")}}
{{(AstSqliteSupport.Uses(fullSource) ? SqliteDbRuntimeSource.Code : "")}}
{{(AstMsSqlSupport.Uses(fullSource) ? MsSqlDbRuntimeSource.Code : "")}}
{{(AstSupabaseSupport.Uses(fullSource) ? SupabaseDbRuntimeSource.Code : "")}}
{{(usesDatabaseDynamicRuntime ? DatabaseUiDataSourceRuntimeSource.Build(AstSqliteSupport.Uses(fullSource), AstMsSqlSupport.Uses(fullSource), true) : "")}}
{{((usesDatabaseDynamicRuntime || usesDatabaseAttachments) ? HttpCoreRuntimeSource.Code + NativeHttpRuntimeSource.Code + HttpJsonRuntimeSource.Code + HttpDbRuntimeSource.Code + AstTlsRuntimeCode() + DatabaseAttachmentRuntimeV2Source.Build(AstSqliteSupport.Uses(fullSource), AstMsSqlSupport.Uses(fullSource)) + DatabaseAttachmentRuntimeV3Source.Code : "")}}
{{AstHttpSupport.RuntimeCode(fullSource)}}
{{(AstNotesJsonSupport.Uses(fullSource) || Regex.IsMatch(fullSource, @"(?i)\bNotesHTTPRequest\b") ? JsonHttpCompatibilityRuntimeSource.Code + (AstHttpSupport.RuntimeCode(fullSource).Contains("XPScriptTlsValidationState", StringComparison.Ordinal) || runtimeFeatures.Http ? "" : AstTlsRuntimeCode()) : "")}}
{{((usesArchive || AstCsvSupport.UsesDocument(fullSource) || AstSpreadsheetSupport.UsesSpreadsheet(fullSource)) && !AstObjectSupportRegistry.UsesObject(fullSource, "XPImage") ? FileSystemPortabilityRuntimeSource.Code : "")}}
{{XPScriptListRuntimeSource.Code}}
{{AstArrayRuntimeCode()}}
{{AstFileRuntimeCode()}}
{{AstCallbackRuntimeCode()}}
{{((runtimeFeatures.Http || usesAi) && !AstHttpSupport.RuntimeCode(fullSource).Contains("XPScriptTlsValidationState", StringComparison.Ordinal) ? AstTlsRuntimeCode() : "")}}
{{AstErrorRuntimeCode()}}
{{nativeCode}}
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
{{ForAllRuntimeSource.Code}}
{{classSupport}}
internal sealed class LSConsoleRuntime
{
    public Action<string, bool>? AndroidLog { get; set; }

    public void WriteLine(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (AndroidLog is not null) AndroidLog(text, false);
        else System.Console.WriteLine(text);
    }

    public void Write(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (AndroidLog is not null) AndroidLog(text, false);
        else System.Console.Write(text);
    }

    public string ReadLine() => System.Console.ReadLine() ?? string.Empty;
    public void Beep() => System.Console.Beep();
}
internal static class Program
{
    {{(AstObjectSupportRegistry.UsesObject(fullSource, "XPImage") || AstSpreadsheetSupport.UsesSpreadsheet(fullSource) ? "static Program() { XPScriptFileSystemRuntime.SetScriptDirectory(" + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, true) + "); }" : "")}}
    private static readonly bool OptionCompareNoCase = {{optionCompareNoCase.ToString().ToLowerInvariant()}};
    public static bool True = true;
    public static bool False = false;
    public static LSConsoleRuntime Console = new();
    public static string AstPrintText(object? value) => XPScriptNullRuntime.IsNull(value) ? "Variable is null" : value is ILSObjectReference reference && reference.IsNothing ? "Variable is Nothing" : value is DateTime date ? date.ToString(CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    public static dynamic RequestScope = new ExpandoObject();
    public static dynamic Err => XPScriptErrorRuntime.Err;
    public static dynamic Erl => XPScriptErrorRuntime.Erl;
    public static dynamic SEARCH_BREADTH = 1723L;
    public static dynamic SEARCH_DEPTH = 1724L;
    public static dynamic Command => XPScriptRuntime.Command();
    public static dynamic Platform => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "MacOS" : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "Linux" : "Unknown";
    public static dynamic CurDir => XPScriptRuntime.CurDir();
    public static string CStr(object? value) => XPScriptRuntime.CStr(value);
    public static string StrTemplate(object? template, object? values)
    {
        var items = values switch
        {
            Array array => array.Cast<object?>().ToArray(),
            IEnumerable enumerable => enumerable.Cast<object?>().ToArray(),
            _ => throw new XPScriptRuntimeException(13, "StrTemplate values must be an array or list.")
        };
        var text = CStr(template);
        text = Regex.Replace(text, @"(?<!\\)\{(\d+)\}", match =>
        {
            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if ((uint)index >= (uint)items.Length) throw new XPScriptRuntimeException(9, "StrTemplate placeholder index is outside the supplied values array.");
            return CStr(items[index]);
        });
        return text.Replace("\\{", "{", StringComparison.Ordinal).Replace("\\}", "}", StringComparison.Ordinal);
    }
    // The shared List runtime requires the date coercion entry point too.
    // Preserve existing XPscript date wrappers and numeric OLE date values.
    public static DateTime CDat(object? value)
    {
        if (value is XpDate date) return date.Value;
        if (value is DateTime dateTime) return dateTime;
        if (value is null) return new DateTime(1899, 12, 30);
        if (value is IConvertible && value is not string)
            return DateTime.FromOADate(Convert.ToDouble(value, CultureInfo.CurrentCulture));
        var text = CStr(value).Trim();
        return double.TryParse(text, System.Globalization.NumberStyles.Any, CultureInfo.CurrentCulture, out var number)
            ? DateTime.FromOADate(number) : DateTime.Parse(text, CultureInfo.CurrentCulture);
    }
    public static long LBound(object value) => LSArrayRuntime.LBound(value);
    public static long UBound(object value) => LSArrayRuntime.UBound(value);
    public static object Base64DecodeBinary(object value) => Convert.FromBase64String(CStr(value));
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
    public static long[] Array(long first, long second) => [first, second];
    public static object[] Array(object first, object second) => [first, second];
    public static object[] Array(params object?[] values) => values!;
    public static long Len(object? value) => XPScriptRuntime.Len(value);
    public static long LenB(object? value) => XPScriptRuntime.LenB(value);
    public static string TypeName(object? value) => XPScriptNullRuntime.TypeName(value);
    public static long FileLen(string path) => new System.IO.FileInfo(RequireExistingFilePath(path)).Length;
    public static DateTime FileDateTime(string path) => System.IO.File.GetLastWriteTime(RequireExistingFilePath(path));
    public static long GetFileAttr(object? path) => (long)System.IO.File.GetAttributes(RequireExistingFilePath(path));
    public static void SetFileAttr(object? path, object? attributes) => System.IO.File.SetAttributes(RequireExistingFilePath(path), (System.IO.FileAttributes)XPScriptRuntime.CLng(attributes));
    private static System.Collections.Generic.IEnumerator<string>? __xpsDirEnumerator;
    public static string Dir(params object?[] args)
    {
        if (args.Length > 0 && args[0] is not null)
        {
            var raw = CStr(args[0]);
            var directory = System.IO.Path.GetDirectoryName(raw);
            if (string.IsNullOrEmpty(directory)) directory = System.Environment.CurrentDirectory;
            var mask = System.IO.Path.GetFileName(raw);
            if (string.IsNullOrEmpty(mask)) mask = "*";
            __xpsDirEnumerator?.Dispose();
            __xpsDirEnumerator = System.IO.Directory.EnumerateFileSystemEntries(directory, mask)
                .Select(System.IO.Path.GetFileName).Where(value => value is not null).Cast<string>().GetEnumerator();
        }
        if (__xpsDirEnumerator is null || !__xpsDirEnumerator.MoveNext())
        {
            __xpsDirEnumerator?.Dispose();
            __xpsDirEnumerator = null;
            return string.Empty;
        }
        return __xpsDirEnumerator.Current;
    }
    public static object ReadFile(params object?[] a) => XPScriptTextFileRuntime.ReadFile(a[0], a.Length > 1 ? a[1] : null);
    public static object ReadLines(params object?[] a) => System.IO.File.ReadAllLines(RequireExistingFilePath(a[0]));
    public static object ReadBytes(params object?[] a) => System.IO.File.ReadAllBytes(RequireExistingFilePath(a[0]));
    public static object FileHash(params object?[] a) {
        using System.Security.Cryptography.HashAlgorithm h = (a.Length > 1 ? CStr(a[1]).ToUpperInvariant() : "SHA256") switch {
            "SHA384" => System.Security.Cryptography.SHA384.Create(),
            "SHA512" => System.Security.Cryptography.SHA512.Create(),
            _ => System.Security.Cryptography.SHA256.Create()
        };
        return Convert.ToHexString(h.ComputeHash(System.IO.File.ReadAllBytes(RequireExistingFilePath(a[0])))).ToLowerInvariant();
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
    public static object IsDir(params object?[] a) => System.IO.Directory.Exists(NormalizeDirectoryProbe(CStr(a[0])));
    public static object FileExists(params object?[] a) => System.IO.File.Exists(CStr(a[0]));
    public static object DirExists(params object?[] a) => System.IO.Directory.Exists(NormalizeDirectoryProbe(CStr(a[0])));
    private static string NormalizeDirectoryProbe(string path) =>
        OperatingSystem.IsWindows() && path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':'
            ? path + System.IO.Path.DirectorySeparatorChar
            : path;
    public static object FileEquals(params object?[] a) => System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(RequireExistingFilePath(a[0])), System.IO.File.ReadAllBytes(RequireExistingFilePath(a[1])));
    public static object CopyFile(params object?[] a) {
        var source = RequireExistingFilePath(a[0]); var target = RequireWritableFilePath(a[1]); var action = a.Length > 2 ? Convert.ToInt32(a[2]) : 1;
        if (System.IO.File.Exists(target)) { if (action == 3) return false; if (action != 2) return false; }
        System.IO.File.Copy(source, target, action == 2); return true;
    }
    public static object MoveFile(params object?[] a) {
        var source = RequireExistingFilePath(a[0]); var target = RequireWritableFilePath(a[1]); var action = a.Length > 2 ? Convert.ToInt32(a[2]) : 1;
        if (System.IO.File.Exists(target)) { if (action == 3) return false; if (action != 2) return false; }
        System.IO.File.Move(source, target, action == 2); return true;
    }
    public static object WriteFile(params object?[] a) { System.IO.File.WriteAllText(RequireWritableFilePath(a[0]), CStr(a[1])); return true; }
    public static object AppendFile(params object?[] a) { System.IO.File.AppendAllText(RequireWritableFilePath(a[0]), CStr(a[1])); return true; }
    public static object WriteLines(params object?[] a) { System.IO.File.WriteAllLines(RequireWritableFilePath(a[0]), ((System.Collections.IEnumerable)a[1]!).Cast<object?>().Select(CStr)); return true; }
    public static object WriteBytes(params object?[] a) { System.IO.File.WriteAllBytes(RequireWritableFilePath(a[0]), (byte[])a[1]!); return true; }
    private static string RequireExistingFilePath(object? value) { var p = System.IO.Path.GetFullPath(CStr(value)); if (!System.IO.File.Exists(p)) throw new FileNotFoundException("File not found.", p); return p; }
    private static string RequireWritableFilePath(object? value) { var p = System.IO.Path.GetFullPath(CStr(value)); var parent = System.IO.Path.GetDirectoryName(p); if (string.IsNullOrWhiteSpace(parent) || !System.IO.Directory.Exists(parent)) throw new DirectoryNotFoundException("Destination directory does not exist."); return p; }
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
        public XpPath(object? path) { _path = XPScriptRuntime.CStr(path); }
        public string FileName() => System.IO.Path.GetFileName(_path);
        public string FileNameWithoutExtension() => System.IO.Path.GetFileNameWithoutExtension(_path);
        public string Extension() => System.IO.Path.GetExtension(_path);
        public string Directory() => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path)) ?? string.Empty;
        public string Root() => System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(_path)) ?? string.Empty;
        public bool Exists() => System.IO.File.Exists(_path) || System.IO.Directory.Exists(_path);
        public string Parent() => System.IO.Path.GetDirectoryName(_path) ?? string.Empty;
        public string Absolute() => System.IO.Path.GetFullPath(_path);
        public string Normalize() => System.IO.Path.GetFullPath(_path);
        public string ChangeExtension(object? extension) => System.IO.Path.ChangeExtension(_path, XPScriptRuntime.CStr(extension)) ?? string.Empty;
        public bool IsAbsolute() => System.IO.Path.IsPathRooted(_path);
        public string Relative(object? other) => System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath(_path), System.IO.Path.GetFullPath(XPScriptRuntime.CStr(other)));
        public string Combine(object? child) => System.IO.Path.Combine(_path, XPScriptRuntime.CStr(child));
    }
    public static object FileInfo(params object?[] a) {
        var path = System.IO.Path.GetFullPath(CStr(a[0]));
        if (System.IO.Directory.Exists(path)) {
            var directory = new System.IO.DirectoryInfo(path);
            return new XpFileInfo { Name = directory.Name, FullPath = directory.FullName, Extension = string.Empty,
                Length = 0, IsFile = false, IsDirectory = true, IsLink = directory.LinkTarget is not null,
                Attributes = (long)directory.Attributes, Created = directory.CreationTime,
                Modified = directory.LastWriteTime, Accessed = directory.LastAccessTime };
        }
        if (!System.IO.File.Exists(path)) throw new FileNotFoundException("Path not found.", path);
        var info = new System.IO.FileInfo(path);
        return new XpFileInfo { Name = info.Name, FullPath = info.FullName, Extension = info.Extension,
            Length = info.Exists ? info.Length : 0, IsFile = info.Exists, IsDirectory = false,
            IsLink = info.LinkTarget is not null, Attributes = (long)info.Attributes,
            Created = info.CreationTime, Modified = info.LastWriteTime, Accessed = info.LastAccessTime };
    }
    public static object MkDir(params object?[] a) { System.IO.Directory.CreateDirectory(CStr(a[0])); return true; }
    public static object RmDir(params object?[] a) { System.IO.Directory.Delete(CStr(a[0]), true); return true; }
    public static long FreeFile() => LSFileRuntime.FreeFile();
    public static long Seek(object? file) => LSFileRuntime.Seek(checked((int)XPScriptRuntime.CLng(file)));
    public static long CInt(object value) => XPScriptRuntime.CInt(value);
    public static long CLng(object value) => XPScriptRuntime.CLng(value);
    public static double CDbl(object value) => XPScriptRuntime.CDbl(value);
    public static bool CBool(object value) => XPScriptRuntime.CBool(value);
    public static string Chr(object value) => Convert.ToChar(value).ToString();
    public static long Asc(object value) => Convert.ToChar(value);
    public static string Replace(string value, string oldValue, string newValue) => value.Replace(oldValue, newValue, StringComparison.Ordinal);
    public static long InStr(object? start, object? value, object? search) { var source = value?.ToString() ?? string.Empty; var needle = search?.ToString() ?? string.Empty; var offset = Math.Max(0, Convert.ToInt32(start) - 1); var index = source.IndexOf(needle, offset, StringComparison.OrdinalIgnoreCase); return index < 0 ? 0 : index + 1; }
    public static long InStr(object? value, object? search) => InStr(1L, value, search);
    public static string Trim(object? value) => XPScriptRuntime.Trim(value);
    public static string UCase(object? value) => XPScriptRuntime.UCase(value);
    public static string LCase(object? value) => XPScriptRuntime.LCase(value);
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
            System.Array.Sort(result, Comparer<object?>.Create((left, right) =>
                left is XpDate leftDate && right is XpDate rightDate ? leftDate.CompareTo(rightDate) :
                left is IComparable comparable ? comparable.CompareTo(right) : 0));
        if (value.GetType().IsArray) {
            var typed = System.Array.CreateInstance(value.GetType().GetElementType()!, result.Length);
            result.CopyTo(typed, 0); return typed;
        }
        return result;
    }
    public static string Join(object? value, object? separator = null) => value is System.Collections.IEnumerable items ? string.Join(separator is null ? " " : CStr(separator), items.Cast<object?>().Where(item => item is not null).Select(CStr)) : string.Empty;
    public static string Implode(object? value, object? separator = null) => Join(value, separator);
    public static object CDate(object value) => new XpDate(XPScriptRuntime.CDate(value));
    public static object Base64Decode(params object?[] values) => values.Length == 0 ? string.Empty : Convert.FromBase64String(CStr(values[0]));
    public static object Base64Encode(params object?[] values) => values.Length == 0 ? string.Empty : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CStr(values[0])));
    public static byte CByte(object value) => XPScriptRuntime.CByte(value);
    public static object CCur(object value) => XPScriptRuntime.CCur(value);
    public static object CSng(object value) => XPScriptRuntime.CSng(value);
    public static object CVar(object value) => XPScriptRuntime.CVar(value);
    public static object DataType(object value) => XPScriptNullRuntime.DataType(value);
    public static object DateAdd(params object?[] values) => values.Length > 2
        ? new XpDate(XPScriptRuntime.DateAdd(CStr(values[0]), Convert.ToDouble(values[1]), ToDateTime(values[2])))
        : new XpDate(DateTime.Now);
    public static object DateDiff(params object?[] values) => values.Length > 2
        ? XPScriptRuntime.DateDiff(CStr(values[0]), ToDateTime(values[1]), ToDateTime(values[2]))
        : 0L;
    public static object DatePart(object? interval, object? value) => XPScriptRuntime.DatePart(CStr(interval), ToDateTime(value));
    public static object Weekday(object? value, object? firstDay = null) => XPScriptRuntime.Weekday(ToDateTime(value), firstDay is null ? 1 : Convert.ToInt32(firstDay));
    public static object MonthName(object? month, object? abbreviate = null) => XPScriptRuntime.MonthName(Convert.ToInt32(month), abbreviate is not null && Convert.ToBoolean(abbreviate));
    public static object WeekdayName(object? weekday, object? abbreviate = null) => XPScriptRuntime.WeekdayName(Convert.ToInt32(weekday), abbreviate is not null && Convert.ToBoolean(abbreviate));
    public static object Day(object value) => ToDateTime(value).Day;
    public static object FromBase64(object value) => Base64Decode(value);
    public static object InstrB(params object?[] values) => InStr(1L, values.Length > 1 ? values[0] : null, values.Length > 1 ? values[1] : null);
    public static object IsArray(object value) => XPScriptRuntime.IsArray(value);
    public static object IsDate(object value) => DateTime.TryParse(CStr(value), out _);
    public static object IsElement(object value) => value is not null;
    public static object IsEmpty(object value) => value is null;
    public static object IsObject(object value) => XPScriptNullRuntime.IsObject(value);
    public static object IsScalar(object value) => XPScriptNullRuntime.IsScalar(value);
    public static object LeftB(object value, object count) => Left(value, count);
    public static object ListTag(object value) => value is System.Collections.DictionaryEntry entry ? CStr(entry.Key) : value.GetType().GetProperty("Key")?.GetValue(value)?.ToString() ?? string.Empty;
    public static object LSet(object value, object width) => CStr(value).PadRight(Convert.ToInt32(width));
    public static object Mid(object value, object start, object? count = null)
    {
        return count is null ? XPScriptRuntime.Mid(value, Convert.ToInt32(start)) : XPScriptRuntime.Mid(value, Convert.ToInt32(start), Convert.ToInt32(count));
    }
    public static object MidB(object value, object start, object? count = null) => Mid(value, start, count);
    public static object MidBP(object value, object start, object? count = null) => Mid(value, start, count);
    public static object Month(object value) => ToDateTime(value).Month;
    public static object Hour(object? value) => XPScriptRuntime.Hour(ToDateTime(value));
    public static object Minute(object? value) => XPScriptRuntime.Minute(ToDateTime(value));
    public static object Second(object? value) => XPScriptRuntime.Second(ToDateTime(value));
    public static object RegexValidate(object value, object pattern) => System.Text.RegularExpressions.Regex.IsMatch(CStr(value), CStr(pattern));
    public static object RightB(object value, object count) => Right(value, count);
    public static object Rnd(object? value = null) => Random.Shared.NextDouble();
    public static long ShellId(object? program, object? windowStyle = null)
    {
        var file = XPScriptRuntime.CStr(program).Trim();
        if (file.Length == 0)
            throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new XPScriptRuntimeException(5, "Shell could not start the program.");
            return process.Id;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new XPScriptRuntimeException(53, ex.Message);
        }
    }
    public static long Shell(object? command, object? windowStyle = null, object? unused = null)
    {
        var text = CStr(command).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");
        var startInfo = OperatingSystem.IsWindows()
            ? new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + text)
            : new System.Diagnostics.ProcessStartInfo("/bin/sh", "-c \"" + text.Replace("\\\"", "\\\\\"", StringComparison.Ordinal) + "\"");
        startInfo.UseShellExecute = false;
        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new XPScriptRuntimeException(5, "Shell could not start the command.");
        return process.Id;
    }
    public static long ShellArgs(object? executable, object? arguments, object? windowStyle = null)
    {
        var file = CStr(executable).Trim();
        if (file.Length == 0) throw new XPScriptRuntimeException(5, "ShellArgs requires a program name.");
        var startInfo = new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = false };
        if (arguments is Array array)
            foreach (var item in array) startInfo.ArgumentList.Add(CStr(item));
        else if (arguments is IEnumerable enumerable)
            foreach (var item in enumerable) startInfo.ArgumentList.Add(CStr(item));
        else throw new XPScriptRuntimeException(5, "ShellArgs arguments must be an array or list.");
        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new XPScriptRuntimeException(5, "ShellArgs could not start the command.");
        return process.Id;
    }
    public sealed class XpShellResult { public int ExitCode { get; init; } public string Output { get; init; } = string.Empty; public string Error { get; init; } = string.Empty; public bool TimedOut { get; init; } }
    public static XpShellResult ShellExecute(object? executable, object? arguments, object? timeoutMilliseconds = null)
    {
        var file = CStr(executable).Trim();
        if (file.Length == 0) throw new XPScriptRuntimeException(5, "ShellExecute requires a program name.");
        var timeout = timeoutMilliseconds is null ? 0 : Convert.ToInt32(timeoutMilliseconds);
        if (timeout < 0) throw new XPScriptRuntimeException(5, "ShellExecute timeoutMilliseconds must be zero or greater.");
        if (arguments is string || arguments is not IEnumerable)
            throw new XPScriptRuntimeException(5, "ShellExecute arguments must be an array or list, not a command string.");
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = file,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in (IEnumerable)arguments)
            startInfo.ArgumentList.Add(CStr(argument));
        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new XPScriptRuntimeException(5, "ShellExecute could not start the program.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            var completed = timeout == 0 ? process.WaitForExitAsync().Wait(TimeSpan.FromMinutes(10)) : process.WaitForExit(timeout);
            if (!completed) { try { process.Kill(true); } catch { } return new XpShellResult { ExitCode = -1, Output = output.Result, Error = error.Result, TimedOut = true }; }
            return new XpShellResult { ExitCode = process.ExitCode, Output = output.Result, Error = error.Result, TimedOut = false };
        }
        catch (System.ComponentModel.Win32Exception ex) { throw new XPScriptRuntimeException(53, ex.Message); }
    }
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
    public static object TimeNumber(object hour, object minute, object second) => new XpDate(XPScriptRuntime.TimeNumber(Convert.ToInt32(hour), Convert.ToInt32(minute), Convert.ToInt32(second)));
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
    public static string Left(object value, object count) => XPScriptRuntime.Left(value, Convert.ToInt32(count));
    public static string Right(object value, object count) => XPScriptRuntime.Right(value, Convert.ToInt32(count));
    public static string LTrim(object? value) => value?.ToString()?.TrimStart() ?? string.Empty;
    public static string RTrim(object? value) => value?.ToString()?.TrimEnd() ?? string.Empty;
    public static object Val(object value) => double.TryParse(value?.ToString(), out var result) ? result : 0d;
    public static string Str(object value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    public static object CVDate(object value) => Convert.ToDateTime(value);
    public static string UChr(object value) => char.ConvertFromUtf32(Convert.ToInt32(value));
    public static long Uni(object value) => char.ConvertToUtf32((value?.ToString() ?? "\0"), 0);
    public static long Year(object value) => ToDateTime(value).Year;
    public static bool IsList(object? value) => value is System.Collections.IDictionary;
    public static bool IsUnknown(object? value) => value is null;
    public static bool IsNumeric(object? value) => XPScriptRuntime.IsNumeric(value);
    public static bool IsNull(object? value) => value is null;
    public static object RegexMatch(object? value, object? pattern) => System.Text.RegularExpressions.Regex.Matches(value?.ToString() ?? "", pattern?.ToString() ?? "").Select(match => match.Value).ToArray();
    public static object ArrayAppend(object? value, object? item)
    {
        if (value is System.Array source && source.Rank == 1)
        {
            var lower = source.GetLowerBound(0);
            var result = System.Array.CreateInstance(typeof(object), [source.Length + 1], [lower]);
            for (var i = 0; i < source.Length; i++) result.SetValue(source.GetValue(lower + i), lower + i);
            result.SetValue(item, lower + source.Length);
            return result;
        }
        return value is System.Collections.IEnumerable values ? values.Cast<object?>().Append(item).ToArray() : new object?[] { item };
    }
    public static object? ArrayGetIndex(object? value, object? item)
    {
        if (value is System.Array source && source.Rank == 1)
        {
            var lower = source.GetLowerBound(0);
            for (var i = lower; i <= source.GetUpperBound(0); i++)
            {
                var candidate = source.GetValue(i);
                if (candidate is string left && item is string right
                    ? string.Equals(left, right, OptionCompareNoCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                    : Equals(candidate, item)) return (long)i;
            }
            return null;
        }
        if (value is not System.Collections.IEnumerable values) return null;
        var index = 0L;
        foreach (var candidate in values)
        {
            if (candidate is string left && item is string right
                ? string.Equals(left, right, OptionCompareNoCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                : Equals(candidate, item)) return index;
            index++;
        }
        return null;
    }
    public static object ArrayUnique(object? value)
    {
        if (value is not System.Collections.IEnumerable values) return System.Array.Empty<object?>();
        var result = new List<object?>();
        foreach (var candidate in values)
            if (!result.Any(existing => existing is string left && candidate is string right
                ? string.Equals(left, right, OptionCompareNoCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                : Equals(existing, candidate))) result.Add(candidate);
        return result.ToArray();
    }
    public static object ArraySlice(object? value, object? start) => ArraySlice(value, start, int.MaxValue);
    public static object ArraySlice(object? value, object? start, object? end)
    {
        var values = ValuesForArray(value);
        var from = NormalizeArrayStart(Convert.ToInt32(start), values.Count);
        var rawEnd = Convert.ToInt32(end);
        var to = rawEnd == int.MaxValue ? values.Count : NormalizeArrayStart(rawEnd, values.Count);
        if (to < from) to = from;
        return values.Skip(from).Take(to - from).ToArray();
    }
    public static object ArraySplice(object? value, object? start, object? count) => ArraySpliceCore(value, start, count, []);
    public static object ArraySplice(object? value, object? start, object? count, object? replacement) => ArraySpliceCore(value, start, count, [replacement]);
    public static object ArraySplice(object? value, object? start, object? count, object? first, object? second) => ArraySpliceCore(value, start, count, [first, second]);
    private static object ArraySpliceCore(object? value, object? start, object? count, object?[] replacements)
    {
        var values = ValuesForArray(value);
        var position = NormalizeArrayStart(Convert.ToInt32(start), values.Count);
        var remove = Math.Clamp(Convert.ToInt32(count), 0, values.Count - position);
        var removed = values.GetRange(position, remove);
        values.RemoveRange(position, remove);
        values.InsertRange(position, replacements);
        ReplaceArrayContents(value, values);
        return removed.ToArray();
    }
    private static List<object?> ValuesForArray(object? value) => value is System.Collections.IEnumerable values ? values.Cast<object?>().ToList() : [];
    private static int NormalizeArrayStart(int index, int length) => index < 0 ? Math.Max(length + index, 0) : Math.Min(index, length);
    private static void ReplaceArrayContents(object? value, IReadOnlyList<object?> values)
    {
        if (value is not System.Array array || array.Rank != 1 || array.Length < values.Count) return;
        var lower = array.GetLowerBound(0);
        for (var i = 0; i < values.Count; i++) array.SetValue(values[i], lower + i);
    }
    public static Array ArrayResize(object? value, object? upper, object? preserve, object? lower = null)
    {
        var lowerBound = lower is null ? 0 : Convert.ToInt32(lower);
        var upperBound = Convert.ToInt32(upper);
        return LSArrayRuntime.ReDimManaged(value, Convert.ToBoolean(preserve), lowerBound, upperBound);
    }
    public static Array ArrayResize(object? value, object? upper1, object? preserve, object? lower1, object? upper2, object? lower2)
        => LSArrayRuntime.ReDimManaged(value, Convert.ToBoolean(preserve), Convert.ToInt32(upper1), Convert.ToInt32(lower1), Convert.ToInt32(upper2), Convert.ToInt32(lower2));
    public static Array ArrayResize(object? value, object? upper1, object? preserve, object? lower1, object? upper2, object? lower2, object? upper3, object? lower3)
        => LSArrayRuntime.ReDimManaged(value, Convert.ToBoolean(preserve), Convert.ToInt32(upper1), Convert.ToInt32(lower1), Convert.ToInt32(upper2), Convert.ToInt32(lower2), Convert.ToInt32(upper3), Convert.ToInt32(lower3));
    public static Array ArrayResize(object? value, object? upper1, object? preserve, object? lower1, object? upper2, object? lower2, object? upper3, object? lower3, object? upper4, object? lower4)
        => LSArrayRuntime.ReDimManaged(value, Convert.ToBoolean(preserve), Convert.ToInt32(upper1), Convert.ToInt32(lower1), Convert.ToInt32(upper2), Convert.ToInt32(lower2), Convert.ToInt32(upper3), Convert.ToInt32(lower3), Convert.ToInt32(upper4), Convert.ToInt32(lower4));
    public static object Explode(object? separator, object? value) => (value?.ToString() ?? string.Empty).Split(separator?.ToString() ?? ",");
    public static string FullTrim(object? value) => value?.ToString()?.Trim() ?? string.Empty;
    public static object JsonEncode(object? value) => XPScriptNativeJson.Stringify(value);
    public static object JsonDecode(object? value) => XPScriptNativeJson.Parse(value);
    public static string Input(object? file) => LSFileRuntime.Input(checked((int)XPScriptRuntime.CLng(file)));
    public static string Input(object? count, object? file) => LSFileRuntime.Input(checked((int)XPScriptRuntime.CLng(file)));
    public static void Sleep(object? milliseconds) => System.Threading.Thread.Sleep(Math.Max(0, Convert.ToInt32(milliseconds)));
    public static void AstPut(object? file, object? position, object? value) => LSFileRuntime.Put(checked((int)XPScriptRuntime.CLng(file)), position, value, value switch { byte => "byte", bool => "boolean", short or int => "integer", long => "long", float => "single", double => "double", decimal => "currency", DateTime => "date", _ => "string" });
    public static object? AstGet(object? file, object? position, object? current) => LSFileRuntime.GetValue(checked((int)XPScriptRuntime.CLng(file)), position, current switch { byte => "byte", bool => "boolean", short or int => "integer", long => "long", float => "single", double => "double", decimal => "currency", DateTime => "date", _ => "string" }, current);
    public static void AstSeek(object? file, object? position) => LSFileRuntime.SeekSet(checked((int)XPScriptRuntime.CLng(file)), XPScriptRuntime.CLng(position));
    public static void AstReset() => LSFileRuntime.Close();
    public static void AstChDrive(object? drive) => LSFileRuntime.ChDrive(drive);
    public static void AstLockBytes(object? file, object? start, object? end) => LSFileRuntime.LockBytes(checked((int)XPScriptRuntime.CLng(file)), XPScriptRuntime.CLng(start), XPScriptRuntime.CLng(end));
    public static void AstUnlockBytes(object? file, object? start, object? end) => LSFileRuntime.UnlockBytes(checked((int)XPScriptRuntime.CLng(file)), XPScriptRuntime.CLng(start), XPScriptRuntime.CLng(end));
    public static void AstOpen(object? path, object? mode, object? file) => LSFileRuntime.Open(path, mode, checked((int)XPScriptRuntime.CLng(file)));
    public static void AstClose(object? file) => LSFileRuntime.Close(checked((int)XPScriptRuntime.CLng(file)));
    public static void AstClose() => LSFileRuntime.Close();
    public static void AstPrintFile(object? file, object? value = null) => LSFileRuntime.PrintFile(checked((int)XPScriptRuntime.CLng(file)), value);
    public static object? GetObject(object? value) => GetObjectCore(value, null);
    public static object? GetObject(object? value, object? context) => GetObjectCore(value, context);
    private static object? GetObjectCore(object? pathname, object? className)
    {
        if (!OperatingSystem.IsWindows())
            throw new XPScriptRuntimeException(5, "GetObject is available only on Windows in XPScript.");
        var path = pathname is null ? string.Empty : XPScriptRuntime.CStr(pathname).Trim();
        var progId = className is null ? string.Empty : XPScriptRuntime.CStr(className).Trim();
        try
        {
            if (path.Length > 0)
                return System.Runtime.InteropServices.Marshal.BindToMoniker(path);
            if (progId.Length > 0)
            {
                var type = Type.GetTypeFromProgID(progId, throwOnError: true)
                    ?? throw new System.Runtime.InteropServices.COMException("COM class was not found.");
                return Activator.CreateInstance(type);
            }
            throw new XPScriptRuntimeException(5, "GetObject requires a pathname or COM ProgID.");
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "COM GetObject activation failed.");
        }
    }
    public static string AstLineInput(object? file) => LSFileRuntime.LineInput(checked((int)XPScriptRuntime.CLng(file)));
    public static void AstKill(object? path) => File.Delete(CStr(path));
    public static void AstFileCopy(object? source, object? destination) => File.Copy(CStr(source), CStr(destination), true);
    public static void AstNameFile(object? source, object? destination) => File.Move(CStr(source), CStr(destination), true);
    public static string AstInputChars(object? count, object? file) => LSFileRuntime.InputChars(checked((int)XPScriptRuntime.CLng(file)), checked((int)XPScriptRuntime.CLng(count)));
    public static void AstNoOp() { }
    public static void AstStop() => throw new XPScriptRuntimeException(5, "Stop statement reached.");
    public sealed class XpDate : IComparable<XpDate> {
        public DateTime Value { get; }
        public XpDate() : this(default) { }
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
    public static object DateValue(object? value) => new XpDate(XPScriptRuntime.DateValue(value));
    public static object TimeValue(object? value) => new XpDate(XPScriptRuntime.TimeValue(value));
    private static DateTime ToDateTime(object? value) => value is XpDate date ? date.Value : Convert.ToDateTime(value);
    public static object Date() => new XpDate(DateTime.Today);
    public static object Format(object? value, object? pattern = null) => pattern is null ? CStr(value) : (value is XpDate date ? date.Value : Convert.ToDateTime(value)).ToString(CStr(pattern), System.Globalization.CultureInfo.InvariantCulture);
    public static object InputBox(object? prompt = null, object? title = null, object? defaultValue = null) => XPScriptRuntime.InputBox(prompt);
    public static object MsgBox(object? prompt = null, object? boxType = null, object? title = null) => XPScriptRuntime.MsgBox(prompt);
    public static object ChDrive(object? drive) { LSFileRuntime.ChDrive(drive); return true; }
    public static void AstChDir(object? path) => System.Environment.CurrentDirectory = System.IO.Path.GetFullPath(CStr(path));
    public static object Erase(object? value) { LSArrayRuntime.Erase(value); return true; }
    public static object JsonParse(object? value) => XPScriptNativeJson.Parse(value);
    public static object JsonStringify(object? value) => XPScriptNativeJson.Stringify(value);
    public static long GetTickCount() => Environment.TickCount64;
    public static string Environ(object? name) => XPScriptRuntime.Environ(name);
    public static object AstUtf8(object? value) => Encoding.UTF8.GetBytes(Convert.ToString(value) ?? string.Empty);
    public static long Loc(object? file) => LSFileRuntime.Loc(checked((int)XPScriptRuntime.CLng(file)));
    public static long LOF(object? file) => LSFileRuntime.LOF(checked((int)XPScriptRuntime.CLng(file)));
    public static string Error(object? number) => XPScriptErrorRuntime.Error(checked((int)XPScriptRuntime.CLng(number)));
    public static string Error() => XPScriptErrorRuntime.Error();
    public static string ErrorValue() => XPScriptErrorRuntime.Description;
    public static class AstCompatibilityHelpers
    {
        public static long GetTickCount() => Environment.TickCount64;
        public static long Loc(object? file) => Program.Loc(file);
        public static string Error(object? number) => Program.Error(number);
        public static string Error() => Program.Error();
        public static string ErrorValue() => XPScriptErrorRuntime.Description;
        private static string InvokeDialog(string methodName, object?[] args)
        {
            var type = Type.GetType("XPScriptUIDialogRuntime", throwOnError: false);
            System.Reflection.MethodInfo? method = null;
            if (type is not null)
            {
                foreach (var candidate in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (candidate.Name == methodName && candidate.GetParameters().Length == args.Length)
                    {
                        method = candidate;
                        break;
                    }
                }
            }
            if (method is null)
                throw new XPScriptRuntimeException(5, "XPScript desktop dialog backend is unavailable.");
            return Convert.ToString(method.Invoke(null, args), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        public static string ShowDialog(object? message, object? title = null, object? kind = null, object? values = null) => InvokeDialog("ShowDialog", [message, title, kind, values]);
        public static string LoadFileDialog(object? title = null, object? initialPath = null, object? filter = null) => InvokeDialog("LoadFileDialog", [title, initialPath, filter]);
        public static string OpenFileDialog(object? title = null, object? initialPath = null, object? filter = null) => InvokeDialog("OpenFileDialog", [title, initialPath, filter]);
        public static string SaveFileDialog(object? title = null, object? initialPath = null, object? filter = null) => InvokeDialog("SaveFileDialog", [title, initialPath, filter]);
        public static bool Like(string value, string pattern) => System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, "^" + System.Text.RegularExpressions.Regex.Escape(pattern ?? string.Empty).Replace("\\\\*", ".*").Replace("\\\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
{{moduleFields}}
{{body}}
{{entryPoint}}
{{androidEntryPoint}}
}
""";
        generated = EnsureAttachmentRuntime(generated);
        if (usesNetworkTools)
            generated = NetworkToolsPhase2RuntimePostProcessor.Transform(generated);
        generated = generated.Replace("Error$", "Error()", StringComparison.Ordinal);
        generated = generated.Replace("XPJsonSchema.", "XPScriptJsonSchema.", StringComparison.Ordinal);
        generated = generated.Replace("((XPScriptJsonSchema)(XPJsonSchema)).Parse", "XPScriptJsonSchema.Parse", StringComparison.Ordinal)
            .Replace("((XPScriptJsonSchema)(XPJsonSchema)).FromJson", "XPScriptJsonSchema.FromJson", StringComparison.Ordinal);
        generated = jsonNameMetadata.ApplyToGeneratedCode(generated);
        generated += Environment.NewLine + AstHttpSupport.ExceptionCode;
        using var assetScope = UIFormAssetCompileContext.Push(embedAssets);
        generated = UIFormAppAssets.InstallEmbeddedAssets(generated, sourcePath);
        if (usesAi)
        {
            generated += Environment.NewLine + AiRuntimeSource.Code + Environment.NewLine + AiToolRuntimeSource.Code;
            generated = new AiSessionRuntimePostProcessor().Transform(generated);
            generated = new AiPromptSchemaRuntimePostProcessor().Transform(generated);
        }
        generated = RemoveDuplicateTlsRuntime(generated);
        if (generatedSourceTransform is not null)
            generated = generatedSourceTransform(generated);
        var applicationMarkers = AstApplicationSupport.BuildMarkers(fullSource, sourcePath);
        string outputResult;
        if (applicationMarkers.Length != 0)
            outputResult = await RunCompiler.CompileGeneratedWithDependenciesAsync(generated + Environment.NewLine + applicationMarkers, outputDirectory, false, cancellationToken, runtimeIdentifier, managedReferences.Managed, sourcePath, generatedSourceTransform is not null ? "Script" : null).ConfigureAwait(false);
        else if (runtimeIdentifier is not null || CompilePublishLayoutContext.IsConfigured)
            outputResult = await RunCompiler.CompileGeneratedWithDependenciesAsync(generated, outputDirectory, false, cancellationToken, runtimeIdentifier, managedReferences.Managed, sourcePath, generatedSourceTransform is not null ? "Script" : null).ConfigureAwait(false);
        else
            outputResult = await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        new NativeDependencyPackager(runtimeIdentifier ?? CompilerDriver.CurrentRuntimeIdentifier()).Stage(sourcePath, outputDirectory, nativeDependencies);
        return outputResult;
    }

    private static async Task<string> CompileWithLegacyFallbackAsync(string sourcePath, string outputDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = Path.Combine(outputDirectory, OperatingSystem.IsWindows() ? "Generated.exe" : "Generated");
        var result = await new CompilerDriver().CompileWithResultAsync(
            sourcePath,
            outputPath,
            selfContained: false,
            CompilerDriver.CurrentRuntimeIdentifier()).ConfigureAwait(false);
        if (!result.Success)
            throw new CompilerException("Legacy compilation failed.", result.Errors);
        return outputPath;
    }

    private static string AstArrayRuntimeCode()
    {
        const string startMarker = "internal interface IXPScriptArrayStorage";
        const string endMarker = "internal sealed class LSByRefValue";
        var source = CoreCompatibilityRuntimeSource.Code;
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            throw new InvalidOperationException("AST array runtime source markers are missing.");
        return source[start..end]
            .Replace("value is ILSObjectReference reference && reference.IsNothing", "value is null", StringComparison.Ordinal)
            .Replace("ILSObjectReference reference when reference.IsNothing => NothingArray(),", "null => NothingArray(),", StringComparison.Ordinal)
            .Replace("null => NothingArray(),\n        null => NothingArray(),", "null => NothingArray(),", StringComparison.Ordinal);
    }

    private static string AstFileRuntimeCode()
    {
        var source = CoreCompatibilityRuntimeSource.Code;
        var start = source.IndexOf("internal static class LSFileRuntime", StringComparison.Ordinal);
        var end = source.IndexOf("// Marker calls are removed", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            throw new InvalidOperationException("AST file runtime source markers are missing.");
        return source[start..end];
    }

    private static string AstCallbackRuntimeCode()
        => CallbackRuntimeSource.Code.Replace("typeof(Script)", "typeof(Program)", StringComparison.Ordinal);

    private static void ValidateAstAiCallbacks(string source)
    {
        var procedures = Regex.Matches(source, @"(?im)^\s*(?:Public\s+|Private\s+)?(?:Sub|Function)\s+(?<name>[A-Za-z_]\w*)\b")
            .Cast<Match>().Select(match => match.Groups["name"].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Match registration in Regex.Matches(source, @"(?im)\.AddFunction\s*\(\s*[""][^""]*[""]\s*,\s*[""][^""]*[""]\s*,\s*[""](?<callback>[A-Za-z_]\w*)[""]"))
        {
            var callback = registration.Groups["callback"].Value;
            if (!procedures.Contains(callback))
                throw new CompilerException($"XPAITool callback '{callback}' was not found as a module Sub or Function.", CompilerDiagnosticCodes.CallbackNotFound, "symbol-resolution");
        }
    }

    private static string AstTlsRuntimeCode()
    {
        var source = CoreCompatibilityRuntimeSource.Code;
        var start = source.IndexOf("internal sealed class XPScriptTlsValidationState", StringComparison.Ordinal);
        var end = source.IndexOf("internal sealed class XPScriptRuntimeException", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            throw new InvalidOperationException("AST TLS runtime source markers are missing.");
        return source[start..end];
    }

    private static string RemoveDuplicateTlsRuntime(string generated)
    {
        var tlsCode = AstTlsRuntimeCode();
        var first = generated.IndexOf(tlsCode, StringComparison.Ordinal);
        if (first < 0) return generated;
        var search = first + tlsCode.Length;
        while (true)
        {
            var duplicate = generated.IndexOf(tlsCode, search, StringComparison.Ordinal);
            if (duplicate < 0) return generated;
            generated = generated.Remove(duplicate, tlsCode.Length);
            search = duplicate;
        }
    }

    private static string AstErrorRuntimeCode()
    {
        var source = CoreCompatibilityRuntimeSource.Code;
        var start = source.IndexOf("internal static class XPScriptErrorRuntime", StringComparison.Ordinal);
        var end = source.IndexOf("internal static class LSCoreCompare", start, StringComparison.Ordinal);
        var normalizeSource = ExtendedCompatibilityRuntimeSource.Code;
        var normalizeEnd = normalizeSource.IndexOf("internal static class LSExtendedRuntime", StringComparison.Ordinal);
        return source[start..end] + normalizeSource[..normalizeEnd];
    }

    internal static IReadOnlyList<BoundStatement> BindProcedure(IReadOnlyList<StatementSyntax> procedureStatements, SymbolTable scope,
        IReadOnlyList<ParameterSymbol> parameters, XpTypeSymbol? returnType, string? functionName, LocalSymbol? result,
        string identity, string sourcePath, string transformedSource, string fullSource, bool allowDynamicConversions = true)
    {
            var allProcedureStatements = FlattenStatements(procedureStatements).ToArray();
            var labels = allProcedureStatements.OfType<LabelStatementSyntax>()
                .GroupBy(label => label.Identifier.Text, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var duplicateLabel = allProcedureStatements.OfType<LabelStatementSyntax>()
                .GroupBy(label => label.Identifier.Text, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Skip(1).FirstOrDefault();
            if (duplicateLabel is not null)
                throw MappedDiagnosticFromTransformed(duplicateLabel.Span, sourcePath, transformedSource, fullSource, $"Duplicate label '{duplicateLabel.Identifier.Text}'.");
            foreach (var transfer in allProcedureStatements.OfType<GoToStatementSyntax>().Where(transfer => !transfer.IsGoSub))
                if (!labels.ContainsKey(transfer.Target.Text))
                    throw MappedDiagnosticFromTransformed(transfer.Span, sourcePath, transformedSource, fullSource, $"Unknown label '{transfer.Target.Text}'.");
            foreach (var control in allProcedureStatements)
            {
                var target = control switch
                {
                    OnErrorStatementSyntax error when error.ActionKeyword.Kind != SyntaxKind.ResumeKeyword && error.TargetToken?.Text != "0" => error.TargetToken,
                    ResumeStatementSyntax resume when resume.TargetToken?.Kind == SyntaxKind.IdentifierToken => resume.TargetToken,
                    _ => null
                };
                if (target is not null && !labels.ContainsKey(target.Text))
                    throw MappedDiagnosticFromTransformed(control.Span, sourcePath, transformedSource, fullSource, $"Unknown label '{target.Text}'.");
            }
            ValidateLoopEntryTransfers(procedureStatements, labels, sourcePath, transformedSource, fullSource);
            var binder = new StatementBinder(scope, returnType, returnType is not null, true, functionName, result, identity,
                allowDynamicConversions: allowDynamicConversions);
            var bound = procedureStatements.Select(binder.Bind).OfType<BoundStatement>().ToList();
            if (binder.Diagnostics.Count > 0)
                throw SyntaxDiagnosticException(binder.Diagnostics[0], sourcePath, transformedSource, fullSource);
            if (result is not null)
            {
                // Function-name assignment is ordinary state mutation, never
                // an early return. Fall-through returns the same slot as Exit.
                bound.Insert(0, new BoundVariableDeclarationStatement(result, result.Type == typeof(object)
                    ? new BoundLiteralExpression(null, typeof(object), result.SemanticType) : null));
                bound.Add(new BoundReturnStatement(new BoundNameExpression(result)));
            }
        return bound;
    }

    internal static IReadOnlyList<BoundStatement> BindProcedure(IReadOnlyList<StatementSyntax> procedureStatements, SymbolTable scope,
        IReadOnlyList<ParameterSymbol> parameters, XpTypeSymbol? returnType, string? functionName, LocalSymbol? result,
        string identity, string sourcePath, string fullSource) =>
        BindProcedure(procedureStatements, scope, parameters, returnType, functionName, result, identity, sourcePath, fullSource, fullSource, true);

    internal static IEnumerable<StatementSyntax> FlattenStatements(IEnumerable<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            yield return statement;
            // Labels belong to this procedure, including branch clauses that are
            // not themselves statements. Never search the full source for targets.
            IEnumerable<StatementSyntax> children = statement switch
            {
                IfStatementSyntax value => value.ThenStatements.Concat(value.ElseIfClauses.SelectMany(clause => clause.Statements)).Concat(value.ElseStatements),
                WhileStatementSyntax value => value.Statements,
                DoStatementSyntax value => value.Statements,
                ForStatementSyntax value => value.Statements,
                ForAllStatementSyntax value => value.Statements,
                SelectStatementSyntax value => value.Cases.SelectMany(clause => clause.Statements),
                _ => []
            };
            foreach (var child in children)
                foreach (var nested in FlattenStatements([child]))
                    yield return nested;
        }
    }

    private static void ValidateLoopEntryTransfers(IEnumerable<StatementSyntax> statements,
        IReadOnlyDictionary<string, LabelStatementSyntax> labels, string sourcePath, string transformedSource, string fullSource)
    {
        var scopes = FlattenStatements(statements)
            .Where(statement => statement is ForStatementSyntax or ForAllStatementSyntax)
            .ToArray();
        foreach (var transfer in FlattenStatements(statements).OfType<GoToStatementSyntax>().Where(item => !item.IsGoSub))
        {
            if (!labels.TryGetValue(transfer.Target.Text, out var label)) continue;
            if (label.Span.Start > transfer.Span.Start &&
                FlattenStatements(statements).OfType<DimStatementSyntax>().Any(declaration =>
                    declaration.Span.Start > transfer.Span.Start && declaration.Span.End < label.Span.Start && declaration.Initializer is not null))
                throw MappedDiagnosticFromTransformed(transfer.Span, sourcePath, transformedSource, fullSource,
                    $"GoTo cannot jump over local declaration to label '{label.Identifier.Text}'. Move the declaration before the GoTo or move the label before the declaration.");
            var sourceScope = InnermostContaining(scopes, transfer.Span);
            var targetScope = InnermostContaining(scopes, label.Span);
            if (!ReferenceEquals(sourceScope, targetScope))
                throw MappedDiagnosticFromTransformed(transfer.Span, sourcePath, transformedSource, fullSource,
                    $"GoTo target '{label.Identifier.Text}' is outside the current For/ForAll scope. GoTo is only allowed within the same loop scope.");
        }
    }

    private static StatementSyntax? InnermostContaining(IEnumerable<StatementSyntax> scopes, TextSpan span)
        => scopes.Where(scope => scope.Span.Start <= span.Start && scope.Span.End >= span.End)
            .OrderBy(scope => scope.Span.Length).FirstOrDefault();

    private static void DeclareNativeStatic(SymbolTable symbols, string name, IReadOnlyList<Type> parameterTypes, string targetName)
    {
        var function = new FunctionSymbol(name, typeof(object), parameterTypes, XpTypeSymbol.Variant,
            parameterTypes.Select(type => type == typeof(byte) ? XpTypeSymbol.FromClr(type) : XpTypeSymbol.Variant).ToArray())
        {
            IsNativeStatic = true,
            NativeTargetName = targetName
        };
        symbols.Declare(function);
    }

    private static void DeclareOptionalSignatures(SymbolTable symbols, string name, Type returnType, IReadOnlyList<ParameterSyntax> parameters)
    {
        // Only a trailing Optional suffix can be omitted by a shorter call.
        // Explicit empty argument slots require separate call-syntax lowering.
        for (var count = parameters.Count - 1; count >= 0 && parameters[count].IsOptional; count--)
        {
            var shortenedTypes = parameters.Take(count).Select(ResolveParameterType).ToArray();
            var shortenedModes = parameters.Take(count).Select(p => p.IsByRef).ToArray();
            if (symbols.LookupAll(name).OfType<FunctionSymbol>().Any(existing =>
                existing.ParameterTypes.SequenceEqual(shortenedTypes.Select(type => type.RuntimeType)) &&
                existing.ByRefParameters is not null && existing.ByRefParameters.SequenceEqual(shortenedModes)))
                continue;
            symbols.Declare(new FunctionSymbol(name, returnType,
                shortenedTypes.Select(type => type.RuntimeType).ToArray(), null, shortenedTypes, shortenedModes)
                { IsOptionalForwarding = true });
        }
    }

    private static bool SameClassMethodSignature(SubDeclarationSyntax left, SubDeclarationSyntax right) =>
        left.Identifier.Text.Equals(right.Identifier.Text, StringComparison.OrdinalIgnoreCase) &&
        left.Parameters.Count == right.Parameters.Count &&
        left.Parameters.Zip(right.Parameters, (a, b) => a.IsByRef == b.IsByRef &&
            string.Equals(a.Type?.Identifier.Text, b.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase)).All(match => match);

    private static bool SameClassMethodSignature(FunctionDeclarationSyntax left, FunctionDeclarationSyntax right) =>
        left.Identifier.Text.Equals(right.Identifier.Text, StringComparison.OrdinalIgnoreCase) &&
        left.Parameters.Count == right.Parameters.Count &&
        left.Parameters.Zip(right.Parameters, (a, b) => a.IsByRef == b.IsByRef &&
            string.Equals(a.Type?.Identifier.Text, b.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase)).All(match => match);

    private static XpTypeSymbol ResolveParameterType(ParameterSyntax parameter) => ResolveParameterType(parameter, null);

    internal static XpTypeSymbol ResolveParameterType(ParameterSyntax parameter, SymbolTable? symbols)
    {
        var element = symbols is null ? ResolveValueType(parameter.Type) : AstClassMethodSupport.DeclaredType(parameter.Type, symbols);
        if (parameter.IsList) return XpTypeSymbol.ListOf(element);
        return parameter.IsArray ? XpTypeSymbol.ArrayOf(element, parameter.ArrayRank) : element;
    }

    private static XpTypeSymbol ResolveValueType(TypeSyntax? type)
    {
        if (type is null || type.Identifier.Text.Equals("Variant", StringComparison.OrdinalIgnoreCase))
            return XpTypeSymbol.Variant;

        var name = type.Identifier.Text.Trim().ToUpperInvariant();
        if (name is "BYTE" or "BOOLEAN" or "STRING" or "INTEGER" or "LONG" or "SINGLE" or "DOUBLE" or "CURRENCY")
            return XpTypeSymbol.FromClr(ResolveRuntimeType(name));

        return BoundExpressionEmitter.RuntimeObjectType(type.Identifier.Text) is not null
            ? XpTypeSymbol.User(type.Identifier.Text)
            : XpTypeSymbol.FromClr(ResolveRuntimeType(type.Identifier.Text));
    }

    internal static void ValidateOptionalDeclarations(IReadOnlyList<ParameterSyntax> parameters, string sourcePath, string source)
    {
        var optionalSeen = false;
        foreach (var parameter in parameters)
        {
            if (parameter.IsOptional)
            {
                optionalSeen = true;
                continue;
            }

            if (optionalSeen)
                throw OptionalDiagnostic(parameter, sourcePath, source,
                    $"Required parameter '{parameter.Identifier.Text}' cannot follow an Optional parameter.");
            if (parameter.DefaultValue is not null)
                throw OptionalDiagnostic(parameter, sourcePath, source,
                    $"Parameter '{parameter.Identifier.Text}' must be declared Optional when it has a default value.");
        }
    }

    private static CompilerException OptionalDiagnostic(ParameterSyntax parameter, string sourcePath, string source, string message)
    {
        var start = SourceTextMap.GetPosition(source, parameter.Span.Start);
        var end = SourceTextMap.GetPosition(source, parameter.Span.End);
        var diagnostic = new CompileDiagnostic
        {
            File = Path.GetFileName(sourcePath), Line = start.Line, Position = start.Column,
            EndLine = end.Line, EndColumn = end.Column, Description = message,
            DiagnosticCode = CompilerDiagnosticCodes.InvalidSyntax, Severity = "error",
            Category = "semantic"
        };
        return new CompilerException(message, CompilerDiagnosticCodes.InvalidSyntax, "semantic", [diagnostic]);
    }

    internal static CompilerException SyntaxDiagnosticException(SyntaxDiagnostic diagnostic, string sourcePath, string source)
        => SyntaxDiagnosticException(diagnostic, sourcePath, source, source);

    internal static CompilerException SyntaxDiagnosticException(SyntaxDiagnostic diagnostic, string sourcePath, string transformedSource, string fullSource)
    {
        var span = MapTransformedSpan(diagnostic.Span, transformedSource, fullSource);
        var start = SourceTextMap.GetPosition(fullSource, span.Start);
        var end = SourceTextMap.GetPosition(fullSource, span.End);
        var mapped = new CompileDiagnostic
        {
            File = Path.GetFileName(sourcePath), Line = start.Line, Position = start.Column,
            EndLine = end.Line, EndColumn = end.Column, Description = diagnostic.Message,
            DiagnosticCode = diagnostic.Code, Severity = "error",
            Category = diagnostic.Code.StartsWith("XPS2", StringComparison.Ordinal) ? "semantic" : "syntax"
        };
        return new CompilerException(diagnostic.Message, diagnostic.Code, mapped.Category, [mapped]);
    }

    internal static CompilerException MappedDiagnostic(TextSpan span, string sourcePath, string source, string message,
        string code = CompilerDiagnosticCodes.InvalidSyntax, string category = "semantic")
        => MappedDiagnosticFromTransformed(span, sourcePath, source, source, message, code, category);

    internal static CompilerException MappedDiagnosticFromTransformed(TextSpan span, string sourcePath, string transformedSource, string fullSource, string message,
        string code = CompilerDiagnosticCodes.InvalidSyntax, string category = "semantic")
    {
        var mappedSpan = MapTransformedSpan(span, transformedSource, fullSource);
        var start = SourceTextMap.GetPosition(fullSource, mappedSpan.Start);
        var end = SourceTextMap.GetPosition(fullSource, mappedSpan.End);
        var mapped = new CompileDiagnostic
        {
            File = Path.GetFileName(sourcePath), Line = start.Line, Position = start.Column,
            EndLine = end.Line, EndColumn = end.Column, Description = message,
            DiagnosticCode = code, Severity = "error", Category = category
        };
        return new CompilerException(message, code, category, [mapped]);
    }

    private static TextSpan MapTransformedSpan(TextSpan span, string transformedSource, string fullSource)
    {
        if (ReferenceEquals(transformedSource, fullSource) || string.Equals(transformedSource, fullSource, StringComparison.Ordinal))
            return span;

        var transformedLine = LineStartAtOffset(transformedSource, span.Start);
        var originalLine = LineStartAtLine(fullSource, SourceTextMap.GetPosition(transformedSource, span.Start).Line);
        if (transformedLine.Text is null || originalLine.Text is null)
            return span;

        var localStart = Math.Clamp(span.Start - transformedLine.Start, 0, transformedLine.Text.Length);
        var token = Regex.Match(transformedLine.Text, $@"(?<![A-Za-z0-9_]){Regex.Escape(TokenAt(transformedLine.Text, localStart))}(?![A-Za-z0-9_])");
        if (!token.Success || string.IsNullOrEmpty(token.Value))
            return originalLine.Start + Math.Min(localStart, originalLine.Text.Length) is var fallback ? new TextSpan(fallback, span.Length) : span;

        var originalToken = Regex.Match(originalLine.Text, $@"(?<![A-Za-z0-9_]){Regex.Escape(token.Value)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);
        if (!originalToken.Success)
            return span;
        return new TextSpan(originalLine.Start + originalToken.Index, span.Length);
    }

    private static string TokenAt(string line, int offset)
    {
        if (line.Length == 0) return string.Empty;
        var index = Math.Min(offset, line.Length - 1);
        if (!char.IsLetterOrDigit(line[index]) && line[index] != '_')
            index = Math.Min(index + 1, line.Length - 1);
        var start = index;
        while (start > 0 && (char.IsLetterOrDigit(line[start - 1]) || line[start - 1] == '_')) start--;
        var end = index;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_')) end++;
        return line[start..end];
    }

    private static (int Start, string? Text) LineStartAtOffset(string source, int offset)
    {
        var line = SourceTextMap.GetPosition(source, offset).Line;
        return LineStartAtLine(source, line);
    }

    private static (int Start, string? Text) LineStartAtLine(string source, int line)
    {
        var current = 1;
        var start = 0;
        while (current < line && start < source.Length)
        {
            if (source[start++] == '\n') current++;
        }
        if (current != line) return (0, null);
        var end = source.IndexOf('\n', start);
        if (end < 0) end = source.Length;
        return (start, source[start..end].TrimEnd('\r'));
    }

    private static void RejectUnsupported(string sourcePath, string source, string pattern, string message)
    {
        var match = Regex.Match(source, pattern);
        if (!match.Success) return;
        var start = SourceTextMap.GetPosition(source, match.Index);
        var end = SourceTextMap.GetPosition(source, match.Index + match.Length);
        var diagnostic = new CompileDiagnostic
        {
            File = Path.GetFileName(sourcePath), Line = start.Line, Position = start.Column,
            EndLine = end.Line, EndColumn = end.Column, Description = message,
            DiagnosticCode = CompilerDiagnosticCodes.InvalidSyntax, Severity = "error", Category = "semantic"
        };
        throw new CompilerException(message, CompilerDiagnosticCodes.InvalidSyntax, "semantic", [diagnostic]);
    }

    internal static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool), "BYTE" => typeof(byte), "STRING" => typeof(string), "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double), _ => typeof(object)
    };
    private static string CSharpType(Type type) => type == typeof(void) ? "void" : type == typeof(byte) ? "byte" : type == typeof(int) ? "int" : type == typeof(long) ? "long" : type == typeof(double) ? "double" : type == typeof(float) ? "float" : type == typeof(decimal) ? "decimal" : type == typeof(bool) ? "bool" : type == typeof(string) ? "string" : type == typeof(DateTime) ? "System.DateTime" : "object";

    private static Type NativeClrType(string type) => type.Trim().ToUpperInvariant() switch
    {
        "VOID" => typeof(void), "STRING" => typeof(string), "INTEGER" => typeof(int), "LONG" => typeof(long),
        "DOUBLE" => typeof(double), "SINGLE" => typeof(float), "BOOLEAN" => typeof(bool), "BYTE" => typeof(byte),
        "CURRENCY" => typeof(decimal), "DATE" => typeof(DateTime), _ => typeof(object)
    };

    private static string NativeCSharpType(string type) => CSharpType(NativeClrType(type)) switch
    {
        "object" when !type.Equals("Variant", StringComparison.OrdinalIgnoreCase) => "object",
        var value => value
    };

    private static string NativeConversion(string type, string value) => type.Trim().ToUpperInvariant() switch
    {
        "STRING" => $"Convert.ToString({value})!", "INTEGER" => $"Convert.ToInt32({value})", "LONG" => $"Convert.ToInt64({value})",
        "DOUBLE" => $"Convert.ToDouble({value})", "SINGLE" => $"Convert.ToSingle({value})", "BOOLEAN" => $"Convert.ToBoolean({value})",
        "BYTE" => $"Convert.ToByte({value})", "CURRENCY" => $"Convert.ToDecimal({value})", "DATE" => $"Convert.ToDateTime({value})",
        _ => value
    };
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

    private static IReadOnlyList<(string Name, long Value)> EnumMembers(string body)
    {
        var members = new List<(string Name, long Value)>();
        var nextValue = 0L;
        foreach (Match match in Regex.Matches(body, @"(?im)^\s*(?<name>[A-Za-z_]\w*)\s*(?:=\s*(?<value>-?\d+))?\s*$"))
        {
            var value = match.Groups["value"].Success
                ? long.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : nextValue;
            members.Add((match.Groups["name"].Value, value));
            nextValue = value + 1;
        }
        return members;
    }

    private static string ModuleArrayInitializer(string bounds, string typeName)
    {
        var elementType = typeName.Trim().ToUpperInvariant() switch
        {
            "BYTE" => "byte",
            "INTEGER" or "LONG" => "long",
            "SINGLE" or "DOUBLE" or "CURRENCY" => "double",
            "BOOLEAN" => "bool",
            "STRING" => "string",
            _ => "object"
        };
        if (string.IsNullOrWhiteSpace(bounds)) return $"System.Array.Empty<{elementType}>()";
        var dimensions = bounds.Split(',').Select(item => item.Trim()).ToArray();
        var parsed = dimensions.Select(dimension =>
        {
            var range = Regex.Match(dimension, @"^(?<lower>-?\d+)\s+To\s+(?<upper>-?\d+)$", RegexOptions.IgnoreCase);
            var lower = range.Success ? long.Parse(range.Groups["lower"].Value, System.Globalization.CultureInfo.InvariantCulture) : 0L;
            var upper = range.Success ? long.Parse(range.Groups["upper"].Value, System.Globalization.CultureInfo.InvariantCulture) : long.Parse(dimension, System.Globalization.CultureInfo.InvariantCulture);
            return (Lower: lower, Length: checked(upper - lower + 1));
        }).ToArray();
        var lengths = string.Join(", ", parsed.Select(item => item.Length));
        var lowers = string.Join(", ", parsed.Select(item => item.Lower));
        return $"LSArrayRuntime.CreateManaged<{elementType}>(new[] {{ {lengths} }}, new[] {{ {lowers} }})";
    }
}
