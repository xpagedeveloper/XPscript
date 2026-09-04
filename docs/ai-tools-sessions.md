# XPAi tools and session memory

XPAi can register explicit AI-callable functions and can keep provider session state on one client instance. The model sees only the AI-visible function name and JSON schema. XPScript application code explicitly maps that function to a module callback, so a model cannot select an arbitrary XPScript procedure name.

For a runnable local demonstration, see [`demo/ai/ai-tool-demo.xps`](../demo/ai/ai-tool-demo.xps). It uses the repository's deterministic `tools/xpai_mock_server.py` fixture and does not require a real provider API key.

## Tool execution flow

1. Create an `AITool`.
2. Register one or more AI-visible functions with `AddFunction`.
3. Declare the JSON parameter schema with `AddParameter`.
4. Add the tool to an `XPAi` client with `AddTool`.
5. `Complete()` sends an OpenAI-compatible `tools` array.
6. A returned `tool_calls` entry is validated against the registered schema.
7. XPScript invokes only the pre-registered callback through the shared callback runtime.
8. The callback result is appended as a `role="tool"` message and XPAi continues the model request automatically.

Automatic tool execution currently applies to non-streaming `Complete()` calls. Streaming tool-call delta aggregation is a separate concern.

## `AITool(name)`

Creates a logical tool container.

**Parameters**

- `name`: required tool name. Names are case-insensitive for registry lookup and must satisfy the runtime identifier limits.

```xpscript
Dim weather As New AITool("weather")
```

## `AITool.Description`

Gets or sets the human-readable tool description.

```xpscript
weather.Description = "Weather information"
```

## `AITool.Timeout`

Gets or sets the tool timeout metadata in seconds. Accepted values are `0.1` through `3600`.

```xpscript
weather.Timeout = 15
```

## `AITool.AddFunction(name, description, callback [, callbackContext...])`

Registers one AI-visible function and binds it to an application callback.

**Parameters**

- `name`: function name sent to the AI provider.
- `description`: function description sent to the provider.
- `callback`: module `Sub` or `Function` name. Static callback names are checked by the compiler.
- `callbackContext...`: optional fixed values appended after the generated `AIToolCall` argument.

```xpscript
Call weather.AddFunction( _
    "get_weather", _
    "Returns weather for a city", _
    "HandleGetWeather", _
    "tenant-42")
```

A static callback registration fails at compile time when the callback does not exist or has the wrong number of parameters.

## Tool callback signature

The callback receives an `AIToolCall` object first, followed by the fixed context values supplied to `AddFunction`.

```xpscript
Function HandleGetWeather(call As Variant, tenant As String) As Variant
    Dim city As String
    city = CStr(call.Arguments.Get("city"))
    Print call.FunctionName & ":" & city & ":" & tenant
    HandleGetWeather = "weather:" & city
End Function
```

`AIToolCall` exposes:

- `ToolName`: owning tool name.
- `FunctionName`: AI-visible function name.
- `CallId`: provider tool-call identifier.
- `Arguments`: validated `XPJsonObject` arguments.
- `SessionId`: current XPAi session identifier, when available.

## `AITool.AddParameter(functionName, name, type, description, required)`

Adds one JSON-schema parameter to a registered function.

**Parameters**

- `functionName`: function registered with `AddFunction`.
- `name`: parameter name.
- `type`: `string`, `integer`, `number`, `boolean`, `object` or `array`.
- `description`: provider-visible parameter description.
- `required`: Boolean indicating whether the argument is mandatory.

```xpscript
Call weather.AddParameter("get_weather", "city", "string", "City name", True)
Call weather.AddParameter("get_weather", "units", "string", "metric or imperial", True)
```

Generated object schemas set `additionalProperties` to `false`. Tool arguments are validated before the callback is invoked.

## Function registry members

### `AITool.HasFunction(name)`

Returns `True` when the tool contains the function.

```xpscript
Print CStr(weather.HasFunction("get_weather"))
```

### `AITool.GetFunction(name)`

Returns the registered function object or raises an XPScript runtime error when it does not exist.

```xpscript
Dim fn As Variant
Set fn = weather.GetFunction("get_weather")
Print fn.Name
```

### `AITool.GetFunctionNames()`

Returns function names in deterministic registration order as a JSON array.

```xpscript
Dim names As Variant
Set names = weather.GetFunctionNames()
Print CStr(names.Get(0))
```

### `AITool.FunctionCount()`

Returns the number of functions in the tool.

