# XPAi prompts and structured results

`XPAi` supports first-class system/user prompt parts and structured JSON output contracts.

The normal AI endpoint configuration is unchanged. Use the existing `XPAi(endpoint [, apiKey])`, provider presets and `EndpointPath` APIs. There is no separate gateway endpoint property.

## Prompt properties

Use `SystemPrompt` and `UserPrompt` when an application has one stable system instruction and one user request:

```xpscript
Dim ai As New XPAi("https://api.example.com/v1/chat/completions", Environ("AI_API_KEY"))
ai.Model = "example-model"
ai.SystemPrompt = "Return concise structured data."
ai.UserPrompt = "Summarize this customer record."
```

Equivalent helper methods are available:

```xpscript
Call ai.SetPrompt("Return concise structured data.", "Summarize this customer record.")
Call ai.ClearPrompt()
```

When a request is built, `SystemPrompt` is inserted as the first `system` message and `UserPrompt` is appended as the final `user` message. Existing messages added with `AddMessage` remain between those prompt parts.

A request may consist only of `SystemPrompt` and/or `UserPrompt`; `AddMessage` is not required in that case.

## XPscript class result contracts

The preferred structured-output API is `SetResultClass`.

Define the shape you want as a normal XPscript class:

```xpscript
Class CustomerSummary
    Public Name As String
    Public RiskScore As Double
    Public Active As Boolean
End Class
```

Pass an instance of the class to `XPAi`:

```xpscript
Dim contract As CustomerSummary
Set contract = New CustomerSummary()

ai.JsonSchemaName = "customer_summary"
ai.JsonSchemaStrict = True
Call ai.SetResultClass(contract)
```

`XPAi` converts the public JSON-visible surface of the class to JSON Schema. The same visibility model used by XPscript JSON serialization applies:

- public fields are included
- public readable properties are included
- private fields and properties are excluded
- write-only properties are excluded
- nested public classes are converted recursively
- recursive class graphs are rejected

All included members are emitted as required properties. Strict object schemas set `additionalProperties` to `false`.

The supported automatic mappings include:

| XPscript/CLR type | JSON Schema |
|---|---|
| `String`, `Char` | `string` |
| `Boolean` | `boolean` |
| integer numeric types | `integer` |
| `Single`, `Double`, `Currency`/decimal | `number` |
| `Date` | `string` with `date-time` format |
| Enum | string enum |
| CLR arrays | `array` with recursively derived `items` |
| XPscript dynamic array (`LSArray`) | `array` with unconstrained items |
| `XPJsonObject`, `XPJsonDocument`, `Variant/Object` | object |
| `XPJsonArray` | array |
| XPscript class | object with public fields/properties |

The overloads are:

```xpscript
Call ai.SetResultClass(contract)
Call ai.SetResultClass(contract, "customer_summary")
Call ai.SetResultClass(contract, "customer_summary", True)
```

The optional name is the JSON Schema name. The optional strict value overrides `JsonSchemaStrict` for that call.

## Raw JSON Schema

Use `SetJsonSchema` when a provider requires schema details that cannot be represented by an XPscript class:

```xpscript
Call ai.SetJsonSchema(schema)
Call ai.SetJsonSchema(schema, "result_name")
Call ai.SetJsonSchema(schema, "result_name", True)
```

`schema` must be a `XPJsonObject` or `XPJsonDocument` with an object root.

Related properties:

| Member | Description |
|---|---|
| `JsonSchemaName` | Schema name. Defaults to `response`. |
| `JsonSchemaStrict` | Whether the provider JSON Schema request uses strict mode. Defaults to `True`. |
| `HasJsonSchema` | `True` when a result schema is configured. |
| `ResponseJsonSchema` | Gets a cloned configured schema, or sets a raw schema. |
| `ClearJsonSchema()` | Removes the structured-output requirement. |

`response_format` is owned by these dedicated APIs and cannot be set through `SetOption`.

The generated OpenAI-compatible request uses:

```json
{
  "response_format": {
    "type": "json_schema",
    "json_schema": {
      "name": "customer_summary",
      "strict": true,
      "schema": {}
    }
  }
}
```

Provider compatibility still depends on the target endpoint supporting OpenAI-compatible `json_schema` response formatting.

## Reading structured responses

`XPAiResponse.Text` remains the provider's assistant text.

For structured output, `XPAiResponse` also exposes:

| Member | Description |
|---|---|
| `HasJsonResult` | Returns `True` when `Text` parses as valid JSON within XPscript JSON limits. |
| `ResultJson` | Parses `Text` and returns it as `XPJsonDocument`; raises an XPscript error when the response text is not valid JSON. |

Example:

```xpscript
Dim response As XPAiResponse
Dim result As XPJsonDocument

Set response = ai.Complete()
If response.HasJsonResult Then
    Set result = response.ResultJson
    Print CStr(result.Root.AsObject().Get("name"))
End If
```

`ResultJson` parses the returned JSON. It does not independently validate the response against the configured schema; schema enforcement is requested from the AI endpoint through `response_format`.

## Complete example

See [`../samples/xpai-structured-output.xps`](../samples/xpai-structured-output.xps).
