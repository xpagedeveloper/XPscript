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

    public void SetActor(object? actor) => _inner.SetActor(actor);

    public XPScriptJsonArray List() => _inner.List();
    public XPScriptJsonArray GetMetadata() => _inner.GetMetadata();
    public XPScriptJsonObject GetMetadata(object? attachmentId) => _inner.GetMetadata(attachmentId);
    public XPScriptJsonArray FindByName(object? originalName) => _inner.FindByName(originalName);

    public XPScriptJsonObject Save(object? sourcePath)
        => _inner.Save(sourcePath);

    public XPScriptJsonObject SaveAs(object? sourcePath, object? originalName)
        => _inner.SaveAs(sourcePath, originalName);

    public XPScriptJsonObject Update(object? attachmentId, object? sourcePath)
        => _inner.Update(attachmentId, sourcePath);

    public XPScriptJsonObject UpdateAs(object? attachmentId, object? sourcePath, object? originalName)
        => _inner.UpdateAs(attachmentId, sourcePath, originalName);

    public bool Get(object? attachmentId, object? targetPath)
        => _inner.Get(attachmentId, targetPath);

    public XPScriptJsonArray GetAll(object? targetFolder)
        => _inner.GetAll(targetFolder);

    public bool Delete(object? attachmentId)
        => _inner.Delete(attachmentId);
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