```xpscript
Print CStr(weather.FunctionCount())
```

### `AITool.RemoveFunction(name)`

Removes a function and returns whether it existed.

```xpscript
Print CStr(weather.RemoveFunction("get_weather"))
```

The function object returned by `AddFunction` or `GetFunction` also supports `AddParameter`, `RemoveParameter`, `HasParameter` and `ParameterCount`.

## Tool request context

Request context is application-owned metadata. It is not allowed to mutate the parent XPAi endpoint, API key or request headers.

### `AITool.SetRequestProperty(name, value)`

Adds or replaces one context property.

```xpscript
Call weather.SetRequestProperty("tenant", "tenant-a")
```

### `AITool.RemoveRequestProperty(name)`

Removes one context property.

```xpscript
Call weather.RemoveRequestProperty("tenant")
```

### `AITool.ClearRequestProperties()`

Clears all tool context properties.

```xpscript
Call weather.ClearRequestProperties()
```

### `AITool.GetRequestContext()`

Returns a defensive `XPJsonObject` copy.

```xpscript
Dim context As Variant
Set context = weather.GetRequestContext()
Print CStr(context.Get("tenant"))
```

### `AITool.ToJson()` and `ToJsonObject()`

Serialize tool metadata for diagnostics or application inspection. They do not expose XPAi credentials.

## XPAi tool registry

### `XPAi.AddTool(tool)`

Registers an `AITool` on one XPAi client.

```xpscript
Call ai.AddTool(weather)
```

Function names share the provider's flat function namespace. Duplicate AI-visible function names are rejected.

### `XPAi.HasTool(name)`

Returns whether a tool is registered.

```xpscript
Print CStr(ai.HasTool("weather"))
```

### `XPAi.GetTool(name)`

Returns the registered tool.

```xpscript
Set weather = ai.GetTool("weather")
```

### `XPAi.GetToolNames()`

Returns tool names in registration order.

```xpscript
Set names = ai.GetToolNames()
```

### `XPAi.ToolCount()`

Returns the number of registered tools.

```xpscript
Print CStr(ai.ToolCount())
```

### `XPAi.RemoveTool(name)`

Removes a tool and returns whether it existed.

```xpscript
Print CStr(ai.RemoveTool("weather"))
```

### `XPAi.ClearTools()`

Removes all tools from the client.

```xpscript
Call ai.ClearTools()
```

### `XPAi.AutoExecuteTools`

Controls whether non-streaming responses with `tool_calls` are executed automatically. The default is `True`.

```xpscript
ai.AutoExecuteTools = True
```

### `XPAi.MaxToolIterations`

Bounds automatic tool/model continuation loops. Accepted values are `1` through `32`.

```xpscript
ai.MaxToolIterations = 4
```

## Session memory

A successful AI response may return `session_id`, `conversation_id`, `response_id` or `id`. XPAi stores the first supported identifier in `SessionId`.

The stored value is not sent automatically unless the application configures `SessionRequestProperty`. This avoids assuming a provider-specific continuation field.

### `XPAi.SessionId`

Returns the stored provider session identifier.

```xpscript
Print ai.SessionId
```

### `XPAi.HasSession`

Returns `True` when a session identifier is stored.

```xpscript
Print CStr(ai.HasSession)
```

### `XPAi.SessionRequestProperty`

Selects the outgoing JSON property that receives the stored session identifier on later requests. Use an empty string to capture session IDs without sending them.

```xpscript
ai.SessionRequestProperty = "previous_response_id"
```

### `XPAi.ResetSession()`

Clears only the stored provider session identifier. Messages and client configuration remain.

```xpscript
Call ai.ResetSession()
```

### `XPAi.NewRequest()`

Starts a fresh logical conversation. It clears messages and session state while preserving endpoint/provider configuration, model, timeout, headers, request options, registered tools and `SessionRequestProperty`.

```xpscript
Call ai.NewRequest()
Call ai.AddMessage("user", "Start fresh")
Set response = ai.Complete()
```

Session identifiers are accepted only from successful responses. Control characters are rejected and identifiers are limited to 4096 characters.

## Security boundary

AITool registration is an allowlist. The model can select only provider-visible function names that the application has registered. It never supplies an XPScript callback/procedure name. Callback invocation uses the shared callback runtime with identifier validation, exact arity matching, controlled argument conversion, sanitized failures and trimming protection.

Browser WebAssembly cannot use XPAi because browser code cannot keep provider credentials private. Browser UI callbacks that need AI must call an explicit server-side HTTP API.
