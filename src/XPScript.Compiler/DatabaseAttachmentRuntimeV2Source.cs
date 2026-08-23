namespace XPScript.Compiler;

internal static class DatabaseAttachmentRuntimeV2Source
{
    public static string Build(bool usesSqlite, bool usesMsSql)
    {
        var code = new System.Text.StringBuilder();
        code.AppendLine(CommonCode);
        if (usesSqlite) code.AppendLine(SqliteCode);
        if (usesMsSql) code.AppendLine(MsSqlCode);
        code.AppendLine(HttpCode);
        return code.ToString();
    }

    private const string CommonCode = """
internal sealed class XPScriptAttachmentCollection
{
    private readonly Func<XPScriptJsonArray> _metadata;
    private readonly Func<string, string, string, string, byte[], XPScriptJsonObject> _create;
    private readonly Func<string, string, string, string, byte[], XPScriptJsonObject> _update;
    private readonly Func<string, byte[]> _get;
    private readonly Func<string, bool> _delete;
    private string _actor = Environment.UserName;

    internal XPScriptAttachmentCollection(
        Func<XPScriptJsonArray> metadata,
        Func<string, string, string, string, byte[], XPScriptJsonObject> create,
        Func<string, string, string, string, byte[], XPScriptJsonObject> update,
        Func<string, byte[]> get,
        Func<string, bool> delete)
    {
        _metadata = metadata;
        _create = create;
        _update = update;
        _get = get;
        _delete = delete;
    }

    public void SetActor(object? actor)
    {
        var value = XPScriptRuntime.CStr(actor).Trim();
        if (value.Length is < 1 or > 512 || value.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new XPScriptRuntimeException(5, "Attachment actor must contain 1 to 512 characters without control line breaks.");
        _actor = value;
    }

    public XPScriptJsonArray List() => GetMetadata();
    public XPScriptJsonArray GetMetadata() => _metadata();

    public XPScriptJsonObject GetMetadata(object? attachmentId)
    {
        var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId);
        foreach (var node in _metadata().Node)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj &&
                string.Equals(obj["attachmentId"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
                return new XPScriptJsonObject((System.Text.Json.Nodes.JsonObject)obj.DeepClone());
        }
        throw new XPScriptRuntimeException(53, "Attachment metadata was not found.");
    }

    public XPScriptJsonArray FindByName(object? originalName)
    {
        var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(originalName);
        var result = new System.Text.Json.Nodes.JsonArray();
        foreach (var node in _metadata().Node)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj &&
                string.Equals(obj["originalName"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
                result.Add(obj.DeepClone());
        }
        return new XPScriptJsonArray(result);
    }

    public XPScriptJsonObject Save(object? sourcePath)
    {
        var path = XPScriptAttachmentFileRuntime.RequiredExistingFile(sourcePath);
        return SaveAs(path, Path.GetFileName(path));
    }

    public XPScriptJsonObject SaveAs(object? sourcePath, object? originalName)
    {
        var path = XPScriptAttachmentFileRuntime.RequiredExistingFile(sourcePath);
        var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(originalName);
        var bytes = XPScriptAttachmentFileRuntime.ReadAllBytes(path);
        var id = Guid.NewGuid().ToString("D");
        return _create(id, name, XPScriptAttachmentFileRuntime.GuessContentType(name), _actor, bytes);
    }

    public XPScriptJsonObject Update(object? attachmentId, object? sourcePath)
    {
        var current = GetMetadata(attachmentId);
        return UpdateAs(attachmentId, sourcePath, current.Get("originalName"));
    }

    public XPScriptJsonObject UpdateAs(object? attachmentId, object? sourcePath, object? originalName)
    {
        var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId);
        var path = XPScriptAttachmentFileRuntime.RequiredExistingFile(sourcePath);
        var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(originalName);
        var bytes = XPScriptAttachmentFileRuntime.ReadAllBytes(path);
        return _update(id, name, XPScriptAttachmentFileRuntime.GuessContentType(name), _actor, bytes);
    }

    public bool Get(object? attachmentId, object? targetPath)
    {
        var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId);
        XPScriptAttachmentFileRuntime.WriteAllBytes(targetPath, _get(id));
        return true;
    }

    public XPScriptJsonArray GetAll(object? targetFolder)
    {
        var folder = XPScriptAttachmentFileRuntime.RequiredTargetFolder(targetFolder);
        var result = new System.Text.Json.Nodes.JsonArray();
        foreach (var node in _metadata().Node)
        {
            if (node is not System.Text.Json.Nodes.JsonObject metadata) continue;
            var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(metadata["attachmentId"]?.GetValue<string>());
            var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(metadata["originalName"]?.GetValue<string>());
            var localName = id + "_" + name;
            var localPath = Path.Combine(folder, localName);
            XPScriptAttachmentFileRuntime.WriteAllBytes(localPath, _get(id));
            var downloaded = (System.Text.Json.Nodes.JsonObject)metadata.DeepClone();
            downloaded["localPath"] = localPath;
            result.Add(downloaded);
        }
        return new XPScriptJsonArray(result);
    }

    public bool Delete(object? attachmentId)
        => _delete(XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId));
}

internal static class XPScriptAttachmentFileRuntime
{
    public const int MaxAttachmentBytes = 64 * 1024 * 1024;

    public static string RequiredAttachmentName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 255 || name is "." or ".." ||
            name.IndexOfAny(['/', '\\', '\0', '\r', '\n']) >= 0 || name.Any(char.IsControl))
            throw new XPScriptRuntimeException(5, "Attachment original name must be a simple file name of 1 to 255 characters.");
        return name;
    }

    public static string RequiredExistingFile(object? value)
    {
        var path = XPScriptRuntime.CStr(value).Trim();
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "Attachment source path cannot be empty.");
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new XPScriptRuntimeException(5, "Attachment source path is invalid."); }
        try
        {
            if (!File.Exists(fullPath)) throw new XPScriptRuntimeException(53, "Attachment source file does not exist.");
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new XPScriptRuntimeException(5, "Attachment source cannot be a symbolic link or reparse point.");
            if (new FileInfo(fullPath).Length > MaxAttachmentBytes)
                throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
            return fullPath;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException) { throw new XPScriptRuntimeException(70, "Permission denied while reading attachment source file."); }
        catch (IOException) { throw new XPScriptRuntimeException(75, "Unable to inspect attachment source file."); }
    }

    public static string RequiredTargetFolder(object? value)
    {
        var path = XPScriptRuntime.CStr(value).Trim();
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "Attachment target folder cannot be empty.");
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new XPScriptRuntimeException(5, "Attachment target folder is invalid."); }
        try
        {
            Directory.CreateDirectory(fullPath);
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new XPScriptRuntimeException(5, "Attachment target folder cannot be a symbolic link or reparse point.");
            return fullPath;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException) { throw new XPScriptRuntimeException(70, "Permission denied while creating attachment target folder."); }
        catch (IOException) { throw new XPScriptRuntimeException(75, "Unable to create attachment target folder."); }
    }

    public static byte[] ReadAllBytes(string fullPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.LongLength > MaxAttachmentBytes) throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
            return bytes;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException) { throw new XPScriptRuntimeException(70, "Permission denied while reading attachment source file."); }
        catch (IOException) { throw new XPScriptRuntimeException(75, "Unable to read attachment source file."); }
    }

    public static void WriteAllBytes(object? targetPath, byte[] bytes)
    {
        if (bytes.LongLength > MaxAttachmentBytes) throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
        XPScriptHttpFileStorage.Save(targetPath, bytes);
    }

    public static string GuessContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".txt" => "text/plain", ".csv" => "text/csv", ".json" => "application/json", ".xml" => "application/xml",
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".svg" => "image/svg+xml", ".zip" => "application/zip",
        ".doc" => "application/msword", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel", ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint", ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}

internal static class XPScriptAttachmentRuntimeHelpers
{
    public static T? PrivateField<T>(object instance, string name) where T : class
    {
        var field = instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new XPScriptRuntimeException(5, "Attachment runtime could not access provider state.");
        return field.GetValue(instance) as T;
    }

    public static string PrivateString(object instance, string name)
    {
        var field = instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new XPScriptRuntimeException(5, "Attachment runtime could not access provider state.");
        return Convert.ToString(field.GetValue(instance), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string RequiredAttachmentId(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (!Guid.TryParse(text, out var id)) throw new XPScriptRuntimeException(5, "Attachment ID must be a GUID.");
        return id.ToString("D");
    }

    public static object DbValue(object? value)
    {
        if (value is null || XPScriptNullRuntime.IsNull(value)) return DBNull.Value;
        return value switch
        {
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or Guid => value,
            _ => XPScriptRuntime.CStr(value)
        };
    }

    public static string OwnerKey(object? value)
    {
        if (value is null || XPScriptNullRuntime.IsNull(value)) throw new XPScriptRuntimeException(5, "Attachment owner key cannot be null.");
        var text = value switch
        {
            DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => XPScriptRuntime.CStr(value)
        };
        if (text.Length is < 1 or > 1024) throw new XPScriptRuntimeException(5, "Attachment owner key must contain 1 to 1024 characters.");
        return text;
    }

    public static byte[] OwnerHash(string table, string keyColumn, string ownerKey)
        => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(table + "\0" + keyColumn + "\0" + ownerKey));

    public static string Base64Url(string value)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Checksum(byte[] bytes)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    public static System.Text.Json.Nodes.JsonObject MetadataNode(string id, string originalName, string contentType, long size,
        string created, string modified, string createdBy, string modifiedBy, string checksum)
        => new()
        {
            ["attachmentId"] = id,
            ["originalName"] = originalName,
            ["contentType"] = contentType,
            ["size"] = size,
            ["created"] = created,
            ["modified"] = modified,
            ["createdBy"] = createdBy,
            ["modifiedBy"] = modifiedBy,
            ["checksumSha256"] = checksum
        };
}

internal static class XPScriptAttachmentHttpRuntime
{
    public static byte[] Send(System.Net.Http.HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body, string contentType, double timeoutSeconds)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "Attachment HTTP URL must be absolute http:// or https://.");
        if (body is not null && body.LongLength > XPScriptAttachmentFileRuntime.MaxAttachmentBytes + 1024 * 1024)
            throw new XPScriptRuntimeException(5, "Attachment HTTP request exceeds the supported size limit.");
        using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
        using var client = new System.Net.Http.HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        using var request = new System.Net.Http.HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = new System.Net.Http.ByteArrayContent(body);
            if (contentType.Length > 0) request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        }
        foreach (var header in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new System.Net.Http.ByteArrayContent([]);
                if (!request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value)) throw new XPScriptRuntimeException(5, "Attachment HTTP header is invalid.");
            }
        }
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed with status " + (int)response.StatusCode + " " + (response.ReasonPhrase ?? string.Empty) + ".");
            if (response.Content.Headers.ContentLength is long length && length > XPScriptAttachmentFileRuntime.MaxAttachmentBytes)
                throw new XPScriptRuntimeException(5, "Attachment HTTP response exceeds the 64 MiB attachment limit.");
            using var input = response.Content.ReadAsStream(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                timeout.Token.ThrowIfCancellationRequested();
                if (read == 0) break;
                if (output.Length + read > XPScriptAttachmentFileRuntime.MaxAttachmentBytes)
                    throw new XPScriptRuntimeException(5, "Attachment HTTP response exceeds the 64 MiB attachment limit.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (OperationCanceledException) { throw new XPScriptRuntimeException(5, "Attachment HTTP operation timed out."); }
        catch (System.Net.Http.HttpRequestException) { throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed."); }
        catch (IOException) { throw new XPScriptRuntimeException(5, "Attachment HTTP response could not be read."); }
    }

    public static byte[] MultipartFile(string fieldName, string fileName, string contentType, byte[] fileBytes, out string multipartContentType)
    {
        var boundary = "----xpscript-" + Guid.NewGuid().ToString("N");
        multipartContentType = "multipart/form-data; boundary=" + boundary;
        using var stream = new MemoryStream();
        void Text(string value) { var bytes = System.Text.Encoding.UTF8.GetBytes(value); stream.Write(bytes, 0, bytes.Length); }
        Text("--" + boundary + "\r\n");
        Text("Content-Disposition: form-data; name=\"" + fieldName + "\"; filename=\"" + fileName.Replace("\"", string.Empty) + "\"\r\n");
        Text("Content-Type: " + contentType + "\r\n\r\n");
        stream.Write(fileBytes, 0, fileBytes.Length);
        Text("\r\n--" + boundary + "--\r\n");
        return stream.ToArray();
    }
}
""";

