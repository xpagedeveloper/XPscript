# OpenAPI v2 implementation TODO

## Goal

XPScript shall be able to consume OpenAPI/Swagger descriptions and generate usable XPScript source in both directions:

1. **REST server skeleton** — generate a server application with endpoint skeleton functions for every operation described by the API.
2. **REST client** — generate a client that can connect to the described service, including the operations, request/response models, schemas, enums, authentication and other API surface required to use the service from XPScript.

The implementation must support both **JSON** and **YAML/YML** input.

## Supported specifications

- [ ] Swagger / OpenAPI 2.0 (Swagger 2.0)
- [ ] OpenAPI 3.0.x
- [ ] OpenAPI 3.1.x
- [ ] OpenAPI 3.2.x
- [ ] JSON input for every supported specification
- [ ] YAML/YML input for every supported specification
- [ ] Reject unsupported/invalid documents with actionable diagnostics
- [ ] Detect the specification version from the document rather than the file extension

## REST server generation

- [ ] Generate one server endpoint skeleton for every OpenAPI operation
- [ ] Preserve HTTP method and route/path semantics
- [ ] Generate path parameters
- [ ] Generate query parameters
- [ ] Generate header parameters where applicable
- [ ] Generate cookie parameters where applicable
- [ ] Generate request bodies and content types
- [ ] Generate response status codes and response models
- [ ] Generate reusable schema/model classes and enums
- [ ] Support references/components/definitions required by generated endpoints
- [ ] Support documented security/authentication declarations
- [ ] Generated server source must compile with the real XPScript compiler
- [ ] Generated server must be usable as a starting skeleton without hand-editing generated infrastructure

## REST client generation

- [ ] Generate one callable XPScript method for every API operation
- [ ] Generate all model/schema classes needed by the API
- [ ] Generate enums
- [ ] Generate path/query/header/cookie parameters
- [ ] Generate request bodies for documented content types
- [ ] Generate typed response handling for documented responses
- [ ] Support reusable schemas and references
- [ ] Support API key authentication
- [ ] Support HTTP authentication schemes described by supported specs
- [ ] Support OAuth/OpenID declarations to the extent required for constructing authenticated requests
- [ ] Preserve operation and schema names when they are legal XPScript identifiers
- [ ] Generated client source must compile with the real XPScript compiler/transpiler
- [ ] Generated client must be usable without hand-editing generated infrastructure

## API naming

- [ ] Explicit user supplied API/class name takes precedence
- [ ] Otherwise use a suitable API name from the document when available
- [ ] Otherwise derive the API name from the server domain
- [ ] Domain-derived names use readable form such as `example.com` / `api.example.com` -> `Example_API`
- [ ] Provide deterministic fallback naming when neither API metadata nor a server domain is available
- [ ] Multiple imported APIs can coexist without accidental class-name collisions

## XPScript identifier and scope correctness

The generator must only rename an OpenAPI identifier when XPScript has a real collision in the exact declaration scope.

- [ ] Runtime/global function names are not globally reserved for class members
- [ ] Properties such as `JsonParse` and `StrLeftBack` remain unchanged when legal in their class scope
- [ ] Methods with runtime/global function names remain unchanged when legal
- [ ] Method overloads with the same name but different valid XPScript signatures are preserved
- [ ] Case-insensitive collisions are detected in the same scope
- [ ] Type-name collisions are handled only in type declaration scope
- [ ] Member collisions are handled independently per class
- [ ] Procedure parameters are handled in procedure parameter scope
- [ ] Local/generated helper variables are handled in procedure-local scope
- [ ] OpenAPI-authored names win over generator-owned helper names when a true collision exists
- [ ] Generator-owned helper names are deterministic
- [ ] Compiler-owned `__*` identifiers remain protected
- [ ] XPScript lexical keywords remain protected
- [ ] Source-authored underscores are preserved
- [ ] Generated collision suffixes use deterministic names such as `Foo2`, `Foo3`

## Import/update/regeneration

- [ ] Initial generation follows all naming/scope rules
- [ ] Client generation follows all naming/scope rules
- [ ] Server generation follows all naming/scope rules
- [ ] Additive import follows all naming/scope rules
- [ ] Additive import preserves existing valid generated names
- [ ] Regeneration/update follows all naming/scope rules
- [ ] Generated ownership marker is stable and recognized by update
- [ ] Updating a generated client does not require manually editing the generated file

## Compatibility/regression cases

- [ ] ScopeCollision server model with `JsonParse` and `StrLeftBack`
- [ ] ScopeCollision output compiled by the real XPScript compiler
- [ ] Two classes may independently contain the same runtime-name property
- [ ] Runtime-name property and runtime-name method coexist where XPScript permits it
- [ ] Same method name with different parameter signatures is treated as a valid overload where XPScript permits it
- [ ] Case-insensitive true same-scope collisions are deterministically disambiguated
- [ ] Parameter/member overlap is tested
- [ ] Generated helper collision is tested
- [ ] Multiple security schemes normalizing to the same identifier are tested
- [ ] Additive import collision behavior is tested
- [ ] Regeneration collision behavior is tested
- [ ] Lexical keyword behavior is tested
- [ ] `__*` compiler-reserved behavior is tested

## CI acceptance

- [ ] OpenAPI smoke tests run early enough on Linux to provide fast failure feedback
- [ ] Swagger/OpenAPI 2.0 JSON server generation + compile test
- [ ] Swagger/OpenAPI 2.0 YAML server generation + compile test
- [ ] Swagger/OpenAPI 2.0 JSON client generation + compile test
- [ ] Swagger/OpenAPI 2.0 YAML client generation + compile test
- [ ] OpenAPI 3.0 JSON server generation + compile test
- [ ] OpenAPI 3.0 YAML server generation + compile test
- [ ] OpenAPI 3.0 JSON client generation + compile test
- [ ] OpenAPI 3.0 YAML client generation + compile test
- [ ] OpenAPI 3.1 JSON server generation + compile test
- [ ] OpenAPI 3.1 YAML server generation + compile test
- [ ] OpenAPI 3.1 JSON client generation + compile test
- [ ] OpenAPI 3.1 YAML client generation + compile test
- [ ] OpenAPI 3.2 JSON server generation + compile test
- [ ] OpenAPI 3.2 YAML server generation + compile test
- [ ] OpenAPI 3.2 JSON client generation + compile test
- [ ] OpenAPI 3.2 YAML client generation + compile test
- [ ] FullTest green on Windows
- [ ] FullTest green on Linux
- [ ] FullTest green on macOS

## Completion rule

Do not merge `openapi-client-v2` / PR #582 to `main` until the applicable items above are implemented and verified. A feature is not considered complete merely because source generation succeeds: generated server/client source must also pass the real XPScript compilation path.
