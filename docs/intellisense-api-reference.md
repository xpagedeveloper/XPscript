# Machine-readable IntelliSense API reference

This reference is the canonical machine-readable supplement for public runtime APIs that are documented in topical pages. The VS Code catalog generator scans this file together with the other XPscript documentation.

Every public runtime object, function, method, or property that is not already represented by a machine-readable five-column row in its authoritative reference must be added here in the same change that introduces the API. One public member occupies one row. Do not group member names in a cell.

The required columns are `Member`, `Syntax`, `Parameters`, `Description`, and `Example`. The example must point to an existing executable `.xps` sample or demo. This format is part of the XPscript documentation contract and is validated by CI.



## Compiler command-line machine interface

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `CompilerCommandLine.ValidateAsync` | `xpscript validate source.xps [--rid runtime] [--result-format FORMAT] (FORMAT: text, json, or xml) [--debug]` | source path, optional runtime identifier and result format; `--stdin --filename name.xps` accepts virtual source input. | Validates XPScript through the normal compiler pipeline without producing or executing the final application. `--debug` is a local compiler troubleshooting option and is not exposed by MCP. | [malformed-source-error.xps](../samples/malformed-source-error.xps) |
| `CompilerCommandLine.Symbols` | `xpscript symbols [--search text] [--result-format FORMAT] (FORMAT: text or json)` | optional symbol search text and result format. | Searches the deterministic public XPScript compiler symbol catalog for machine and developer tooling. | [application-crypto.xps](../samples/application-crypto.xps) |
| `CompilerCommandLine.Describe` | `xpscript describe symbol [--result-format FORMAT] (FORMAT: text or json)` | exact public XPScript symbol name and optional result format. | Returns compiler-owned metadata for an exact public symbol, including signature, kind, documentation ID and target restrictions when present. | [application-crypto.xps](../samples/application-crypto.xps) |
| `CompilerCommandLine.Explain` | `xpscript explain diagnosticCode [--result-format FORMAT] (FORMAT: text or json)` | stable XPS diagnostic code and optional result format. | Returns the compiler-owned definition and explanation for a stable XPScript diagnostic code. | [malformed-source-error.xps](../samples/malformed-source-error.xps) |

## UIForm JSON Schema validation

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `UIForm.SetValidationSchema` | `form.SetValidationSchema(schema)` | `schema`: `XPJsonSchema` or `Nothing`. | Sets or clears JSON Schema validation for bound form data. Without a schema, validation succeeds by default. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `UIForm.HasValidationSchema` | `form.HasValidationSchema` | none | Returns `True` when a JSON Schema is attached to the form. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `UIForm.ValidateData` | `form.ValidateData()` | none | Returns `XPJsonValidationResult` for bound form data; the result is valid when no schema is attached. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `UIForm.IsDataValid` | `form.IsDataValid` | none | Returns whether bound form data passes its attached JSON Schema, or `True` when no schema is attached. | [ui-form-core.xps](../samples/ui-form-core.xps) |

