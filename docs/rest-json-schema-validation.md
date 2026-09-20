# REST request JSON Schema validation

XPScript REST routes can validate a JSON request body with the same `XPJsonSchema` runtime used by application code and UIForm validation.

## Route metadata

Add `[JsonSchema:relative/path.schema.json]` to a REST route:

```vb
[Anonymous]
[Post]
[Route:/api/users]
[JsonSchema:schemas/create-user.schema.json]
Sub CreateUser([FromBody] payload As CreateUserRequest)
    Response.OK(payload)
End Sub
```

The schema path is relative to the web root. Absolute paths, parent-directory traversal, control characters, and non-`.json` paths are rejected by route metadata validation. Resolution also rejects file or directory symbolic links/reparse points whose resolved target escapes the web root. The same hardened resolver is used by API documentation generation and runtime request validation.

Validation runs before REST binding and before the route procedure executes.

## Response behavior

A syntactically invalid JSON request or a request that does not satisfy the configured schema returns HTTP 400 with `application/problem+json`.

Schema constraint failures expose two representations:

- `errors` is the field/path-oriented map intended for existing consumers.
- `validationErrors` preserves the structured `XPJsonValidationResult` entries with `path`, `schemaPath`, `keyword`, `message`, `expected`, and `actual`.

Example:

```json
{
  "type": "about:blank",
  "title": "JSON Schema validation failed",
  "status": 400,
  "detail": "The request body does not satisfy the route XPJsonSchema.",
  "errors": {
    "$.age": ["Value is below the minimum."]
  },
  "validationErrors": [
    {
      "path": "$.age",
      "schemaPath": "$.properties.age.minimum",
      "keyword": "minimum",
      "message": "Value is below the minimum.",
      "expected": ">= 18",
      "actual": "12"
    }
  ]
}
```

Consumers should use `validationErrors` when they need schema keywords or expected/actual values, and `errors` when they only need messages grouped by JSON path.

## OpenAPI documentation

When API documentation is enabled, a route with `[JsonSchema:...]` embeds that JSON Schema directly into the OpenAPI 3.1 `requestBody` schema. The generated `apidoc/openapi.json` is therefore self-contained and does not depend on the original schema file at consumption time.

Routes without `[JsonSchema:...]` keep the existing typed request-body schema generation based on the XPscript parameter type. `[JsonSchema:...]` is the authoritative request schema when it is present.

API documentation generation validates the configured schema path and requires the schema file to exist and contain valid JSON whose root is an object or boolean schema. Invalid paths, symlink escapes, missing files, malformed JSON, and unsupported schema root shapes fail API documentation generation instead of emitting a broken OpenAPI document.

Swagger 2.0 generation retains its existing type-derived behavior.

## Server configuration errors

The schema file is server configuration, not client input. A missing configured schema, an invalid schema document, or an unexpected schema-runtime failure is therefore a server error and is not converted into a request-validation 400 response.

Malformed request JSON remains a client error (400).

## Execution guarantee

When request JSON parsing or schema validation fails, the route procedure is not executed. Persistence and other side effects should therefore be placed inside the route procedure, after this validation gate.

## Relationship to UIForm

The intended validation flow is:

```text
UIForm -> XPJsonDocument -> XPJsonSchema -> HTTP
       -> REST route -> same XPJsonSchema -> binding/persistence
```

This allows client-side validation to improve UX while the REST endpoint remains the authoritative validation boundary.
