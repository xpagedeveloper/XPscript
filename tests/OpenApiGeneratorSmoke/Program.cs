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

var optionalPresenceClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Optional Presence, version: 1.0.0 }
paths:
  /values:
    get:
      operationId: values
      parameters:
        - { name: text, in: query, schema: { type: string } }
        - { name: count, in: query, schema: { type: integer } }
        - { name: enabled, in: query, schema: { type: boolean } }
      responses:
        '204': { description: ok }
""", "optional-presence.yaml").Source;
foreach (var marker in new[] { "Optional Text As Variant = Nothing", "Optional Count As Variant = Nothing", "Optional Enabled As Variant = Nothing", "If Not Text Is Nothing Then", "If Not Count Is Nothing Then", "If Not Enabled Is Nothing Then" })
    if (!optionalPresenceClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Optional OpenAPI parameters must preserve explicit empty/zero/false values: " + marker);

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

var allOfClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: AllOf Models, version: 1.0.0 }
components:
  schemas:
    Base:
      type: object
      properties:
        id: { type: integer, format: int32 }
        name: { type: string }
    Audit:
      type: object
      properties:
        createdAt: { type: string, format: date-time }
    Combined:
      allOf:
        - { $ref: '#/components/schemas/Base' }
        - { $ref: '#/components/schemas/Audit' }
        - type: object
          properties:
            active: { type: boolean }
paths:
  /combined:
    get:
      operationId: getCombined
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Combined' }
""", "allof-models.yaml").Source;
foreach (var marker in new[] { "Public Class Combined", "[JsonName(\"id\")]", "Public Id As Integer", "[JsonName(\"name\")]", "Public Name As String", "[JsonName(\"createdAt\")]", "Public CreatedAt As Date", "[JsonName(\"active\")]", "Public Active As Boolean", "Public Combined As Combined", "ResponseType = \"Combined\"" })
    if (!allOfClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Rich allOf client model is missing inherited property/type marker: " + marker);

try
{
    _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: AllOf Conflict, version: 1.0.0 }
components:
  schemas:
    Conflict:
      allOf:
        - { type: object, properties: { value: { type: string } } }
        - { type: object, properties: { value: { type: integer } } }
paths:
  /conflict:
    get:
      responses: { '200': { description: ok, content: { application/json: { schema: { $ref: '#/components/schemas/Conflict' } } } } }
""", "allof-conflict.yaml");
    throw new Exception("Conflicting allOf model properties must be rejected.");
}
catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("conflicting XPScript types", StringComparison.OrdinalIgnoreCase)) { }

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

var collisionClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Collision, version: 1.0.0 }
paths:
  /items:
    get:
      operationId: collision
      parameters:
        - { name: foo-bar, in: query, schema: { type: string } }
        - { name: foo bar, in: header, schema: { type: string } }
      responses:
        '204': { description: ok }
""", "collision.yaml").Source;
if (!collisionClient.Contains("Optional FooBar As Variant", StringComparison.Ordinal) || !collisionClient.Contains("Optional FooBar2 As Variant", StringComparison.Ordinal))
    throw new Exception("Colliding generated parameter identifiers must receive deterministic same-scope names.");

var modelAliasClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Model Alias, version: 1.0.0 }
components:
  schemas:
    Payload:
      type: object
      properties:
        class: { type: string }
        api-key: { type: string }
        api key: { type: integer }
        PascalName: { type: boolean }