    private const string SqliteCode = """
internal static partial class XPScriptDatabaseAttachmentRuntime
{
    public static XPScriptAttachmentCollection ForSqlite(XPScriptDbSqlite db, object? table, object? keyColumn, object? keyValue)
    {
        var tableName = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(table, "SQLite attachment owner table");
        var keyName = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQLite attachment key column");
        var ownerKey = XPScriptAttachmentRuntimeHelpers.OwnerKey(keyValue);
        var connection = XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.Sqlite.SqliteConnection>(db, "_connection")
            ?? throw new XPScriptRuntimeException(5, "SQLite database is closed.");
        var transaction = XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.Sqlite.SqliteTransaction>(db, "_transaction");
        using (var verify = connection.CreateCommand())
        {
            verify.Transaction = transaction;
            verify.CommandText = "SELECT COUNT(*) FROM \"" + tableName + "\" WHERE \"" + keyName + "\"=$key";
            verify.Parameters.AddWithValue("$key", XPScriptAttachmentRuntimeHelpers.DbValue(keyValue));
            if (Convert.ToInt64(verify.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new XPScriptRuntimeException(5, "SQLite Attachments requires exactly one matching owner row.");
        }
        EnsureSqliteTable(connection, transaction);
        return new XPScriptAttachmentCollection(
            () => ListSqlite(connection, transaction, tableName, keyName, ownerKey),
            (id, name, type, actor, bytes) => CreateSqlite(connection, transaction, tableName, keyName, ownerKey, id, name, type, actor, bytes),
            (id, name, type, actor, bytes) => UpdateSqlite(connection, transaction, tableName, keyName, ownerKey, id, name, type, actor, bytes),
            id => GetSqlite(connection, transaction, tableName, keyName, ownerKey, id),
            id => DeleteSqlite(connection, transaction, tableName, keyName, ownerKey, id));
    }

    private static void EnsureSqliteTable(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx)
    {
        using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "CREATE TABLE IF NOT EXISTS __xps_attachments_v2(" +
            "attachment_id TEXT PRIMARY KEY, owner_table TEXT NOT NULL, owner_key_column TEXT NOT NULL, owner_key TEXT NOT NULL," +
            "original_name TEXT NOT NULL, content_type TEXT NOT NULL, size INTEGER NOT NULL, created_utc TEXT NOT NULL, modified_utc TEXT NOT NULL," +
            "created_by TEXT NOT NULL, modified_by TEXT NOT NULL, checksum_sha256 TEXT NOT NULL, data BLOB NOT NULL);" +
            "CREATE INDEX IF NOT EXISTS ix_xps_attach_owner ON __xps_attachments_v2(owner_table,owner_key_column,owner_key);" +
            "CREATE INDEX IF NOT EXISTS ix_xps_attach_name ON __xps_attachments_v2(owner_table,owner_key_column,owner_key,original_name);";
        command.ExecuteNonQuery();
    }

    private static XPScriptJsonArray ListSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key)
    {
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "SELECT attachment_id,original_name,content_type,size,created_utc,modified_utc,created_by,modified_by,checksum_sha256 FROM __xps_attachments_v2 WHERE owner_table=$t AND owner_key_column=$c AND owner_key=$k ORDER BY created_utc,attachment_id";
        command.Parameters.AddWithValue("$t", table); command.Parameters.AddWithValue("$c", column); command.Parameters.AddWithValue("$k", key);
        using var reader = command.ExecuteReader(); var rows = new System.Text.Json.Nodes.JsonArray();
        while (reader.Read()) rows.Add(XPScriptAttachmentRuntimeHelpers.MetadataNode(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetInt64(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8)));
        return new XPScriptJsonArray(rows);
    }

    private static XPScriptJsonObject CreateSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string id, string name, string type, string actor, byte[] bytes)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture); var checksum = XPScriptAttachmentRuntimeHelpers.Checksum(bytes);
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "INSERT INTO __xps_attachments_v2(attachment_id,owner_table,owner_key_column,owner_key,original_name,content_type,size,created_utc,modified_utc,created_by,modified_by,checksum_sha256,data) VALUES($id,$t,$c,$k,$n,$ct,$s,$created,$modified,$cb,$mb,$hash,$data)";
        command.Parameters.AddWithValue("$id",id); command.Parameters.AddWithValue("$t",table); command.Parameters.AddWithValue("$c",column); command.Parameters.AddWithValue("$k",key); command.Parameters.AddWithValue("$n",name); command.Parameters.AddWithValue("$ct",type); command.Parameters.AddWithValue("$s",bytes.LongLength); command.Parameters.AddWithValue("$created",now); command.Parameters.AddWithValue("$modified",now); command.Parameters.AddWithValue("$cb",actor); command.Parameters.AddWithValue("$mb",actor); command.Parameters.AddWithValue("$hash",checksum); command.Parameters.Add("$data",Microsoft.Data.Sqlite.SqliteType.Blob).Value=bytes; command.ExecuteNonQuery();
        return new XPScriptJsonObject(XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,now,now,actor,actor,checksum));
    }

    private static XPScriptJsonObject UpdateSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string id, string name, string type, string actor, byte[] bytes)
    {
        var existing = ListSqlite(c,tx,table,column,key).Node.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x => string.Equals(x["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase)) ?? throw new XPScriptRuntimeException(53,"SQLite attachment was not found.");
        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture); var checksum = XPScriptAttachmentRuntimeHelpers.Checksum(bytes);
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "UPDATE __xps_attachments_v2 SET original_name=$n,content_type=$ct,size=$s,modified_utc=$m,modified_by=$mb,checksum_sha256=$hash,data=$data WHERE attachment_id=$id AND owner_table=$t AND owner_key_column=$c AND owner_key=$k";
        command.Parameters.AddWithValue("$n",name); command.Parameters.AddWithValue("$ct",type); command.Parameters.AddWithValue("$s",bytes.LongLength); command.Parameters.AddWithValue("$m",now); command.Parameters.AddWithValue("$mb",actor); command.Parameters.AddWithValue("$hash",checksum); command.Parameters.Add("$data",Microsoft.Data.Sqlite.SqliteType.Blob).Value=bytes; command.Parameters.AddWithValue("$id",id); command.Parameters.AddWithValue("$t",table); command.Parameters.AddWithValue("$c",column); command.Parameters.AddWithValue("$k",key);
        if(command.ExecuteNonQuery()!=1) throw new XPScriptRuntimeException(53,"SQLite attachment was not found.");
        return new XPScriptJsonObject(XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,existing["created"]!.GetValue<string>(),now,existing["createdBy"]!.GetValue<string>(),actor,checksum));
    }

    private static byte[] GetSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string id)
    {
        using var command=c.CreateCommand(); command.Transaction=tx; command.CommandText="SELECT data FROM __xps_attachments_v2 WHERE attachment_id=$id AND owner_table=$t AND owner_key_column=$c AND owner_key=$k"; command.Parameters.AddWithValue("$id",id); command.Parameters.AddWithValue("$t",table); command.Parameters.AddWithValue("$c",column); command.Parameters.AddWithValue("$k",key); var value=command.ExecuteScalar(); if(value is not byte[] bytes) throw new XPScriptRuntimeException(53,"SQLite attachment was not found."); return bytes;
    }

    private static bool DeleteSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string id)
    {
        using var command=c.CreateCommand(); command.Transaction=tx; command.CommandText="DELETE FROM __xps_attachments_v2 WHERE attachment_id=$id AND owner_table=$t AND owner_key_column=$c AND owner_key=$k"; command.Parameters.AddWithValue("$id",id); command.Parameters.AddWithValue("$t",table); command.Parameters.AddWithValue("$c",column); command.Parameters.AddWithValue("$k",key); return command.ExecuteNonQuery()==1;
    }
}
""";

