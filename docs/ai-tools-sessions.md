# XPAi tools and session memory

XPAi can keep provider session state on a client instance and can register isolated AI tools without giving those tools control over endpoint, authentication or request headers.

## Session memory

A successful AI response may return a provider identifier such as `session_id`, `conversation_id`, `response_id` or `id`. XPAi stores the first supported identifier it finds in `SessionId`.

The stored value is not sent automatically unless the application configures `SessionRequestProperty`. This keeps generic OpenAI-compatible endpoints free from provider-specific request fields.

```xpscript
Dim ai As New XPAi("https://api.example.com/v1/responses", Environ("AI_API_KEY"))
Dim response As XPAiResponse

ai.Model = "example-model"
ai.SessionRequestProperty = "previous_response_id"

Call ai.AddMessage("user", "Start a conversation")
Set response = ai.Complete()
Print ai.SessionId

Call ai.AddMessage("user", "Continue")
Set response = ai.Complete()
```

`HasSession` is `True` when a session identifier is stored.

`ResetSession()` clears only the stored provider session identifier. Stored messages and the rest of the client configuration remain unchanged.

`NewRequest()` starts a fresh logical request on the same XPAi object. It clears stored messages and the provider session identifier while preserving endpoint, provider configuration, model, timeout, headers, request options, registered tools and `SessionRequestProperty`.

```xpscript
Call ai.NewRequest()
Call ai.AddMessage("user", "Start fresh")
Set response = ai.Complete()
```

Session identifiers are accepted only from successful responses. Control characters are rejected and identifiers are bounded to 4096 characters.

## Provider compatibility

There is no universal continuation property across all OpenAI-compatible providers. For example, a provider may return an `id` but not accept that value as `previous_response_id` on a chat-completions request.

For that reason applications must configure the outgoing property explicitly:

```xpscript
ai.SessionRequestProperty = "previous_response_id"
```

Set it to an empty string to keep capturing `SessionId` without sending the value on later requests.

## AITool registry

`AITool` provides the first layer of the optional XPAi tool architecture.

```xpscript
Dim ai As New XPAi(endpoint, apiKey)
Dim retrieval As New AITool("retrieval")

retrieval.Timeout = 15
Call retrieval.SetRequestProperty("tenant", "tenant-a")
Call ai.AddTool(retrieval)

Print ai.ToolCount()
Print ai.HasTool("retrieval")
```

Tool names are unique case-insensitively. Registration order is preserved deterministically.

Available registry operations:

```xpscript
Call ai.AddTool(tool)
Print ai.HasTool("retrieval")
Set tool = ai.GetTool("retrieval")
Set names = ai.GetToolNames()
Print ai.RemoveTool("retrieval")
Call ai.ClearTools()
```

`GetRequestContext()` returns a defensive JSON copy of a tool's request context.

An `AITool` has no reference back to XPAi and therefore cannot mutate the client's endpoint, API key, provider headers or other transport configuration through the tool API.

This foundation does not yet execute MCP tools, perform RAG retrieval or embeddings, or automatically merge tool context into the provider request. Those capabilities build on this registry in later steps.
