using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var fixture = Path.Combine(AppContext.BaseDirectory, "petstore.yaml");
var reimportFixture = Path.Combine(AppContext.BaseDirectory, "petstore-reimport.yaml");
var generator = new XpsOpenApiGenerator();
var result = generator.GenerateFile(fixture);
var clientResult = new XpsOpenApiClientGenerator().GenerateFile(fixture);
var securityClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Security Smoke, version: 1.0.0 }
components:
  securitySchemes:
    BearerAuth: { type: http, scheme: bearer }
    BasicAuth: { type: http, scheme: basic }
    ApiKey: { type: apiKey, in: header, name: X-API-Key }
security:
  - BearerAuth: []
  - BasicAuth: []
paths:
  /secure:
    get:
      operationId: secure
      security:
        - BearerAuth: []
          ApiKey: []
        - BasicAuth: []
      responses:
        '200': { description: ok }
  /public:
    get:
      operationId: publicCall
      security: []
      responses:
        '204': { description: ok }
""", "security.yaml").Source;
foreach (var marker in new[] { "request.SetBearerToken(AuthBearerAuth)", "request.SetAuthorization(AuthBasicAuthAuthorization)", "request.SetHeader(\"X-API-Key\", AuthApiKey)", "ElseIf", "Public Function PublicCall" })
    if (!securityClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Generated OpenAPI security client is missing marker: " + marker);
if (!securityClient.Contains("Private AuthBasicAuthAuthorization As String", StringComparison.Ordinal) ||
    !securityClient.Contains("AuthBasicAuthAuthorization = Http.BasicAuthorization(username, password)", StringComparison.Ordinal) ||
    securityClient.Contains("Public AuthBasicAuthAuthorization", StringComparison.Ordinal) ||
    securityClient.Contains("AuthBasicAuthUsername", StringComparison.Ordinal) ||
    securityClient.Contains("AuthBasicAuthPassword", StringComparison.Ordinal))
    throw new Exception("Generated Basic auth must store only a private precomputed Authorization value.");

var publicStart = securityClient.IndexOf("Public Function PublicCall", StringComparison.Ordinal);
var publicEnd = securityClient.IndexOf("End Function", publicStart, StringComparison.Ordinal);
var publicSource = securityClient[publicStart..publicEnd];
if (publicSource.Contains("SetBearerToken", StringComparison.Ordinal) || publicSource.Contains("SetBasicAuth", StringComparison.Ordinal) || publicSource.Contains("X-API-Key", StringComparison.Ordinal))
    throw new Exception("security: [] must not inherit authentication.");

try
{
    _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Conflict, version: 1.0.0 }
components:
  securitySchemes:
    BearerAuth: { type: http, scheme: bearer }
    BasicAuth: { type: http, scheme: basic }
security:
  - BearerAuth: []
    BasicAuth: []
paths:
  /bad:
    get:
      operationId: bad
      responses:
        '200': { description: ok }
""", "conflict.yaml");
    throw new Exception("Bearer+Basic AND security must be rejected.");
}
catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("Authorization header", StringComparison.OrdinalIgnoreCase)) { }

var nestedRefClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Nested Ref, version: 1.0.0 }
components:
  schemas:
    Child:
      type: object
      required: [name]
      properties: { name: { type: string } }
    Parent:
      type: object
      required: [child]
      properties:
        child: { $ref: '#/components/schemas/Child' }
paths:
  /parent:
    get:
      operationId: parent
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Parent' }
""", "nested-ref.yaml").Source;
if (!nestedRefClient.Contains("#/$defs/Child", StringComparison.Ordinal) || nestedRefClient.Contains("#/components/schemas/Child", StringComparison.Ordinal))
    throw new Exception("Generated response validation schema must rewrite nested OpenAPI component references to standalone JSON Schema $defs.");

var compositionClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Composition, version: 1.0.0 }
paths:
  /choice:
    post:
      operationId: choose
      requestBody:
        required: true
        content:
          application/json:
            schema:
              oneOf:
                - { type: string }
                - { type: integer }
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema:
                anyOf:
                  - { type: string }
                  - { type: integer }
""", "composition.yaml").Source;
if (!compositionClient.Contains("payload As Variant", StringComparison.Ordinal) || !compositionClient.Contains("ResponseType = \"Variant\"", StringComparison.Ordinal))
    throw new Exception("Mixed oneOf/anyOf schemas must use conservative Variant typing.");

