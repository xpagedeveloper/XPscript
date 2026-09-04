# HCL Domino data integration TODO

(c) xpagedeveloper.com 2026

Implement after `todo/sql-database-todo.md` is complete and merged.

## Architecture decision

- [ ] Use HCL Domino REST API as the required primary/default integration path for remote and cross-platform XPScript applications.
- [ ] All first implementation phases must target Domino REST API. Java NotesFactory/IIOP must not be required for normal use.
- [ ] Keep the XPScript public API independent of Domino REST API endpoint details where practical.
- [ ] Do not make a local Notes/Domino client installation a requirement for the default integration.
- [ ] Treat Java NotesFactory/IIOP access as an optional legacy/advanced adapter only if a concrete use case requires capabilities unavailable through Domino REST API.
- [ ] Do not build a new direct NSF wire protocol or proprietary Domino client protocol.

Rationale to preserve in the final design: Domino REST API is HCL's HTTP(S)-based integration layer for documents, views, folders and agents, supports configured schemas/scopes, DQL and OData modes, and works with applications that can speak HTTP(S). HCL's Java NotesFactory remote API uses IIOP and local calls require Notes/Domino installation, making it a less suitable default for a .NET cross-platform XPScript runtime.

## Core object model

- [ ] Add a top-level `DominoClient` class.
- [ ] Add a `DominoDatabase` / `DominoDataSource` abstraction using a Domino REST API dataSource/scope.
- [ ] Add a `DominoDocument` abstraction backed by JSON.
- [ ] Add a `DominoView` / list-result abstraction.
- [ ] Add a `DominoQueryResult` abstraction for DQL/OData queries where appropriate.
- [ ] Reuse `XPJsonObject` / `XPJsonArray` for document/item data whenever this produces a clear XPScript API.
- [ ] Preserve Domino UNID, note ID and other stable identifiers returned by the API.

## Connection and authentication

- [ ] Support configurable Domino REST API base URL.
- [ ] Support bearer/JWT access tokens.
- [ ] Support OAuth/OIDC token acquisition only where it can be implemented generically and securely.
- [ ] Support application-supplied tokens so authentication can remain external to the runtime.
- [ ] Support additional required headers without exposing secrets in logs.
- [ ] Prefer HCL's OIDC IdP Catalog integration where the deployment uses Domino 14+ and OIDC.
- [ ] Support configurable timeout and cancellation.
- [ ] Enforce HTTPS by default for non-loopback remote endpoints, with any insecure override explicit and development-only.
- [ ] Never log access tokens, client secrets, Authorization headers or sensitive Domino item values by default.

## Schema, scope and ACL model

- [ ] Treat Domino REST API schema configuration as the server-side authority for which forms, fields, views, folders and agents are exposed.
- [ ] Treat Domino REST API scopes/dataSource aliases as part of the connection/data-source identity.
- [ ] Preserve Domino database ACL enforcement rather than attempting to recreate ACL logic client-side.
- [ ] Document that REST API scopes limit attempted access but do not replace the Domino database ACL.
- [ ] Do not provide a client-side option that silently bypasses configured schema/form-mode restrictions.
- [ ] Support named form modes such as `default`, `dql` and `odata` when configured server-side.

## Documents

- [ ] Create documents through Domino REST API.
- [ ] Read a document by supported identifier.
- [ ] Update documents.
- [ ] Delete documents where the configured schema/mode and user ACL permit it.
- [ ] Read/write only fields exposed by the configured Domino REST API schema/mode.
- [ ] Map Domino/JSON String, Number, Boolean, Date/DateTime, Names, Readers, Authors and multivalue data to suitable XPScript values.
- [ ] Preserve empty, missing and NULL-like states explicitly where Domino and JSON semantics differ.
- [ ] Support document data conversion to and from `XPJsonObject`.
- [ ] Investigate rich-text access separately and define a bounded/safe representation before exposing it.
- [ ] Treat attachments and embedded objects as untrusted binary data and apply size limits.

## Views and folders

- [ ] List configured views and folders.
- [ ] Read view/folder entries.
- [ ] Support pagination/count/start controls.
- [ ] Support returning view column data.
- [ ] Support requesting underlying document data when the configured API mode permits it.
- [ ] Preserve entry/document identifiers required for subsequent document access.
- [ ] Add bounded result limits to convenience methods that materialize all entries.

## DQL query support

