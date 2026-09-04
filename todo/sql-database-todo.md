# SQL database runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/pdf-runtime-todo.md` is complete and merged. Complete this before the HCL Domino data integration block.

## Goals

- [ ] Add a provider-neutral SQL database API to XPScript.
- [ ] Support Microsoft SQL Server.
- [ ] Support MariaDB/MySQL-compatible servers through a maintained .NET provider.
- [ ] Support PostgreSQL.
- [ ] Keep the public XPScript API stable across database providers where practical.
- [ ] Build on ADO.NET provider abstractions and maintained NuGet database providers. Do not implement database wire protocols.
- [ ] Keep provider-specific extensions optional and clearly namespaced.
- [ ] Make the SQL layer reusable by later RAG/retrieval implementations where appropriate.

## Required NuGet provider strategy

- [ ] Use `Microsoft.Data.SqlClient` for Microsoft SQL Server unless a documented compatibility/security blocker is found during implementation.
- [ ] Use `MySqlConnector` for MariaDB/MySQL-compatible servers unless a documented compatibility/security blocker is found during implementation.
- [ ] Use `Npgsql` for PostgreSQL unless a documented compatibility/security blocker is found during implementation.
- [ ] Use the providers' built-in connection pooling, TLS, parameter binding, transactions, async I/O, cancellation and type mapping instead of reimplementing those features.
- [ ] Use `DbConnection`, `DbCommand`, `DbDataReader`, `DbTransaction` and related ADO.NET abstractions for the common XPScript layer where practical.
- [ ] Keep each provider behind an XPScript-owned adapter so provider packages can be upgraded/replaced without changing the public script API.
- [ ] Pin/centrally manage provider package versions.
- [ ] Verify .NET 10 support, Windows/Linux/macOS behavior where applicable, package maintenance, license and security advisories before finalizing versions.
- [ ] Do not create custom TDS, MySQL/MariaDB or PostgreSQL protocol implementations.

## Core object model

- [ ] Add a top-level `SqlConnection` or `DatabaseConnection` class.
- [ ] Add a `SqlCommand` / `DatabaseCommand` class.
- [ ] Add a `SqlDataReader` / `DatabaseReader` abstraction.
- [ ] Add a result-set abstraction convertible to XPScript arrays and JSON.
- [ ] Add a transaction abstraction.
- [ ] Add parameter collection support.
- [ ] Expose provider name, server/database metadata and connection state where safe.
- [ ] Support deterministic close/dispose semantics.

## Connection configuration

- [ ] Support connection strings supplied by the application.
- [ ] Support structured connection configuration so applications do not need to concatenate credentials into strings.
- [ ] Support host, port, database/catalog, username and password where applicable.
- [ ] Support integrated/Windows authentication for SQL Server where the provider and platform support it.
- [ ] Support TLS options and certificate validation settings without insecure defaults.
- [ ] Support configurable connection timeout and command timeout.
- [ ] Use provider connection pooling by default where appropriate.
- [ ] Allow pooling to be disabled explicitly for testing or special cases.
- [ ] Never include passwords, access tokens or complete secret-bearing connection strings in diagnostics.

## Query and command execution

- [ ] Execute parameterized SELECT queries.
- [ ] Execute INSERT, UPDATE and DELETE statements.
- [ ] Execute stored procedures/functions where supported.
- [ ] Return affected-row count for non-query commands.
- [ ] Return scalar results.
- [ ] Stream/read result rows without requiring the full result set in memory.
- [ ] Add convenience APIs for returning a complete result set when bounded and appropriate.
- [ ] Support multiple result sets where the provider supports them.
- [ ] Support cancellation and timeouts.
- [ ] Add asynchronous runtime internals even if the first XPScript-facing API is synchronous.

## Parameters and SQL injection protection

- [ ] Make parameters the normal documented way to pass values into SQL.
- [ ] Support named provider parameters through a provider-neutral XPScript parameter API.
- [ ] Map String, Integer, Long, Double, Currency/Decimal, Boolean, Date/DateTime, Byte arrays, Null and Empty safely.
- [ ] Support explicit database type/size/precision where needed.
- [ ] Do not attempt to parameterize SQL identifiers such as table or column names. Document safe allow-list patterns instead.
- [ ] Add adversarial SQL-injection regression tests proving values are not concatenated into commands by the runtime helpers.