paths:
  /payload:
    get:
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Payload' }
""", "model-alias.yaml").Source;
foreach (var marker in new[] { "[JsonName(\"class\")]", "Public ApiClass As String", "[JsonName(\"api-key\")]", "Public ApiKey As String", "[JsonName(\"api key\")]", "Public ApiKey2 As Long", "[JsonName(\"PascalName\")]", "Public PascalName As Boolean" })
    if (!modelAliasClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Model property alias/collision generation is missing marker: " + marker);

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
foreach (var marker in new[] { "Optional Q As Variant = Nothing", "Optional Limit As Variant = Nothing", "Optional XTrace As Variant = Nothing", "Optional Payload As Variant = Nothing", "If Not Q Is Nothing Then url = Http.AddQuery", "If Not XTrace Is Nothing Then Call request.SetHeader", "If Not payload Is Nothing Then" })
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

var typedArrayClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Typed Model Arrays, version: 1.0.0 }
components:
  schemas:
    Child:
      type: object
      properties:
        name: { type: string }
    ArrayModel:
      type: object
      properties:
        names: { type: array, items: { type: string } }
        counts: { type: array, items: { type: integer, format: int32 } }
        children: { type: array, items: { $ref: '#/components/schemas/Child' } }
        objects: { type: array, items: { type: object } }
        nested: { type: array, items: { type: array, items: { type: string } } }
paths:
  /arrays:
    get:
      operationId: typedArrays
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ArrayModel' }
""", "typed-model-arrays.yaml").Source;
foreach (var marker in new[] { "[JsonName(\"names\")]", "Public Names() As String", "[JsonName(\"counts\")]", "Public Counts() As Integer", "[JsonName(\"children\")]", "Public Children() As Child", "[JsonName(\"objects\")]", "Public Objects As XPJsonArray", "[JsonName(\"nested\")]", "Public Nested As XPJsonArray" })
    if (!typedArrayClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Typed OpenAPI model array is missing marker: " + marker);

var securityCollisionSource = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.0.3
info: { title: Security Collision, version: 1.0.0 }
components:
  securitySchemes:
    Header:
      type: apiKey
      in: header
      name: X-Api-Key
    api-key:
      type: apiKey
      in: header
      name: X-Second-Key
    api key:
      type: apiKey
      in: header
      name: X-Third-Key
paths:
  /ping:
    get:
      operationId: setHeader
      responses:
        '204': { description: ok }
""", "security-collision.yaml").Source;
if (!securityCollisionSource.Contains("Sub SetHeader2(", StringComparison.Ordinal) ||
    !securityCollisionSource.Contains("Sub SetApiKey(", StringComparison.Ordinal) ||
    !securityCollisionSource.Contains("Sub SetApiKey2(", StringComparison.Ordinal) ||
    !securityCollisionSource.Contains("Function SetHeader3(", StringComparison.Ordinal))
    throw new Exception("Generated API member collisions must be resolved with deterministic deterministic same-scope names.");

var keywordEnumMemberRejected = false;
try { _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Keyword Enum, version: 1.0.0 }
components:
  schemas:
    State:
      type: string
      enum: [Ready, Class]
paths:
  /state:
    get:
      operationId: getState
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/State' }
""", "keyword-enum.yaml"); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("reserved XPScript keyword", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("Class", StringComparison.OrdinalIgnoreCase)) { keywordEnumMemberRejected = true; }
if (!keywordEnumMemberRejected) throw new Exception("OpenAPI enum members that map to XPScript keywords must be rejected.");

var keywordComponentClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Keyword Component, version: 1.0.0 }
components:
  schemas:
    Class:
      type: object
      properties:
        value: { type: string }
paths:
  /value:
    get:
      operationId: getValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Class' }
""", "keyword-component.yaml");
if (!keywordComponentClient.Source.Contains("Public Class ApiClass", StringComparison.Ordinal) ||
    !keywordComponentClient.Source.Contains("Public ApiClass As ApiClass", StringComparison.Ordinal))
    throw new Exception("OpenAPI component names that map to XPScript keywords must use a safe type prefix and references must use the generated type name.");

var collidingComponentClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Colliding Components, version: 1.0.0 }
components:
  schemas:
    api-key:
      type: object
      properties:
        value: { type: string }
    api key:
      type: object
      properties:
        count: { type: integer }
paths:
  /value:
    get:
      operationId: getValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/api key' }
""", "colliding-components.yaml");
if (!collidingComponentClient.Source.Contains("Public Class ApiKey", StringComparison.Ordinal) ||
    !collidingComponentClient.Source.Contains("Public Class ApiKey2", StringComparison.Ordinal) ||
    !collidingComponentClient.Source.Contains("Public ApiKey2 As ApiKey2", StringComparison.Ordinal))
    throw new Exception("Colliding OpenAPI component identifiers must receive deterministic same-scope names while references preserve the original schema identity. Generated source:\n" + collidingComponentClient.Source);

var keywordPropertyClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Keyword Property, version: 1.0.0 }
components:
  schemas:
    Item:
      type: object
      properties:
        end: { type: string }
paths:
  /value:
    get:
      operationId: getValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Item' }
