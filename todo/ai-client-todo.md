# Generic OpenAI-compatible AI client TODO

(c) xpagedeveloper.com 2026

Implement after the current UIForm work is complete and merged.

## Core API

- [ ] Add a top-level generic AI client class, proposed name `AIClient`.
- [ ] Base the wire format on the OpenAI-compatible HTTP API rather than a provider-specific SDK.
- [ ] Support normal non-streaming request/response calls.
- [ ] Support streaming responses with incremental text/event delivery to XPScript code.
- [ ] Support configurable model name per request or client instance.
- [ ] Expose timeout and cancellation behavior consistent with the native HTTP runtime.
- [ ] Keep advanced capabilities optional so the base client stays lightweight.

## Endpoint and provider compatibility

- [ ] Make the base URL/endpoint configurable. Do not hard-code the OpenAI endpoint.
- [ ] Support endpoint path overrides for providers that expose OpenAI-compatible routes under custom paths.
- [ ] Support standard bearer API key authentication.
- [ ] Allow arbitrary additional request headers through `SetHeader`, `RemoveHeader` and `ClearHeaders`.
- [ ] Do not overwrite caller-supplied provider headers unless required for transport correctness.
- [ ] Support OpenAI-compatible providers and gateways such as Azure AI, OpenRouter and any other service implementing compatible request/response schemas.
- [ ] Keep provider-specific features outside the common schema as explicit optional extensions, not hard-coded provider branches.

## Request model

- [ ] Support system, user and assistant messages.
- [ ] Add a reusable message collection object or JsonArray-backed message model.
- [ ] Support common optional parameters such as temperature and max output tokens.
- [ ] Allow additional provider-compatible JSON properties without requiring compiler changes.
- [ ] Reuse the existing native JSON API for structured request bodies where useful.

## Response model

- [ ] Add a stable response object with HTTP status, model, content/text and raw JSON access.
- [ ] Expose usage/token accounting when returned by the provider.
- [ ] Preserve unknown response properties through raw JSON access.
- [ ] Map HTTP/provider errors to clear XPScript runtime errors without leaking API keys or authorization headers.

## Streaming

- [ ] Support OpenAI-compatible SSE streaming.
- [ ] Add incremental callback/event delivery for received text chunks.
- [ ] Optionally collect the complete streamed response as a convenience.
- [ ] Handle stream termination markers and malformed/incomplete streams safely.
- [ ] Cancellation must close the HTTP response/stream promptly.
- [ ] Enforce bounded line, event and payload limits in the streaming parser.

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

- [ ] Never include API keys, Authorization headers or configured secret headers in diagnostics.
- [ ] Apply existing HTTP redirect and SSRF/security policy consistently unless a documented safe override is explicitly configured.
- [ ] Regression-test custom endpoint URLs and additional headers against a local mock OpenAI-compatible server.
- [ ] Regression-test non-streaming and streaming calls on Windows, Ubuntu and macOS.
- [ ] Add compatibility fixtures that emulate OpenAI-style, Azure/OpenAI-compatible and OpenRouter-style endpoint/header variations without external network calls in CI.
- [ ] Add local mock MCP server regression tests.
- [ ] Add RAG isolation tests proving tenant/user filters cannot leak records across scopes.
- [ ] Add embeddings batching, dimension and malformed-response tests.
- [ ] Add combined MCP + RAG + embeddings tool-composition tests.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.