## NotesSession additions

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `NotesSession.HashPassword` | `session.HashPassword(password)` | `password`: plaintext password. | Returns a native Domino password digest. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.VerifyPassword` | `session.VerifyPassword(password, hashedPassword)` | `password`: plaintext password; `hashedPassword`: Domino digest. | Returns `True` when the password matches the native Domino digest. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.AddressBooks` | `session.AddressBooks` | none | Returns the Domino Directories and Personal Address Books known to the session. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.GetEnvironmentString` | `session.GetEnvironmentString(name [, system])` | `name`: environment variable; optional `system`: use exact system name. | Reads a string from the active Notes/Domino environment. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.GetEnvironmentValue` | `session.GetEnvironmentValue(name [, system])` | `name`: environment variable; optional `system`: use exact system name. | Reads a numeric Notes/Domino environment value. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.SetEnvironmentVar` | `session.SetEnvironmentVar(name, value [, system])` | `name`, `value`, optional `system`. | Creates or updates a Notes/Domino environment variable. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |

## Notes MIME

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `NotesMIMEEntity` | `Dim entity As NotesMIMEEntity` | none | Native Notes/Domino MIME entity wrapper. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentType` | `entity.ContentType` | none | Returns the MIME content type. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentSubType` | `entity.ContentSubType` | none | Returns the MIME content subtype. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.Charset` | `entity.Charset` | none | Returns the MIME charset. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentAsText` | `entity.ContentAsText` | none | Returns decoded entity content as text. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentID` | `entity.ContentID` | none | Returns the MIME Content-ID value. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentLocation` | `entity.ContentLocation` | none | Returns the MIME Content-Location value. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsMultipart` | `entity.IsMultipart` | none | Reports whether the entity is multipart. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsDiscretePart` | `entity.IsDiscretePart` | none | Reports whether the entity is a discrete MIME part. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsMessagePart` | `entity.IsMessagePart` | none | Reports whether the entity is a message MIME part. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetFirstChildEntity` | `entity.GetFirstChildEntity()` | none | Returns the first child MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNextSibling` | `entity.GetNextSibling()` | none | Returns the next sibling MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNextEntity` | `entity.GetNextEntity([search])` | optional traversal `search`. | Returns the next MIME entity using the supported traversal mode. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetChildren` | `entity.GetChildren()` | none | Returns child MIME entities. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.CreateChildEntity` | `entity.CreateChildEntity([nextSibling])` | optional sibling entity. | Creates a child MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNthHeader` | `entity.GetNthHeader(name [, occurrence])` | header `name`; optional one-based `occurrence`. | Returns a MIME header. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.SetContentFromText` | `entity.SetContentFromText(stream, contentType, encoding)` | source `stream`, `contentType`, transfer `encoding`. | Replaces entity content from text. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.SetContentFromBytes` | `entity.SetContentFromBytes(stream, contentType, encoding)` | source `stream`, `contentType`, transfer `encoding`. | Replaces entity content from bytes. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetContentAsText` | `entity.GetContentAsText(stream)` | destination `stream`. | Writes decoded text content to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetContentAsBytes` | `entity.GetContentAsBytes(stream)` | destination `stream`. | Writes decoded bytes to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetEntityAsText` | `entity.GetEntityAsText(stream)` | destination `stream`. | Writes the serialized MIME entity to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEHeader.GetParamVal` | `header.GetParamVal(name)` | parameter `name`. | Reads a MIME header parameter. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEHeader.SetParamVal` | `header.SetParamVal(name, value)` | parameter `name` and `value`. | Sets a MIME header parameter. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |

## SystemInventory

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `SystemInventory` | `Dim inventory As New SystemInventory` | none | Creates a read-only host inventory object. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetSnapshot` | `inventory.GetSnapshot()` | none | Returns a complete `SystemInventorySnapshot`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetSystemInfo` | `inventory.GetSystemInfo()` | none | Returns `SystemInventorySystemInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetCpuInfo` | `inventory.GetCpuInfo()` | none | Returns `SystemInventoryCpuInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetMemoryInfo` | `inventory.GetMemoryInfo()` | none | Returns `SystemInventoryMemoryInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetPhysicalDisks` | `inventory.GetPhysicalDisks()` | none | Returns physical disk inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetVolumes` | `inventory.GetVolumes()` | none | Returns filesystem volume inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetGraphicsAdapters` | `inventory.GetGraphicsAdapters()` | none | Returns graphics adapter inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetNetworkAdapters` | `inventory.GetNetworkAdapters()` | none | Returns network adapter inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetInstalledApplications` | `inventory.GetInstalledApplications()` | none | Returns installed application inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetInstalledPackages` | `inventory.GetInstalledPackages()` | none | Returns installed package inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.FindInstalledSoftware` | `inventory.FindInstalledSoftware(name)` | software `name` search text. | Finds installed software by name or package identifier. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.IsSoftwareInstalled` | `inventory.IsSoftwareInstalled(name)` | exact software `name` or package identifier. | Returns `True` when matching software is installed. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.MachineName` | `systemInfo.MachineName` | none | Returns the host machine name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.OperatingSystem` | `systemInfo.OperatingSystem` | none | Returns the operating-system name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.Manufacturer` | `systemInfo.Manufacturer` | none | Returns the system manufacturer when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.Model` | `systemInfo.Model` | none | Returns the system model when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryCpuInfo.Name` | `cpu.Name` | none | Returns the processor name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryCpuInfo.LogicalProcessors` | `cpu.LogicalProcessors` | none | Returns the logical processor count. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryMemoryInfo.TotalPhysicalMemory` | `memory.TotalPhysicalMemory` | none | Returns total physical memory in bytes. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryMemoryInfo.AvailablePhysicalMemory` | `memory.AvailablePhysicalMemory` | none | Returns available physical memory in bytes. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Name` | `software.Name` | none | Returns the installed software name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Version` | `software.Version` | none | Returns the installed software version. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Publisher` | `software.Publisher` | none | Returns the software publisher when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |

## XPJsonSchema

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `JsonSchema` | `Dim schema As New XPJsonSchema` | none | Creates a mutable JSON Schema object that can also be passed directly to JSON-aware runtime APIs such as `XPAi.SetJsonSchema`. | [xpai-structured-output.xps](../samples/xpai-structured-output.xps) |
| `JsonSchema.Parse` | `XPJsonSchema.Parse(text)` | JSON Schema `text`. | Parses a JSON Schema object from JSON text. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.FromJson` | `XPJsonSchema.FromJson(json)` | example JSON value. | Infers a JSON Schema from an example JSON value, marking inferred object properties as required. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Json` | `schema.Json` | none | Returns the schema as an XPJsonDocument. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Text` | `schema.Text` | none | Returns compact JSON Schema text. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Type` | `schema.Type` | none | Gets or sets the JSON Schema type keyword. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Title` | `schema.Title` | none | Gets or sets the schema title. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Description` | `schema.Description` | none | Gets or sets the schema description. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AdditionalProperties` | `schema.AdditionalProperties` | none | Gets or sets whether undeclared object properties are allowed. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Validate` | `schema.Validate(json)` | JSON value to validate. | Validates JSON and returns XPJsonValidationResult. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.IsValid` | `schema.IsValid(json)` | JSON value to validate. | Returns True when JSON satisfies the schema. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Set` | `schema.Set(keyword, value)` | schema keyword and value. | Sets an arbitrary schema keyword. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Remove` | `schema.Remove(keyword)` | schema keyword. | Removes a schema keyword. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddProperty` | `schema.AddProperty(name, childSchema [, required])` | property name, child schema, optional required flag. | Adds an object property schema. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddJson` | `schema.AddJson(name, json [, required])` | property name, example JSON, optional required flag. | Adds a property using an inferred schema. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddString` | `schema.AddString(name [, required])` | property name, optional required flag. | Adds a string property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddInteger` | `schema.AddInteger(name [, required])` | property name, optional required flag. | Adds an integer property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddNumber` | `schema.AddNumber(name [, required])` | property name, optional required flag. | Adds a numeric property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddBoolean` | `schema.AddBoolean(name [, required])` | property name, optional required flag. | Adds a boolean property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddObject` | `schema.AddObject(name [, required])` | property name, optional required flag. | Adds an object property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.AddArray` | `schema.AddArray(name, itemSchema [, required])` | property name, item schema, optional required flag. | Adds an array property. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Require` | `schema.Require(name)` | property name. | Marks an object property as required. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Enum` | `schema.Enum(values)` | JSON array of allowed values. | Sets the enum keyword. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Items` | `schema.Items(itemSchema)` | item schema. | Sets the array items schema. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.Clone` | `schema.Clone()` | none | Returns an independent copy of the schema. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonSchema.ToString` | `schema.ToString()` | none | Returns compact JSON Schema text. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult` | `Dim result As XPJsonValidationResult` | none | Represents JSON Schema validation output returned by `schema.Validate`. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.Valid` | `result.Valid` | none | True when validation produced no errors. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.IsValid` | `result.IsValid` | none | Alias for Valid; True when validation produced no errors. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.ErrorCount` | `result.ErrorCount` | none | Number of validation errors. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.GetError` | `result.GetError(index)` | Zero-based validation error index. | Returns one validation error as XPJsonObject with path, schemaPath, keyword, message, expected, and actual fields. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.Errors` | `result.Errors` | none | Returns validation errors as XPJsonArray. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.FailedPaths` | `result.FailedPaths` | none | Returns the unique failing data paths as XPJsonArray in validation error order. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |
| `JsonValidationResult.Json` | `result.Json` | none | Returns the complete validation result as XPJsonDocument. | [xpjsonschema-runtime.xps](../samples/xpjsonschema-runtime.xps) |