    private const string MsSqlCode = """
internal static partial class XPScriptDatabaseAttachmentRuntime
{
    public static XPScriptAttachmentCollection ForMsSql(XPScriptDbMsSql db, object? table, object? keyColumn, object? keyValue)
    {
        var tableInfo=XPScriptDatabaseDataSourceRuntime.MsSqlTable(table); var keyName=XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn,"SQL Server attachment key column"); var ownerKey=XPScriptAttachmentRuntimeHelpers.OwnerKey(keyValue);
        var connection=XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.SqlClient.SqlConnection>(db,"_connection") ?? throw new XPScriptRuntimeException(5,"SQL Server database is closed."); var tx=XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.SqlClient.SqlTransaction>(db,"_transaction");
        using(var verify=connection.CreateCommand()){verify.Transaction=tx;verify.CommandText="SELECT COUNT_BIG(*) FROM "+tableInfo.Qualified+" WHERE ["+keyName+"]=@key";verify.Parameters.AddWithValue("@key",XPScriptAttachmentRuntimeHelpers.DbValue(keyValue));if(Convert.ToInt64(verify.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture)!=1)throw new XPScriptRuntimeException(5,"SQL Server Attachments requires exactly one matching owner row.");}
        EnsureMsSqlTable(connection,tx); var ownerTable=tableInfo.Schema+"."+tableInfo.Table; var ownerHash=XPScriptAttachmentRuntimeHelpers.OwnerHash(ownerTable,keyName,ownerKey);
        return new XPScriptAttachmentCollection(
            ()=>ListMsSql(connection,tx,ownerHash),
            (id,name,type,actor,bytes)=>CreateMsSql(connection,tx,ownerTable,keyName,ownerKey,ownerHash,id,name,type,actor,bytes),
            (id,name,type,actor,bytes)=>UpdateMsSql(connection,tx,ownerHash,id,name,type,actor,bytes),
            id=>GetMsSql(connection,tx,ownerHash,id),
            id=>DeleteMsSql(connection,tx,ownerHash,id));
    }

    private static void EnsureMsSqlTable(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx)
    {
        using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="IF OBJECT_ID(N'dbo.__xps_attachments_v2',N'U') IS NULL BEGIN CREATE TABLE dbo.__xps_attachments_v2(attachment_id uniqueidentifier NOT NULL PRIMARY KEY,owner_hash binary(32) NOT NULL,owner_table nvarchar(257) NOT NULL,owner_key_column nvarchar(128) NOT NULL,owner_key nvarchar(1024) NOT NULL,original_name nvarchar(255) NOT NULL,content_type nvarchar(255) NOT NULL,size bigint NOT NULL,created_utc datetimeoffset(7) NOT NULL,modified_utc datetimeoffset(7) NOT NULL,created_by nvarchar(512) NOT NULL,modified_by nvarchar(512) NOT NULL,checksum_sha256 char(64) NOT NULL,data varbinary(max) NOT NULL); CREATE INDEX IX___xps_attachments_v2_owner ON dbo.__xps_attachments_v2(owner_hash); CREATE INDEX IX___xps_attachments_v2_name ON dbo.__xps_attachments_v2(owner_hash,original_name); END";command.ExecuteNonQuery();
    }

    private static XPScriptJsonArray ListMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx, byte[] ownerHash)
    {
        using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="SELECT attachment_id,original_name,content_type,size,created_utc,modified_utc,created_by,modified_by,checksum_sha256 FROM dbo.__xps_attachments_v2 WHERE owner_hash=@h ORDER BY created_utc,attachment_id";command.Parameters.Add("@h",System.Data.SqlDbType.Binary,32).Value=ownerHash;using var reader=command.ExecuteReader();var rows=new System.Text.Json.Nodes.JsonArray();while(reader.Read())rows.Add(XPScriptAttachmentRuntimeHelpers.MetadataNode(reader.GetGuid(0).ToString("D"),reader.GetString(1),reader.GetString(2),reader.GetInt64(3),reader.GetFieldValue<DateTimeOffset>(4).ToString("O"),reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),reader.GetString(6),reader.GetString(7),reader.GetString(8)));return new XPScriptJsonArray(rows);
    }

    private static XPScriptJsonObject CreateMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx,string table,string column,string key,byte[] ownerHash,string id,string name,string type,string actor,byte[] bytes)
    {
        var now=DateTimeOffset.UtcNow;var checksum=XPScriptAttachmentRuntimeHelpers.Checksum(bytes);using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="INSERT INTO dbo.__xps_attachments_v2(attachment_id,owner_hash,owner_table,owner_key_column,owner_key,original_name,content_type,size,created_utc,modified_utc,created_by,modified_by,checksum_sha256,data) VALUES(@id,@h,@t,@c,@k,@n,@ct,@s,@created,@modified,@cb,@mb,@hash,@data)";command.Parameters.AddWithValue("@id",Guid.Parse(id));command.Parameters.Add("@h",System.Data.SqlDbType.Binary,32).Value=ownerHash;command.Parameters.AddWithValue("@t",table);command.Parameters.AddWithValue("@c",column);command.Parameters.AddWithValue("@k",key);command.Parameters.AddWithValue("@n",name);command.Parameters.AddWithValue("@ct",type);command.Parameters.AddWithValue("@s",bytes.LongLength);command.Parameters.AddWithValue("@created",now);command.Parameters.AddWithValue("@modified",now);command.Parameters.AddWithValue("@cb",actor);command.Parameters.AddWithValue("@mb",actor);command.Parameters.AddWithValue("@hash",checksum);command.Parameters.Add("@data",System.Data.SqlDbType.VarBinary,-1).Value=bytes;command.ExecuteNonQuery();var stamp=now.ToString("O");return new XPScriptJsonObject(XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,stamp,stamp,actor,actor,checksum));
    }

    private static XPScriptJsonObject UpdateMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx,byte[] ownerHash,string id,string name,string type,string actor,byte[] bytes)
    {
        var existing=ListMsSql(c,tx,ownerHash).Node.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x=>string.Equals(x["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase))??throw new XPScriptRuntimeException(53,"SQL Server attachment was not found.");var now=DateTimeOffset.UtcNow;var checksum=XPScriptAttachmentRuntimeHelpers.Checksum(bytes);using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="UPDATE dbo.__xps_attachments_v2 SET original_name=@n,content_type=@ct,size=@s,modified_utc=@m,modified_by=@mb,checksum_sha256=@hash,data=@data WHERE attachment_id=@id AND owner_hash=@h";command.Parameters.AddWithValue("@n",name);command.Parameters.AddWithValue("@ct",type);command.Parameters.AddWithValue("@s",bytes.LongLength);command.Parameters.AddWithValue("@m",now);command.Parameters.AddWithValue("@mb",actor);command.Parameters.AddWithValue("@hash",checksum);command.Parameters.Add("@data",System.Data.SqlDbType.VarBinary,-1).Value=bytes;command.Parameters.AddWithValue("@id",Guid.Parse(id));command.Parameters.Add("@h",System.Data.SqlDbType.Binary,32).Value=ownerHash;if(command.ExecuteNonQuery()!=1)throw new XPScriptRuntimeException(53,"SQL Server attachment was not found.");return new XPScriptJsonObject(XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,existing["created"]!.GetValue<string>(),now.ToString("O"),existing["createdBy"]!.GetValue<string>(),actor,checksum));
    }

    private static byte[] GetMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx,byte[] ownerHash,string id){using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="SELECT data FROM dbo.__xps_attachments_v2 WHERE attachment_id=@id AND owner_hash=@h";command.Parameters.AddWithValue("@id",Guid.Parse(id));command.Parameters.Add("@h",System.Data.SqlDbType.Binary,32).Value=ownerHash;var value=command.ExecuteScalar();if(value is not byte[] bytes)throw new XPScriptRuntimeException(53,"SQL Server attachment was not found.");return bytes;}
    private static bool DeleteMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx,byte[] ownerHash,string id){using var command=c.CreateCommand();command.Transaction=tx;command.CommandText="DELETE FROM dbo.__xps_attachments_v2 WHERE attachment_id=@id AND owner_hash=@h";command.Parameters.AddWithValue("@id",Guid.Parse(id));command.Parameters.Add("@h",System.Data.SqlDbType.Binary,32).Value=ownerHash;return command.ExecuteNonQuery()==1;}
}
""";

