# OpenAPI v2 implementation TODO

## Goal

XPScript shall be able to consume OpenAPI descriptions and generate usable XPScript source in both directions:

1. **REST server skeleton** — generate a server application with endpoint skeleton functions for every operation described by the API.
2. **REST client** — generate a client that can connect to the described service, including the operations, request/response models, schemas, enums, authentication and other API surface required to use the service from XPScript.

The implementation must support OpenAPI **3.0, 3.1 and 3.2** in both **JSON** and **YAML/YML** input. Swagger/OpenAPI 2.0 is explicitly out of scope for this workstream and can be evaluated separately later.

## Supported specifications

- [x] OpenAPI 3.0.x
- [x] OpenAPI 3.1.x
- [x] OpenAPI 3.2.x
- [x] JSON input for every supported specification
- [x] YAML/YML input for every supported specification
- [ ] Reject unsupported/invalid documents with actionable diagnostics
- [x] Detect the specification version from the document rather than the file extension

## Specification normalization layer

OpenAPI 3.0, 3.1 and 3.2 should share a common internal representation before server/client emission where version-specific semantics differ.

- [x] Detect the OpenAPI specification version from the document content
- [ ] Normalize OpenAPI 3.0/3.1/3.2 components, parameters, request bodies, responses and security into a common internal model where needed
- [ ] Handle version-specific JSON Schema dialect differences deliberately instead of silently treating all versions as 3.1
- [x] Add a fixture that proves version detection is content-based even when the file extension is misleading
- [x] Add negative tests for unsupported OpenAPI versions

## REST server generation

- [x] Generate one server endpoint skeleton for every OpenAPI operation
- [x] Preserve HTTP method and route/path semantics
- [x] Generate path parameters
- [x] Generate query parameters
- [x] Generate header parameters where applicable
- [ ] Generate cookie parameters where applicable
- [x] Generate request bodies and content types
- [x] Generate response status codes and response models
- [x] Generate reusable schema/model classes and enums
- [x] Support references/components/definitions required by generated endpoints
- [ ] Support documented security/authentication declarations
- [x] Generated server source must compile with the real XPScript compiler
- [ ] Generated server must be usable as a starting skeleton without hand-editing generated infrastructure

## REST client generation

- [x] Generate one callable XPScript method for every API operation
- [x] Generate all model/schema classes needed by the API
- [x] Generate enums
- [x] Generate path/query/header/cookie parameters
- [x] Generate request bodies for documented content types
- [x] Generate typed response handling for documented responses
- [x] Support reusable schemas and references
- [x] Support API key authentication
- [x] Support HTTP authentication schemes described by supported specs
- [ ] Support OAuth/OpenID declarations to the extent required for constructing authenticated requests
- [x] Preserve operation and schema names when they are legal XPScript identifiers
- [x] Generated client source must compile with the real XPScript compiler/transpiler
- [ ] Generated client must be usable without hand-editing generated infrastructure

## API naming

- [x] Explicit user supplied API/class name takes precedence
- [x] Otherwise use a suitable API name from the document when available
- [x] Otherwise derive the API name from the server domain
- [x] Domain-derived names use readable form such as `example.com` / `api.example.com` -> `Example_API`
- [x] Provide deterministic fallback naming when neither API metadata nor a server domain is available
- [ ] Multiple imported APIs can coexist without accidental class-name collisions

## XPScript identifier and scope correctness

The generator must only rename an OpenAPI identifier when XPScript has a real collision in the exact declaration scope.

- [x] Runtime/global function names are not globally reserved for class members
- [x] Properties such as `JsonParse` and `StrLeftBack` remain unchanged when legal in their class scope
- [x] Methods with runtime/global function names remain unchanged when legal
- [x] Method overloads with the same name but different valid XPScript signatures are preserved
- [x] Case-insensitive collisions are detected in the same scope
- [ ] Type-name collisions are handled only in type declaration scope
- [x] Member collisions are handled independently per class
- [ ] Procedure parameters are handled in procedure parameter scope
- [ ] Local/generated helper variables are handled in procedure-local scope
- [x] OpenAPI-authored names win over generator-owned helper names when a true collision exists
- [x] Generator-owned helper names are deterministic
- [x] Compiler-owned `__*` identifiers remain protected
- [x] XPScript lexical keywords remain protected
- [x] Source-authored underscores are preserved
- [x] Generated collision suffixes use deterministic names such as `Foo2`, `Foo3`