- [ ] Support Domino Query Language through Domino REST API `/query` when DQL is enabled in the schema.
- [ ] Support query variables/parameters rather than string-concatenating untrusted values into DQL.
- [ ] Expose configurable count/start pagination.
- [ ] Expose safe configurable scan/time limits only within application-defined maximums.
- [ ] Preserve the server-side requirement for a configured `dql` form mode.
- [ ] Add clear errors when DQL is not enabled for a form/schema instead of falling back to unrestricted access.

## OData support

- [ ] Investigate whether the XPScript API needs first-class OData helpers or whether raw Domino REST API support plus normal HTTP/query helpers is sufficient.
- [ ] If first-class OData is added, keep it optional and use the server-configured `odata` form mode.
- [ ] Do not duplicate a complete OData parser/client if a maintained .NET library can be reused safely.

## Agents and Domino operations

- [ ] Investigate Domino REST API agent invocation and expose it only if it can be bounded and authorized clearly.
- [ ] Keep agent invocation separate from ordinary data read/write APIs because it can execute server-side logic.
- [ ] Require explicit opt-in/policy for calling Domino agents from web-hosted XPScript.
- [ ] Apply timeout, cancellation and response-size limits.

## RAG and AI integration

- [ ] Add an optional `DominoRAGSource` / retrieval adapter that can feed `RAGTool` without coupling `AIClient` directly to Domino.
- [ ] Support retrieving documents/view entries with source metadata such as server, dataSource, UNID, form, view and document identifiers.
- [ ] Support application-enforced tenant/user/security filters before retrieved Domino content is injected into AI context.
- [ ] Preserve Domino ACL/schema enforcement on every retrieval request.
- [ ] Allow embeddings to be generated through the attached `EmbeddingsTool` and stored in a separate SQL/vector backend when desired.
- [ ] Do not require embeddings to be stored in Domino itself.
- [ ] Investigate whether Domino document metadata plus SQL/vector indexes is the preferred architecture for large RAG workloads.
- [ ] Add source citations that allow an application to link an AI answer back to the originating Domino document/view entry where permitted.

## Optional Java/IIOP adapter investigation

- [ ] Document HCL Java NotesFactory/IIOP as a secondary integration option, not the default.
- [ ] Evaluate only if Domino REST API lacks a required operation.
- [ ] Document that local Java Notes API calls require a local Notes/Domino installation.
- [ ] Document that remote NotesFactory sessions use IIOP/DIIOP and have additional Domino server configuration and security requirements.
- [ ] Avoid embedding a Java runtime bridge into the base XPScript runtime unless a real requirement justifies the operational cost.
- [ ] If implemented, isolate it behind the same `DominoClient`-level abstraction so scripts do not depend on IIOP-specific APIs.

## Security and resource limits

- [ ] Treat all Domino document/view data as untrusted input.
- [ ] Bound response sizes, document counts, attachment sizes and query result counts.
- [ ] Respect cancellation and timeouts on every HTTP request.
- [ ] Reuse the native HTTP runtime's redirect/SSRF protections where appropriate.
- [ ] Prevent endpoint configuration from leaking tokens to redirects or unrelated hosts.
- [ ] Never expose server filesystem paths or internal REST API diagnostics directly to web clients.
- [ ] Add concurrency tests proving clients, headers, tokens and result objects do not leak state across users or requests.
- [ ] Add explicit web-host isolation tests for Kestrel, CGI and FastCGI.

## Tests and quality gates

- [ ] Build a local/mock Domino REST API fixture for deterministic CI tests without requiring public network access.
- [ ] Add schema/scope authorization fixtures.
- [ ] Add document CRUD regression tests.
- [ ] Add view/folder regression tests.
- [ ] Add DQL variable/parameter tests.
- [ ] Add pagination and result-limit tests.
- [ ] Add malformed/error response tests.
- [ ] Add authentication/header redaction tests.
- [ ] Add Unicode, names, dates, booleans and multivalue item round-trip tests.
- [ ] Add RAG-source isolation tests if Domino-backed retrieval is implemented.
- [ ] Add Kestrel, CGI and FastCGI smoke tests.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.

## Recommended implementation order

- [ ] Phase 1: `DominoClient`, endpoint/auth, dataSource/scope selection and read-only document access through Domino REST API.
- [ ] Phase 2: views/folders and pagination through Domino REST API.
- [ ] Phase 3: document create/update/delete with schema-mode enforcement through Domino REST API.
- [ ] Phase 4: DQL queries using variables and bounded execution limits through Domino REST API.
- [ ] Phase 5: attachments/rich-text investigation and safe implementation where required.
- [ ] Phase 6: optional RAG retrieval adapter.
- [ ] Phase 7: reconsider Java/IIOP only if a specific unsupported Domino capability remains.