## HTTP and JSON API consumer additions

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HttpClient.Send` | `client.Send(request)` | `request`: XPHttpRequest; overloads also accept method, URL and optional body. | Sends an HTTP request through the core HTTP runtime. Request bodies use the supplied content type; JSON callers should use UTF-8 JSON. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpClient.BasicAuthorization` | `client.BasicAuthorization(username, password)` | username and password. | Builds a Basic Authorization header value once using UTF-8 credentials followed by Base64; useful when the value will be reused across request-scoped calls. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpClient.EncodePath` | `client.EncodePath(value)` | `value`: path-segment value. | Percent-encodes a path segment using UTF-8 semantics. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers` | `XPHttpClient` | none | Internal implementation surface behind the public XPHttp API; not intended to be called directly from XPScript. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers.BasicAuthorization` | `client.BasicAuthorization(username, password)` | username and password. | Internal implementation of the public Basic authorization precomputation helper. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers.AddQuery` | `client.AddQuery(url, name, value)` | URL, query name and value. | Implements the public query helper; names and values are UTF-8 percent-encoded. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers.ResponseJson` | `response.Json()` | none | Implements JSON parsing for XPHttpResponse. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers.SetBasicAuth` | `client.SetBasicAuth(username, password)` | username and password. | Implements Basic authentication using UTF-8 credentials before Base64 encoding; a colon is not permitted in the username. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpCoreHelpers.SetBearerToken` | `client.SetBearerToken(token)` | bearer token. | Implements Bearer authentication and rejects CR, LF and NUL credential characters. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest` | `Dim request As New XPHttpRequest` | none | Request-scoped HTTP method, URL, body, headers and authentication. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.Method` | `request.Method = "GET"` | HTTP method. | Sets the request method. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.Url` | `request.Url = url` | absolute HTTP/HTTPS URL. | Sets the request URL. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.Body` | `request.Body = JsonStringify(payload)` | request body. | Sets the request body. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.Headers` | `request.Headers` | none | Exposes the request-scoped header collection. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.SetHeader` | `request.SetHeader(name, value)` | header name and value. | Sets a request-scoped header and rejects unsafe framing/control characters. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.SetBearerToken` | `request.SetBearerToken(token)` | bearer token. | Sets request-scoped Bearer authorization; it replaces any existing Authorization header on that request. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.SetAuthorization` | `request.SetAuthorization(value)` | complete Authorization header value. | Applies a precomputed Authorization value to this request only; it replaces any existing Authorization header on that request. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpRequest.SetBasicAuth` | `request.SetBasicAuth(username, password)` | username and password. | Sets request-scoped Basic authorization using UTF-8 before Base64; it replaces any existing Authorization header. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `NativeHttp.CreateRequest` | `New XPHttpRequest` | none | Runtime constructor backing XPHttpRequest. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `JsonDocument.ToObject` | `document.ToObject(target)` | typed target object. | Deserializes JSON into the target XPScript model type. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `JsonObject.ToObject` | `obj.ToObject(target)` | typed target object. | Deserializes a JSON object into the target XPScript model type. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `JsonElement.ToObject` | `element.ToObject(target)` | typed target object. | Deserializes a JSON element into the target XPScript model type. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpMultipart.GetMediaTypeParameter` | `response.ContentType` | none | Internal HTTP media-type parameter parser used by response decoding. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpUiFormHelpers.SetBearerToken` | `client.SetBearerToken(token)` | bearer token. | Compatibility runtime declaration; public callers use XPHttpClient. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |
| `HttpUiFormHelpers.SetBasicAuth` | `client.SetBasicAuth(username, password)` | username and password. | Compatibility runtime declaration; public callers use XPHttpClient. | [openapi-client-consumer.xps](../samples/openapi-client-consumer.xps) |

