namespace XPScript.Compiler;

internal sealed class AiSessionRuntimePostProcessor
{
    private const string Sentinel = "public string SessionRequestProperty";
    private const string ToolRuntimeSentinel = "internal sealed class XPScriptAiTool";

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

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install XPAi session runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
