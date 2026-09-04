# XPAi OpenAI-compatible client

`XPAi` connects XPScript applications to services that implement an OpenAI-compatible chat-completions HTTP API. The endpoint is supplied by the application. The class does not hard-code a provider or provider hostname.

`XPAi` is available in normal applications and server-side web targets. It is intentionally unavailable in browser WebAssembly because browser code cannot keep API credentials private.

## Basic request

```xpscript
Sub Main()
    Dim ai As New XPAi("https://api.example.com/v1/chat/completions", Environ("AI_API_KEY"))
    Dim response As XPAiResponse

    ai.Model = "example-model"
    ai.Temperature = 0.2
    ai.MaxOutputTokens = 500
    ai.SystemPrompt = "Answer clearly and briefly."
    ai.UserPrompt = "What is privacy by design?"

    Set response = ai.Complete()
    Print response.Text
End Sub
```

Keep API keys outside source files. Read them from environment variables, a protected service configuration store or a secrets manager.

## Constructor and endpoint

```xpscript
Dim ai As New XPAi(endpoint)
Dim ai As New XPAi(endpoint, apiKey)
Dim ai As New XPAi(preset, apiKey [, providerConfiguration])
```

`endpoint` must be an absolute HTTP or HTTPS URL. Credentials and URL fragments are rejected. `apiKey` is optional, which allows local providers and providers that use a custom authentication header.

When an API key is supplied, `XPAi` adds `Authorization: Bearer <key>`. An explicit `Authorization` header set with `SetHeader` takes precedence.

## Provider presets

Presets select the official OpenAI-compatible chat-completions endpoint and authentication style. Preset names are case-insensitive.

| Preset | Constructor | Endpoint | Authentication |
|---|---|---|---|
| OpenAI | `New XPAi("openai", apiKey)` | `https://api.openai.com/v1/chat/completions` | Bearer token |
| Claude | `New XPAi("claude", apiKey)` | `https://api.anthropic.com/v1/chat/completions` | Bearer token |
| OpenRouter | `New XPAi("openrouter", apiKey)` | `https://openrouter.ai/api/v1/chat/completions` | Bearer token |
| Azure OpenAI | `New XPAi("azure", apiKey, resourceName)` | `https://{resourceName}.openai.azure.com/openai/v1/chat/completions` | `api-key` header |

`anthropic` is accepted as an alias for `claude`. `azureopenai` is accepted as an alias for `azure`.

OpenAI:

```xpscript
Dim ai As New XPAi("openai", Environ("OPENAI_API_KEY"))
ai.Model = "your-openai-model"
```

Claude through Anthropic's OpenAI compatibility layer:

```xpscript
Dim ai As New XPAi("claude", Environ("ANTHROPIC_API_KEY"))
ai.Model = "your-claude-model"
```

Anthropic describes this compatibility layer as suitable for evaluating OpenAI-style integrations. Native Claude-only capabilities can require the native Anthropic API and are outside the common `XPAi` schema.

OpenRouter:

```xpscript
Dim ai As New XPAi("openrouter", Environ("OPENROUTER_API_KEY"))
ai.Model = "provider/model-name"
Call ai.SetHeader("HTTP-Referer", "https://application.example.com")
Call ai.SetHeader("X-OpenRouter-Title", "My application")
```

The two OpenRouter attribution headers are optional.

Azure OpenAI v1:

```xpscript
Dim ai As New XPAi("azure", Environ("AZURE_OPENAI_API_KEY"), "my-resource")
ai.Model = "my-deployment-name"
```

The third argument is the Azure OpenAI resource name, not a full URL. The Azure v1 endpoint does not require a dated `api-version` query parameter. Use a custom full endpoint instead of the preset when an installation still requires a legacy deployment URL.

`Provider` returns `OpenAI`, `Claude`, `OpenRouter`, `Azure` or `Custom`. `Endpoint` returns the resolved request URL.

`EndpointPath` replaces the path below the configured endpoint origin. This supports providers whose OpenAI-compatible route is selected at runtime:

```xpscript
Dim ai As New XPAi("https://gateway.example.com")
ai.EndpointPath = "/tenant-a/v1/chat/completions"
```

The path must remain relative to the configured origin. It cannot redirect requests to another host.

## Prompt parts

For the common two-part prompt shape, use `SystemPrompt` and `UserPrompt`:

```xpscript
ai.SystemPrompt = "Return concise structured data."
ai.UserPrompt = "Summarize this customer record."
Set response = ai.Complete()
```

`SystemPrompt` is inserted as the first `system` message. `UserPrompt` is appended as the final `user` message. Any messages added with `AddMessage` remain between them.

The helper methods are:

```xpscript
Call ai.SetPrompt("Return concise structured data.", "Summarize this customer record.")
Call ai.ClearPrompt()
```

A request can use only prompt properties; `AddMessage` is not required when at least one prompt part is set.

## Messages

The internal message collection supports `system`, `user` and `assistant` roles:

