namespace XPScript.Compiler;

internal static class AiRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptAi : IDisposable
{
    private const int MaxRequestBytes = 8 * 1024 * 1024;
    private const int MaxResponseBytes = 16 * 1024 * 1024;
    private const int MaxStreamLineBytes = 1024 * 1024;
    private const int MaxStreamEvents = 100_000;
    private const int MaxMessages = 10_000;

    private readonly object _sync = new();
    private readonly System.Net.Http.HttpClientHandler _handler;
    private readonly System.Net.Http.HttpClient _client;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Text.Json.Nodes.JsonArray _messages = [];
    private readonly System.Text.Json.Nodes.JsonObject _options = [];
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _provider;
    private readonly bool _useBearerAuthentication;
    private CancellationTokenSource? _activeRequest;
    private TimeSpan _timeout = TimeSpan.FromSeconds(60);
    private bool _disposed;
    private string _model = string.Empty;
    private string _endpointPath = string.Empty;
    private double? _temperature;
    private int? _maxOutputTokens;

    public XPScriptAi(object? endpointOrPreset) : this(endpointOrPreset, null, null)
    {
    }

    public XPScriptAi(object? endpointOrPreset, object? apiKey) : this(endpointOrPreset, apiKey, null)
    {
    }

    public XPScriptAi(object? endpointOrPreset, object? apiKey, object? providerConfiguration)
    {
        var preset = ResolvePreset(endpointOrPreset, providerConfiguration);
        _endpoint = preset.Endpoint;
        _provider = preset.Provider;
        _useBearerAuthentication = preset.UseBearerAuthentication;
        _apiKey = apiKey is null || XPScriptNullRuntime.IsNull(apiKey) ? string.Empty : XPScriptRuntime.CStr(apiKey).Trim();
        if (_apiKey.IndexOfAny(['\r', '\n', '\0']) >= 0 || _apiKey.Length > 16 * 1024)
            throw new XPScriptRuntimeException(5, "XPAi API key is invalid.");

        _handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
        _client = new System.Net.Http.HttpClient(_handler, disposeHandler: false)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    public string Endpoint => _endpoint.ToString();
    public string Provider => _provider;

    public string EndpointPath
    {
        get => _endpointPath;
        set
        {
            EnsureNotDisposed();
            var path = value?.Trim() ?? string.Empty;
            if (path.IndexOfAny(['\r', '\n', '\0', '#']) >= 0 || path.Length > 4096)
                throw new XPScriptRuntimeException(5, "XPAi EndpointPath is invalid.");
            _endpointPath = path;
            _ = ResolveEndpoint();
        }
    }

    public string Model
    {
        get => _model;
        set
        {
            EnsureNotDisposed();
            var model = value?.Trim() ?? string.Empty;
            if (model.IndexOfAny(['\r', '\n', '\0']) >= 0 || model.Length > 1024)
                throw new XPScriptRuntimeException(5, "XPAi Model is invalid.");
            _model = model;
        }
    }

    public double Timeout
    {
        get => _timeout.TotalSeconds;
        set
        {
            EnsureNotDisposed();
            if (value < 0.1 || value > 3600 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "XPAi Timeout must be between 0.1 and 3600 seconds.");
            _timeout = TimeSpan.FromSeconds(value);
        }
    }

    public double Temperature
    {
        get => _temperature ?? -1;
        set
        {
            EnsureNotDisposed();
            if (value < 0 || value > 2 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "XPAi Temperature must be between 0 and 2.");
            _temperature = value;
        }
    }

    public int MaxOutputTokens
    {
        get => _maxOutputTokens ?? 0;
        set
        {
            EnsureNotDisposed();
            if (value < 1 || value > 1_000_000)
                throw new XPScriptRuntimeException(5, "XPAi MaxOutputTokens must be between 1 and 1000000.");
            _maxOutputTokens = value;
        }
    }

    public bool CollectStreamedResponse { get; set; } = true;
    public bool ThrowOnHttpError { get; set; } = true;

    public void AddMessage(object? roleValue, object? contentValue)
    {
        EnsureNotDisposed();
        var role = XPScriptRuntime.CStr(roleValue).Trim().ToLowerInvariant();
        if (role is not ("system" or "user" or "assistant"))
            throw new XPScriptRuntimeException(5, "XPAi message role must be system, user or assistant.");
        var content = XPScriptRuntime.CStr(contentValue);
        if (System.Text.Encoding.UTF8.GetByteCount(content) > MaxRequestBytes)
            throw new XPScriptRuntimeException(5, "XPAi message content exceeds the 8 MiB limit.");
        lock (_sync)
        {
            if (_messages.Count >= MaxMessages)
                throw new XPScriptRuntimeException(5, "XPAi message count exceeds the 10000-message limit.");
            _messages.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["role"] = role,
                ["content"] = content
            });
        }
    }

    public void ClearMessages()
    {
        EnsureNotDisposed();
        lock (_sync) _messages.Clear();
    }

    public XPScriptJsonDocument GetMessages()
    {
        EnsureNotDisposed();
        lock (_sync) return new XPScriptJsonDocument(_messages.DeepClone());
    }

    public void SetOption(object? nameValue, object? value)
    {
        EnsureNotDisposed();
        var name = ValidateJsonPropertyName(nameValue);
        if (name.Equals("model", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("messages", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("stream", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "XPAi model, messages and stream must use their dedicated APIs.");
        lock (_sync) _options[name] = XPScriptNativeJson.ToNode(value);
    }

    public void RemoveOption(object? nameValue)
    {
        EnsureNotDisposed();
        lock (_sync) _options.Remove(ValidateJsonPropertyName(nameValue));
    }

    public void ClearOptions()
    {
        EnsureNotDisposed();
        lock (_sync) _options.Clear();
    }

    public void SetHeader(object? nameValue, object? value)
    {
        EnsureNotDisposed();
        var name = ValidateHeaderName(nameValue);
        if (name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "XPAi does not allow caller control of transport headers.");
        var text = XPScriptRuntime.CStr(value);
        ValidateHeaderValue(text);
        lock (_sync) _headers[name] = text;
    }

    public void RemoveHeader(object? nameValue)
    {
        EnsureNotDisposed();
        lock (_sync) _headers.Remove(ValidateHeaderName(nameValue));
    }

    public void ClearHeaders()
    {
        EnsureNotDisposed();
        lock (_sync) _headers.Clear();
    }

    public XPScriptAiResponse Complete() => Send(null, false, null, null);
    public XPScriptAiResponse Complete(object? messages) => Send(messages, false, null, null);
    public XPScriptAiResponse Complete(object? messages, object? model) => Send(messages, false, null, model);
    public XPScriptAiResponse Stream(object? callbackName) => Send(null, true, callbackName, null);
    public XPScriptAiResponse Stream(object? messages, object? callbackName) => Send(messages, true, callbackName, null);
    public XPScriptAiResponse Stream(object? messages, object? callbackName, object? model) => Send(messages, true, callbackName, model);

    public void Cancel()
    {
        lock (_sync) _activeRequest?.Cancel();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _activeRequest?.Cancel();
            _activeRequest?.Dispose();
            _activeRequest = null;
        }
        _client.Dispose();
        _handler.Dispose();
        GC.SuppressFinalize(this);
    }

    private XPScriptAiResponse Send(object? messagesValue, bool stream, object? callbackValue, object? modelValue)
    {
        EnsureNotDisposed();
        var callbackName = stream ? XPScriptRuntime.CStr(callbackValue).Trim() : string.Empty;
        if (stream && callbackName.Length == 0)
            throw new XPScriptRuntimeException(5, "XPAi.Stream requires a callback procedure name.");
        if (callbackName.Length > 256 || (callbackName.Length > 0 && !IsIdentifier(callbackName)))
            throw new XPScriptRuntimeException(5, "XPAi stream callback name is invalid.");

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
                ? ReadStream(response, callbackName, cancellation.Token)
                : ReadResponse(response, cancellation.Token);
            if (ThrowOnHttpError && !result.IsSuccess)
                throw new XPScriptRuntimeException(5, $"XPAi request failed with HTTP status {result.StatusCode}.");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (cancellation.IsCancellationRequested)
                throw new XPScriptRuntimeException(5, "XPAi request was cancelled or timed out.");
            throw new XPScriptRuntimeException(5, "XPAi request was cancelled.");
        }
        catch (System.Net.Http.HttpRequestException)
        {
            throw new XPScriptRuntimeException(5, "XPAi HTTP request failed.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(5, "XPAi response could not be read.");
        }
        finally
        {
            EndRequest(cancellation);
        }
    }

    private System.Text.Json.Nodes.JsonObject BuildRequest(object? messagesValue, bool stream, object? modelValue)
    {
        var model = modelValue is null || XPScriptNullRuntime.IsNull(modelValue)
            ? _model.Trim()
            : XPScriptRuntime.CStr(modelValue).Trim();
        if (model.Length == 0)
            throw new XPScriptRuntimeException(5, "XPAi Model must be configured before sending a request.");
        if (model.Length > 1024 || model.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "XPAi request model is invalid.");

        System.Text.Json.Nodes.JsonArray messages;
        System.Text.Json.Nodes.JsonObject options;
        lock (_sync)
        {
            messages = messagesValue is null || XPScriptNullRuntime.IsNull(messagesValue)
                ? (System.Text.Json.Nodes.JsonArray)_messages.DeepClone()
                : CopyMessages(messagesValue);
            options = (System.Text.Json.Nodes.JsonObject)_options.DeepClone();
        }
        if (messages.Count == 0)
            throw new XPScriptRuntimeException(5, "XPAi requires at least one message.");

        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = stream
        };
        if (_temperature is double temperature) body["temperature"] = temperature;
        if (_maxOutputTokens is int maxOutputTokens) body["max_tokens"] = maxOutputTokens;
        foreach (var option in options)
            body[option.Key] = option.Value?.DeepClone();
        return body;
    }

    private static System.Text.Json.Nodes.JsonArray CopyMessages(object? value)
    {
        var node = XPScriptNativeJson.ToNode(value);
        var array = node switch
        {
            System.Text.Json.Nodes.JsonArray jsonArray => jsonArray,
            _ => throw new XPScriptRuntimeException(13, "XPAi messages must be a JsonArray or JsonDocument with an array root.")
        };
        if (array.Count == 0 || array.Count > MaxMessages)
            throw new XPScriptRuntimeException(5, "XPAi messages must contain between 1 and 10000 items.");
        foreach (var item in array)
        {
            if (item is not System.Text.Json.Nodes.JsonObject message ||
                message["role"] is not System.Text.Json.Nodes.JsonValue roleValue ||
                !roleValue.TryGetValue<string>(out var role) ||
                !(string.Equals(role, "system", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)))
                throw new XPScriptRuntimeException(5, "Each XPAi message must contain a supported role.");
            if (!message.ContainsKey("content"))
                throw new XPScriptRuntimeException(5, "Each XPAi message must contain content.");
        }
        return (System.Text.Json.Nodes.JsonArray)array.DeepClone();
    }

    private System.Net.Http.HttpRequestMessage BuildHttpRequest(string requestText, bool stream)
    {
        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, ResolveEndpoint());
        request.Content = new System.Net.Http.StringContent(requestText, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd(stream ? "text/event-stream" : "application/json");

        Dictionary<string, string> headers;
        lock (_sync) headers = new Dictionary<string, string>(_headers, StringComparer.OrdinalIgnoreCase);
        if (_apiKey.Length > 0)
        {
            if (_useBearerAuthentication && !headers.ContainsKey("Authorization"))
                headers["Authorization"] = "Bearer " + _apiKey;
            else if (!_useBearerAuthentication && !headers.ContainsKey("api-key"))
                headers["api-key"] = _apiKey;
        }

        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                try { request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(header.Value); }
                catch (FormatException) { request.Dispose(); throw new XPScriptRuntimeException(5, "XPAi Content-Type header is invalid."); }
                continue;
            }
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value) &&
                !request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Dispose();
                throw new XPScriptRuntimeException(5, "XPAi header is invalid for this request.");
            }
        }
        return request;
    }

    private XPScriptAiResponse ReadResponse(System.Net.Http.HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = ReadBoundedBody(response.Content, cancellationToken);
        System.Text.Json.Nodes.JsonNode? node = null;
        if (body.Length > 0)
        {
            try { node = System.Text.Json.Nodes.JsonNode.Parse(body, documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 }); }
            catch (System.Text.Json.JsonException)
            {
                if (response.IsSuccessStatusCode)
                    throw new XPScriptRuntimeException(5, "XPAi returned an invalid JSON response.");
            }
        }
        node ??= new System.Text.Json.Nodes.JsonObject();
        XPScriptNativeJson.ValidateBudget(node);
        return CreateResponse(response, node, ExtractText(node), ExtractModel(node), ExtractUsage(node));
    }

    private XPScriptAiResponse ReadStream(System.Net.Http.HttpResponseMessage response, string callbackName, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            return ReadResponse(response, cancellationToken);

        using var stream = response.Content.ReadAsStream(cancellationToken);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: false);
        var complete = new StringBuilder();
        var dataLines = new List<string>();
        var events = 0;
        var payloadBytes = 0;
        var model = string.Empty;
        System.Text.Json.Nodes.JsonObject? usage = null;
        var done = false;

        while (!done)
        {
            var line = reader.ReadLineAsync(cancellationToken).AsTask().GetAwaiter().GetResult();
            if (line is null)
            {
                if (dataLines.Count > 0) ProcessEvent();
                break;
            }
            if (System.Text.Encoding.UTF8.GetByteCount(line) > MaxStreamLineBytes)
                throw new XPScriptRuntimeException(5, "XPAi stream line exceeds the 1 MiB limit.");
            if (line.Length == 0)
            {
                if (dataLines.Count > 0) ProcessEvent();
                continue;
            }
            if (line.StartsWith("data:", StringComparison.Ordinal))
                dataLines.Add(line[5..].TrimStart());
        }

        var raw = new System.Text.Json.Nodes.JsonObject
        {
            ["stream"] = true,
            ["completed"] = done,
            ["event_count"] = events
        };
        if (usage is not null) raw["usage"] = usage.DeepClone();
        return CreateResponse(response, raw, complete.ToString(), model, usage);

        void ProcessEvent()
        {
            var data = string.Join("\n", dataLines);
            dataLines.Clear();
            if (data == "[DONE]") { done = true; return; }
            events = checked(events + 1);
            if (events > MaxStreamEvents)
                throw new XPScriptRuntimeException(5, "XPAi stream exceeds the 100000-event limit.");
            payloadBytes = checked(payloadBytes + System.Text.Encoding.UTF8.GetByteCount(data));
            if (payloadBytes > MaxResponseBytes)
                throw new XPScriptRuntimeException(5, "XPAi stream payload exceeds the 16 MiB limit.");

            System.Text.Json.Nodes.JsonNode? eventNode;
            try { eventNode = System.Text.Json.Nodes.JsonNode.Parse(data, documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 }); }
            catch (System.Text.Json.JsonException)
            {
                throw new XPScriptRuntimeException(5, "XPAi stream contains malformed JSON.");
            }
            XPScriptNativeJson.ValidateBudget(eventNode);
            var chunk = ExtractStreamText(eventNode);
            if (chunk.Length > 0)
            {
                if (CollectStreamedResponse) complete.Append(chunk);
                InvokeCallback(callbackName, chunk);
            }
            var eventModel = ExtractModel(eventNode);
            if (eventModel.Length > 0) model = eventModel;
            usage = ExtractUsage(eventNode) ?? usage;
        }
    }

    private static XPScriptAiResponse CreateResponse(
        System.Net.Http.HttpResponseMessage response,
        System.Text.Json.Nodes.JsonNode raw,
        string text,
        string model,
        System.Text.Json.Nodes.JsonObject? usage)
    {
        return new XPScriptAiResponse(
            (int)response.StatusCode,
            response.IsSuccessStatusCode,
            model,
            text,
            new XPScriptJsonDocument(raw.DeepClone()),
            new XPScriptJsonDocument(usage?.DeepClone() ?? new System.Text.Json.Nodes.JsonObject()));
    }

    private static string ExtractText(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is not System.Text.Json.Nodes.JsonObject root) return string.Empty;
        if (TryString(root["output_text"], out var outputText)) return outputText;
        if (root["choices"] is not System.Text.Json.Nodes.JsonArray choices || choices.Count == 0 ||
            choices[0] is not System.Text.Json.Nodes.JsonObject choice) return string.Empty;
        if (choice["message"] is System.Text.Json.Nodes.JsonObject message)
        {
            if (TryString(message["content"], out var content)) return content;
            if (message["content"] is System.Text.Json.Nodes.JsonArray parts)
            {
                var joined = new StringBuilder();
                foreach (var part in parts)
                    if (part is System.Text.Json.Nodes.JsonObject item && TryString(item["text"], out var text)) joined.Append(text);
                return joined.ToString();
            }
        }
        return TryString(choice["text"], out var legacyText) ? legacyText : string.Empty;
    }

    private static string ExtractStreamText(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is not System.Text.Json.Nodes.JsonObject root ||
            root["choices"] is not System.Text.Json.Nodes.JsonArray choices || choices.Count == 0 ||
            choices[0] is not System.Text.Json.Nodes.JsonObject choice) return string.Empty;
        if (choice["delta"] is System.Text.Json.Nodes.JsonObject delta && TryString(delta["content"], out var content))
            return content;
        return TryString(choice["text"], out var text) ? text : string.Empty;
    }

    private static string ExtractModel(System.Text.Json.Nodes.JsonNode? node)
        => node is System.Text.Json.Nodes.JsonObject root && TryString(root["model"], out var model) ? model : string.Empty;

    private static System.Text.Json.Nodes.JsonObject? ExtractUsage(System.Text.Json.Nodes.JsonNode? node)
        => node is System.Text.Json.Nodes.JsonObject root && root["usage"] is System.Text.Json.Nodes.JsonObject usage
            ? (System.Text.Json.Nodes.JsonObject)usage.DeepClone()
            : null;

    private static bool TryString(System.Text.Json.Nodes.JsonNode? node, out string value)
    {
        if (node is System.Text.Json.Nodes.JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string ReadBoundedBody(System.Net.Http.HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long declared && declared > MaxResponseBytes)
            throw new XPScriptRuntimeException(5, "XPAi response exceeds the 16 MiB limit.");
        using var stream = content.ReadAsStream(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[16 * 1024];
        var total = 0;
        while (true)
        {
            var read = stream.Read(bytes, 0, bytes.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0) break;
            total = checked(total + read);
            if (total > MaxResponseBytes)
                throw new XPScriptRuntimeException(5, "XPAi response exceeds the 16 MiB limit.");
            buffer.Write(bytes, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void InvokeCallback(string callbackName, string chunk)
    {
        var scriptType = typeof(XPScriptAi).Assembly.GetType("Script", throwOnError: false, ignoreCase: false);
        var method = scriptType?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .FirstOrDefault(candidate => candidate.Name.Equals(callbackName, StringComparison.OrdinalIgnoreCase) &&
                                         candidate.GetParameters().Length == 1 &&
                                         candidate.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(string)));
        if (method is null)
            throw new XPScriptRuntimeException(5, "XPAi stream callback was not found or must accept one ByVal String parameter.");
        try { method.Invoke(null, [chunk]); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is XPScriptRuntimeException runtime)
        {
            throw runtime;
        }
        catch (Exception)
        {
            throw new XPScriptRuntimeException(5, "XPAi stream callback failed.");
        }
    }

    private CancellationTokenSource BeginRequest()
    {
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_activeRequest is not null)
                throw new XPScriptRuntimeException(5, "XPAi only allows one active request per instance.");
            _activeRequest = new CancellationTokenSource(_timeout);
            return _activeRequest;
        }
    }

    private void EndRequest(CancellationTokenSource request)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_activeRequest, request)) _activeRequest = null;
        }
        request.Dispose();
    }

    private Uri ResolveEndpoint()
    {
        if (_endpointPath.Length == 0) return _endpoint;
        if (Uri.TryCreate(_endpointPath, UriKind.Absolute, out _))
            throw new XPScriptRuntimeException(5, "XPAi EndpointPath must be relative.");
        var origin = new Uri(_endpoint.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
        var resolved = new Uri(origin, _endpointPath.TrimStart('/'));
        return ValidateEndpoint(resolved.ToString());
    }

    private static (Uri Endpoint, string Provider, bool UseBearerAuthentication) ResolvePreset(object? endpointOrPreset, object? providerConfiguration)
    {
        var value = XPScriptRuntime.CStr(endpointOrPreset).Trim();
        switch (value.ToLowerInvariant())
        {
            case "openai":
                RequireNoProviderConfiguration(providerConfiguration, "OpenAI");
                return (new Uri("https://api.openai.com/v1/chat/completions"), "OpenAI", true);
            case "claude":
            case "anthropic":
                RequireNoProviderConfiguration(providerConfiguration, "Claude");
                return (new Uri("https://api.anthropic.com/v1/chat/completions"), "Claude", true);
            case "openrouter":
                RequireNoProviderConfiguration(providerConfiguration, "OpenRouter");
                return (new Uri("https://openrouter.ai/api/v1/chat/completions"), "OpenRouter", true);
            case "azure":
            case "azureopenai":
                var resource = XPScriptRuntime.CStr(providerConfiguration).Trim();
                if (resource.Length == 0 || resource.Length > 63 ||
                    resource[0] == '-' || resource[^1] == '-' ||
                    resource.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
                    throw new XPScriptRuntimeException(5, "XPAi Azure preset requires a valid Azure OpenAI resource name.");
                return (new Uri($"https://{resource}.openai.azure.com/openai/v1/chat/completions"), "Azure", false);
            default:
                if (providerConfiguration is not null && !XPScriptNullRuntime.IsNull(providerConfiguration) &&
                    XPScriptRuntime.CStr(providerConfiguration).Trim().Length > 0)
                    throw new XPScriptRuntimeException(5, "XPAi provider configuration is only supported by presets that require it.");
                return (ValidateEndpoint(value), "Custom", true);
        }
    }

    private static void RequireNoProviderConfiguration(object? value, string provider)
    {
        if (value is not null && !XPScriptNullRuntime.IsNull(value) && XPScriptRuntime.CStr(value).Trim().Length > 0)
            throw new XPScriptRuntimeException(5, $"XPAi {provider} preset does not accept provider configuration.");
    }

    private static Uri ValidateEndpoint(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0 || text.Length > 8192 || text.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            !Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new XPScriptRuntimeException(5, "XPAi endpoint must be an absolute HTTP or HTTPS URL without credentials or a fragment.");
        return uri;
    }

    private static string ValidateJsonPropertyName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length == 0 || name.Length > 256 || name.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "XPAi option name is invalid.");
        return name;
    }

    private static string ValidateHeaderName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length == 0 || name.Length > 256 || name.Any(c => !(char.IsLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c))))
            throw new XPScriptRuntimeException(5, "XPAi header name is invalid.");
        return name;
    }

    private static void ValidateHeaderValue(string value)
    {
        if (value.Length > 64 * 1024 || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "XPAi header value is invalid.");
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        for (var i = 1; i < value.Length; i++)
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_')) return false;
        return true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new XPScriptRuntimeException(5, "XPAi has been disposed.");
    }
}

internal sealed class XPScriptAiResponse
{
    internal XPScriptAiResponse(
        int statusCode,
        bool isSuccess,
        string model,
        string text,
        XPScriptJsonDocument rawJson,
        XPScriptJsonDocument usage)
    {
        StatusCode = statusCode;
        IsSuccess = isSuccess;
        Model = model;
        Text = text;
        RawJson = rawJson;
        Usage = usage;
    }

    public int StatusCode { get; }
    public bool IsSuccess { get; }
    public string Model { get; }
    public string Text { get; }
    public string Content => Text;
    public XPScriptJsonDocument RawJson { get; }
    public XPScriptJsonDocument Usage { get; }
}
""";
}
