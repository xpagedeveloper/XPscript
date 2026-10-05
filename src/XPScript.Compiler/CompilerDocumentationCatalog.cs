namespace XPScript.Compiler;

public sealed record CompilerDocumentationDefinition(
    string Id,
    string Kind,
    string Symbol);

public static class CompilerDocumentationCatalog
{
    private static readonly IReadOnlyDictionary<string, CompilerDocumentationDefinition> Definitions =
        new[]
        {
            Define("language.If", "language", "If"),
            Define("language.ForAll", "language", "ForAll"),
            Define("api.XPJsonSchema", "api", "XPJsonSchema"),
            Define("api.XPJsonSchema.Parse", "api", "XPJsonSchema.Parse"),
            Define("api.XPJsonSchema.FromJson", "api", "XPJsonSchema.FromJson"),
            Define("api.XPJsonSchema.Validate", "api", "XPJsonSchema.Validate"),
            Define("api.XPJsonSchema.IsValid", "api", "XPJsonSchema.IsValid"),
            Define("api.UIForm.SetValidationSchema", "api", "UIForm.SetValidationSchema"),
            Define("api.UIForm.ValidateData", "api", "UIForm.ValidateData"),
            Define("api.XPAi", "api", "XPAi"),
            Define("api.XPAi.AddTool", "api", "XPAi.AddTool"),
            Define("api.XPAi.HasTool", "api", "XPAi.HasTool"),
            Define("api.XPAi.GetTool", "api", "XPAi.GetTool"),
            Define("api.XPAi.GetToolNames", "api", "XPAi.GetToolNames"),
            Define("api.XPAi.ToolCount", "api", "XPAi.ToolCount"),
            Define("api.XPAi.RemoveTool", "api", "XPAi.RemoveTool"),
            Define("api.XPAi.ClearTools", "api", "XPAi.ClearTools"),
            Define("api.XPAi.AutoExecuteTools", "api", "XPAi.AutoExecuteTools"),
            Define("api.XPAi.MaxToolIterations", "api", "XPAi.MaxToolIterations"),
            Define("api.XPAi.SessionId", "api", "XPAi.SessionId"),
            Define("api.XPAi.HasSession", "api", "XPAi.HasSession"),
            Define("api.XPAi.SessionRequestProperty", "api", "XPAi.SessionRequestProperty"),
            Define("api.XPAi.ResetSession", "api", "XPAi.ResetSession"),
            Define("api.XPAi.NewRequest", "api", "XPAi.NewRequest"),
            Define("api.XPDBSQLite", "api", "XPDBSQLite"),
            Define("api.XPDBSQLite.Open", "api", "XPDBSQLite.Open"),
            Define("api.XPDBSQLite.Close", "api", "XPDBSQLite.Close"),
            Define("api.XPDBSQLite.Execute", "api", "XPDBSQLite.Execute"),
            Define("api.XPDBSQLite.Query", "api", "XPDBSQLite.Query"),
            Define("api.XPDBSQLite.Scalar", "api", "XPDBSQLite.Scalar"),
            Define("api.XPDBSQLite.BeginTransaction", "api", "XPDBSQLite.BeginTransaction"),
            Define("api.XPDBSQLite.Commit", "api", "XPDBSQLite.Commit"),
            Define("api.XPDBSQLite.Rollback", "api", "XPDBSQLite.Rollback"),
            Define("api.XPDBSQLite.LastInsertRowId", "api", "XPDBSQLite.LastInsertRowId"),
            Define("api.XPDBSQLite.DatabasePath", "api", "XPDBSQLite.DatabasePath"),
            Define("api.XPDBSQLite.ReadOnly", "api", "XPDBSQLite.ReadOnly"),
            Define("api.XPDBSQLite.IsOpen", "api", "XPDBSQLite.IsOpen"),
            Define("api.XPDBSQLite.InTransaction", "api", "XPDBSQLite.InTransaction"),
            Define("api.XPHttpClient", "api", "XPHttpClient"),
            Define("api.XPHttpClient.ClearHeaders", "api", "XPHttpClient.ClearHeaders"),
            Define("api.XPHttpClient.Dispose", "api", "XPHttpClient.Dispose"),
            Define("api.XPHttpClient.Timeout", "api", "XPHttpClient.Timeout"),
            Define("api.XPHttpRequest", "api", "XPHttpRequest"),
            Define("api.XPHttpRequest.Method", "api", "XPHttpRequest.Method"),
            Define("api.XPHttpRequest.Url", "api", "XPHttpRequest.Url"),
            Define("api.XPHttpRequest.Body", "api", "XPHttpRequest.Body"),
            Define("api.XPHttpResponse", "api", "XPHttpResponse"),
            Define("api.XPHttpResponse.StatusCode", "api", "XPHttpResponse.StatusCode"),
            Define("api.XPHttpResponse.StatusText", "api", "XPHttpResponse.StatusText"),
            Define("api.XPHttpResponse.Body", "api", "XPHttpResponse.Body"),
            Define("api.XPHttpResponse.BodyLength", "api", "XPHttpResponse.BodyLength"),
            Define("api.XPHttpResponse.ContentType", "api", "XPHttpResponse.ContentType"),
            Define("api.XPHttpResponse.IsSuccess", "api", "XPHttpResponse.IsSuccess"),
            Define("api.XPHttpResponse.Json", "api", "XPHttpResponse.Json"),
            Define("api.XPImage", "api", "XPImage"),
            Define("api.XPImage.ChangeGamma", "api", "XPImage.ChangeGamma"),
            Define("api.XPImage.Compare", "api", "XPImage.Compare"),
            Define("api.XPImage.Difference", "api", "XPImage.Difference"),
            Define("api.XPImage.Statistics", "api", "XPImage.Statistics"),
            Define("api.XPImage.Histogram", "api", "XPImage.Histogram"),
            Define("api.XPImage.HasAlpha", "api", "XPImage.HasAlpha"),
            Define("api.XPImage.ColorSpace", "api", "XPImage.ColorSpace"),
            Define("api.XPImage.ColorType", "api", "XPImage.ColorType"),
            Define("api.XPImage.Gamma", "api", "XPImage.Gamma"),
            Define("api.XPImage.Depth", "api", "XPImage.Depth"),
            Define("api.XPImage.Quality", "api", "XPImage.Quality"),
            Define("api.XPImage.Orientation", "api", "XPImage.Orientation"),
            Define("api.XPImage.PixelCount", "api", "XPImage.PixelCount"),
            Define("api.XPJsonArray", "api", "XPJsonArray"),
            Define("api.XPJsonArray.Add", "api", "XPJsonArray.Add"),
            Define("api.XPJsonArray.Get", "api", "XPJsonArray.Get"),
            Define("api.XPJsonArray.Set", "api", "XPJsonArray.Set"),
            Define("api.XPJsonArray.RemoveAt", "api", "XPJsonArray.RemoveAt"),
            Define("api.XPJsonArray.Count", "api", "XPJsonArray.Count"),
            Define("api.XPJsonDocument", "api", "XPJsonDocument"),
            Define("api.XPJsonDocument.Parse", "api", "XPJsonDocument.Parse"),
            Define("api.XPJsonDocument.Stringify", "api", "XPJsonDocument.Stringify"),
            Define("api.XPJsonElement", "api", "XPJsonElement"),
            Define("api.XPJsonElement.Type", "api", "XPJsonElement.Type"),
            Define("api.XPJsonElement.Value", "api", "XPJsonElement.Value"),
            Define("api.XPJsonObject", "api", "XPJsonObject"),
            Define("api.XPJsonObject.Get", "api", "XPJsonObject.Get"),
            Define("api.XPJsonObject.Set", "api", "XPJsonObject.Set"),
            Define("api.XPHttpClient.BasicAuthorization", "api", "XPHttpClient.BasicAuthorization"),
            Define("api.XPHttpClient.Delete", "api", "XPHttpClient.Delete"),
            Define("api.XPHttpClient.EncodePath", "api", "XPHttpClient.EncodePath"),
            Define("api.XPHttpClient.Get", "api", "XPHttpClient.Get"),
            Define("api.XPHttpClient.Patch", "api", "XPHttpClient.Patch"),
            Define("api.XPHttpClient.Post", "api", "XPHttpClient.Post"),
            Define("api.XPHttpClient.Put", "api", "XPHttpClient.Put"),
            Define("api.XPHttpClient.RemoveHeader", "api", "XPHttpClient.RemoveHeader"),
            Define("api.XPHttpClient.SetHeader", "api", "XPHttpClient.SetHeader"),
            Define("api.XPHttpResponse.SaveBodyToFile", "api", "XPHttpResponse.SaveBodyToFile"),
            Define("api.XPJsonObject.Remove", "api", "XPJsonObject.Remove"),
            Define("api.XPJsonObject.Contains", "api", "XPJsonObject.Contains"),
            Define("api.XPJsonObject.Count", "api", "XPJsonObject.Count"),
            Define("target.BrowserWasm", "target", "browser-wasm"),
            Define("target.ServerSide", "target", "ServerSide"),
            Define("security.Shell", "security", "Shell"),
            Define("security.DependencyAudit", "security", "dependency-audit")
        }.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public static CompilerDocumentationDefinition? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Definitions.TryGetValue(id.Trim(), out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<CompilerDocumentationDefinition> All =>
        Definitions.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();

    private static CompilerDocumentationDefinition Define(string id, string kind, string symbol) =>
        new(id, kind, symbol);
}
