# AST generated-runtime dependency inventory

This is the first inventory for the transitive-runtime audit. It records the
current provider and the known conditional dependencies; it is not yet the
dependency guard.

| AST object/family | Primary provider | Known conditional/transitive providers |
| --- | --- | --- |
| All AST programs | `AstRuntimeSource`, `CoreCompatibilityRuntimeSource` | `FileSystemPortabilityRuntimeSource`, list/array/error/callback helpers |
| UIForm | `UIExtensionRuntimeSource` | `JsonSchemaRuntimeSource`; desktop callback and Avalonia providers in desktop output |
| XPJson / JSON objects | `AstJsonRuntimeSource` | schema provider for `XPJsonSchema` and validation results |
| XPHttp | `AstHttpSupport.RuntimeCode` | HTTP core/JSON, TLS validation, filesystem helpers |
| CSV | `AstCsvSupport.RuntimeCode` | CSV file runtime; JSON runtime for document conversion |
| XML | `AstXmlSupport.RuntimeCode` | core compatibility and JSON conversion helpers |
| Spreadsheet | `AstSpreadsheetSupport.RuntimeCode` | CSV interop when document conversion is used |
| XPImage | `AstImageSupport.RuntimeCode` | image-array runtime for array storage |
| Application | `AstApplicationSupport.RuntimeCode` | persistence, crypto, secrets, logging, debug and source-line providers |
| Archive | `AstCompiler` archive gates | archive memory/iterator and optional extended reader/writer providers |
| Database families | database support providers | dynamic object, HTTP, TLS, attachment and database-specific providers |
| AI | `AstCompiler` AI gates | JSON schema, callback, HTTP and TLS providers |
| Network/System inventory | family support providers | shared core compatibility and network/TLS providers |

Known risk exposed by the UIForm fixture: a provider may reference generated
types supplied by another provider even when the source does not mention those
types directly. The follow-up guard must inspect generated source, not only the
original XPscript source.
