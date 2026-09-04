namespace XPScript.Compiler;

internal sealed class AiPromptSchemaRuntimePostProcessor
{
    private const string Sentinel = "public string SystemPrompt";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("internal sealed class XPScriptAi : IDisposable", StringComparison.Ordinal))
            return generated;
        if (generated.Contains(Sentinel, StringComparison.Ordinal))
            return generated;

        generated = ReplaceRequired(generated,
            """
    private string _sessionId = string.Empty;
    private string _sessionRequestProperty = string.Empty;
""",
            """
    private string _sessionId = string.Empty;
    private string _sessionRequestProperty = string.Empty;
    private string _systemPrompt = string.Empty;
    private string _userPrompt = string.Empty;
    private System.Text.Json.Nodes.JsonObject? _responseJsonSchema;
    private string _responseJsonSchemaName = "response";
    private bool _responseJsonSchemaStrict = true;
""", "fields");

        generated = ReplaceRequired(generated,
            """
    public void NewRequest()
    {
""",
            """
    public string SystemPrompt
    {
        get { lock (_sync) return _systemPrompt; }
        set { SetPromptPart(true, value); }
    }

    public string UserPrompt
    {
        get { lock (_sync) return _userPrompt; }
        set { SetPromptPart(false, value); }
    }

    public string JsonSchemaName
    {
        get { lock (_sync) return _responseJsonSchemaName; }
        set
        {
            EnsureNotDisposed();
            var name = NormalizeJsonSchemaName(value);
            lock (_sync) _responseJsonSchemaName = name;
        }
    }

    public bool JsonSchemaStrict
    {
        get { lock (_sync) return _responseJsonSchemaStrict; }
        set { EnsureNotDisposed(); lock (_sync) _responseJsonSchemaStrict = value; }
    }

    public bool HasJsonSchema
    {
        get { lock (_sync) return _responseJsonSchema is not null; }
    }

    public object? ResponseJsonSchema
    {
        get
        {
            lock (_sync)
                return _responseJsonSchema is null ? null : new XPScriptJsonDocument(_responseJsonSchema.DeepClone());
        }
        set { SetJsonSchema(value); }
    }

    public void SetPrompt(object? systemPrompt, object? userPrompt)
    {
        SetPromptPart(true, systemPrompt);
        SetPromptPart(false, userPrompt);
    }

    public void ClearPrompt()
    {
        EnsureNotDisposed();
        lock (_sync)
        {
            _systemPrompt = string.Empty;
            _userPrompt = string.Empty;
        }
    }

    public void SetJsonSchema(object? schema) => SetJsonSchema(schema, null, null);
    public void SetJsonSchema(object? schema, object? name) => SetJsonSchema(schema, name, null);
    public void SetJsonSchema(object? schema, object? name, object? strict)
    {
        EnsureNotDisposed();
        if (schema is null || XPScriptNullRuntime.IsNull(schema))
        {
            ClearJsonSchema();
            return;
        }
        var node = XPScriptNativeJson.ToNode(schema);
        if (node is not System.Text.Json.Nodes.JsonObject schemaObject)
            throw new XPScriptRuntimeException(13, "XPAi JSON schema must be a JsonObject or JsonDocument with an object root.");
        XPScriptNativeJson.ValidateBudget(schemaObject);
        var schemaName = name is null || XPScriptNullRuntime.IsNull(name)
            ? JsonSchemaName
            : NormalizeJsonSchemaName(XPScriptRuntime.CStr(name));
        var strictValue = strict is null || XPScriptNullRuntime.IsNull(strict)
            ? JsonSchemaStrict
            : XPScriptRuntime.CBool(strict);
        lock (_sync)
        {
            _responseJsonSchema = (System.Text.Json.Nodes.JsonObject)schemaObject.DeepClone();
            _responseJsonSchemaName = schemaName;
            _responseJsonSchemaStrict = strictValue;
        }
    }

    public void ClearJsonSchema()
    {
        EnsureNotDisposed();
        lock (_sync) _responseJsonSchema = null;
    }

    private void SetPromptPart(bool system, object? value)
    {
        EnsureNotDisposed();
        var text = value is null || XPScriptNullRuntime.IsNull(value) ? string.Empty : XPScriptRuntime.CStr(value);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxRequestBytes)
            throw new XPScriptRuntimeException(5, "XPAi prompt part exceeds the 8 MiB limit.");
        lock (_sync)
        {
            if (system) _systemPrompt = text;
            else _userPrompt = text;
        }
    }

    private static string NormalizeJsonSchemaName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 64 || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new XPScriptRuntimeException(5, "XPAi JSON schema name must contain 1 to 64 letters, digits, underscores or hyphens.");
        return name;
    }

    public void NewRequest()
    {
""", "api");

        generated = ReplaceRequired(generated,
            """
            name.Equals("messages", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("stream", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "XPAi model, messages and stream must use their dedicated APIs.");
""",
            """
            name.Equals("messages", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("stream", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("response_format", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "XPAi model, messages, stream and response_format must use their dedicated APIs.");
""", "reserved-response-format");

        generated = ReplaceRequired(generated,
            """
        string sessionId;
        string sessionRequestProperty;
        lock (_sync)
        {
            messages = messagesValue is null || XPScriptNullRuntime.IsNull(messagesValue)
                ? (System.Text.Json.Nodes.JsonArray)_messages.DeepClone()
                : CopyMessages(messagesValue);
            options = (System.Text.Json.Nodes.JsonObject)_options.DeepClone();
            sessionId = _sessionId;
            sessionRequestProperty = _sessionRequestProperty;
        }
        if (messages.Count == 0)
            throw new XPScriptRuntimeException(5, "XPAi requires at least one message.");
""",
            """
        string sessionId;
        string sessionRequestProperty;
        string systemPrompt;
        string userPrompt;
        System.Text.Json.Nodes.JsonObject? responseJsonSchema;
        string responseJsonSchemaName;
        bool responseJsonSchemaStrict;
        lock (_sync)
        {
            messages = messagesValue is null || XPScriptNullRuntime.IsNull(messagesValue)
                ? (System.Text.Json.Nodes.JsonArray)_messages.DeepClone()
                : CopyMessages(messagesValue);
            options = (System.Text.Json.Nodes.JsonObject)_options.DeepClone();
            sessionId = _sessionId;
            sessionRequestProperty = _sessionRequestProperty;
            systemPrompt = _systemPrompt;
            userPrompt = _userPrompt;
            responseJsonSchema = _responseJsonSchema is null
                ? null
                : (System.Text.Json.Nodes.JsonObject)_responseJsonSchema.DeepClone();
            responseJsonSchemaName = _responseJsonSchemaName;
            responseJsonSchemaStrict = _responseJsonSchemaStrict;
        }
        var promptMessageCount = (systemPrompt.Length > 0 ? 1 : 0) + (userPrompt.Length > 0 ? 1 : 0);
        if (messages.Count + promptMessageCount == 0)
            throw new XPScriptRuntimeException(5, "XPAi requires at least one message or prompt part.");
        if (messages.Count + promptMessageCount > MaxMessages)
            throw new XPScriptRuntimeException(5, "XPAi message count exceeds the 10000-message limit.");
        if (systemPrompt.Length > 0)
            messages.Insert(0, new System.Text.Json.Nodes.JsonObject { ["role"] = "system", ["content"] = systemPrompt });
        if (userPrompt.Length > 0)
            messages.Add(new System.Text.Json.Nodes.JsonObject { ["role"] = "user", ["content"] = userPrompt });
""", "prompt-request");

        generated = ReplaceRequired(generated,
            """
        var providerTools = XPScriptAiToolRegistry.BuildProviderTools(this);
        if (providerTools.Count > 0)
            body["tools"] = providerTools;
        return body;
""",
            """
        var providerTools = XPScriptAiToolRegistry.BuildProviderTools(this);
        if (providerTools.Count > 0)
            body["tools"] = providerTools;
        if (responseJsonSchema is not null)
        {
            body["response_format"] = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = responseJsonSchemaName,
                    ["strict"] = responseJsonSchemaStrict,
                    ["schema"] = responseJsonSchema
                }
            };
        }
        return body;
""", "schema-request");

        generated = ReplaceRequired(generated,
            """
    public string Content => Text;
    public XPScriptJsonDocument RawJson { get; }
    public XPScriptJsonDocument Usage { get; }
""",
            """
    public string Content => Text;
    public XPScriptJsonDocument RawJson { get; }
    public XPScriptJsonDocument Usage { get; }

    public bool HasJsonResult
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text)) return false;
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(Text,
                    documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
                if (node is null) return false;
                XPScriptNativeJson.ValidateBudget(node);
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }
    }

    public XPScriptJsonDocument ResultJson
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text))
                throw new XPScriptRuntimeException(5, "XPAi response does not contain a JSON result.");
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(Text,
                    documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 })
                    ?? throw new XPScriptRuntimeException(5, "XPAi response does not contain a JSON result.");
                XPScriptNativeJson.ValidateBudget(node);
                return new XPScriptJsonDocument(node);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new XPScriptRuntimeException(5, "XPAi response text is not valid JSON.");
            }
        }
    }
""", "response-json");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install XPAi prompt/schema runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