```xpscript
Call ai.AddMessage("system", "Use Swedish.")
Call ai.AddMessage("user", "Summarize the supplied text.")
Set response = ai.Complete()
Call ai.ClearMessages()
```

`GetMessages()` returns a cloned `JsonDocument`. A request can instead receive a `JsonArray` or a `JsonDocument` with an array root:

```xpscript
Dim messages As New JsonArray
Dim message As New JsonObject

Call message.Set("role", "user")
Call message.Set("content", "Hello")
Call messages.Add(message)
Set response = ai.Complete(messages)
```

Pass a model as the second argument to override the client model for one request:

```xpscript
Set response = ai.Complete(messages, "another-model")
```

## Structured JSON results

The preferred API is to define the desired result shape as an XPscript class and pass an instance to `SetResultClass`:

```xpscript
Class CustomerSummary
    Public Name As String
    Public RiskScore As Double
    Public Active As Boolean
End Class

Dim contract As CustomerSummary
Set contract = New CustomerSummary()

ai.JsonSchemaName = "customer_summary"
ai.JsonSchemaStrict = True
Call ai.SetResultClass(contract)
Set response = ai.Complete()
```

`XPAi` derives OpenAI-compatible JSON Schema from the class using the same visibility model as XPscript JSON serialization. Public fields and public readable properties are included. Private fields/properties and write-only properties are excluded. Nested classes are converted recursively. Recursive class graphs are rejected.

Included members are emitted as required properties. Object schemas disallow additional properties. Scalar mappings include strings, booleans, integer and floating-point numbers, dates (`string` with `date-time` format), enums, arrays and nested classes.

The overloads are:

```xpscript
Call ai.SetResultClass(contract)
Call ai.SetResultClass(contract, "customer_summary")
Call ai.SetResultClass(contract, "customer_summary", True)
```

For advanced/provider-specific schemas, raw JSON Schema is also supported:

```xpscript
Call ai.SetJsonSchema(schema)
Call ai.SetJsonSchema(schema, "result_name")
Call ai.SetJsonSchema(schema, "result_name", True)
```

`ResponseJsonSchema` gets a cloned configured schema or sets a raw schema. `HasJsonSchema` reports whether one is configured. `ClearJsonSchema()` removes it. `JsonSchemaName` defaults to `response`, and `JsonSchemaStrict` defaults to `True`.

The generated request uses OpenAI-compatible `response_format.type = "json_schema"`. Provider support depends on the configured endpoint implementing that request shape.

See [XPAi prompts and structured results](xpai-structured-output.md) for the detailed class-to-schema mapping and examples.

## Request options

`Temperature` accepts `0` through `2`. `MaxOutputTokens` accepts `1` through `1000000` and is sent as `max_tokens`.

Use `SetOption` for other provider-compatible JSON properties. Values use the native JSON conversion rules.

```xpscript
Call ai.SetOption("top_p", 0.9)
Call ai.RemoveOption("top_p")
Call ai.ClearOptions()
```

`model`, `messages`, `stream` and `response_format` cannot be supplied through `SetOption`. Use their dedicated APIs.

## Provider headers

```xpscript
Call ai.SetHeader("api-key", Environ("AZURE_AI_KEY"))
Call ai.SetHeader("HTTP-Referer", "https://application.example.com")
Call ai.RemoveHeader("HTTP-Referer")
Call ai.ClearHeaders()
```

Header names and values are validated. CR, LF and null characters are rejected. Callers cannot set `Host`, `Content-Length` or `Transfer-Encoding`.

## Response

`Complete` returns an `XPAiResponse`.

| Property | Description |
|---|---|
| `StatusCode` | HTTP response status code. |
| `IsSuccess` | `True` for an HTTP success status. |
| `Model` | Model returned by the provider. |
| `Text` | Assistant text extracted from the response. |
| `Content` | Alias for `Text`. |
| `RawJson` | Complete provider JSON as a `JsonDocument`. Unknown properties are preserved. |
| `Usage` | Provider usage object as a `JsonDocument`. |
| `HasJsonResult` | `True` when `Text` contains valid JSON within XPscript JSON limits. |
| `ResultJson` | Parses `Text` and returns a `JsonDocument`; raises an XPScript error if the text is not valid JSON. |

`ResultJson` parses the provider result but does not independently validate it against the configured JSON Schema. Schema enforcement is requested from the provider.

`ThrowOnHttpError` defaults to `True`. A non-success response then raises an error containing only the HTTP status code. Set it to `False` when the application needs to inspect a failed response object.

Provider response bodies, API keys and authorization headers are never copied into runtime error messages.

## Streaming

Streaming uses OpenAI-compatible server-sent events. The callback must be a module procedure that accepts one `ByVal String` parameter.

```xpscript
Dim collected As String

Sub OnChunk(ByVal text As String)
    collected = collected & text
    Print text
End Sub

Sub Main()
    Dim ai As New XPAi("https://api.example.com/v1/chat/completions", Environ("AI_API_KEY"))
    Dim response As XPAiResponse

    ai.Model = "example-model"
    Call ai.AddMessage("user", "Write one sentence.")
    Set response = ai.Stream("OnChunk")
    Print response.Text
End Sub
```