var compositionServer = new XpsOpenApiGenerator().Generate("""
openapi: 3.1.0
info: { title: Composition Server, version: 1.0.0 }
paths:
  /combined:
    post:
      operationId: combined
      requestBody:
        required: true
        content:
          application/json:
            schema:
              allOf:
                - { type: object, properties: { a: { type: string } } }
                - { type: object, properties: { b: { type: integer } } }
      responses:
        '200': { description: ok }
""", "composition-server.yaml").Source;
if (!compositionServer.Contains("As XPJsonObject", StringComparison.Ordinal))
    throw new Exception("Object allOf schemas must use XPJsonObject.");

var jsonTypesServer = new XpsOpenApiGenerator().Generate("""
openapi: 3.1.0
info: { title: JSON Types Server, version: 1.0.0 }
paths:
  /values:
    post:
      operationId: jsonTypes
      requestBody:
        required: true
        content:
          application/json:
            schema: { type: array, items: { type: string } }
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { type: object, additionalProperties: true }
""", "json-types-server.yaml").Source;
if (!jsonTypesServer.Contains("As XPJsonArray", StringComparison.Ordinal) || !jsonTypesServer.Contains("OpenAPI responses: 200 XPJsonObject", StringComparison.Ordinal))
    throw new Exception("REST server generation must reuse public XPJsonArray/XPJsonObject types.");

var overrideServer = new XpsOpenApiGenerator().Generate("""
openapi: 3.1.0
info: { title: Server Override, version: 1.0.0 }
paths:
  /items:
    parameters:
      - { name: q, in: query, required: false, schema: { type: string } }
    get:
      operationId: searchItemsServer
      parameters:
        - { name: q, in: query, required: true, schema: { type: integer, format: int32 } }
      responses:
        '204': { description: ok }
""", "server-override.yaml").Source;
if (!overrideServer.Contains("Q As Integer", StringComparison.Ordinal) || overrideServer.Contains("Q As String", StringComparison.Ordinal))
    throw new Exception("REST server operation-level parameters must override matching path-level parameters.");

var badServerPathRequired = false;
try { _ = new XpsOpenApiGenerator().Generate("""
openapi: 3.1.0
info: { title: Bad Server Path, version: 1.0.0 }
paths:
  /items/{id}:
    get:
      parameters:
        - { name: id, in: path, schema: { type: string } }
      responses: { '204': { description: ok } }
"""); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("required: true", StringComparison.Ordinal)) { badServerPathRequired = true; }
if (!badServerPathRequired) throw new Exception("REST server path parameters must require required: true.");

var overrideClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Override, version: 1.0.0 }
paths:
  /items:
    parameters:
      - { name: q, in: query, required: false, schema: { type: string } }
    get:
      operationId: searchItems
      parameters:
        - { name: q, in: query, required: true, schema: { type: integer, format: int32 } }
      responses:
        '204': { description: ok }
""", "override.yaml").Source;
if (!overrideClient.Contains("Public Function SearchItems(Q As Integer)", StringComparison.Ordinal) || overrideClient.Contains("Q As String", StringComparison.Ordinal))
    throw new Exception("Operation-level OpenAPI parameters must override matching path-level parameters.");

try
{
    _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Collision, version: 1.0.0 }
paths:
  /items:
    get:
      operationId: collision
      parameters:
        - { name: foo-bar, in: query, schema: { type: string } }
        - { name: foo.bar, in: query, schema: { type: string } }
      responses:
        '204': { description: ok }
""", "collision.yaml");
    throw new Exception("Colliding generated parameter identifiers must be rejected.");
}
catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("both map to XPScript identifier", StringComparison.OrdinalIgnoreCase)) { }

var optionalClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Optional, version: 1.0.0 }
components:
  schemas:
    Filter:
      type: object
      properties:
        name: { type: string }