## Import/update/regeneration

- [x] Initial generation follows all naming/scope rules
- [x] Client generation follows all naming/scope rules
- [x] Server generation follows all naming/scope rules
- [ ] Additive import follows all naming/scope rules
- [x] Additive import preserves existing valid generated names
- [ ] Regeneration/update follows all naming/scope rules
- [x] Generated ownership marker is stable and recognized by update
- [x] Updating a generated client does not require manually editing the generated file

## Compatibility/regression cases

- [x] ScopeCollision server model with `JsonParse` and `StrLeftBack`
- [x] ScopeCollision output compiled by the real XPScript compiler
- [x] Two classes may independently contain the same runtime-name property
- [ ] Runtime-name property and runtime-name method coexist where XPScript permits it
- [x] Same method name with different parameter signatures is treated as a valid overload where XPScript permits it
- [x] Case-insensitive true same-scope collisions are deterministically disambiguated
- [ ] Parameter/member overlap is tested
- [x] Generated helper collision is tested
- [x] Multiple security schemes normalizing to the same identifier are tested
- [ ] Additive import collision behavior is tested
- [ ] Regeneration collision behavior is tested
- [x] Lexical keyword behavior is tested
- [x] `__*` compiler-reserved behavior is tested

## CI acceptance

- [x] OpenAPI smoke tests run early enough on Linux to provide fast failure feedback
- [x] OpenAPI 3.0 JSON server generation + compile test
- [x] OpenAPI 3.0 YAML server generation + compile test
- [x] OpenAPI 3.0 JSON client generation + compile test
- [x] OpenAPI 3.0 YAML client generation + compile test
- [x] OpenAPI 3.1 JSON server generation + compile test
- [x] OpenAPI 3.1 YAML server generation + compile test
- [x] OpenAPI 3.1 JSON client generation + compile test
- [x] OpenAPI 3.1 YAML client generation + compile test
- [x] OpenAPI 3.2 JSON server generation + compile test
- [x] OpenAPI 3.2 YAML server generation + compile test
- [x] OpenAPI 3.2 JSON client generation + compile test
- [x] OpenAPI 3.2 YAML client generation + compile test
- [x] FullTest green on Windows
- [x] FullTest green on Linux
- [x] FullTest green on macOS

## Completion rule

Do not merge `openapi-client-v2` / PR #582 to `main` until the applicable items above are implemented and verified. A feature is not considered complete merely because source generation succeeds: generated server/client source must also pass the real XPScript compilation path.

## Verified CI inventory (2026-09-23)

The current CI coverage is split strictly into **server generation** and **client generation**. The explicit matrix smoke now proves OpenAPI 3.0, 3.1 and 3.2 in both JSON and YAML for server and client generation, including compilation through the real XPScript paths.

### Server already covered

- OpenAPI 3.1 YAML: `petstore.yaml` generates REST endpoint skeletons, models, route/path/query/header/body bindings and responses; generated source is compiled with `XpsWebCompiler`.
- OpenAPI 3.1 YAML additive import: `petstore-reimport.yaml` verifies preservation of edited handlers/existing declarations, addition of new models/endpoints, drift warnings, and recompilation.
- OpenAPI 3.0 JSON: `petstore.json` is exercised through the server CLI generation path.
- OpenAPI 3.0 in-memory server compatibility is explicitly checked by `OpenApiGeneratorSmoke`.
- Scope-collision server output is compiled through the real web compiler.

### Client already covered

- OpenAPI 3.1 YAML: `client-ci.yaml` is generated through the CLI and compiled as XPScript.
- OpenAPI 3.1 YML extension: the same fixture is copied to `.yml`, generated and compiled.
- OpenAPI 3.0 JSON: `petstore.json` is generated as a client and compiled.
- Client update/regeneration from OpenAPI 3.1 YAML is generated and compiled.
- Smoke coverage includes security, API keys, bearer/basic auth, schemas, enums, arrays, oneOf/allOf, nullable 3.1 types, additionalProperties, parameter encoding, response validation and identifier/scope collision behavior.

### Matrix acceptance verified

- OpenAPI 3.0/3.1/3.2 JSON and YAML server generation + real web compilation.
- OpenAPI 3.0/3.1/3.2 JSON and YAML client generation + real transpiler compilation.
- Full CI #612 green across Windows, Linux and macOS after the scope migration.
