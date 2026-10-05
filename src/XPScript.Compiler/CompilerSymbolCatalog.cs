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
            Define("XPAi.AddTool", "method", "XPAi.AddTool(tool As AITool)", [new("tool", "AITool")], null, "api.XPAi.AddTool"),
            Define("XPAi.HasTool", "method", "XPAi.HasTool(name As String) As Boolean", [new("name", "String")], "Boolean", "api.XPAi.HasTool"),
            Define("XPAi.GetTool", "method", "XPAi.GetTool(name As String) As AITool", [new("name", "String")], "AITool", "api.XPAi.GetTool"),
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
            Define("XPAi.NewRequest", "method", "XPAi.NewRequest() As XPAiRequest", [], "XPAiRequest", "api.XPAi.NewRequest")
        }.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

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
