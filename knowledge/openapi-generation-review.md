# OpenAPI generation review

The DigitalOcean investigation exposed a declaration/reference mismatch. Server
generation preserved valid component names, while reference generation rebuilt
them independently. The focused model-name regression reproduces that failure
and compiles both generated and imported server sources.

Server and client now collect model declarations and reference names through
`XpsOpenApiModelCatalog`. Each generator retains its existing collision policy:
the server rejects duplicate generated component names, while the client
allocates suffixes. OpenAPI component keys remain case-sensitive; generated
XPscript names are compared without case sensitivity.

Actual CLI client compilation found two more export failures. Typed array class
fields were emitted even though the class parser does not accept them. Exported
arrays now use the supported `XPJsonArray` contract and retain item schemas in
response validation. Also, unqualified enum constants could replace model or
field identifiers (`ERROR` changed the `error` declaration into `Class 4`).
Client emission now reserves emitted identifiers before allocating enum members.
The focused regression compiles scalar/model arrays together with enum values
that collide with model and property names through `CompilerDriver`.
The enum reservation currently reads identifiers from emitted source. Treat
this as an interim safeguard; a shared declaration plan should supply that
information directly instead of making emission responsible for semantic naming.

## Assessment

A complete rewrite is not justified by this failure alone. The current parser,
normalizer, HTTP runtime and CLI already have executable regression coverage.
The generation layer does need a staged refactor because it makes related
decisions in several places:

- Server and client each define keyword sets and identifier conversion rules.
- Both allocate operation and member names independently.
- Client model emission handles composed properties, typed arrays, enums and
  JSON names separately from server model emission.
- Server emission records renamed JSON properties in comments, while client
  emission adds `JsonName` metadata. Successful compilation cannot verify that
  renamed properties retain their wire contract. Test JSON binding and output
  before claiming equivalent client/server models.
- Server import merges generated source using regular expressions rather than
  a shared declaration representation.
- Generated string enums go through the compiler's numeric enum lowering. Add
  response deserialization tests for string enum wire values before assuming
  that successful validation and compilation preserve those values.

The shared plan now starts in `XpsOpenApiGenerationPlan`. It contains schema
identity, allocated type names, enum classification and wire-to-generated
the existing source emitters are migrated incrementally. The next additions are
operation records are now collected with method, path, wire operationId and raw
OpenAPI nodes. The next additions are resolved property types and operation
signatures. Validate reference closure
and generated name scopes before emitting source.
Keep client HTTP response mapping and server route/handler emission separate.

Move one responsibility at a time, with small regressions and actual generated
assembly compilation. Preserve documented APIs and current collision policies
until a deliberate compatibility change is covered by tests and documentation.
Use the complete DigitalOcean fixture as the integration gate before testing
the next provider. Passing generation or transpilation alone does not establish
that an emitted program compiles or preserves its JSON contract.

## Size constraint

The current bundled DigitalOcean definition generates a server of about 1.4 MB
and a client of about 10.4 MB. The compiler source limit is now 16 MiB, so the
complete client fits within the input budget. The regression selector retains three original
POST operations, including all transitive component references, yielding 78
models and a client of about 116 KB. Full generation and model-reference closure
checks remain in the complete smoke suite. This is representative compilation,
and the complete generated client can now be compiled through the CLI.

If complete large-definition compilation is required, the export design also
needs a supported strategy for multiple source files or smaller generated units.
Do not silently raise compiler resource limits to disguise this constraint.
