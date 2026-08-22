namespace XPScript.Compiler;

internal sealed class AiSessionRuntimePostProcessor
{
    private const string Sentinel = "public string SessionRequestProperty";
    private const string ToolRuntimeSentinel = "internal sealed class XPScriptAiTool";
    private const string ToolRequestSentinel = "XPScriptAiToolRegistry.BuildProviderTools(this)";
    private const string ToolLoopSentinel = "private bool ExecuteToolCalls(";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("internal sealed class XPScriptAi : IDisposable", StringComparison.Ordinal))
            return generated;

        if (!generated.Contains(Sentinel, StringComparison.Ordinal))
        {
            generated = ReplaceRequired(generated,
                "    private int? _maxOutputTokens;\n",
                """
    private int? _maxOutputTokens;
    private string _sessionId = string.Empty;
    private string _sessionRequestProperty = string.Empty;
""", "session-fields");

            generated = ReplaceRequired(generated,
                """
    public bool CollectStreamedResponse { get; set; } = true;
    public bool ThrowOnHttpError { get; set; } = true;

    public void AddMessage(object? roleValue, object? contentValue)
""",
                """
    public bool CollectStreamedResponse { get; set; } = true;
    public bool ThrowOnHttpError { get; set; } = true;

    public string SessionId
    {
        get { lock (_sync) return _sessionId; }
    }

    public bool HasSession
    {
        get { lock (_sync) return _sessionId.Length > 0; }
    }

    public string SessionRequestProperty
    {
        get { lock (_sync) return _sessionRequestProperty; }
        set
        {
            EnsureNotDisposed();
            var property = value?.Trim() ?? string.Empty;
            if (property.Length > 0)
            {
                property = ValidateJsonPropertyName(property);
                if (property.Equals("model", StringComparison.OrdinalIgnoreCase) ||
                    property.Equals("messages", StringComparison.OrdinalIgnoreCase) ||
                    property.Equals("stream", StringComparison.OrdinalIgnoreCase))
                    throw new XPScriptRuntimeException(5, "XPAi SessionRequestProperty conflicts with a reserved request property.");
            }
            lock (_sync) _sessionRequestProperty = property;
        }
    }

    public void NewRequest()
    {
        EnsureNotDisposed();
        lock (_sync)
        {
            _messages.Clear();
            _sessionId = string.Empty;
        }
    }

    public void ResetSession()
    {
        EnsureNotDisposed();
        lock (_sync) _sessionId = string.Empty;
    }

    public void AddMessage(object? roleValue, object? contentValue)
""", "session-api");

            generated = ReplaceRequired(generated,
                """
        System.Text.Json.Nodes.JsonArray messages;
        System.Text.Json.Nodes.JsonObject options;
        lock (_sync)
        {
            messages = messagesValue is null || XPScriptNullRuntime.IsNull(messagesValue)
                ? (System.Text.Json.Nodes.JsonArray)_messages.DeepClone()
                : CopyMessages(messagesValue);
            options = (System.Text.Json.Nodes.JsonObject)_options.DeepClone();
        }
""",
                """
        System.Text.Json.Nodes.JsonArray messages;
        System.Text.Json.Nodes.JsonObject options;
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
""", "request-session-snapshot");

            generated = ReplaceRequired(generated,
                """
        foreach (var option in options)
            body[option.Key] = option.Value?.DeepClone();
        return body;
""",
                """
        foreach (var option in options)
            body[option.Key] = option.Value?.DeepClone();
        if (sessionId.Length > 0 && sessionRequestProperty.Length > 0)
            body[sessionRequestProperty] = sessionId;
        return body;
""", "request-session-injection");

            generated = ReplaceRequired(generated,
                """
        XPScriptNativeJson.ValidateBudget(node);
        return CreateResponse(response, node, ExtractText(node), ExtractModel(node), ExtractUsage(node));
""",
                """
        XPScriptNativeJson.ValidateBudget(node);
        RememberSessionId(node, response);
        return CreateResponse(response, node, ExtractText(node), ExtractModel(node), ExtractUsage(node));
""", "response-session-capture");

            generated = ReplaceRequired(generated,
                """
            XPScriptNativeJson.ValidateBudget(eventNode);
            var chunk = ExtractStreamText(eventNode);
""",
                """
            XPScriptNativeJson.ValidateBudget(eventNode);
            RememberSessionId(eventNode, response);
            var chunk = ExtractStreamText(eventNode);
""", "stream-session-capture");

            generated = ReplaceRequired(generated,
                """
    private static XPScriptAiResponse CreateResponse(
""",
                """
    private void RememberSessionId(System.Text.Json.Nodes.JsonNode? node, System.Net.Http.HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return;
        var sessionId = ExtractSessionId(node);
        if (sessionId.Length == 0 && response.Headers.TryGetValues("X-Session-Id", out var headerValues))
            sessionId = headerValues.FirstOrDefault()?.Trim() ?? string.Empty;
        if (sessionId.Length == 0) return;
        if (sessionId.Length > 4096 || sessionId.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "XPAi returned an invalid session identifier.");
        lock (_sync) _sessionId = sessionId;
    }

    private static string ExtractSessionId(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is not System.Text.Json.Nodes.JsonObject root) return string.Empty;
        foreach (var property in new[] { "session_id", "sessionId", "conversation_id", "conversationId", "response_id", "id" })
            if (TryString(root[property], out var value) && value.Trim().Length > 0)
                return value.Trim();
        return string.Empty;
    }

    private static XPScriptAiResponse CreateResponse(
""", "session-helper");
        }

        if (!generated.Contains(ToolRuntimeSentinel, StringComparison.Ordinal))
            generated += "\n\n" + AiToolRuntimeSource.Code + "\n";

        if (!generated.Contains(ToolRequestSentinel, StringComparison.Ordinal))
        {
            generated = ReplaceRequired(generated,
                """
        if (sessionId.Length > 0 && sessionRequestProperty.Length > 0)
            body[sessionRequestProperty] = sessionId;
        return body;
""",
                """
        if (sessionId.Length > 0 && sessionRequestProperty.Length > 0)
            body[sessionRequestProperty] = sessionId;
        var providerTools = XPScriptAiToolRegistry.BuildProviderTools(this);
        if (providerTools.Count > 0)
            body["tools"] = providerTools;
        return body;
""", "tool-request-schema");
        }

        if (!generated.Contains("public bool AutoExecuteTools", StringComparison.Ordinal))
        {
            generated = ReplaceRequired(generated,
                """
    public bool CollectStreamedResponse { get; set; } = true;
    public bool ThrowOnHttpError { get; set; } = true;

    public string SessionId
""",
                """
    public bool CollectStreamedResponse { get; set; } = true;
    public bool ThrowOnHttpError { get; set; } = true;
    public bool AutoExecuteTools { get; set; } = true;

    private int _maxToolIterations = 8;
    public int MaxToolIterations
    {
        get => _maxToolIterations;
        set
        {
            if (value < 1 || value > 32)
                throw new XPScriptRuntimeException(5, "XPAi MaxToolIterations must be between 1 and 32.");
            _maxToolIterations = value;
        }
    }

    public string SessionId
""", "tool-settings");
        }

        if (!generated.Contains(ToolLoopSentinel, StringComparison.Ordinal))
        {
            generated = ReplaceRequired(generated,
                """
        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        var requestText = requestJson.ToJsonString();
        if (System.Text.Encoding.UTF8.GetByteCount(requestText) > MaxRequestBytes)
            throw new XPScriptRuntimeException(5, "XPAi request body exceeds the 8 MiB limit.");

        var cancellation = BeginRequest();
        try
        {
            using var request = BuildHttpRequest(requestText, stream);
            using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            var result = stream
                ? ReadStream(response, callbackName, callbackArguments ?? [], cancellation.Token)
                : ReadResponse(response, cancellation.Token);
            if (ThrowOnHttpError && !result.IsSuccess)
                throw new XPScriptRuntimeException(5, $"XPAi request failed with HTTP status {result.StatusCode}.");
            return result;
        }
""",
                """
        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        var cancellation = BeginRequest();
        try
        {
            for (var iteration = 0; ; iteration++)
            {
                var requestText = requestJson.ToJsonString();
                if (System.Text.Encoding.UTF8.GetByteCount(requestText) > MaxRequestBytes)
                    throw new XPScriptRuntimeException(5, "XPAi request body exceeds the 8 MiB limit.");

                using var request = BuildHttpRequest(requestText, stream);
                using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                var result = stream
                    ? ReadStream(response, callbackName, callbackArguments ?? [], cancellation.Token)
                    : ReadResponse(response, cancellation.Token);
                if (ThrowOnHttpError && !result.IsSuccess)
                    throw new XPScriptRuntimeException(5, $"XPAi request failed with HTTP status {result.StatusCode}.");
                if (stream || !AutoExecuteTools || !ExecuteToolCalls(result, requestJson))
                    return result;
                if (iteration + 1 >= MaxToolIterations)
                    throw new XPScriptRuntimeException(5, "XPAi tool execution exceeded MaxToolIterations.");
            }
        }
""", "tool-loop");

            generated = ReplaceRequired(generated,
                """
    private static XPScriptAiResponse CreateResponse(
""",
                """
    private bool ExecuteToolCalls(XPScriptAiResponse response, System.Text.Json.Nodes.JsonObject requestJson)
    {
        if (!response.IsSuccess || response.RawJson.Node is not System.Text.Json.Nodes.JsonObject root ||
            root["choices"] is not System.Text.Json.Nodes.JsonArray choices || choices.Count == 0 ||
            choices[0] is not System.Text.Json.Nodes.JsonObject choice ||
            choice["message"] is not System.Text.Json.Nodes.JsonObject assistantMessage ||
            assistantMessage["tool_calls"] is not System.Text.Json.Nodes.JsonArray toolCalls || toolCalls.Count == 0)
            return false;
        if (toolCalls.Count > 64)
            throw new XPScriptRuntimeException(5, "XPAi response exceeds the 64 tool-call limit per iteration.");
        if (requestJson["messages"] is not System.Text.Json.Nodes.JsonArray messages)
            throw new XPScriptRuntimeException(5, "XPAi tool continuation requires a message array.");

        messages.Add(assistantMessage.DeepClone());
        string sessionId;
        lock (_sync) sessionId = _sessionId;

        foreach (var item in toolCalls)
        {
            if (item is not System.Text.Json.Nodes.JsonObject call ||
                !TryString(call["id"], out var callId) || callId.Length == 0 || callId.Length > 4096 ||
                call["function"] is not System.Text.Json.Nodes.JsonObject function ||
                !TryString(function["name"], out var functionName) || functionName.Length == 0 ||
                !TryString(function["arguments"], out var argumentsText))
                throw new XPScriptRuntimeException(5, "XPAi returned an invalid tool call.");
            if (System.Text.Encoding.UTF8.GetByteCount(argumentsText) > 1024 * 1024)
                throw new XPScriptRuntimeException(5, "XPAi tool arguments exceed the 1 MiB limit.");

            System.Text.Json.Nodes.JsonObject arguments;
            try
            {
                arguments = System.Text.Json.Nodes.JsonNode.Parse(argumentsText,
                    documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 }) as System.Text.Json.Nodes.JsonObject
                    ?? throw new XPScriptRuntimeException(5, "XPAi tool arguments must be a JSON object.");
            }
            catch (System.Text.Json.JsonException)
            {
                throw new XPScriptRuntimeException(5, "XPAi tool arguments contain malformed JSON.");
            }
            XPScriptNativeJson.ValidateBudget(arguments);
            var result = XPScriptAiToolRegistry.InvokeFunction(this, functionName, callId, arguments, sessionId);
            var resultNode = XPScriptNativeJson.ToNode(result);
            var content = resultNode is System.Text.Json.Nodes.JsonValue stringValue && stringValue.TryGetValue<string>(out var text)
                ? text
                : resultNode?.ToJsonString() ?? "null";
            if (System.Text.Encoding.UTF8.GetByteCount(content) > 4 * 1024 * 1024)
                throw new XPScriptRuntimeException(5, "XPAi tool result exceeds the 4 MiB limit.");
            messages.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = callId,
                ["content"] = content
            });
        }
        return true;
    }

    private static XPScriptAiResponse CreateResponse(
""", "tool-call-execution");
        }

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install XPAi session/tool runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