paths:
  /items:
    post:
      operationId: optionalValues
      parameters:
        - { name: q, in: query, schema: { type: string } }
        - { name: limit, in: query, schema: { type: integer, format: int32 } }
        - { name: X-Trace, in: header, schema: { type: string } }
      requestBody:
        required: false
        content:
          application/json:
            schema: { $ref: '#/components/schemas/Filter' }
      responses:
        '204': { description: ok }
""", "optional.yaml").Source;
foreach (var marker in new[] { "Optional Q As String = \"\"", "Optional Limit As Integer = 0", "Optional XTrace As String = \"\"", "Optional payload As Filter = Nothing", "If Len(Q) > 0 Then url = Http.AddQuery", "If Len(XTrace) > 0 Then Call request.SetHeader", "If Not payload Is Nothing Then" })
    if (!optionalClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Generated optional OpenAPI values are missing marker: " + marker);

var arrayClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Arrays, version: 1.0.0 }
paths:
  /items:
    post:
      operationId: arrayValues
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: array
              items: { type: string }
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema:
                type: array
                items: { type: integer }
""", "arrays.yaml").Source;
if (!arrayClient.Contains("payload As XPJsonArray", StringComparison.Ordinal) ||
    !arrayClient.Contains("ResponseType = \"XPJsonArray\"", StringComparison.Ordinal) ||
    !arrayClient.Contains("XPJsonSchema.Parse(", StringComparison.Ordinal))
    throw new Exception("OpenAPI arrays must use XPJsonArray and XPJsonSchema.");

var nullableClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Nullable, version: 1.0.0 }
paths:
  /value:
    post:
      operationId: nullableValue
      requestBody:
        required: true
        content:
          application/json:
            schema: { type: [string, 'null'] }
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { type: [integer, 'null'] }
""", "nullable.yaml").Source;
if (!nullableClient.Contains("payload As String", StringComparison.Ordinal) || !nullableClient.Contains("ResponseType = \"Long\"", StringComparison.Ordinal))
    throw new Exception("OpenAPI 3.1 nullable type unions must preserve their non-null XPScript type.");

var badPathRequired = false;
try { _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: BadPath, version: 1.0.0 }
paths:
  /items/{id}:
    get:
      parameters:
        - { name: id, in: path, schema: { type: string } }
      responses: { '204': { description: ok } }
"""); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("required: true", StringComparison.Ordinal)) { badPathRequired = true; }
if (!badPathRequired) throw new Exception("OpenAPI path parameters must require required: true.");

var unicodeClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Unicode, version: 1.0.0 }
paths:
  /cities/{city}:
    get:
      operationId: unicodePath
      parameters:
        - { name: city, in: path, required: true, schema: { type: string } }
        - { name: q, in: query, schema: { type: string } }
      responses:
        '204': { description: ok }
""", "unicode.yaml").Source;
if (!unicodeClient.Contains("Http.EncodePath(City)", StringComparison.Ordinal) || !unicodeClient.Contains("Http.AddQuery(url, \"q\", Q)", StringComparison.Ordinal))
    throw new Exception("OpenAPI Unicode path/query values must flow through XPHttp UTF-8 encoding helpers.");

if (!clientResult.Source.Contains("Public Validation As XPJsonValidationResult", StringComparison.Ordinal) ||
    !clientResult.Source.Contains("XPJsonSchema.Parse(", StringComparison.Ordinal) ||
    !clientResult.Source.Contains(".Validate(result.Json)", StringComparison.Ordinal))
    throw new Exception("Generated OpenAPI JSON responses must expose XPJsonSchema validation results.");

if (clientResult.Source.Contains("UIForm", StringComparison.OrdinalIgnoreCase) || clientResult.Source.Contains("XPScriptHttpUiFormHelpers", StringComparison.Ordinal))
    throw new Exception("Generated OpenAPI client must not depend on UIForm runtime.");
foreach (var marker in new[] { "XPHttpClient", "XPHttpRequest", "XPHttpResponse", "XPJsonDocument", "Http.Send(request)" })
    if (!clientResult.Source.Contains(marker, StringComparison.Ordinal)) throw new Exception("Generated OpenAPI client is missing core API marker: " + marker);

if (result.OpenApiVersion != "3.1.0") throw new Exception("OpenAPI version was not retained.");
if (result.Operations.Count != 2 || !result.Operations.Contains("GetPet") || !result.Operations.Contains("CreatePet"))
    throw new Exception("Expected OpenAPI operations were not generated.");
if (!result.Models.Contains("Pet") || !result.Models.Contains("CreatePet") || !result.Models.Contains("ApiError"))
    throw new Exception("Expected OpenAPI component schemas were not generated.");

foreach (var marker in new[]
{
    "Public Class Pet",
    "[Required]",
    "[MaxLength:100]",
    "[Email]",
    "[Range:0;40]",
    "Public Class GetPetRequest",
    "Public Class GetPetResponse",
    "Function HandleGetPet(request As GetPetRequest) As GetPetResponse",
    "Function HandleCreatePet(request As CreatePetRequest) As CreatePetResponse",
    "Dim request As GetPetRequest",
    "Set request = New GetPetRequest",
    "Dim result As GetPetResponse",
    "Set result = New GetPetResponse",
    "Sub EndpointGetPet(",
    "Sub EndpointCreatePet(",
    "[Route:/pets/{petId}]",
    "[FromRoute:\"petId\"] pPetId As Long",
    "[FromQuery:\"includeHistory\"] pIncludeHistory As Boolean",
    "[FromHeader:\"X-Request-Id\"] pXRequestId As String",
    "[FromBody] payload As CreatePet",
    "Set result = HandleGetPet(request)",
    "Response.Json(result.StatusCode, result.Data)"
})
{
    if (!result.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Generated XPScript is missing expected marker: " + marker);
}

var openApi30 = generator.Generate("""
openapi: 3.0.3
info:
  title: Compatibility Smoke
  version: 1.0.0
