# Generic OpenAI-compatible AI client TODO

(c) xpagedeveloper.com 2026

Implement after the current UIForm work is complete and merged.

## Core API

- [x] Add a top-level generic AI client class named `XPAi`.
- [x] Base the wire format on the OpenAI-compatible HTTP API rather than a provider-specific SDK.
- [x] Support normal non-streaming request/response calls.
- [x] Support streaming responses with incremental text/event delivery to XPScript code.
- [x] Support configurable model name per request or client instance.
- [x] Expose timeout and cancellation behavior consistent with the native HTTP runtime.
- [x] Keep advanced capabilities optional so the base client stays lightweight.
- [x] Route streaming callbacks through the shared XPScript callback runtime.
- [x] Allow streaming callbacks to receive caller-supplied context parameters after the streamed chunk.
- [x] Reuse the shared callback runtime for native asynchronous HTTP requests.

## Endpoint and provider compatibility

- [x] Make the base URL/endpoint configurable. Do not hard-code the OpenAI endpoint.
- [x] Support endpoint path overrides for providers that expose OpenAI-compatible routes under custom paths.
- [x] Support standard bearer API key authentication.
- [x] Allow arbitrary additional request headers through `SetHeader`, `RemoveHeader` and `ClearHeaders`.
- [x] Do not overwrite caller-supplied provider headers unless required for transport correctness.
- [x] Support OpenAI-compatible providers and gateways such as Azure AI, OpenRouter and any other service implementing compatible request/response schemas.
- [x] Keep provider-specific features outside the common schema as explicit optional extensions, not hard-coded provider branches.
- [x] Add documented endpoint and authentication presets for OpenAI, Claude, OpenRouter and Azure OpenAI.

## Request model

- [x] Support system, user and assistant messages.
- [x] Add a reusable message collection object or JsonArray-backed message model.
- [x] Support common optional parameters such as temperature and max output tokens.
- [x] Allow additional provider-compatible JSON properties without requiring compiler changes.
- [x] Reuse the existing native JSON API for structured request bodies where useful.

## Response model

- [x] Add a stable response object with HTTP status, model, content/text and raw JSON access.
- [x] Expose usage/token accounting when returned by the provider.
- [x] Preserve unknown response properties through raw JSON access.
- [x] Map HTTP/provider errors to clear XPScript runtime errors without leaking API keys or authorization headers.

## Streaming

- [x] Support OpenAI-compatible SSE streaming.
- [x] Add incremental callback/event delivery for received text chunks.
- [x] Optionally collect the complete streamed response as a convenience.
- [x] Handle stream termination markers and malformed/incomplete streams safely.
- [x] Cancellation must close the HTTP response/stream promptly.
- [x] Enforce bounded line, event and payload limits in the streaming parser.

## Optional tool architecture

- [ ] Add an `AITool` abstraction so optional AI capabilities can be attached to an `AIClient` instance.
- [ ] Add methods such as `AddTool`, `RemoveTool`, `ClearTools` and `HasTool`.
- [ ] Keep each tool isolated from the core transport client and from unrelated tools.
- [ ] Allow tools to contribute request context, callable functions/tool definitions, response processing or retrieval results where appropriate.
- [ ] Prevent one tool from silently changing endpoint, authentication or unrelated headers owned by the base client.
- [ ] Define deterministic tool execution order.
- [ ] Add per-tool timeout/cancellation boundaries where external work is performed.

## MCP tool

- [ ] Add optional `MCPTool` support attachable to `AIClient`.
- [ ] Support one or more MCP servers per client.
- [ ] Support MCP tool discovery and capability enumeration.
- [ ] Support invoking MCP tools from an AI request flow.
- [ ] Support MCP resources and prompts where exposed by the connected MCP server.
- [ ] Keep MCP transport abstract enough to support the MCP transports implemented by the project without coupling AIClient to one transport.
- [ ] Support per-MCP-server headers/authentication/configuration.
- [ ] Apply strict timeouts, cancellation and bounded response sizes to MCP calls.
- [ ] Do not expose MCP credentials or sensitive tool arguments in diagnostics.
- [ ] Define explicit policy for which MCP tools the model may invoke automatically versus those requiring application approval.

## RAG tool

- [ ] Add optional `RAGTool` support attachable to `AIClient`.
- [ ] Keep retrieval storage/provider independent behind a stable interface.
- [ ] Support retrieval by query text and optionally by precomputed embedding.
- [ ] Support configurable top-k, similarity threshold and metadata filters.
- [ ] Support tenant/user/security filters supplied by the application before retrieval.
- [ ] Return source metadata with retrieved chunks so applications can expose citations/references.
- [ ] Support configurable context assembly and maximum injected context size.
- [ ] Avoid silently mixing results across tenants, users or configured data scopes.
- [ ] Allow custom retrieval backends through a provider interface instead of hard-coding one vector database.
- [ ] Support hybrid retrieval later without changing the public `AIClient` API.

## Embeddings tool

- [ ] Add optional `EmbeddingsTool` support attachable to `AIClient`.
- [ ] Use OpenAI-compatible embeddings endpoints by default.
- [ ] Allow independent embeddings endpoint, model and headers from the main chat/completions endpoint.
- [ ] Support one text and batch text embedding calls.
- [ ] Return embeddings as normal XPScript arrays/objects with stable metadata.
- [ ] Validate vector dimensions and numeric payloads.
- [ ] Support cancellation and bounded batch sizes.
- [ ] Allow RAGTool to use an attached EmbeddingsTool without requiring the base AI client to know retrieval details.
- [ ] Permit applications to supply precomputed embeddings to avoid duplicate API calls.

## Tool composition

- [ ] Allow MCPTool, RAGTool and EmbeddingsTool to be attached independently or together.
- [ ] Define how RAG context, MCP tool definitions and normal user/system messages are composed into one request.
- [ ] Prevent duplicate tool names and ambiguous routing.
- [ ] Preserve raw provider tool-call payloads where possible for forward compatibility.
- [ ] Add a configurable maximum number of recursive AI/tool iterations to prevent infinite agent loops.
- [ ] Add a total request budget across model calls, MCP calls, retrieval calls and embedding calls.
- [ ] Expose tool execution traces in a safe structured form without secrets.

## Security and compatibility

- [x] Never include API keys, Authorization headers or configured secret headers in diagnostics.
- [x] Apply existing HTTP redirect and SSRF/security policy consistently unless a documented safe override is explicitly configured.
- [x] Regression-test custom endpoint URLs and additional headers against a local mock OpenAI-compatible server.
- [x] Regression-test non-streaming and streaming calls on Windows, Ubuntu and macOS.
- [x] Add compatibility fixtures that emulate OpenAI-style, Azure/OpenAI-compatible and OpenRouter-style endpoint/header variations without external network calls in CI.
- [ ] Add local mock MCP server regression tests.
- [ ] Add RAG isolation tests proving tenant/user filters cannot leak records across scopes.
- [ ] Add embeddings batching, dimension and malformed-response tests.
- [ ] Add combined MCP + RAG + embeddings tool-composition tests.
- [x] Add documentation and a reusable example under `docs/` and `samples/`.