`CollectStreamedResponse` defaults to `True`. It controls whether `response.Text` contains all received chunks. The callback still receives each chunk when collection is disabled.

The overloads are:

```xpscript
Set response = ai.Stream("OnChunk")
Set response = ai.Stream(messages, "OnChunk")
Set response = ai.Stream(messages, "OnChunk", "request-model")
```

The parser handles `[DONE]`, rejects malformed JSON and enforces limits for lines, events and total payload size.

## Timeout and cancellation

`Timeout` is measured in seconds and defaults to 60. Values from `0.1` through `3600` are accepted.

```xpscript
ai.Timeout = 120
Call ai.Cancel()
```

`Cancel` stops the active HTTP request and closes an active response stream. One `XPAi` instance allows one active request. Use separate instances for concurrent requests.

## Supported provider patterns

| Provider pattern | Configuration |
|---|---|
| OpenAI-compatible endpoint | Full chat-completions URL plus bearer key. |
| Azure-compatible endpoint | Full deployment URL with API version plus an `api-key` header. |
| OpenRouter-compatible endpoint | Full endpoint, bearer key and optional provider headers. |
| Local model server | Local HTTP endpoint with no key, or custom headers when required. |

`XPAi` does not follow HTTP redirects. Applications that accept user-controlled endpoint URLs must apply an endpoint allowlist before creating the client.

## Resource limits

| Resource | Limit |
|---|---:|
| Request body | 8 MiB |
| Non-streaming response | 16 MiB |
| Streaming payload | 16 MiB |
| One SSE line | 1 MiB |
| SSE events | 100000 |
| Messages | 10000 |

See [xpai.xps](../samples/xpai.xps) for the general executable example and [xpai-structured-output.xps](../samples/xpai-structured-output.xps) for prompt/schema compilation coverage.

Provider endpoint references: [OpenAI Chat API](https://developers.openai.com/api/reference/resources/chat), [Claude OpenAI compatibility](https://platform.claude.com/docs/en/cli-sdks-libraries/libraries/openai-sdk), [OpenRouter quickstart](https://openrouter.ai/docs/quickstart) and [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle).

## Complete API reference

| Member | Behavior |
|---|---|
| `XPAi(endpoint [, apiKey])` | Creates a client for a custom full endpoint. The API key uses bearer authentication. |
| `XPAi(preset, apiKey [, providerConfiguration])` | Creates a client from a provider preset. Azure requires its resource name as provider configuration. |
| `Endpoint` | Returns the resolved request endpoint. |
| `Provider` | Returns the selected provider name. |
| `EndpointPath` | Replaces the path while keeping the configured origin. |
| `Model` | Gets or sets the default model. |
| `SystemPrompt` | Gets or sets the first system prompt message. |
| `UserPrompt` | Gets or sets the final user prompt message. |
| `SetPrompt(system, user)` | Sets both prompt parts. |
| `ClearPrompt()` | Clears both prompt parts. |
| `JsonSchemaName` | Gets or sets the structured result schema name. |
| `JsonSchemaStrict` | Gets or sets strict JSON Schema request mode. |
| `HasJsonSchema` | Reports whether structured output is configured. |
| `ResponseJsonSchema` | Gets a cloned configured schema or sets a raw schema. |
| `SetResultClass(contract [, name [, strict]])` | Derives structured-result JSON Schema from an XPscript class instance. |
| `SetJsonSchema(schema [, name [, strict]])` | Sets an explicit raw JSON Schema. |
| `ClearJsonSchema()` | Removes structured-output configuration. |
| `Temperature` | Gets or sets a value from 0 through 2. |
| `MaxOutputTokens` | Gets or sets the output token limit. |
| `Timeout` | Gets or sets the total request timeout in seconds. |
| `CollectStreamedResponse` | Controls whether streaming also builds `XPAiResponse.Text`. |
| `ThrowOnHttpError` | Controls whether non-success HTTP responses raise an XPScript error. |
| `AddMessage(role, content)` | Adds a system, user or assistant message. |
| `GetMessages()` | Returns a cloned message array as `JsonDocument`. |
| `ClearMessages()` | Removes all stored messages. |
| `SetOption(name, value)` | Adds or replaces an extra request JSON property except dedicated properties. |
| `RemoveOption(name)` | Removes one extra request property. |
| `ClearOptions()` | Removes all extra request properties. |
| `SetHeader(name, value)` | Adds or replaces a request header. |
| `RemoveHeader(name)` | Removes one request header. |
| `ClearHeaders()` | Removes all caller-defined headers. |
| `Complete([messages [, model]])` | Sends a non-streaming request. |
| `Stream([messages,] callback [, model])` | Sends an SSE request and invokes the callback for each text chunk. |
| `Cancel()` | Cancels the active request. |
| `Dispose()` | Cancels active work and releases HTTP resources. |