paths:
  /health:
    get:
      operationId: health
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema:
                type: string
""", "openapi30.yaml");
if (openApi30.OpenApiVersion != "3.0.3" || !openApi30.Operations.Contains("Health"))
    throw new Exception("OpenAPI 3.0 compatibility generation failed.");

var root = Path.Combine(Path.GetTempPath(), "xps-openapi-generator-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var clientPath = Path.Combine(root, "petstore-client.xps");
    await File.WriteAllTextAsync(clientPath, clientResult.Source);

    var sourcePath = Path.Combine(root, "petstore.xps");
    await File.WriteAllTextAsync(sourcePath, result.Source);

    var parsed = new XpsWebRouteMetadataParser().Parse(result.Source);
    if (parsed.Routes["EndpointGetPet"].RouteTemplate != "/pets/{petId}")
        throw new Exception("Generated GET route metadata did not match the OpenAPI path.");
    if (parsed.Routes["EndpointGetPet"].ParameterBindings?.Count != 3)
        throw new Exception("Generated GET parameter bindings did not match the OpenAPI parameters.");
    if (parsed.Routes["EndpointCreatePet"].ParameterBindings?.Count != 1)
        throw new Exception("Generated POST body binding did not match the OpenAPI requestBody.");

    var compiler = new XpsWebCompiler();
    await using (var unit = await compiler.CompileAsync(sourcePath, root))
    {
        if (!unit.Routes.ContainsKey("EndpointGetPet") || !unit.Routes.ContainsKey("EndpointCreatePet"))
            throw new Exception("Generated XPScript did not compile into the expected REST routes.");
    }

    const string getPetOriginal = "    result.StatusCode = 501\n    HandleGetPet = result";
    const string getPetEdited = "    Print \"fråga funktionen GetPet\"\n    result.StatusCode = 200\n    result.Data = \"custom-get\"\n    HandleGetPet = result";
    const string createPetOriginal = "    result.StatusCode = 501\n    HandleCreatePet = result";
    const string createPetEdited = "    Print \"fråga funktionen CreatePet\"\n    result.StatusCode = 201\n    result.Data = \"custom-create\"\n    HandleCreatePet = result";

    var userEdited = result.Source
        .Replace(getPetOriginal, getPetEdited, StringComparison.Ordinal)
        .Replace(createPetOriginal, createPetEdited, StringComparison.Ordinal);

    foreach (var printMarker in new[]
    {
        "Print \"fråga funktionen GetPet\"",
        "Print \"fråga funktionen CreatePet\""
    })
    {
        if (!userEdited.Contains(printMarker, StringComparison.Ordinal))
            throw new Exception("Smoke setup failed to add generated handler print line: " + printMarker);
    }

    await File.WriteAllTextAsync(sourcePath, userEdited);
    await using (var editedUnit = await compiler.CompileAsync(sourcePath, root))
    {
        if (!editedUnit.Routes.ContainsKey("EndpointGetPet") || !editedUnit.Routes.ContainsKey("EndpointCreatePet"))
            throw new Exception("Manually edited generated XPScript did not compile before reimport.");
    }

    var importResult = new XpsOpenApiImporter().ImportFile(reimportFixture, userEdited);

    foreach (var preserved in new[]
    {
        "Print \"fråga funktionen GetPet\"",
        "Print \"fråga funktionen CreatePet\"",
        "result.Data = \"custom-get\"",
        "result.Data = \"custom-create\"",
        "Public name As String",
        "Sub EndpointGetPet([FromRoute:\"petId\"] pPetId As Long, [FromQuery:\"includeHistory\"] pIncludeHistory As Boolean, [FromHeader:\"X-Request-Id\"] pXRequestId As String)"
    })
    {
        if (!importResult.Source.Contains(preserved, StringComparison.Ordinal))
            throw new Exception("Additive import changed or removed existing source: " + preserved);
    }

    foreach (var added in new[]
    {
        "Public microchip As String",
        "Public status As String",
        "Public source As String",
        "Public externalId As String",
        "Public traceId As String",
        "Public Expand As String",
        "Public Class UpdatePet",
        "Public Class UpdatePetRequest",
        "Public Class UpdatePetResponse",
        "Function HandleUpdatePet(request As UpdatePetRequest) As UpdatePetResponse",
        "Sub EndpointUpdatePet(",
        "Function HandleListPets(request As ListPetsRequest) As ListPetsResponse",
        "Sub EndpointListPets(",
        "Function HandleDeletePet(request As DeletePetRequest) As DeletePetResponse",
        "Sub EndpointDeletePet(",
        "Sub WriteUpdatePetResponse(result As UpdatePetResponse)",
        "Sub WriteListPetsResponse(result As ListPetsResponse)",
        "Sub WriteDeletePetResponse(result As DeletePetResponse)"
    })
    {
        if (!importResult.Source.Contains(added, StringComparison.Ordinal))
            throw new Exception("Additive import did not add expected declaration: " + added);
    }

    if (importResult.Source.Contains("pExpand As String", StringComparison.Ordinal))
        throw new Exception("Additive import rewrote the existing GetPet endpoint signature.");
    if (!importResult.Warnings.Any(warning => warning.Contains("Pet.name", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Expected changed existing property type to produce a drift warning.");
    if (!importResult.Warnings.Any(warning => warning.Contains("EndpointGetPet", StringComparison.Ordinal)))
        throw new Exception("Expected changed existing endpoint signature to produce a drift warning.");

    var importedPath = Path.Combine(root, "petstore-imported.xps");
    await File.WriteAllTextAsync(importedPath, importResult.Source);
    await using (var importedUnit = await compiler.CompileAsync(importedPath, root))
    {
        foreach (var route in new[]
        {
            "EndpointGetPet",
            "EndpointCreatePet",
            "EndpointUpdatePet",
            "EndpointListPets",
            "EndpointDeletePet"
        })
        {
            if (!importedUnit.Routes.ContainsKey(route))
                throw new Exception("Additively imported XPScript did not compile expected REST route: " + route);
        }
    }

    foreach (var marker in new[] { "OPENAPI-CLIENT-SECURITY=OK", "OPENAPI-CLIENT-CORE-ONLY=OK" })
        Console.WriteLine(marker);
    Console.WriteLine("OPENAPI-CLIENT-COMPILE=OK");
    Console.WriteLine("OPENAPI-3.0-GENERATOR=OK");
    Console.WriteLine("OPENAPI-3.1-YAML-GENERATOR=OK");
    Console.WriteLine("OPENAPI-GENERATED-XPS-COMPILE=OK");
    Console.WriteLine("OPENAPI-EDITED-HANDLERS-COMPILE=OK");
    Console.WriteLine("OPENAPI-PRINT-PRESERVATION=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-PRESERVE=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-NEW-OPERATIONS=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-COMPILE=OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