## Application.Crypto

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `ApplicationCryptoRuntime` | `Application.Crypto` | none | Internal runtime implementation behind the public `Application.Crypto` facade for versioned authenticated string encryption. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.Encrypt` | `Application.Crypto.Encrypt(value, password [, algorithm [, context]])` | `value`: plaintext; `password`: non-empty password; optional `algorithm` and authenticated `context`. | Encrypts UTF-8 text with the current password-based authenticated-encryption profile and returns a self-describing envelope. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.Decrypt` | `Application.Crypto.Decrypt(value, password [, context])` | `value`: encrypted envelope; `password`: password; optional authenticated `context`. | Authenticates and decrypts a password-based envelope using the algorithm and parameters recorded in it. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.GenerateKey` | `Application.Crypto.GenerateKey()` | none | Generates a cryptographically random 256-bit key encoded as standard Base64. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.EncryptWithKey` | `Application.Crypto.EncryptWithKey(value, key [, context])` | `value`: plaintext; `key`: Base64-encoded 256-bit key; optional authenticated `context`. | Encrypts UTF-8 text with a supplied AES-256 key and returns a self-describing authenticated envelope. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.DecryptWithKey` | `Application.Crypto.DecryptWithKey(value, key [, context])` | `value`: encrypted envelope; `key`: Base64-encoded 256-bit key; optional authenticated `context`. | Authenticates and decrypts a key-based envelope. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.IsEncrypted` | `Application.Crypto.IsEncrypted(value)` | `value`: string to inspect. | Returns whether the value starts with an XPscript encrypted-envelope prefix. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.Algorithm` | `Application.Crypto.Algorithm(value)` | `value`: encrypted envelope. | Validates the envelope and returns its algorithm profile identifier. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.Version` | `Application.Crypto.Version(value)` | `value`: encrypted envelope. | Validates the envelope and returns its numeric format version. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.NeedsUpgrade` | `Application.Crypto.NeedsUpgrade(value)` | `value`: encrypted envelope. | Returns whether the envelope uses an older supported format, cipher or password work factor. | [application-crypto.xps](../samples/application-crypto.xps) |
| `ApplicationCryptoRuntime.ReEncrypt` | `Application.Crypto.ReEncrypt(value, oldPassword, newPassword [, context])` | `value`: password-based envelope; old and new passwords; optional authenticated `context`. | Decrypts a password-based value and creates a new envelope with the current default profile. | [application-crypto.xps](../samples/application-crypto.xps) |


## Application structured web logging

| Member | Syntax | Parameters | Behavior | Example |
|---|---|---|---|---|
| `Application.Log.Trace` | `Application.Log.Trace(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes a TRACE event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Log.Debug` | `Application.Log.Debug(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes a DEBUG event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Log.Info` | `Application.Log.Info(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes an INFO event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Log.Warning` | `Application.Log.Warning(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes a WARN event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Log.Error` | `Application.Log.Error(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes an ERROR event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Log.Critical` | `Application.Log.Critical(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes a FATAL event to the application JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `Application.Audit.Write` | `Application.Audit.Write(eventName, message [, attributes])` | Stable event name, message and optional `XPJsonObject`. | Writes an immutable-intent audit event to the security JSONL stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |


## Generated application logging runtime declarations

| Member | Syntax | Parameters | Behavior | Example |
|---|---|---|---|---|
| `ApplicationLogRuntime` | `Application.Log` | none | Structured application-log namespace used by generated programs. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Trace` | `Application.Log.Trace(eventName, message [, attributes])` | Event, message, optional attributes. | Writes TRACE. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Debug` | `Application.Log.Debug(eventName, message [, attributes])` | Event, message, optional attributes. | Writes DEBUG. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Info` | `Application.Log.Info(eventName, message [, attributes])` | Event, message, optional attributes. | Writes INFO. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Warning` | `Application.Log.Warning(eventName, message [, attributes])` | Event, message, optional attributes. | Writes WARN. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Error` | `Application.Log.Error(eventName, message [, attributes])` | Event, message, optional attributes. | Writes ERROR. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.Critical` | `Application.Log.Critical(eventName, message [, attributes])` | Event, message, optional attributes. | Writes FATAL. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationAuditRuntime` | `Application.Audit` | none | Structured security/audit namespace used by generated programs. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationAuditRuntime.Write` | `Application.Audit.Write(eventName, message [, attributes])` | Event, message, optional attributes. | Writes to the security stream. | [application-web-logging.xps](../samples/application-web-logging.xps) |

| `Application.Log.CaptureExchange` | `Application.Log.CaptureExchange = True` | Boolean request flag. | Writes a redacted, size-limited request and response capture for the current request. | [application-web-logging.xps](../samples/application-web-logging.xps) |
| `ApplicationLogRuntime.CaptureExchange` | `Application.Log.CaptureExchange` | Boolean request flag. | Generated runtime property for exchange capture. | [application-web-logging.xps](../samples/application-web-logging.xps) |