## Transactions

- [ ] Begin transaction.
- [ ] Commit transaction.
- [ ] Roll back transaction.
- [ ] Support isolation-level selection where portable.
- [ ] Define nested transaction/savepoint behavior explicitly per provider.
- [ ] Ensure exceptions do not silently commit a pending transaction.
- [ ] Deterministically release transactions and connections.

## Data mapping

- [ ] Preserve database NULL distinctly from empty string and numeric zero.
- [ ] Map numeric types without silent precision loss where possible.
- [ ] Map Date/DateTime values consistently.
- [ ] Support binary/blob values as XPScript Byte arrays.
- [ ] Convert rows/results to `XPJsonObject` / `XPJsonArray`.
- [ ] Define duplicate-column-name behavior.
- [ ] Preserve provider-specific values through a documented fallback representation when no native XPScript type exists.

## Schema and metadata

- [ ] List tables/views where supported.
- [ ] Read column metadata and types.
- [ ] Read primary-key metadata where supported.
- [ ] Read indexes where practical.
- [ ] Keep metadata APIs optional so normal query execution does not depend on schema discovery.

## RAG and vector retrieval integration

- [ ] Allow the SQL runtime to act as a storage/retrieval backend for `RAGTool` through an adapter rather than coupling `AIClient` directly to SQL.
- [ ] Support ordinary SQL retrieval for metadata/document stores independently of vector search.
- [ ] Investigate PostgreSQL vector retrieval through pgvector or equivalent maintained extension/library rather than implementing vector indexing algorithms in XPScript.
- [ ] Investigate current SQL Server vector capabilities when the database/provider version supports them.
- [ ] Investigate current MariaDB vector capabilities when the database/provider version supports them.
- [ ] Keep vector support capability-based and optional. Base SQL support must not require vector extensions.
- [ ] Support tenant/user/security filters as required predicates supplied before retrieval execution.
- [ ] Ensure RAG retrieval queries use parameters and cannot bypass configured tenant/security predicates.
- [ ] Support storing document/chunk identifiers, embeddings, metadata and source references when a provider backend supports the configured schema.

## Security and limits

- [ ] Apply maximum row/result limits for convenience APIs that materialize complete result sets.
- [ ] Support configurable maximum field/blob size.
- [ ] Support command timeout and cancellation.
- [ ] Never log credentials or secret connection properties.
- [ ] Avoid logging parameter values by default because they may contain personal or secret data.
- [ ] Add opt-in safe query tracing that redacts values.
- [ ] Validate provider names against an allow-list. Do not dynamically load arbitrary assemblies from connection data.
- [ ] Define connection-string security guidance for web, CGI and FastCGI hosting.
- [ ] Verify concurrent connections/commands do not share mutable command/parameter state.

## Web/runtime integration

- [ ] Work from standalone XPScript programs.
- [ ] Work from Kestrel-hosted XPScript.
- [ ] Work from FastCGI-hosted XPScript.
- [ ] Work from CGI-hosted XPScript with the documented limitation that process-local pooling/state does not persist across requests.
- [ ] Ensure one request cannot access another request's connection/transaction object through runtime state leakage.
- [ ] Document safe credential/configuration patterns for server-hosted scripts.

## Tests and quality gates

- [ ] Add SQL Server integration tests using a CI-appropriate local/containerized test database where available.
- [ ] Add MariaDB integration tests.
- [ ] Add PostgreSQL integration tests.
- [ ] Run common provider-neutral behavior tests against all three providers.
- [ ] Add NULL, Unicode, decimal, DateTime and binary round-trip tests.
- [ ] Add transaction commit/rollback tests.
- [ ] Add timeout/cancellation tests.
- [ ] Add malformed/invalid connection tests without exposing secrets.
- [ ] Add concurrency and connection-pool stress tests.
- [ ] Add Kestrel, CGI and FastCGI database-access smoke tests.
- [ ] Add RAG adapter isolation tests if SQL-backed retrieval is implemented.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.

## Next stage

- [ ] After SQL support is complete and merged, implement `todo/hcl-domino-data-todo.md`.
