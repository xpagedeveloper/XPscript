namespace XPScript.Compiler;

public sealed record CompilerSymbolParameter(string Name, string Type);

public sealed record CompilerSymbolDefinition(
    string Name,
    string Kind,
    string Signature,
    IReadOnlyList<CompilerSymbolParameter> Parameters,
    string? ReturnType,
    IReadOnlyList<string> AllowedTargets,
    string DocumentationId,
    bool Deprecated = false);

public static class CompilerSymbolCatalog
{
    // This is the compiler-owned public symbol table used by resolution-facing machine APIs.
    // Diagnostics/candidate lookup must reuse this table rather than maintain an AI-only catalog.
    private static readonly IReadOnlyDictionary<string, CompilerSymbolDefinition> Definitions =
        new[]
        {
            Define("XPJsonSchema", "class", "XPJsonSchema", [], null, "api.XPJsonSchema"),
            Define("XPJsonSchema.Parse", "method", "XPJsonSchema.Parse(text As String) As XPJsonSchema",
                [new("text", "String")], "XPJsonSchema", "api.XPJsonSchema.Parse"),
            Define("XPJsonSchema.FromJson", "method", "XPJsonSchema.FromJson(value As Variant) As XPJsonSchema",
                [new("value", "Variant")], "XPJsonSchema", "api.XPJsonSchema.FromJson"),
            Define("XPJsonSchema.Type", "property", "XPJsonSchema.Type As String", [], "String", "api.XPJsonSchema.Type"),
            Define("XPJsonSchema.AdditionalProperties", "property", "XPJsonSchema.AdditionalProperties As Boolean", [], "Boolean", "api.XPJsonSchema.AdditionalProperties"),
            Define("XPJsonSchema.AddString", "method", "XPJsonSchema.AddString(name As String, required As Boolean)", [new("name", "String"), new("required", "Boolean")], "XPJsonSchema", "api.XPJsonSchema.AddString"),
            Define("XPJsonSchema.Validate", "method", "XPJsonSchema.Validate(json As Variant) As XPJsonValidationResult",
                [new("json", "Variant")], "XPJsonValidationResult", "api.XPJsonSchema.Validate"),
            Define("XPJsonSchema.IsValid", "method", "XPJsonSchema.IsValid(json As Variant) As Boolean",
                [new("json", "Variant")], "Boolean", "api.XPJsonSchema.IsValid"),
            Define("UIForm.SetValidationSchema", "method", "UIForm.SetValidationSchema(schema As XPJsonSchema)",
                [new("schema", "XPJsonSchema")], null, "api.UIForm.SetValidationSchema"),
            Define("UIForm.ValidateData", "method", "UIForm.ValidateData() As XPJsonValidationResult",
                [], "XPJsonValidationResult", "api.UIForm.ValidateData"),
            Define("XPHttpClient", "class", "XPHttpClient", [], null, "api.XPHttpClient"),
            Define("XPHttpClient.Get", "method", "XPHttpClient.Get(url As String) As XPHttpResponse",
                [new("url", "String")], "XPHttpResponse", "api.XPHttpClient.Get"),
            Define("XPHttpClient.Post", "method", "XPHttpClient.Post(url As String, body As Variant, contentType As String)",
                [new("url", "String"), new("body", "Variant"), new("contentType", "String")], "XPHttpResponse", "api.XPHttpClient.Post"),
            Define("XPHttpClient.Put", "method", "XPHttpClient.Put(url As String, body As Variant, contentType As String)",
                [new("url", "String"), new("body", "Variant"), new("contentType", "String")], "XPHttpResponse", "api.XPHttpClient.Put"),
            Define("XPHttpClient.Patch", "method", "XPHttpClient.Patch(url As String, body As Variant, contentType As String)",
                [new("url", "String"), new("body", "Variant"), new("contentType", "String")], "XPHttpResponse", "api.XPHttpClient.Patch"),
            Define("XPHttpClient.Delete", "method", "XPHttpClient.Delete(url As String) As XPHttpResponse",
                [new("url", "String")], "XPHttpResponse", "api.XPHttpClient.Delete"),
            Define("XPHttpClient.SetHeader", "method", "XPHttpClient.SetHeader(name As String, value As String)",
                [new("name", "String"), new("value", "String")], null, "api.XPHttpClient.SetHeader"),
            Define("XPHttpClient.RemoveHeader", "method", "XPHttpClient.RemoveHeader(name As String)",
                [new("name", "String")], null, "api.XPHttpClient.RemoveHeader"),
            Define("XPHttpClient.ClearHeaders", "method", "XPHttpClient.ClearHeaders()", [], null, "api.XPHttpClient.ClearHeaders"),
            Define("XPHttpClient.Dispose", "method", "XPHttpClient.Dispose()", [], null, "api.XPHttpClient.Dispose"),
            Define("XPHttpClient.BasicAuthorization", "method", "XPHttpClient.BasicAuthorization(username As String, password As String) As String",
                [new("username", "String"), new("password", "String")], "String", "api.XPHttpClient.BasicAuthorization"),
            Define("XPHttpClient.EncodePath", "method", "XPHttpClient.EncodePath(value As String) As String",
                [new("value", "String")], "String", "api.XPHttpClient.EncodePath"),
            Define("XPHttpClient.Timeout", "method", "XPHttpClient.Timeout As Double", [], "Double", "api.XPHttpClient.Timeout"),
            Define("XPHttpRequest", "class", "XPHttpRequest", [], null, "api.XPHttpRequest"),
            Define("XPHttpRequest.Method", "method", "XPHttpRequest.Method As String", [], "String", "api.XPHttpRequest.Method"),
            Define("XPHttpRequest.Url", "method", "XPHttpRequest.Url As String", [], "String", "api.XPHttpRequest.Url"),
            Define("XPHttpRequest.Body", "method", "XPHttpRequest.Body As Variant", [], "Variant", "api.XPHttpRequest.Body"),
            Define("XPHttpResponse", "class", "XPHttpResponse", [], null, "api.XPHttpResponse"),
            Define("XPHttpResponse.StatusCode", "method", "XPHttpResponse.StatusCode As Integer", [], "Integer", "api.XPHttpResponse.StatusCode"),
            Define("XPHttpResponse.StatusText", "method", "XPHttpResponse.StatusText As String", [], "String", "api.XPHttpResponse.StatusText"),
            Define("XPHttpResponse.Body", "method", "XPHttpResponse.Body As String", [], "String", "api.XPHttpResponse.Body"),
            Define("XPHttpResponse.BodyLength", "method", "XPHttpResponse.BodyLength As Long", [], "Long", "api.XPHttpResponse.BodyLength"),
            Define("XPHttpResponse.ContentType", "method", "XPHttpResponse.ContentType As String", [], "String", "api.XPHttpResponse.ContentType"),
            Define("XPHttpResponse.IsSuccess", "method", "XPHttpResponse.IsSuccess As Boolean", [], "Boolean", "api.XPHttpResponse.IsSuccess"),
            Define("XPHttpResponse.Json", "method", "XPHttpResponse.Json() As XPJsonDocument", [], "XPJsonDocument", "api.XPHttpResponse.Json"),
            Define("XPHttpResponse.SaveBodyToFile", "method", "XPHttpResponse.SaveBodyToFile(path As String)",
                [new("path", "String")], null, "api.XPHttpResponse.SaveBodyToFile"),
            Define("XPJsonDocument", "class", "XPJsonDocument", [], null, "api.XPJsonDocument"),
            Define("XPJsonDocument.Parse", "method", "XPJsonDocument.Parse(text As String) As XPJsonDocument", [new("text", "String")], "XPJsonDocument", "api.XPJsonDocument.Parse"),
            Define("XPJsonDocument.Stringify", "method", "XPJsonDocument.Stringify() As String", [], "String", "api.XPJsonDocument.Stringify"),
            Define("XPJsonDocument.Root", "method", "XPJsonDocument.Root As XPJsonElement", [], "XPJsonElement", "api.XPJsonDocument.Root"),
            Define("XPJsonElement.AsObject", "method", "XPJsonElement.AsObject() As XPJsonObject", [], "XPJsonObject", "api.XPJsonElement.AsObject"),
            Define("XPJsonElement.AsArray", "method", "XPJsonElement.AsArray() As XPJsonArray", [], "XPJsonArray", "api.XPJsonElement.AsArray"),
            Define("XPJsonObject.Stringify", "method", "XPJsonObject.Stringify() As String", [], "String", "api.XPJsonObject.Stringify"),
            Define("XPJsonArray.Stringify", "method", "XPJsonArray.Stringify() As String", [], "String", "api.XPJsonArray.Stringify"),
            Define("XPJsonDocument.ToObject", "method", "XPJsonDocument.ToObject(contract As Variant) As Variant", [new("contract", "Variant")], "Variant", "api.XPJsonDocument.ToObject"),
            Define("XPJsonObject.ToObject", "method", "XPJsonObject.ToObject(contract As Variant) As Variant", [new("contract", "Variant")], "Variant", "api.XPJsonObject.ToObject"),
            Define("XPJsonArray.ToObject", "method", "XPJsonArray.ToObject(contract As Variant) As Variant", [new("contract", "Variant")], "Variant", "api.XPJsonArray.ToObject"),
            Define("XPJsonElement.ToObject", "method", "XPJsonElement.ToObject(contract As Variant) As Variant", [new("contract", "Variant")], "Variant", "api.XPJsonElement.ToObject"),
            Define("XPJsonObject", "class", "XPJsonObject", [], null, "api.XPJsonObject"),
            Define("XPJsonObject.Get", "method", "XPJsonObject.Get(name As String) As Variant", [new("name", "String")], "Variant", "api.XPJsonObject.Get"),
            Define("XPJsonObject.Set", "method", "XPJsonObject.Set(name As String, value As Variant)", [new("name", "String"), new("value", "Variant")], null, "api.XPJsonObject.Set"),
            Define("XPJsonObject.Remove", "method", "XPJsonObject.Remove(name As String)", [new("name", "String")], null, "api.XPJsonObject.Remove"),
            Define("XPJsonObject.Contains", "method", "XPJsonObject.Contains(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPJsonObject.Contains"),
            Define("XPJsonObject.Count", "method", "XPJsonObject.Count As Integer", [], "Integer", "api.XPJsonObject.Count"),
            Define("XPJsonArray", "class", "XPJsonArray", [], null, "api.XPJsonArray"),
            Define("XPJsonArray.Add", "method", "XPJsonArray.Add(value As Variant)", [new("value", "Variant")], null, "api.XPJsonArray.Add"),
            Define("XPJsonArray.Get", "method", "XPJsonArray.Get(index As Integer) As Variant", [new("index", "Integer")], "Variant", "api.XPJsonArray.Get"),
            Define("XPJsonArray.Set", "method", "XPJsonArray.Set(index As Integer, value As Variant)", [new("index", "Integer"), new("value", "Variant")], null, "api.XPJsonArray.Set"),
            Define("XPJsonArray.RemoveAt", "method", "XPJsonArray.RemoveAt(index As Integer)", [new("index", "Integer")], null, "api.XPJsonArray.RemoveAt"),
            Define("XPJsonArray.Count", "method", "XPJsonArray.Count As Integer", [], "Integer", "api.XPJsonArray.Count"),
            Define("XPJsonElement", "class", "XPJsonElement", [], null, "api.XPJsonElement"),
            Define("XPJsonElement.Type", "method", "XPJsonElement.Type As String", [], "String", "api.XPJsonElement.Type"),
            Define("XPJsonElement.Value", "method", "XPJsonElement.Value As Variant", [], "Variant", "api.XPJsonElement.Value"),
            Define("XPImage", "class", "XPImage", [], null, "api.XPImage"),
            Define("XPDBSQLite", "class", "XPDBSQLite", [], null, "api.XPDBSQLite"),
            Define("XPDBSQLite.Open", "method", "XPDBSQLite.Open()", [], null, "api.XPDBSQLite.Open"),
            Define("XPDBSQLite.Close", "method", "XPDBSQLite.Close()", [], null, "api.XPDBSQLite.Close"),
            Define("XPDBSQLite.Execute", "method", "XPDBSQLite.Execute(sql As String, parameters As XPJsonObject)", [new("sql", "String"), new("parameters", "XPJsonObject")], null, "api.XPDBSQLite.Execute"),
            Define("XPDBSQLite.Query", "method", "XPDBSQLite.Query(sql As String, parameters As XPJsonObject) As XPJsonArray", [new("sql", "String"), new("parameters", "XPJsonObject")], "XPJsonArray", "api.XPDBSQLite.Query"),
            Define("XPDBSQLite.Scalar", "method", "XPDBSQLite.Scalar(sql As String, parameters As XPJsonObject) As Variant", [new("sql", "String"), new("parameters", "XPJsonObject")], "Variant", "api.XPDBSQLite.Scalar"),
            Define("XPDBSQLite.BeginTransaction", "method", "XPDBSQLite.BeginTransaction()", [], null, "api.XPDBSQLite.BeginTransaction"),
            Define("XPDBSQLite.Commit", "method", "XPDBSQLite.Commit()", [], null, "api.XPDBSQLite.Commit"),
            Define("XPDBSQLite.Rollback", "method", "XPDBSQLite.Rollback()", [], null, "api.XPDBSQLite.Rollback"),
            Define("XPDBSQLite.LastInsertRowId", "method", "XPDBSQLite.LastInsertRowId As Long", [], "Long", "api.XPDBSQLite.LastInsertRowId"),
            Define("XPDBSQLite.DatabasePath", "method", "XPDBSQLite.DatabasePath As String", [], "String", "api.XPDBSQLite.DatabasePath"),
            Define("XPDBSQLite.ReadOnly", "method", "XPDBSQLite.ReadOnly As Boolean", [], "Boolean", "api.XPDBSQLite.ReadOnly"),
            Define("XPDBSQLite.IsOpen", "method", "XPDBSQLite.IsOpen As Boolean", [], "Boolean", "api.XPDBSQLite.IsOpen"),
            Define("XPDBSQLite.InTransaction", "method", "XPDBSQLite.InTransaction As Boolean", [], "Boolean", "api.XPDBSQLite.InTransaction"),
            Define("XPAi", "class", "XPAi", [], null, "api.XPAi"),
            Define("XPAi.Model", "property", "XPAi.Model As String", [], "String", "api.XPAi.Model"),
            Define("XPAi.Endpoint", "property", "XPAi.Endpoint As String", [], "String", "api.XPAi.Endpoint"),
            Define("XPAi.Provider", "property", "XPAi.Provider As String", [], "String", "api.XPAi.Provider"),
            Define("XPAi.CertificateValidation", "property", "XPAi.CertificateValidation As String", [], "String", "api.XPAi.CertificateValidation"),
            Define("XPAi.EndpointPath", "property", "XPAi.EndpointPath As String", [], "String", "api.XPAi.EndpointPath"),
            Define("XPAi.Timeout", "property", "XPAi.Timeout As Double", [], "Double", "api.XPAi.Timeout"),
            Define("XPAi.Temperature", "property", "XPAi.Temperature As Double", [], "Double", "api.XPAi.Temperature"),
            Define("XPAi.MaxOutputTokens", "property", "XPAi.MaxOutputTokens As Integer", [], "Integer", "api.XPAi.MaxOutputTokens"),
            Define("XPAi.CollectStreamedResponse", "property", "XPAi.CollectStreamedResponse As Boolean", [], "Boolean", "api.XPAi.CollectStreamedResponse"),
            Define("XPAi.ThrowOnHttpError", "property", "XPAi.ThrowOnHttpError As Boolean", [], "Boolean", "api.XPAi.ThrowOnHttpError"),
            Define("XPAi.SystemPrompt", "property", "XPAi.SystemPrompt As String", [], "String", "api.XPAi.SystemPrompt"),
            Define("XPAi.UserPrompt", "property", "XPAi.UserPrompt As String", [], "String", "api.XPAi.UserPrompt"),
            Define("XPAi.JsonSchemaName", "property", "XPAi.JsonSchemaName As String", [], "String", "api.XPAi.JsonSchemaName"),
            Define("XPAi.JsonSchemaStrict", "property", "XPAi.JsonSchemaStrict As Boolean", [], "Boolean", "api.XPAi.JsonSchemaStrict"),
            Define("XPAi.HasJsonSchema", "property", "XPAi.HasJsonSchema As Boolean", [], "Boolean", "api.XPAi.HasJsonSchema"),
            Define("XPAi.AddMessage", "method", "XPAi.AddMessage(role As String, content As String)", [new("role", "String"), new("content", "String")], null, "api.XPAi.AddMessage"),
            Define("XPAi.ClearMessages", "method", "XPAi.ClearMessages()", [], null, "api.XPAi.ClearMessages"),
            Define("XPAi.GetMessages", "method", "XPAi.GetMessages() As XPJsonDocument", [], "XPJsonDocument", "api.XPAi.GetMessages"),
            Define("XPAi.SetOption", "method", "XPAi.SetOption(name As String, value As Variant)", [new("name", "String"), new("value", "Variant")], null, "api.XPAi.SetOption"),
            Define("XPAi.RemoveOption", "method", "XPAi.RemoveOption(name As String)", [new("name", "String")], null, "api.XPAi.RemoveOption"),
            Define("XPAi.ClearOptions", "method", "XPAi.ClearOptions()", [], null, "api.XPAi.ClearOptions"),
            Define("XPAi.SetHeader", "method", "XPAi.SetHeader(name As String, value As String)", [new("name", "String"), new("value", "String")], null, "api.XPAi.SetHeader"),
            Define("XPAi.RemoveHeader", "method", "XPAi.RemoveHeader(name As String)", [new("name", "String")], null, "api.XPAi.RemoveHeader"),
            Define("XPAi.ClearHeaders", "method", "XPAi.ClearHeaders()", [], null, "api.XPAi.ClearHeaders"),
            Define("XPAi.SetPrompt", "method", "XPAi.SetPrompt(systemPrompt As String, userPrompt As String)", [new("systemPrompt", "String"), new("userPrompt", "String")], null, "api.XPAi.SetPrompt"),
            Define("XPAi.ClearPrompt", "method", "XPAi.ClearPrompt()", [], null, "api.XPAi.ClearPrompt"),
            Define("XPAi.SetResultClass", "method", "XPAi.SetResultClass(contract As Object)", [new("contract", "Object")], null, "api.XPAi.SetResultClass"),
            Define("XPAi.SetJsonSchema", "method", "XPAi.SetJsonSchema(schema As Object)", [new("schema", "Object")], null, "api.XPAi.SetJsonSchema"),
            Define("XPAi.ClearJsonSchema", "method", "XPAi.ClearJsonSchema()", [], null, "api.XPAi.ClearJsonSchema"),
            Define("XPAi.Cancel", "method", "XPAi.Cancel()", [], null, "api.XPAi.Cancel"),
            Define("XPAi.Complete", "method", "XPAi.Complete() As XPAiResponse", [], "XPAiResponse", "api.XPAi.Complete"),
            Define("XPAi.Stream", "method", "XPAi.Stream(callback As String) As XPAiResponse", [new("callback", "String")], "XPAiResponse", "api.XPAi.Stream"),
            Define("XPAi.StreamCallback", "method", "XPAi.StreamCallback(callback As String) As XPAiResponse", [new("callback", "String")], "XPAiResponse", "api.XPAi.StreamCallback"),
            Define("XPAi.NewRequest", "method", "XPAi.NewRequest()", [], null, "api.XPAi.NewRequest"),
            Define("XPAi.Dispose", "method", "XPAi.Dispose()", [], null, "api.XPAi.Dispose"),
            Define("XPAi.AddTool", "method", "XPAi.AddTool(tool As XPAITool)", [new("tool", "XPAITool")], null, "api.XPAi.AddTool"),
            Define("XPAi.HasTool", "method", "XPAi.HasTool(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAi.HasTool"),
            Define("XPAi.GetTool", "method", "XPAi.GetTool(name As String) As XPAITool", [new("name", "String")], "XPAITool", "api.XPAi.GetTool"),
            Define("XPAi.GetToolNames", "method", "XPAi.GetToolNames() As XPJsonArray", [], "XPJsonArray", "api.XPAi.GetToolNames"),
            Define("XPAi.ToolCount", "method", "XPAi.ToolCount() As Integer", [], "Integer", "api.XPAi.ToolCount"),
            Define("XPAi.RemoveTool", "method", "XPAi.RemoveTool(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAi.RemoveTool"),
            Define("XPAi.ClearTools", "method", "XPAi.ClearTools()", [], null, "api.XPAi.ClearTools"),
            Define("XPAi.AutoExecuteTools", "property", "XPAi.AutoExecuteTools As Boolean", [], "Boolean", "api.XPAi.AutoExecuteTools"),
            Define("XPAi.MaxToolIterations", "property", "XPAi.MaxToolIterations As Integer", [], "Integer", "api.XPAi.MaxToolIterations"),
            Define("XPAi.SessionId", "property", "XPAi.SessionId As String", [], "String", "api.XPAi.SessionId"),
            Define("XPAi.HasSession", "property", "XPAi.HasSession As Boolean", [], "Boolean", "api.XPAi.HasSession"),
            Define("XPAi.SessionRequestProperty", "property", "XPAi.SessionRequestProperty As String", [], "String", "api.XPAi.SessionRequestProperty"),
            Define("XPAi.ResetSession", "method", "XPAi.ResetSession()", [], null, "api.XPAi.ResetSession"),
            Define("XPAiResponse", "class", "XPAiResponse", [], null, "api.XPAiResponse"),
            Define("XPAiResponse.StatusCode", "property", "XPAiResponse.StatusCode As Integer", [], "Integer", "api.XPAiResponse.StatusCode"),
            Define("XPAiResponse.IsSuccess", "property", "XPAiResponse.IsSuccess As Boolean", [], "Boolean", "api.XPAiResponse.IsSuccess"),
            Define("XPAiResponse.Model", "property", "XPAiResponse.Model As String", [], "String", "api.XPAiResponse.Model"),
            Define("XPAiResponse.Text", "property", "XPAiResponse.Text As String", [], "String", "api.XPAiResponse.Text"),
            Define("XPAiResponse.Content", "property", "XPAiResponse.Content As String", [], "String", "api.XPAiResponse.Content"),
            Define("XPAiResponse.RawJson", "property", "XPAiResponse.RawJson As XPJsonDocument", [], "XPJsonDocument", "api.XPAiResponse.RawJson"),
            Define("XPAiResponse.Usage", "property", "XPAiResponse.Usage As XPJsonDocument", [], "XPJsonDocument", "api.XPAiResponse.Usage"),
            Define("XPAiResponse.HasJsonResult", "property", "XPAiResponse.HasJsonResult As Boolean", [], "Boolean", "api.XPAiResponse.HasJsonResult"),
            Define("XPAiResponse.ResultJson", "property", "XPAiResponse.ResultJson As XPJsonDocument", [], "XPJsonDocument", "api.XPAiResponse.ResultJson"),
            Define("XPAITool", "class", "XPAITool", [], null, "api.XPAITool"),
            Define("XPAITool.Name", "property", "XPAITool.Name As String", [], "String", "api.XPAITool.Name"),
            Define("XPAITool.Description", "property", "XPAITool.Description As String", [], "String", "api.XPAITool.Description"),
            Define("XPAITool.Timeout", "property", "XPAITool.Timeout As Double", [], "Double", "api.XPAITool.Timeout")
            ,Define("XPAITool.AddFunction", "method", "XPAITool.AddFunction(name As String, description As String, callback As String, context As Variant) As XPAIToolFunction", [new("name", "String"), new("description", "String"), new("callback", "String"), new("context", "Variant")], "XPAIToolFunction", "api.XPAITool.AddFunction")
            ,Define("XPAITool.AddParameter", "method", "XPAITool.AddParameter(functionName As String, name As String, type As String, description As String, required As Boolean)", [new("functionName", "String"), new("name", "String"), new("type", "String"), new("description", "String"), new("required", "Boolean")], null, "api.XPAITool.AddParameter")
            ,Define("XPAITool.HasFunction", "method", "XPAITool.HasFunction(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAITool.HasFunction")
            ,Define("XPAITool.FunctionCount", "method", "XPAITool.FunctionCount() As Integer", [], "Integer", "api.XPAITool.FunctionCount")
            ,Define("XPAITool.RemoveFunction", "method", "XPAITool.RemoveFunction(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAITool.RemoveFunction")
            ,Define("XPAITool.GetFunction", "method", "XPAITool.GetFunction(name As String) As XPAIToolFunction", [new("name", "String")], "XPAIToolFunction", "api.XPAITool.GetFunction")
            ,Define("XPAITool.GetFunctionNames", "method", "XPAITool.GetFunctionNames() As XPJsonArray", [], "XPJsonArray", "api.XPAITool.GetFunctionNames")
            ,Define("XPAITool.SetRequestProperty", "method", "XPAITool.SetRequestProperty(name As String, value As Variant)", [new("name", "String"), new("value", "Variant")], null, "api.XPAITool.SetRequestProperty")
            ,Define("XPAITool.RemoveRequestProperty", "method", "XPAITool.RemoveRequestProperty(name As String)", [new("name", "String")], null, "api.XPAITool.RemoveRequestProperty")
            ,Define("XPAITool.ClearRequestProperties", "method", "XPAITool.ClearRequestProperties()", [], null, "api.XPAITool.ClearRequestProperties")
            ,Define("XPAITool.GetRequestContext", "method", "XPAITool.GetRequestContext() As XPJsonObject", [], "XPJsonObject", "api.XPAITool.GetRequestContext")
            ,Define("XPAITool.ToJsonObject", "method", "XPAITool.ToJsonObject() As XPJsonObject", [], "XPJsonObject", "api.XPAITool.ToJsonObject")
            ,Define("XPAITool.ToJson", "method", "XPAITool.ToJson() As String", [], "String", "api.XPAITool.ToJson")
            ,Define("XPAIToolFunction", "class", "XPAIToolFunction", [], null, "api.XPAIToolFunction")
            ,Define("XPAIToolFunction.ToolName", "property", "XPAIToolFunction.ToolName As String", [], "String", "api.XPAIToolFunction.ToolName")
            ,Define("XPAIToolFunction.Name", "property", "XPAIToolFunction.Name As String", [], "String", "api.XPAIToolFunction.Name")
            ,Define("XPAIToolFunction.Description", "property", "XPAIToolFunction.Description As String", [], "String", "api.XPAIToolFunction.Description")
            ,Define("XPAIToolFunction.CallbackName", "property", "XPAIToolFunction.CallbackName As String", [], "String", "api.XPAIToolFunction.CallbackName")
            ,Define("XPAIToolFunction.AddParameter", "method", "XPAIToolFunction.AddParameter(name As String, type As String, description As String, required As Boolean)", [new("name", "String"), new("type", "String"), new("description", "String"), new("required", "Boolean")], null, "api.XPAIToolFunction.AddParameter")
            ,Define("XPAIToolFunction.RemoveParameter", "method", "XPAIToolFunction.RemoveParameter(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAIToolFunction.RemoveParameter")
            ,Define("XPAIToolFunction.HasParameter", "method", "XPAIToolFunction.HasParameter(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAIToolFunction.HasParameter")
            ,Define("XPAIToolFunction.ParameterCount", "method", "XPAIToolFunction.ParameterCount() As Integer", [], "Integer", "api.XPAIToolFunction.ParameterCount")
            ,Define("XPAIToolCall", "class", "XPAIToolCall", [], null, "api.XPAIToolCall")
            ,Define("XPAIToolCall.ToolName", "property", "XPAIToolCall.ToolName As String", [], "String", "api.XPAIToolCall.ToolName")
            ,Define("XPAIToolCall.FunctionName", "property", "XPAIToolCall.FunctionName As String", [], "String", "api.XPAIToolCall.FunctionName")
            ,Define("XPAIToolCall.CallId", "property", "XPAIToolCall.CallId As String", [], "String", "api.XPAIToolCall.CallId")
            ,Define("XPAIToolCall.Arguments", "property", "XPAIToolCall.Arguments As XPJsonObject", [], "XPJsonObject", "api.XPAIToolCall.Arguments")
            ,Define("XPAIToolCall.SessionId", "property", "XPAIToolCall.SessionId As String", [], "String", "api.XPAIToolCall.SessionId")
        }.Where(x => !AstHttpSupport.OwnsSymbol(x.Name) && !AstApplicationSupport.OwnsSymbol(x.Name) && !AstSqliteSupport.OwnsSymbol(x.Name) && !AstMySqlSupport.OwnsSymbol(x.Name) && !AstMsSqlSupport.OwnsSymbol(x.Name) && !AstSupabaseSupport.OwnsSymbol(x.Name)).Concat(AstApplicationSupport.Catalog).Concat(AstSpreadsheetSupport.Catalog).Concat(AstHttpSupport.Catalog).Concat(AstImageSupport.Catalog).Concat(AstCsvSupport.Catalog).Concat(AstXmlSupport.Catalog).Concat(AstSystemInventorySupport.Catalog).Concat(AstSqliteSupport.Catalog).Concat(AstMySqlSupport.Catalog).Concat(AstMsSqlSupport.Catalog).Concat(AstSupabaseSupport.Catalog).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public static CompilerSymbolDefinition? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Definitions.TryGetValue(name.Trim(), out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<CompilerSymbolDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        return Definitions.Values
            .Where(x => x.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyCollection<CompilerSymbolDefinition> All =>
        Definitions.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<CompilerSymbolDefinition> Candidates(string requestedName, int limit = 5) =>
        Candidates(requestedName, receiverType: null, limit);

    public static IReadOnlyList<CompilerSymbolDefinition> Candidates(string requestedName, string? receiverType, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(requestedName) || limit <= 0) return [];
        var requested = requestedName.Trim();
        var receiver = receiverType?.Trim();
        var candidates = Definitions.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(receiver))
        {
            var prefix = receiver + ".";
            candidates = candidates.Where(definition => definition.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return candidates
            .Select(definition =>
            {
                var candidateName = MemberName(definition.Name);
                return (Definition: definition, Distance: EditDistance(requested, candidateName));
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Definition.Name, StringComparer.Ordinal)
            .Take(Math.Min(limit, 10))
            .Select(item => item.Definition)
            .ToArray();
    }

    private static string MemberName(string name)
    {
        var separator = name.LastIndexOf('.');
        return separator >= 0 && separator + 1 < name.Length ? name[(separator + 1)..] : name;
    }

    private static int EditDistance(string left, string right)
    {
        left = left.ToUpperInvariant();
        right = right.ToUpperInvariant();
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static CompilerSymbolDefinition Define(
        string name,
        string kind,
        string signature,
        IReadOnlyList<CompilerSymbolParameter> parameters,
        string? returnType,
        string documentationId,
        params string[] allowedTargets) =>
        new(name, kind, signature, parameters, returnType, allowedTargets, documentationId);
}
