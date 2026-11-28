using System.Text.Json;
using System.Text.Json.Serialization;

namespace XPScript.Compiler;

public static class CompilerMcpServer
{
    private const string ProtocolVersion = "2025-06-18";
    private const int MaxSourceChars = 16 * 1024 * 1024;
    private const int MaxSourcePackageChars = 64 * 1024 * 1024;
    private const int MaxIncludedFiles = 256;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("mcp does not accept command-line arguments; JSON-RPC is read from stdin.");
            return 1;
        }

        var compiler = new CompilerDriver();
        var responseGate = new SemaphoreSlim(1, 1);
        var requests = new List<Task>();
        string? line;
        while ((line = await Console.In.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            requests.Add(ProcessRequestAsync(line, compiler, responseGate));
        }
        await Task.WhenAll(requests).ConfigureAwait(false);
        return 0;
    }

    private static async Task ProcessRequestAsync(string line, CompilerDriver compiler, SemaphoreSlim responseGate)
    {
        try
        {
            using var request = JsonDocument.Parse(line);
            var root = request.RootElement;
            var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : (JsonElement?)null;
            var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
            var parameters = root.TryGetProperty("params", out var paramsElement) ? paramsElement.Clone() : default;
            if (method == "notifications/initialized" || id is null) return;
            object result = method switch
            {
                "initialize" => Initialize(),
                "ping" => new { },
                "tools/list" => new { tools = Tools() },
                "tools/call" => await CallToolAsync(compiler, parameters).ConfigureAwait(false),
                _ => throw new McpException(-32601, $"Method not found: {method}")
            };
            await responseGate.WaitAsync().ConfigureAwait(false);
            try { WriteResponse(id.Value, result); } finally { responseGate.Release(); }
        }
        catch (McpException ex) { await WriteErrorAsync(responseGate, TryReadId(line), ex.Code, ex.Message).ConfigureAwait(false); }
        catch (JsonException) { await WriteErrorAsync(responseGate, null, -32700, "Parse error.").ConfigureAwait(false); }
        catch (Exception) { await WriteErrorAsync(responseGate, TryReadId(line), -32603, "Internal error.").ConfigureAwait(false); }
    }

    private static object Initialize() => new
    {
        protocolVersion = ProtocolVersion,
        capabilities = new { tools = new { listChanged = false } },
        serverInfo = new { name = "xpscript-compiler", version = typeof(CompilerMcpServer).Assembly.GetName().Version?.ToString() ?? "unknown" }
    };

    private static object[] Tools() =>
    [
        Tool("xpscript_validate", "Validate XPScript source without executing it. Include additional source files with Include and includeFiles.", new { type="object", properties=new { source=new { type="string" }, filename=new { type="string", description="Safe relative .xps path for the virtual entry point." }, includeFiles=IncludeFilesSchema(), runtimeIdentifier=new { type="string" }, debug=new { type="boolean", description="Include debug diagnostics while preserving source-mapped XPScript locations." } }, required=new[]{"source"} }),
        Tool("xpscript_ast_validate", "Validate source explicitly through the AST compiler without executing it. Include additional source files with Include and includeFiles.", new { type="object", properties=new { source=new { type="string" }, filename=new { type="string", description="Safe relative .xps path for the virtual entry point." }, includeFiles=IncludeFilesSchema(), debug=new { type="boolean", description="Enable debug diagnostics without changing AST source diagnostic identity." } }, required=new[]{"source"} }),
        Tool("xpscript_virtual_compile", "Compile source through the AST compiler without publishing an application. Include additional source files with Include and includeFiles. Always returns extended compiler info.", new { type="object", properties=new { source=new { type="string" }, filename=new { type="string", description="Safe relative .xps path for the virtual entry point." }, includeFiles=IncludeFilesSchema(), runtimeIdentifier=new { type="string" }, debug=new { type="boolean" }, securityMode=new { type="string" }, restricted=new { type="boolean" }, sourceRoots=new { type="array", items=new { type="string" } }, sourcePreprocessors=new { type="array", items=new { type="string" } } }, required=new[]{"source"} }),
        Tool("xpscript_compile", "Compile and publish an XPScript application through the same AST run compiler used by CLI/debugger execution. Include additional source files with Include and includeFiles. Always returns extended compiler info.", new { type="object", properties=new { source=new { type="string" }, filename=new { type="string", description="Safe relative .xps path for the virtual entry point." }, includeFiles=IncludeFilesSchema(), outputDirectory=new { type="string" }, runtimeIdentifier=new { type="string" }, debug=new { type="boolean" }, securityMode=new { type="string" }, restricted=new { type="boolean" }, sourceRoots=new { type="array", items=new { type="string" } }, sourcePreprocessors=new { type="array", items=new { type="string" } } }, required=new[]{"source"} }),
        Tool("xpscript_symbols", "Search the public XPScript symbol catalog.", new { type="object", properties=new { search=new { type="string" } } }),
        Tool("xpscript_describe", "Describe an exact public XPScript symbol.", new { type="object", properties=new { name=new { type="string" } }, required=new[]{"name"} }),
        Tool("xpscript_explain", "Explain a stable XPScript diagnostic code.", new { type="object", properties=new { diagnosticCode=new { type="string" } }, required=new[]{"diagnosticCode"} })
    ];

    private static object Tool(string name, string description, object inputSchema) => new { name, description, inputSchema };

    private static object IncludeFilesSchema() => new
    {
        type = "object",
        description = "Optional relative .xps paths mapped to their source text. Reference these files from the main source with Include \"path/file.xps\".",
        additionalProperties = new { type = "string" }
    };

    private static async Task<object> CallToolAsync(CompilerDriver compiler, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var nameElement))
            throw new McpException(-32602, "tools/call requires a tool name.");
        var name = nameElement.GetString() ?? "";
        var arguments = parameters.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object ? args : default;
        object value = name switch
        {
            "xpscript_validate" => await ValidateAsync(compiler, arguments).ConfigureAwait(false),
            "xpscript_ast_validate" => await AstValidateAsync(arguments).ConfigureAwait(false),
            "xpscript_virtual_compile" => await VirtualCompileAsync(arguments).ConfigureAwait(false),
            "xpscript_compile" => await CompileAsync(arguments).ConfigureAwait(false),
            "xpscript_symbols" => CompilerSymbolCatalog.Search(GetOptionalString(arguments, "search") ?? ""),
            "xpscript_describe" => CompilerSymbolCatalog.Find(GetRequiredString(arguments, "name")) ?? throw new McpException(-32602, "Unknown XPScript symbol."),
            "xpscript_explain" => CompilerDiagnosticCatalog.Find(GetRequiredString(arguments, "diagnosticCode")) ?? throw new McpException(-32602, "Unknown XPScript diagnostic code."),
            _ => throw new McpException(-32602, $"Unknown tool: {name}")
        };
        var isError = value is CompileResult compileResult && !compileResult.Success
            || value.GetType().GetProperty("result")?.GetValue(value) is string result && result.Equals("error", StringComparison.OrdinalIgnoreCase);
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, JsonOptions) } }, structuredContent = value, isError };
    }

    private static async Task<CompileResult> ValidateAsync(CompilerDriver compiler, JsonElement arguments)
    {
        var source = GetRequiredString(arguments, "source");
        if (source.Length > MaxSourceChars) throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character validation limit.");
        var filename = ValidateFilename(GetOptionalString(arguments, "filename") ?? "program.xps");
        var includeFiles = ReadIncludeFiles(arguments, source.Length, filename);
        var rid = GetOptionalString(arguments, "runtimeIdentifier") ?? CompilerDriver.CurrentRuntimeIdentifier();
        var debug = GetOptionalBoolean(arguments, "debug");
        var root = CompilerPathSecurity.CreateOwnedTemporaryDirectory("mcp-validate-");
        try
        {
            var path = Path.Combine(root, filename);
            await WriteSourcePackageAsync(root, path, source, includeFiles).ConfigureAwait(false);
            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);
            var result = await compiler.ValidateWithResultAsync(path, rid).ConfigureAwait(false);
            result.Source = new CompileSource { EntryPoint = filename };
            NormalizeDiagnosticFiles(result.Errors, root, filename);
            return result;
        }
        finally { try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(root); } catch { } }
    }

    private static async Task<object> AstValidateAsync(JsonElement arguments)
    {
        var source = GetRequiredString(arguments, "source");
        if (source.Length > MaxSourceChars) throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character validation limit.");
        var filename = ValidateFilename(GetOptionalString(arguments, "filename") ?? "program.xps");
        var includeFiles = ReadIncludeFiles(arguments, source.Length, filename);
        var root = CompilerPathSecurity.CreateOwnedTemporaryDirectory("mcp-ast-");
        var debug = GetOptionalBoolean(arguments, "debug");
        try
        {
            var sourcePath = Path.Combine(root, filename);
            var outputPath = Path.Combine(root, "output");
            await WriteSourcePackageAsync(root, sourcePath, source, includeFiles).ConfigureAwait(false);
            var warnings = new List<string>();
            using var diagnosticMode = CompilerDiagnosticMode.Push(debug, warningSink: warnings.Add);
            await AstCompiler.CompileAsync(sourcePath, outputPath).ConfigureAwait(false);
            return new { result = "ok", source = new { entryPoint = filename }, compiler = "ast",
                warnings = warnings.Select(description => new { file = filename, severity = "warning", description }).ToArray() };
        }
        catch (CompilerException exception)
        {
            NormalizeDiagnosticFiles(exception.GeneratedDiagnostics, root, filename);
            var diagnostics = exception.GeneratedDiagnostics.Count > 0
                ? exception.GeneratedDiagnostics.Select(diagnostic => new
                {
                    file = string.IsNullOrWhiteSpace(diagnostic.File) ? filename : diagnostic.File,
                    line = diagnostic.Line,
                    position = diagnostic.Position,
                    endLine = diagnostic.EndLine,
                    endColumn = diagnostic.EndColumn,
                    description = diagnostic.Description,
                    diagnosticCode = string.IsNullOrWhiteSpace(diagnostic.DiagnosticCode) ? exception.DiagnosticCode : diagnostic.DiagnosticCode,
                    severity = diagnostic.Severity,
                    category = diagnostic.Category,
                    includeTrace = diagnostic.IncludeTrace
                }).ToArray()
                : [new { file = filename, line = 0, position = 0, endLine = 0, endColumn = 0, description = exception.Message, diagnosticCode = exception.DiagnosticCode, severity = "error", category = exception.Category, includeTrace = (List<CompileIncludeFrame>?)null }];
            return new { result = "error", source = new { entryPoint = filename }, compiler = "ast", errors = diagnostics };
        }
        catch (Exception exception)
        {
            return new { result = "error", source = new { entryPoint = filename }, compiler = "ast", errors = new[] { new { file = filename, line = 0, position = 0, endLine = 0, endColumn = 0, description = exception.Message, diagnosticCode = CompilerDiagnosticCodes.InternalCompilationFailed, severity = "error", category = "compiler" } } };
        }
        finally { try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(root); } catch { } }
    }

    private static async Task<object> VirtualCompileAsync(JsonElement arguments)
    {
        var source = GetRequiredString(arguments, "source");
        if (source.Length > MaxSourceChars) throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character validation limit.");
        var filename = ValidateFilename(GetOptionalString(arguments, "filename") ?? "program.xps");
        var includeFiles = ReadIncludeFiles(arguments, source.Length, filename);
        var root = CompilerPathSecurity.CreateOwnedTemporaryDirectory("mcp-virtual-");
        var sourcePath = Path.Combine(root, filename);
        try
        {
            await WriteSourcePackageAsync(root, sourcePath, source, includeFiles).ConfigureAwait(false);
            using var diagnosticMode = CompilerDiagnosticMode.Push(GetOptionalBoolean(arguments, "debug"), true);
            using var securityMode = ApplicationSecurityModeContext.Push(ParseSecurityMode(arguments));
            using var preprocessors = SourcePreprocessorConfigurationContext.Push(GetOptionalStringArray(arguments, "sourcePreprocessors"));
            using var restricted = PushIncludePolicy(arguments, root);
            var result = await AstCompiler.CompileAsync(sourcePath, Path.Combine(root, "output")).ConfigureAwait(false);
            return new { result = "ok", source = new { entryPoint = filename }, compiler = "ast", info = BuildInfo("virtual-compile", filename, GetOptionalString(arguments, "runtimeIdentifier"), null, true) };
        }
        catch (CompilerException ex) { NormalizeDiagnosticFiles(ex.GeneratedDiagnostics, root, filename); return AstError(filename, ex); }
        catch (Exception ex) { return AstInternalError(filename, "virtual-compile", ex); }
        finally { try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(root); } catch { } }
    }

    private static async Task<object> CompileAsync(JsonElement arguments)
    {
        var source = GetRequiredString(arguments, "source");
        if (source.Length > MaxSourceChars) throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character compilation limit.");
        var filename = ValidateFilename(GetOptionalString(arguments, "filename") ?? "program.xps");
        var includeFiles = ReadIncludeFiles(arguments, source.Length, filename);
        var root = CompilerPathSecurity.CreateOwnedTemporaryDirectory("mcp-compile-");
        var output = GetOptionalString(arguments, "outputDirectory");
        // The request root is for source/staging files only. Published output must
        // survive request cleanup; when the caller does not provide an output
        // directory, use the server working directory rather than the temp root.
        var outputDirectory = string.IsNullOrWhiteSpace(output) ? Environment.CurrentDirectory : Path.GetFullPath(output);
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, filename);
        try
        {
            await WriteSourcePackageAsync(root, sourcePath, source, includeFiles).ConfigureAwait(false);
            using var diagnosticMode = CompilerDiagnosticMode.Push(GetOptionalBoolean(arguments, "debug"), true);
            var security = ParseSecurityMode(arguments);
            var roots = GetOptionalStringArray(arguments, "sourceRoots");
            if (GetOptionalBoolean(arguments, "restricted")) roots = roots.Append(root).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var preprocessors = GetOptionalStringArray(arguments, "sourcePreprocessors");
            var outputPath = await AstCompiler.CompileAsync(
                sourcePath,
                outputDirectory,
                GetOptionalString(arguments, "runtimeIdentifier"),
                security,
                GetOptionalBoolean(arguments, "restricted"),
                roots,
                preprocessors,
                cancellationToken: default).ConfigureAwait(false);
            return new { result = "ok", compiler = "ast", source = new { entryPoint = filename }, output = outputPath, errors = Array.Empty<CompileDiagnostic>(), info = BuildInfo("compile", filename, GetOptionalString(arguments, "runtimeIdentifier"), outputDirectory, true) };
        }
        catch (CompilerException ex)
        {
            NormalizeDiagnosticFiles(ex.GeneratedDiagnostics, root, filename);
            IEnumerable<CompileDiagnostic> errors = ex.GeneratedDiagnostics.Count > 0 ? ex.GeneratedDiagnostics : [new CompileDiagnostic { File = filename, Description = ex.Message, DiagnosticCode = ex.DiagnosticCode, Category = ex.Category }];
            return new { result = "error", compiler = "ast", source = new { entryPoint = filename }, errors, info = BuildInfo("compile", filename, GetOptionalString(arguments, "runtimeIdentifier"), outputDirectory, true) };
        }
        catch (Exception ex) { return new { result = "error", compiler = "ast", source = new { entryPoint = filename }, errors = new[] { new CompileDiagnostic { File = filename, Description = ex.Message, DiagnosticCode = CompilerDiagnosticCodes.InternalCompilationFailed, Category = "compiler" } }, info = BuildInfo("compile", filename, GetOptionalString(arguments, "runtimeIdentifier"), outputDirectory, true) }; }
        finally { try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(root); } catch { } }
    }

    private static IReadOnlyDictionary<string, string> ReadIncludeFiles(JsonElement arguments, int rootSourceLength, string filename)
    {
        if (rootSourceLength > MaxSourceChars)
            throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character validation limit.");
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("includeFiles", out var files))
            return new Dictionary<string, string>(StringComparer.Ordinal);
        if (files.ValueKind != JsonValueKind.Object)
            throw new McpException(-32602, "includeFiles must be an object mapping relative .xps paths to source text.");

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var result = new Dictionary<string, string>(comparer);
        var totalChars = rootSourceLength;
        foreach (var file in files.EnumerateObject())
        {
            if (result.Count >= MaxIncludedFiles)
                throw new McpException(-32602, $"includeFiles cannot contain more than {MaxIncludedFiles} files.");
            if (file.Value.ValueKind != JsonValueKind.String)
                throw new McpException(-32602, "Every includeFiles value must be source text.");

            var relativePath = file.Name.Replace('\\', '/');
            var segments = relativePath.Split('/');
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(file.Name) || file.Name.Contains(':', StringComparison.Ordinal) ||
                segments.Any(segment => segment.Length == 0 || segment is "." or "..") ||
                !Path.GetExtension(relativePath).Equals(".xps", StringComparison.OrdinalIgnoreCase) ||
                relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new McpException(-32602, $"includeFiles path must be a safe relative .xps path: {file.Name}");
            if (comparer.Equals(relativePath, filename))
                throw new McpException(-32602, $"includeFiles cannot replace the main source file: {filename}");

            var text = file.Value.GetString() ?? "";
            totalChars = checked(totalChars + text.Length);
            if (totalChars > MaxSourcePackageChars)
                throw new McpException(-32602, $"source and includeFiles exceed the {MaxSourcePackageChars} character package limit.");
            if (!result.TryAdd(relativePath, text))
                throw new McpException(-32602, $"includeFiles contains a duplicate path: {relativePath}");
        }
        return result;
    }

    private static async Task WriteSourcePackageAsync(string root, string sourcePath, string source, IReadOnlyDictionary<string, string> includeFiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, source).ConfigureAwait(false);
        foreach (var (relativePath, text) in includeFiles)
        {
            var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var relativeTarget = Path.GetRelativePath(root, target);
            if (Path.IsPathRooted(relativeTarget) || relativeTarget == ".." || relativeTarget.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new McpException(-32602, $"includeFiles path must stay inside the virtual source package: {relativePath}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, text).ConfigureAwait(false);
        }
    }

    private static void NormalizeDiagnosticFiles(IEnumerable<CompileDiagnostic> diagnostics, string root, string filename)
    {
        var fullRoot = Path.GetFullPath(root);
        foreach (var diagnostic in diagnostics)
        {
            if (string.IsNullOrWhiteSpace(diagnostic.File))
            {
                diagnostic.File = filename;
                continue;
            }
            if (!Path.IsPathRooted(diagnostic.File))
            {
                diagnostic.File = diagnostic.File.Replace('\\', '/');
                continue;
            }
            var relative = Path.GetRelativePath(fullRoot, Path.GetFullPath(diagnostic.File));
            diagnostic.File = Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? filename
                : relative.Replace('\\', '/');
        }
    }

    private static string ValidateFilename(string filename)
    {
        var relativePath = filename.Replace('\\', '/');
        var segments = relativePath.Split('/');
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(filename) || filename.Contains(':', StringComparison.Ordinal) ||
            segments.Any(segment => segment.Length == 0 || segment is "." or "..") ||
            !Path.GetExtension(relativePath).Equals(".xps", StringComparison.OrdinalIgnoreCase) ||
            relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new McpException(-32602, "filename must be a safe relative .xps path inside the virtual source package.");
        return relativePath;
    }

    private static object BuildInfo(string operation, string filename, string? runtimeIdentifier, string? output, bool ast) => new
    {
        operation, compiler = ast ? "ast" : "legacy", filename,
        runtimeIdentifier = runtimeIdentifier ?? CompilerDriver.CurrentRuntimeIdentifier(), output,
        debug = CompilerDiagnosticMode.Debug, info = true,
        processId = Environment.ProcessId, compilerVersion = typeof(CompilerMcpServer).Assembly.GetName().Version?.ToString() ?? "unknown"
    };

    private static object AstError(string filename, CompilerException exception) => new
    {
        result = "error", source = new { entryPoint = filename }, compiler = "ast",
        errors = exception.GeneratedDiagnostics.Count > 0 ? exception.GeneratedDiagnostics : [new CompileDiagnostic { File = filename, Description = exception.Message, DiagnosticCode = exception.DiagnosticCode, Category = exception.Category }],
        info = BuildInfo("virtual-compile", filename, null, null, true)
    };

    private static object AstInternalError(string filename, string operation, Exception exception) => new
    {
        result = "error", source = new { entryPoint = filename }, compiler = "ast",
        errors = new[] { new CompileDiagnostic { File = filename, Description = exception.Message, DiagnosticCode = CompilerDiagnosticCodes.InternalCompilationFailed, Category = "compiler" } },
        info = BuildInfo(operation, filename, null, null, true)
    };

    private static string GetRequiredString(JsonElement arguments, string name) => GetOptionalString(arguments, name) ?? throw new McpException(-32602, $"Missing required argument: {name}");
    private static string? GetOptionalString(JsonElement arguments, string name) => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool GetOptionalBoolean(JsonElement arguments, string name) => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static IReadOnlyList<string> GetOptionalStringArray(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray()
            : [];
    private static ApplicationSecurityMode ParseSecurityMode(JsonElement arguments)
    {
        var text = GetOptionalString(arguments, "securityMode");
        try { return ApplicationSecurityModeContext.Parse(text, ApplicationSecurityMode.Off); }
        catch (ArgumentException ex) { throw new McpException(-32602, ex.Message); }
    }
    private static IDisposable? PushIncludePolicy(JsonElement arguments, string sourceRoot)
    {
        if (!GetOptionalBoolean(arguments, "restricted")) return null;
        var roots = GetOptionalStringArray(arguments, "sourceRoots");
        return IncludeSecurityContext.Push(roots.Count == 0 ? [sourceRoot] : roots.Append(sourceRoot).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void WriteResponse(JsonElement id, object result) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc="2.0", id, result }, JsonOptions));
    private static void WriteError(JsonElement? id, int code, string message) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc="2.0", id, error=new { code, message } }, JsonOptions));
    private static async Task WriteErrorAsync(SemaphoreSlim gate, JsonElement? id, int code, string message) { await gate.WaitAsync().ConfigureAwait(false); try { WriteError(id, code, message); } finally { gate.Release(); } }
    private static JsonElement? TryReadId(string line) { try { using var doc=JsonDocument.Parse(line); return doc.RootElement.TryGetProperty("id", out var id) ? id.Clone() : null; } catch { return null; } }
    private sealed class McpException(int code, string message) : Exception(message) { public int Code { get; } = code; }
}