""", "keyword-property.yaml");
if (!keywordPropertyClient.Source.Contains("[JsonName(\"end\")]", StringComparison.Ordinal) ||
    !keywordPropertyClient.Source.Contains("Public ApiEnd As String", StringComparison.Ordinal))
    throw new Exception("OpenAPI model properties that map to XPScript lexical keywords must use a safe identifier while preserving their JSON wire name.");

var reservedApiClass = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Reserved API Class, version: 1.0.0 }
paths:
  /value:
    get:
      operationId: getValue
      responses:
        '204': { description: ok }
""", "reserved-api-class.yaml", "Class");
if (!string.Equals(reservedApiClass.ClassName, "ApiClass", StringComparison.Ordinal) ||
    !reservedApiClass.Source.Contains("Public Class ApiClass", StringComparison.Ordinal))
    throw new Exception("Requested API class names that are reserved XPScript keywords must use a safe type prefix.");

var keywordOperationClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Keyword Operation, version: 1.0.0 }
paths:
  /value:
    get:
      operationId: class
      responses:
        '204': { description: ok }
  /other:
    get:
      operationId: class_2
      responses:
        '204': { description: ok }
""", "keyword-operation.yaml");
if (!keywordOperationClient.Source.Contains("Public Function ApiClass(", StringComparison.Ordinal) ||
    !keywordOperationClient.Source.Contains("Public Function Class_2(", StringComparison.Ordinal))
    throw new Exception("Lexical-keyword OpenAPI operations must use a safe name while real same-scope collisions remain deterministic.");

var parameterSuffixSource = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Parameter Suffix, version: 1.0.0 }
paths:
  /value:
    post:
      operationId: getValue
      parameters:
        - { name: end, in: query, schema: { type: string } }
        - { name: api-key, in: query, schema: { type: string } }
        - { name: api key, in: header, schema: { type: string } }
        - { name: url, in: query, schema: { type: string } }
        - { name: payload, in: query, schema: { type: string } }
      requestBody:
        content:
          application/json:
            schema: { type: string }
      responses:
        '204': { description: ok }
""", "parameter-suffix.yaml").Source;
if (!parameterSuffixSource.Contains("Optional ApiEnd As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Optional ApiKey As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Optional ApiKey2 As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Optional Url As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Optional payload As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Dim ApiUrl As String", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("Optional ApiPayload As Variant", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("ApiUrl = Http.AddQuery(ApiUrl, \"end\", ApiEnd)", StringComparison.Ordinal) ||
    !parameterSuffixSource.Contains("request.SetHeader(\"api key\", CStr(ApiKey2))", StringComparison.Ordinal))
    throw new Exception("OpenAPI parameter identifiers must preserve names unless a real procedure-scope collision requires disambiguation. Generated source:\n" + parameterSuffixSource);

var generatedTypeCollisionRejected = false;
try { _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Type Collision, version: 1.0.0 }
components:
  schemas:
    TypeCollisionApiResponse:
      type: object
      properties:
        value: { type: string }
paths:
  /value:
    get:
      operationId: getValue
      responses:
        '200': { description: ok }
""", "type-collision.yaml", "TypeCollisionApi"); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("generated type", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("TypeCollisionApiResponse", StringComparison.OrdinalIgnoreCase)) { generatedTypeCollisionRejected = true; }
if (!generatedTypeCollisionRejected) throw new Exception("OpenAPI component names that collide with generated API types must be rejected.");

var apiMemberCollisionSource = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Member Collision, version: 1.0.0 }
paths:
  /header:
    get:
      operationId: setHeader
      responses:
        '204': { description: ok }
""", "api-member-collision.yaml").Source;
if (!apiMemberCollisionSource.Contains("Function SetHeader2(", StringComparison.Ordinal))
    throw new Exception("OpenAPI operation names that collide with generated API members must receive a deterministic same-scope name.");

var authMemberCollisionSource = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Auth Member Collision, version: 1.0.0 }
components:
  securitySchemes:
    Token:
      type: http
      scheme: bearer
paths:
  /auth:
    get:
      operationId: setToken
      responses:
        '204': { description: ok }
""", "auth-member-collision.yaml").Source;
if (!authMemberCollisionSource.Contains("Sub SetToken(", StringComparison.Ordinal) ||
    !authMemberCollisionSource.Contains("Function SetToken2(", StringComparison.Ordinal))
    throw new Exception("OpenAPI operation names that collide with generated auth setter members must receive a deterministic same-scope name.");

var scalarResponseCollisionClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Scalar Response Collision, version: 1.0.0 }
components:
  schemas:
    StringValue:
      type: object
      properties:
        value: { type: string }
paths:
  /text:
    get:
      operationId: textValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { type: string }
  /model:
    get:
      operationId: modelValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StringValue' }
""", "scalar-response-collision.yaml").Source;
if (!scalarResponseCollisionClient.Contains("Public StringValue As StringValue", StringComparison.Ordinal) ||
    !scalarResponseCollisionClient.Contains("Public StringValue2 As String", StringComparison.Ordinal) ||
    !scalarResponseCollisionClient.Contains("result.StringValue2 = result.Json.ToObject(\"\")", StringComparison.Ordinal))
    throw new Exception("Scalar response value fields that collide with model response fields must receive a deterministic same-scope name.");

var scalarResponseClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Scalar Responses, version: 1.0.0 }
paths:
  /name:
    get:
      operationId: getName
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { type: string }
""", "scalar-responses.yaml").Source;
foreach (var marker in new[] { "Public StringValue As String", "result.StringValue = result.Json.ToObject(\"\")" })
    if (!scalarResponseClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("OpenAPI scalar response is missing typed value marker: " + marker);

var refSiblingClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Ref Siblings, version: 1.0.0 }
components:
  schemas:
    BaseModel:
      type: object
      properties:
        baseValue: { type: string }
    ExtendedModel:
      $ref: '#/components/schemas/BaseModel'
      properties:
        extraValue: { type: integer, format: int32 }
paths:
  /extended:
    get:
      operationId: extendedValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ExtendedModel' }
""", "ref-siblings.yaml").Source;
foreach (var marker in new[] { "Public Class ExtendedModel", "[JsonName(\"baseValue\")]", "Public BaseValue As String", "[JsonName(\"extraValue\")]", "Public ExtraValue As Integer" })
    if (!refSiblingClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("OpenAPI 3.1 $ref sibling model is missing marker: " + marker);

var responseCollisionClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Response Collision, version: 1.0.0 }
components:
  schemas:
    StatusCode:
      type: object
      properties:
        value: { type: string }
paths:
  /collision:
    get:
      operationId: responseCollision
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StatusCode' }
""", "response-collision.yaml").Source;
if (!responseCollisionClient.Contains("Public StatusCode2 As StatusCode", StringComparison.Ordinal) ||
    !responseCollisionClient.Contains("result.StatusCode2 = result.Json.ToObject(mappedStatusCode)", StringComparison.Ordinal))
    throw new Exception("Response model names that collide with reserved response envelope members must receive a deterministic same-scope name.");

var dictionaryClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Dictionary Models, version: 1.0.0 }
components:
  schemas:
    Labels:
      type: object
      additionalProperties: { type: string }
    DictionaryModel:
      type: object
      properties:
        labels:
          type: object
          additionalProperties: { type: string }
        children:
          type: object
          additionalProperties: { $ref: '#/components/schemas/Child' }
    Child:
      type: object
      properties:
        name: { type: string }
paths:
  /dictionary:
    get:
      operationId: dictionaryValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/DictionaryModel' }
""", "dictionary-models.yaml").Source;
foreach (var marker in new[] { "Public Class Labels", "Public Value As XPJsonObject", "[JsonName(\"labels\")]", "Public Labels As XPJsonObject", "[JsonName(\"children\")]", "Public Children As XPJsonObject" })
    if (!dictionaryClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("OpenAPI additionalProperties model is missing marker: " + marker);

var enumClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Typed Enums, version: 1.0.0 }
components:
  schemas:
    PetStatus:
      type: string
      enum: [Available, Pending, Sold]
    EnumModel:
      type: object
      properties:
        status: { $ref: '#/components/schemas/PetStatus' }
        history:
          type: array
          items: { $ref: '#/components/schemas/PetStatus' }
paths:
  /enum:
    get:
      operationId: enumValue
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/EnumModel' }
""", "typed-enums.yaml").Source;
foreach (var marker in new[] { "Enum PetStatus", "    Available", "    Pending", "    Sold", "End Enum", "[JsonName(\"status\")]", "Public Status As PetStatus", "[JsonName(\"history\")]", "Public History() As PetStatus" })
    if (!enumClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Typed OpenAPI enum is missing marker: " + marker);

var badEnumValue = false;
try { _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Bad Enum, version: 1.0.0 }
components:
  schemas:
    BadStatus:
      type: string
      enum: [in-progress]
paths:
  /enum:
    get:
      operationId: badEnum
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/BadStatus' }
"""); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("cannot be represented losslessly", StringComparison.OrdinalIgnoreCase)) { badEnumValue = true; }
if (!badEnumValue) throw new Exception("String enum values that cannot be represented losslessly must be rejected.");

