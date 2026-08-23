namespace XPScript.Compiler;

internal static class DatabaseAttachmentRuntimeV3Source
{
    public const string Code = """
internal sealed class XPScriptAttachmentCollectionV3
{
    private readonly XPScriptAttachmentCollection _inner;

    internal XPScriptAttachmentCollectionV3(XPScriptAttachmentCollection inner)
    {
        _inner = inner;
    }

    public XPScriptJsonArray List() => CleanArray(_inner.List());
    public XPScriptJsonArray GetMetadata() => CleanArray(_inner.GetMetadata());
    public XPScriptJsonObject GetMetadata(object? attachmentId) => CleanObject(_inner.GetMetadata(attachmentId));
    public XPScriptJsonArray FindByName(object? originalName) => CleanArray(_inner.FindByName(originalName));

    public XPScriptJsonObject Save(object? sourcePath, object? createdBy)
    {
        var actor = RequiredActor(createdBy);
        _inner.SetActor(actor);
        return CleanObject(_inner.Save(sourcePath));
    }

    public XPScriptJsonObject SaveAs(object? sourcePath, object? originalName, object? createdBy)
    {
        var actor = RequiredActor(createdBy);
        _inner.SetActor(actor);
        return CleanObject(_inner.SaveAs(sourcePath, originalName));
    }

    public bool Get(object? attachmentId, object? targetPath)
        => _inner.Get(attachmentId, targetPath);

    public XPScriptJsonArray GetAll(object? targetFolder)
        => CleanArray(_inner.GetAll(targetFolder));

    public bool Delete(object? attachmentId)
        => _inner.Delete(attachmentId);

    private static string RequiredActor(object? value)
    {
        var actor = XPScriptRuntime.CStr(value).Trim();
        if (actor.Length is < 1 or > 512 || actor.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new XPScriptRuntimeException(5, "Attachment createdBy must contain 1 to 512 characters without control line breaks.");
        return actor;
    }

    private static XPScriptJsonArray CleanArray(XPScriptJsonArray source)
    {
        var result = new System.Text.Json.Nodes.JsonArray();
        foreach (var node in source.Node)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj)
                result.Add(CleanNode(obj));
            else
                result.Add(node?.DeepClone());
        }
        return new XPScriptJsonArray(result);
    }

    private static XPScriptJsonObject CleanObject(XPScriptJsonObject source)
        => new(CleanNode(source.Node));

    private static System.Text.Json.Nodes.JsonObject CleanNode(System.Text.Json.Nodes.JsonObject source)
    {
        var obj = (System.Text.Json.Nodes.JsonObject)source.DeepClone();
        obj.Remove("modified");
        obj.Remove("modifiedBy");
        return obj;
    }
}

internal static class XPScriptDatabaseAttachmentApi
{
    public static XPScriptAttachmentCollectionV3 ForSqlite(XPScriptDbSqlite db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForSqlite(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForMsSql(XPScriptDbMsSql db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForMsSql(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForSupabase(XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForSupabase(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForDomino(XPScriptHttpDbDominoRest db, object? unid)
        => new(XPScriptDatabaseAttachmentRuntime.ForDomino(db, unid));

    public static XPScriptAttachmentCollectionV3 ForDomino(XPScriptHttpDbDominoRest db, object? unid, object? fieldName)
        => new(XPScriptDatabaseAttachmentRuntime.ForDomino(db, unid, fieldName));

    public static void SetSupabaseBucket(XPScriptHttpDbSupabase db, object? bucket)
        => XPScriptDatabaseAttachmentRuntime.SetSupabaseBucket(db, bucket);
}
""";
}