    private const string HttpCode = """
internal static partial class XPScriptDatabaseAttachmentRuntime
{
    private sealed class SupabaseAttachmentConfig { public string Bucket { get; set; } = "attachments"; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XPScriptHttpDbSupabase,SupabaseAttachmentConfig> SupabaseConfigs=new();

    public static void SetSupabaseBucket(XPScriptHttpDbSupabase db, object? bucket)
    {
        var name=XPScriptRuntime.CStr(bucket).Trim();if(name.Length is <1 or >100 || name.IndexOfAny(['/','\\','?','#','\0','\r','\n'])>=0)throw new XPScriptRuntimeException(5,"Supabase attachment bucket name is invalid.");SupabaseConfigs.GetOrCreateValue(db).Bucket=name;
    }

    public static XPScriptAttachmentCollection ForSupabase(XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? keyValue)
    {
        var tableName=XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(table,"Supabase attachment owner table");var keyName=XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn,"Supabase attachment key column");var ownerKey=XPScriptAttachmentRuntimeHelpers.OwnerKey(keyValue);_=XPScriptHttpDatabaseDataSourceExtensions.GetRow(db,tableName,keyName,keyValue);var bucket=SupabaseConfigs.GetOrCreateValue(db).Bucket;var prefix=tableName+"/"+keyName+"/"+XPScriptAttachmentRuntimeHelpers.Base64Url(ownerKey);var baseUrl=db.BaseUrl.TrimEnd('/')+"/storage/v1";var apiKey=XPScriptAttachmentRuntimeHelpers.PrivateString(db,"_apiKey");var bearer=XPScriptAttachmentRuntimeHelpers.PrivateString(db,"_bearerToken");if(bearer.Length==0)bearer=apiKey;
        Dictionary<string,string> Headers()=>new(StringComparer.OrdinalIgnoreCase){{"apikey",apiKey},{"Authorization","Bearer "+bearer},{"Accept","application/json"}};
        return new XPScriptAttachmentCollection(
            ()=>ListSupabase(baseUrl,bucket,prefix,Headers(),db.Timeout),
            (id,name,type,actor,bytes)=>CreateSupabase(baseUrl,bucket,prefix,id,name,type,actor,bytes,Headers(),db.Timeout),
            (id,name,type,actor,bytes)=>UpdateSupabase(baseUrl,bucket,prefix,id,name,type,actor,bytes,Headers(),db.Timeout),
            id=>GetSupabase(baseUrl,bucket,prefix,id,Headers(),db.Timeout),
            id=>DeleteSupabase(baseUrl,bucket,prefix,id,Headers(),db.Timeout));
    }

    private static string EncodePath(string path)=>string.Join("/",path.Split('/').Select(Uri.EscapeDataString));
    private static string SupabaseDataPath(string prefix,string id)=>prefix+"/"+id+".bin";
    private static string SupabaseMetaPath(string prefix,string id)=>prefix+"/"+id+".meta.json";
    private static byte[] JsonBytes(System.Text.Json.Nodes.JsonNode node)=>System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());

    private static XPScriptJsonArray ListSupabase(string baseUrl,string bucket,string prefix,Dictionary<string,string> headers,double timeout)
    {
        var request=new System.Text.Json.Nodes.JsonObject{{"prefix",prefix},{"limit",1000},{"offset",0},{"sortBy",new System.Text.Json.Nodes.JsonObject{{"column","name"},{"order","asc"}}}};var response=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post,baseUrl+"/object/list/"+Uri.EscapeDataString(bucket),headers,JsonBytes(request),"application/json",timeout);var doc=XPScriptNativeJson.Parse(System.Text.Encoding.UTF8.GetString(response));if(doc.Node is not System.Text.Json.Nodes.JsonArray source)throw new XPScriptRuntimeException(13,"Supabase attachment list must return a JSON array.");var result=new System.Text.Json.Nodes.JsonArray();foreach(var node in source){if(node is not System.Text.Json.Nodes.JsonObject obj)continue;var name=obj["name"]?.GetValue<string>()??string.Empty;if(!name.EndsWith(".meta.json",StringComparison.OrdinalIgnoreCase))continue;var id=name[..^10];if(!Guid.TryParse(id,out _))continue;try{var meta=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get,baseUrl+"/object/authenticated/"+Uri.EscapeDataString(bucket)+"/"+EncodePath(SupabaseMetaPath(prefix,id)),headers,null,string.Empty,timeout);var parsed=XPScriptNativeJson.Parse(System.Text.Encoding.UTF8.GetString(meta));if(parsed.Node is System.Text.Json.Nodes.JsonObject m)result.Add(m.DeepClone());}catch(XPScriptRuntimeException){}}
        return new XPScriptJsonArray(result);
    }

    private static XPScriptJsonObject CreateSupabase(string baseUrl,string bucket,string prefix,string id,string name,string type,string actor,byte[] bytes,Dictionary<string,string> headers,double timeout)
    {
        var now=DateTimeOffset.UtcNow.ToString("O");var checksum=XPScriptAttachmentRuntimeHelpers.Checksum(bytes);var metadata=XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,now,now,actor,actor,checksum);UploadSupabase(baseUrl,bucket,SupabaseDataPath(prefix,id),bytes,type,headers,timeout,false);UploadSupabase(baseUrl,bucket,SupabaseMetaPath(prefix,id),JsonBytes(metadata),"application/json",headers,timeout,false);return new XPScriptJsonObject(metadata);
    }

    private static XPScriptJsonObject UpdateSupabase(string baseUrl,string bucket,string prefix,string id,string name,string type,string actor,byte[] bytes,Dictionary<string,string> headers,double timeout)
    {
        var existing=ListSupabase(baseUrl,bucket,prefix,headers,timeout).Node.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x=>string.Equals(x["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase))??throw new XPScriptRuntimeException(53,"Supabase attachment was not found.");var now=DateTimeOffset.UtcNow.ToString("O");var checksum=XPScriptAttachmentRuntimeHelpers.Checksum(bytes);var metadata=XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,existing["created"]!.GetValue<string>(),now,existing["createdBy"]!.GetValue<string>(),actor,checksum);UploadSupabase(baseUrl,bucket,SupabaseDataPath(prefix,id),bytes,type,headers,timeout,true);UploadSupabase(baseUrl,bucket,SupabaseMetaPath(prefix,id),JsonBytes(metadata),"application/json",headers,timeout,true);return new XPScriptJsonObject(metadata);
    }

    private static void UploadSupabase(string baseUrl,string bucket,string objectPath,byte[] bytes,string type,Dictionary<string,string> headers,double timeout,bool upsert)
    {
        var h=new Dictionary<string,string>(headers,StringComparer.OrdinalIgnoreCase);if(upsert)h["x-upsert"]="true";_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post,baseUrl+"/object/"+Uri.EscapeDataString(bucket)+"/"+EncodePath(objectPath),h,bytes,type,timeout);
    }

    private static byte[] GetSupabase(string baseUrl,string bucket,string prefix,string id,Dictionary<string,string> headers,double timeout)=>XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get,baseUrl+"/object/authenticated/"+Uri.EscapeDataString(bucket)+"/"+EncodePath(SupabaseDataPath(prefix,id)),headers,null,string.Empty,timeout);
    private static bool DeleteSupabase(string baseUrl,string bucket,string prefix,string id,Dictionary<string,string> headers,double timeout){_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Delete,baseUrl+"/object/"+Uri.EscapeDataString(bucket)+"/"+EncodePath(SupabaseDataPath(prefix,id)),headers,null,string.Empty,timeout);_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Delete,baseUrl+"/object/"+Uri.EscapeDataString(bucket)+"/"+EncodePath(SupabaseMetaPath(prefix,id)),headers,null,string.Empty,timeout);return true;}

    public static XPScriptAttachmentCollection ForDomino(XPScriptHttpDbDominoRest db, object? unid)=>ForDomino(db,unid,string.Empty);
    public static XPScriptAttachmentCollection ForDomino(XPScriptHttpDbDominoRest db, object? unid, object? fieldName)
    {
        var id=XPScriptRuntime.CStr(unid).Trim().ToUpperInvariant();if(id.Length!=32||!id.All(Uri.IsHexDigit))throw new XPScriptRuntimeException(5,"Domino attachment owner UNID must contain exactly 32 hexadecimal characters.");var field=XPScriptRuntime.CStr(fieldName).Trim();if(field.Length>128||field.IndexOfAny(['&','?','#','\0','\r','\n'])>=0)throw new XPScriptRuntimeException(5,"Domino attachment rich-text field name is invalid.");var baseUrl=db.BaseUrl.TrimEnd('/')+"/api/v1";var dataSource=db.DataSource;var bearer=db.BearerToken;if(string.IsNullOrWhiteSpace(bearer))throw new XPScriptRuntimeException(5,"Domino attachment access requires a bearer token.");var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"Authorization","Bearer "+bearer},{"Accept","application/json"}};
        return new XPScriptAttachmentCollection(
            ()=>DominoMetadata(db,id),
            (attachmentId,name,type,actor,bytes)=>CreateDomino(db,baseUrl,dataSource,id,field,attachmentId,name,type,actor,bytes,headers),
            (attachmentId,name,type,actor,bytes)=>UpdateDomino(db,baseUrl,dataSource,id,field,attachmentId,name,type,actor,bytes,headers),
            attachmentId=>GetDomino(db,baseUrl,dataSource,id,attachmentId,headers),
            attachmentId=>DeleteDomino(db,baseUrl,dataSource,id,field,attachmentId,headers));
    }

    private const string DominoMetadataItem="XPSAttachmentsJson";
    private static XPScriptJsonArray DominoMetadata(XPScriptHttpDbDominoRest db,string unid)
    {
        var doc=db.GetDocument(unid);if(doc.Node is not System.Text.Json.Nodes.JsonObject root)return new XPScriptJsonArray(new System.Text.Json.Nodes.JsonArray());var text=root[DominoMetadataItem]?.GetValue<string>()??string.Empty;if(string.IsNullOrWhiteSpace(text))return new XPScriptJsonArray(new System.Text.Json.Nodes.JsonArray());try{var parsed=XPScriptNativeJson.Parse(text);return parsed.Node is System.Text.Json.Nodes.JsonArray array?new XPScriptJsonArray((System.Text.Json.Nodes.JsonArray)array.DeepClone()):new XPScriptJsonArray(new System.Text.Json.Nodes.JsonArray());}catch{return new XPScriptJsonArray(new System.Text.Json.Nodes.JsonArray());}
    }

    private static void SaveDominoMetadata(XPScriptHttpDbDominoRest db,string unid,XPScriptJsonArray metadata)
    {
        var patch=XPScriptNativeJson.CreateObject();patch.Set(DominoMetadataItem,metadata.Stringify());_=db.PatchDocument(unid,patch);
    }

    private static string DominoStorageName(string id,string originalName){var ext=Path.GetExtension(originalName);if(ext.Length>16)ext=string.Empty;return id+ext.ToLowerInvariant();}

    private static XPScriptJsonObject CreateDomino(XPScriptHttpDbDominoRest db,string baseUrl,string dataSource,string unid,string field,string id,string name,string type,string actor,byte[] bytes,Dictionary<string,string> headers)
    {
        var storage=DominoStorageName(id,name);var multipart=XPScriptAttachmentHttpRuntime.MultipartFile("filename",storage,type,bytes,out var multipartType);var url=baseUrl+"/attachments/"+Uri.EscapeDataString(unid)+"?dataSource="+Uri.EscapeDataString(dataSource);if(field.Length>0)url+="&fieldName="+Uri.EscapeDataString(field);_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post,url,headers,multipart,multipartType,db.Timeout);var now=DateTimeOffset.UtcNow.ToString("O");var metadata=XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,now,now,actor,actor,XPScriptAttachmentRuntimeHelpers.Checksum(bytes));metadata["storageName"]=storage;var list=DominoMetadata(db,unid);list.Node.Add(metadata.DeepClone());SaveDominoMetadata(db,unid,list);metadata.Remove("storageName");return new XPScriptJsonObject(metadata);
    }

    private static XPScriptJsonObject UpdateDomino(XPScriptHttpDbDominoRest db,string baseUrl,string dataSource,string unid,string field,string id,string name,string type,string actor,byte[] bytes,Dictionary<string,string> headers)
    {
        var list=DominoMetadata(db,unid);var current=list.Node.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x=>string.Equals(x["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase))??throw new XPScriptRuntimeException(53,"Domino attachment was not found.");var oldStorage=current["storageName"]?.GetValue<string>()??DominoStorageName(id,current["originalName"]?.GetValue<string>()??name);DeleteDominoBinary(db,baseUrl,dataSource,unid,field,oldStorage,headers);var storage=DominoStorageName(id,name);var multipart=XPScriptAttachmentHttpRuntime.MultipartFile("filename",storage,type,bytes,out var multipartType);var url=baseUrl+"/attachments/"+Uri.EscapeDataString(unid)+"?dataSource="+Uri.EscapeDataString(dataSource);if(field.Length>0)url+="&fieldName="+Uri.EscapeDataString(field);_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post,url,headers,multipart,multipartType,db.Timeout);var now=DateTimeOffset.UtcNow.ToString("O");var replacement=XPScriptAttachmentRuntimeHelpers.MetadataNode(id,name,type,bytes.LongLength,current["created"]!.GetValue<string>(),now,current["createdBy"]!.GetValue<string>(),actor,XPScriptAttachmentRuntimeHelpers.Checksum(bytes));replacement["storageName"]=storage;for(var i=0;i<list.Node.Count;i++)if(list.Node[i] is System.Text.Json.Nodes.JsonObject obj&&string.Equals(obj["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase)){list.Node[i]=replacement.DeepClone();break;}SaveDominoMetadata(db,unid,list);replacement.Remove("storageName");return new XPScriptJsonObject(replacement);
    }

    private static byte[] GetDomino(XPScriptHttpDbDominoRest db,string baseUrl,string dataSource,string unid,string id,Dictionary<string,string> headers)
    {
        var current=DominoMetadata(db,unid).Node.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x=>string.Equals(x["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase))??throw new XPScriptRuntimeException(53,"Domino attachment was not found.");var storage=current["storageName"]?.GetValue<string>()??DominoStorageName(id,current["originalName"]?.GetValue<string>()??string.Empty);return XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get,baseUrl+"/attachments/"+Uri.EscapeDataString(unid)+"/"+Uri.EscapeDataString(storage)+"?dataSource="+Uri.EscapeDataString(dataSource),headers,null,string.Empty,db.Timeout);
    }

    private static bool DeleteDomino(XPScriptHttpDbDominoRest db,string baseUrl,string dataSource,string unid,string field,string id,Dictionary<string,string> headers)
    {
        var list=DominoMetadata(db,unid);for(var i=0;i<list.Node.Count;i++){if(list.Node[i] is not System.Text.Json.Nodes.JsonObject current||!string.Equals(current["attachmentId"]?.GetValue<string>(),id,StringComparison.OrdinalIgnoreCase))continue;var storage=current["storageName"]?.GetValue<string>()??DominoStorageName(id,current["originalName"]?.GetValue<string>()??string.Empty);DeleteDominoBinary(db,baseUrl,dataSource,unid,field,storage,headers);list.Node.RemoveAt(i);SaveDominoMetadata(db,unid,list);return true;}return false;
    }

    private static void DeleteDominoBinary(XPScriptHttpDbDominoRest db,string baseUrl,string dataSource,string unid,string field,string storage,Dictionary<string,string> headers)
    {
        var url=baseUrl+"/attachments/"+Uri.EscapeDataString(unid)+"/"+Uri.EscapeDataString(storage)+"?dataSource="+Uri.EscapeDataString(dataSource);if(field.Length>0)url+="&fieldName="+Uri.EscapeDataString(field);_=XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Delete,url,headers,null,string.Empty,db.Timeout);
    }
}
""";
}