var badNumericEnum = false;
try { _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Numeric Enum, version: 1.0.0 }
components:
  schemas:
    NumericStatus:
      type: integer
      enum: [1, 2]
paths:
  /enum:
    get:
      operationId: numericEnum
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/NumericStatus' }
"""); } catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("must use type: string", StringComparison.OrdinalIgnoreCase)) { badNumericEnum = true; }
if (!badNumericEnum) throw new Exception("Numeric OpenAPI enums must be rejected until lossless wire serialization is supported.");

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

var scopeCollision = generator.Generate("""
openapi: 3.1.0
info:
  title: Scope Collision
  version: 1.0.0
components:
  schemas:
    CollisionModel:
      type: object
      properties:
        JsonParse: { type: string }
        StrLeftBack: { type: string }
paths:
  /collision:
    get:
      operationId: collision
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CollisionModel'
""", "scope-collision.yaml");
foreach (var marker in new[] { "Public JsonParse As String", "Public StrLeftBack As String" })
    if (!scopeCollision.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Generated OpenAPI scope-collision model is missing member: " + marker);

var crossScopeRuntimeNames = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Cross Scope Runtime Names, version: 1.0.0 }
components:
  schemas:
    First:
      type: object
      properties:
        JsonParse: { type: string }
    Second:
      type: object
      properties:
        jsonparse: { type: string }
paths:
  /runtime:
    get:
      operationId: JsonParse
      parameters:
        - { name: StrLeftBack, in: query, schema: { type: string } }
      responses:
        '204': { description: ok }
""", "cross-scope-runtime-names.yaml");
foreach (var marker in new[] { "Public JsonParse As String", "Public Jsonparse As String", "Public Function JsonParse(", "Optional StrLeftBack As Variant" })
    if (!crossScopeRuntimeNames.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Runtime/global identifier was renamed even though its declaration scope permits it: " + marker);

var compilerReservedClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Compiler Reserved, version: 1.0.0 }
components:
  schemas:
    Reserved:
      type: object
      properties:
        __state: { type: string }
paths:
  /reserved:
    get:
      operationId: __dispatch
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Reserved' }
""", "compiler-reserved.yaml");
foreach (var marker in new[] { "[JsonName(\"__state\")]", "Public Api__state As String", "Public Function Api__dispatch(" })
    if (!compilerReservedClient.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Compiler-reserved __ identifier was not protected: " + marker);

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
    var scopeCollisionPath = Path.Combine(root, "scope-collision.xps");
    await File.WriteAllTextAsync(scopeCollisionPath, scopeCollision.Source);
    var crossScopeRuntimePath = Path.Combine(root, "cross-scope-runtime-names.xps");
    await File.WriteAllTextAsync(crossScopeRuntimePath, crossScopeRuntimeNames.Source);
    var compilerReservedPath = Path.Combine(root, "compiler-reserved.xps");
    await File.WriteAllTextAsync(compilerReservedPath, compilerReservedClient.Source);

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
    await using (var crossScopeRuntimeUnit = await compiler.CompileAsync(crossScopeRuntimePath, root)) { }
    await using (var compilerReservedUnit = await compiler.CompileAsync(compilerReservedPath, root)) { }
    await using (var scopeCollisionUnit = await compiler.CompileAsync(scopeCollisionPath, root))
    {
        if (!scopeCollisionUnit.Routes.ContainsKey("EndpointCollision"))
            throw new Exception("Generated OpenAPI scope-collision XPScript did not compile into the expected REST route.");
    }
    await using (var unit = await compiler.CompileAsync(sourcePath, root))
    {
        if (!unit.Routes.ContainsKey("EndpointGetPet") || !unit.Routes.ContainsKey("EndpointCreatePet"))
            throw new Exception("Generated XPScript did not compile into the expected REST routes.");
    }

    const string getPetOriginal = "    result.StatusCode = 501\n    Set HandleGetPet = result";
    const string getPetEdited = "    Print \"fråga funktionen GetPet\"\n    result.StatusCode = 200\n    result.Data = \"custom-get\"\n    Set HandleGetPet = result";
    const string createPetOriginal = "    result.StatusCode = 501\n    Set HandleCreatePet = result";
    const string createPetEdited = "    Print \"fråga funktionen CreatePet\"\n    result.StatusCode = 201\n    result.Data = \"custom-create\"\n    Set HandleCreatePet = result";

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
    Console.WriteLine("OPENAPI-SCOPE-COLLISION-COMPILE=OK");
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
