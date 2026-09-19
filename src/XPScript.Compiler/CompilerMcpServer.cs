using System.Text.Json;
using System.Text.Json.Serialization;

namespace XPScript.Compiler;

public static class CompilerMcpServer
{
    private const string ProtocolVersion = "2025-06-18";
    private const int MaxSourceChars = 1_048_576;
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
        string? line;
        while ((line = await Console.In.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var request = JsonDocument.Parse(line);
                var root = request.RootElement;
                var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : (JsonElement?)null;
                var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
                var parameters = root.TryGetProperty("params", out var paramsElement) ? paramsElement : default;

                if (method == "notifications/initialized") continue;
                if (id is null) continue;

                object result = method switch
                {
                    "initialize" => Initialize(),
                    "ping" => new { },
                    "tools/list" => new { tools = Tools() },
                    "tools/call" => await CallToolAsync(compiler, parameters).ConfigureAwait(false),
                    _ => throw new McpException(-32601, $"Method not found: {method}")
                };
                WriteResponse(id.Value, result);
            }
            catch (McpException ex)
            {
                WriteError(TryReadId(line), ex.Code, ex.Message);
            }
            catch (JsonException ex)
            {
                WriteError(null, -32700, ex.Message);
            }
            catch (Exception ex)
            {
                WriteError(TryReadId(line), -32603, ex.Message);
            }
        }
        return 0;
    }

    private static object Initialize() => new
    {
        protocolVersion = ProtocolVersion,
        capabilities = new { tools = new { listChanged = false } },
        serverInfo = new { name = "xpscript-compiler", version = typeof(CompilerMcpServer).Assembly.GetName().Version?.ToString() ?? "unknown" }
    };

    private static object[] Tools() =>
    [
        Tool("xpscript_validate", "Validate XPScript source without executing it.", new { type="object", properties=new { source=new { type="string" }, filename=new { type="string", description="Simple virtual .xps filename." }, runtimeIdentifier=new { type="string" } }, required=new[]{"source"} }),
        Tool("xpscript_symbols", "Search the public XPScript symbol catalog.", new { type="object", properties=new { search=new { type="string" } } }),
        Tool("xpscript_describe", "Describe an exact public XPScript symbol.", new { type="object", properties=new { name=new { type="string" } }, required=new[]{"name"} }),
        Tool("xpscript_explain", "Explain a stable XPScript diagnostic code.", new { type="object", properties=new { diagnosticCode=new { type="string" } }, required=new[]{"diagnosticCode"} })
    ];

    private static object Tool(string name, string description, object inputSchema) => new { name, description, inputSchema };

    private static async Task<object> CallToolAsync(CompilerDriver compiler, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var nameElement))
            throw new McpException(-32602, "tools/call requires a tool name.");
        var name = nameElement.GetString() ?? "";
        var arguments = parameters.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object ? args : default;
        object value = name switch
        {
            "xpscript_validate" => await ValidateAsync(compiler, arguments).ConfigureAwait(false),
            "xpscript_symbols" => CompilerSymbolCatalog.Search(GetOptionalString(arguments, "search") ?? ""),
            "xpscript_describe" => CompilerSymbolCatalog.Find(GetRequiredString(arguments, "name")) ?? throw new McpException(-32602, "Unknown XPScript symbol."),
            "xpscript_explain" => CompilerDiagnosticCatalog.Find(GetRequiredString(arguments, "diagnosticCode")) ?? throw new McpException(-32602, "Unknown XPScript diagnostic code."),
            _ => throw new McpException(-32602, $"Unknown tool: {name}")
        };
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, JsonOptions) } }, structuredContent = value, isError = false };
    }

    private static async Task<CompileResult> ValidateAsync(CompilerDriver compiler, JsonElement arguments)
    {
        var source = GetRequiredString(arguments, "source");
        if (source.Length > MaxSourceChars) throw new McpException(-32602, $"source exceeds the {MaxSourceChars} character validation limit.");
        var filename = GetOptionalString(arguments, "filename") ?? "program.xps";
        if (!Path.GetFileName(filename).Equals(filename, StringComparison.Ordinal) || !Path.GetExtension(filename).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            throw new McpException(-32602, "filename must be a simple .xps filename without a directory path.");
        var rid = GetOptionalString(arguments, "runtimeIdentifier") ?? CompilerDriver.CurrentRuntimeIdentifier();
        var root = Path.Combine(Path.GetTempPath(), "XPScript", "mcp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, filename);
            await File.WriteAllTextAsync(path, source).ConfigureAwait(false);
            var result = await compiler.ValidateWithResultAsync(path, rid).ConfigureAwait(false);
            result.Source = new CompileSource { EntryPoint = filename };
            foreach (var diagnostic in result.Errors) if (!string.IsNullOrWhiteSpace(diagnostic.File)) diagnostic.File = filename;
            return result;
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static string GetRequiredString(JsonElement arguments, string name) => GetOptionalString(arguments, name) ?? throw new McpException(-32602, $"Missing required argument: {name}");
    private static string? GetOptionalString(JsonElement arguments, string name) => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void WriteResponse(JsonElement id, object result) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc="2.0", id, result }, JsonOptions));
    private static void WriteError(JsonElement? id, int code, string message) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc="2.0", id, error=new { code, message } }, JsonOptions));
    private static JsonElement? TryReadId(string line) { try { using var doc=JsonDocument.Parse(line); return doc.RootElement.TryGetProperty("id", out var id) ? id.Clone() : null; } catch { return null; } }
    private sealed class McpException(int code, string message) : Exception(message) { public int Code { get; } = code; }
}
