namespace XPScript.Compiler;

internal static class DatabaseAttachmentRuntimeSource
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
    private readonly Func<XPScriptJsonArray> _list;
    private readonly Func<string, string, byte[], bool> _save;
    private readonly Func<string, byte[]> _get;
    private readonly Func<string, bool> _delete;

    internal XPScriptAttachmentCollection(
        Func<XPScriptJsonArray> list,
        Func<string, string, byte[], bool> save,
        Func<string, byte[]> get,
        Func<string, bool> delete)
    {
        _list = list;
        _save = save;
        _get = get;
        _delete = delete;
    }

    public XPScriptJsonArray List() => _list();

    public bool Save(object? sourcePath)
    {
        var path = XPScriptAttachmentFileRuntime.RequiredExistingFile(sourcePath);
        return SaveAs(path, Path.GetFileName(path));
    }

    public bool SaveAs(object? sourcePath, object? attachmentName)
    {
        var path = XPScriptAttachmentFileRuntime.RequiredExistingFile(sourcePath);
        var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(attachmentName);
        var bytes = XPScriptAttachmentFileRuntime.ReadAllBytes(path);
        return _save(name, XPScriptAttachmentFileRuntime.GuessContentType(name), bytes);
    }

    public bool Get(object? attachmentName, object? targetPath)
    {
        var bytes = _get(XPScriptAttachmentFileRuntime.RequiredAttachmentName(attachmentName));
        XPScriptAttachmentFileRuntime.WriteAllBytes(targetPath, bytes);
        return true;
    }

    public bool Delete(object? attachmentName)
        => _delete(XPScriptAttachmentFileRuntime.RequiredAttachmentName(attachmentName));
}

internal static class XPScriptAttachmentFileRuntime
{
    public const int MaxAttachmentBytes = 64 * 1024 * 1024;

    public static string RequiredAttachmentName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 255 || name is "." or ".." ||
            name.IndexOfAny(['/', '\\', '\0', '\r', '\n']) >= 0 || name.Any(char.IsControl))
            throw new XPScriptRuntimeException(5, "Attachment name must be a simple file name of 1 to 255 characters.");
        return name;
    }

    public static string RequiredExistingFile(object? value)
    {
        var path = XPScriptRuntime.CStr(value).Trim();
        if (path.Length == 0)
            throw new XPScriptRuntimeException(5, "Attachment source path cannot be empty.");
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Attachment source path is invalid.");
        }
        try
        {
            if (!File.Exists(fullPath))
                throw new XPScriptRuntimeException(53, "Attachment source file does not exist.");
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new XPScriptRuntimeException(5, "Attachment source cannot be a symbolic link or reparse point.");
            if (new FileInfo(fullPath).Length > MaxAttachmentBytes)
                throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
            return fullPath;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException)
        {
            throw new XPScriptRuntimeException(70, "Permission denied while reading attachment source file.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(75, "Unable to inspect attachment source file.");
        }
    }

    public static byte[] ReadAllBytes(string fullPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.LongLength > MaxAttachmentBytes)
                throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
            return bytes;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException)
        {
            throw new XPScriptRuntimeException(70, "Permission denied while reading attachment source file.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(75, "Unable to read attachment source file.");
        }
    }

    public static void WriteAllBytes(object? targetPath, byte[] bytes)
    {
        if (bytes.LongLength > MaxAttachmentBytes)
            throw new XPScriptRuntimeException(5, "Attachment exceeds the 64 MiB XPScript attachment limit.");
        XPScriptHttpFileStorage.Save(targetPath, bytes);
    }

    public static string GuessContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".zip" => "application/zip",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
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
        if (value is null || XPScriptNullRuntime.IsNull(value))
            throw new XPScriptRuntimeException(5, "Attachment owner key cannot be null.");
        var text = value switch
        {
            DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => XPScriptRuntime.CStr(value)
        };
        if (text.Length is < 1 or > 1024)
            throw new XPScriptRuntimeException(5, "Attachment owner key must contain 1 to 1024 characters.");
        return text;
    }

    public static byte[] OwnerHash(string table, string keyColumn, string ownerKey)
        => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(table + "\0" + keyColumn + "\0" + ownerKey));

    public static string Base64Url(string value)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
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
            if (contentType.Length > 0)
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        }
        foreach (var header in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new System.Net.Http.ByteArrayContent([]);
                if (!request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    throw new XPScriptRuntimeException(5, "Attachment HTTP header is invalid.");
            }
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed with status " + (int)response.StatusCode + " " + (response.ReasonPhrase ?? string.Empty) + ".");
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
        catch (OperationCanceledException)
        {
            throw new XPScriptRuntimeException(5, "Attachment HTTP operation timed out.");
        }
        catch (System.Net.Http.HttpRequestException)
        {
            throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(5, "Attachment HTTP response could not be read.");
        }
    }

    public static byte[] MultipartFile(string fieldName, string fileName, string contentType, byte[] fileBytes, out string multipartContentType)
    {
        var boundary = "----xpscript-" + Guid.NewGuid().ToString("N");
        multipartContentType = "multipart/form-data; boundary=" + boundary;
        using var stream = new MemoryStream();
        void Text(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
        Text("--" + boundary + "\r\n");
        Text("Content-Disposition: form-data; name=\"" + fieldName + "\"; filename=\"" + fileName.Replace("\"", string.Empty) + "\"\r\n");
        Text("Content-Type: " + contentType + "\r\n\r\n");
        stream.Write(fileBytes, 0, fileBytes.Length);
        Text("\r\n--" + boundary + "--\r\n");
        return stream.ToArray();
    }

    public static XPScriptJsonDocument ParseJson(byte[] bytes, string operation)
    {
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        if (string.IsNullOrWhiteSpace(text))
            throw new XPScriptRuntimeException(5, operation + " returned an empty JSON response.");
        return XPScriptNativeJson.Parse(text);
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
            verify.CommandText = "SELECT COUNT(*) FROM \"" + tableName + "\" WHERE \"" + keyName + "\" = $key";
            verify.Parameters.AddWithValue("$key", XPScriptAttachmentRuntimeHelpers.DbValue(keyValue));
            if (Convert.ToInt64(verify.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new XPScriptRuntimeException(5, "SQLite Attachments requires exactly one matching owner row.");
        }
        EnsureSqliteTable(connection, transaction);
        return new XPScriptAttachmentCollection(
            () => ListSqlite(connection, transaction, tableName, keyName, ownerKey),
            (name, type, bytes) => SaveSqlite(connection, transaction, tableName, keyName, ownerKey, name, type, bytes),
            name => GetSqlite(connection, transaction, tableName, keyName, ownerKey, name),
            name => DeleteSqlite(connection, transaction, tableName, keyName, ownerKey, name));
    }

    private static void EnsureSqliteTable(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "CREATE TABLE IF NOT EXISTS __xps_attachments (owner_table TEXT NOT NULL, owner_key_column TEXT NOT NULL, owner_key TEXT NOT NULL, name TEXT NOT NULL, content_type TEXT NOT NULL, size INTEGER NOT NULL, modified_utc TEXT NOT NULL, data BLOB NOT NULL, PRIMARY KEY(owner_table,owner_key_column,owner_key,name))";
        command.ExecuteNonQuery();
    }

    private static XPScriptJsonArray ListSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key)
    {
        using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT name,content_type,size,modified_utc FROM __xps_attachments WHERE owner_table=$table AND owner_key_column=$column AND owner_key=$key ORDER BY name";
        command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); command.Parameters.AddWithValue("$key", key);
        using var reader = command.ExecuteReader();
        var rows = new System.Text.Json.Nodes.JsonArray();
        while (reader.Read()) rows.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = reader.GetString(0), ["contentType"] = reader.GetString(1), ["size"] = reader.GetInt64(2), ["modified"] = reader.GetString(3) });
        return new XPScriptJsonArray(rows);
    }

    private static bool SaveSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string name, string type, byte[] bytes)
    {
        using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "INSERT INTO __xps_attachments(owner_table,owner_key_column,owner_key,name,content_type,size,modified_utc,data) VALUES($table,$column,$key,$name,$type,$size,$modified,$data) ON CONFLICT(owner_table,owner_key_column,owner_key,name) DO UPDATE SET content_type=excluded.content_type,size=excluded.size,modified_utc=excluded.modified_utc,data=excluded.data";
        command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$name", name); command.Parameters.AddWithValue("$type", type); command.Parameters.AddWithValue("$size", bytes.LongLength); command.Parameters.AddWithValue("$modified", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)); command.Parameters.Add("$data", Microsoft.Data.Sqlite.SqliteType.Blob).Value = bytes;
        command.ExecuteNonQuery(); return true;
    }

    private static byte[] GetSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string name)
    {
        using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT data FROM __xps_attachments WHERE owner_table=$table AND owner_key_column=$column AND owner_key=$key AND name=$name";
        command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$name", name);
        if (command.ExecuteScalar() is not byte[] bytes) throw new XPScriptRuntimeException(53, "SQLite attachment was not found.");
        if (bytes.LongLength > XPScriptAttachmentFileRuntime.MaxAttachmentBytes) throw new XPScriptRuntimeException(5, "SQLite attachment exceeds the 64 MiB XPScript attachment limit.");
        return bytes;
    }

    private static bool DeleteSqlite(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction? tx, string table, string column, string key, string name)
    {
        using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "DELETE FROM __xps_attachments WHERE owner_table=$table AND owner_key_column=$column AND owner_key=$key AND name=$name";
        command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$name", name);
        return command.ExecuteNonQuery() == 1;
    }
}
""";

    private const string MsSqlCode = """
internal static partial class XPScriptDatabaseAttachmentRuntime
{
    public static XPScriptAttachmentCollection ForMsSql(XPScriptDbMsSql db, object? table, object? keyColumn, object? keyValue)
    {
        var info = XPScriptDatabaseDataSourceRuntime.MsSqlTable(table);
        var column = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQL Server attachment key column");
        var ownerKey = XPScriptAttachmentRuntimeHelpers.OwnerKey(keyValue);
        var ownerTable = info.Schema + "." + info.Table;
        var ownerHash = XPScriptAttachmentRuntimeHelpers.OwnerHash(ownerTable, column, ownerKey);
        var connection = XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.SqlClient.SqlConnection>(db, "_connection")
            ?? throw new XPScriptRuntimeException(5, "SQL Server database is closed.");
        var transaction = XPScriptAttachmentRuntimeHelpers.PrivateField<Microsoft.Data.SqlClient.SqlTransaction>(db, "_transaction");

        using (var verify = connection.CreateCommand())
        {
            verify.Transaction = transaction;
            verify.CommandText = "SELECT COUNT_BIG(*) FROM " + info.Qualified + " WHERE [" + column + "]=@key";
            verify.Parameters.AddWithValue("@key", XPScriptAttachmentRuntimeHelpers.DbValue(keyValue));
            if (Convert.ToInt64(verify.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new XPScriptRuntimeException(5, "SQL Server Attachments requires exactly one matching owner row.");
        }
        EnsureMsSqlTable(connection, transaction);
        return new XPScriptAttachmentCollection(
            () => ListMsSql(connection, transaction, ownerHash),
            (name, type, bytes) => SaveMsSql(connection, transaction, ownerTable, column, ownerKey, ownerHash, name, type, bytes),
            name => GetMsSql(connection, transaction, ownerHash, name),
            name => DeleteMsSql(connection, transaction, ownerHash, name));
    }

    private static void EnsureMsSqlTable(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "IF OBJECT_ID(N'dbo.__xps_attachments',N'U') IS NULL BEGIN CREATE TABLE dbo.__xps_attachments(attachment_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK___xps_attachments PRIMARY KEY, owner_hash binary(32) NOT NULL, owner_table nvarchar(257) NOT NULL, owner_key_column nvarchar(128) NOT NULL, owner_key nvarchar(1024) NOT NULL, name nvarchar(255) NOT NULL, content_type nvarchar(255) NOT NULL, size bigint NOT NULL, modified_utc datetimeoffset(7) NOT NULL, data varbinary(max) NOT NULL, CONSTRAINT UQ___xps_attachments_owner_name UNIQUE(owner_hash,name)); END";
        command.ExecuteNonQuery();
    }

    private static Microsoft.Data.SqlClient.SqlParameter HashParameter(Microsoft.Data.SqlClient.SqlCommand command, byte[] hash)
    {
        var p = command.Parameters.Add("@owner_hash", System.Data.SqlDbType.Binary, 32); p.Value = hash; return p;
    }

    private static XPScriptJsonArray ListMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx, byte[] hash)
    {
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "SELECT name,content_type,size,modified_utc FROM dbo.__xps_attachments WHERE owner_hash=@owner_hash ORDER BY name"; HashParameter(command, hash);
        using var reader = command.ExecuteReader();
        var rows = new System.Text.Json.Nodes.JsonArray();
        while (reader.Read()) rows.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = reader.GetString(0), ["contentType"] = reader.GetString(1), ["size"] = reader.GetInt64(2), ["modified"] = reader.GetFieldValue<DateTimeOffset>(3).ToString("O", System.Globalization.CultureInfo.InvariantCulture) });
        return new XPScriptJsonArray(rows);
    }

    private static bool SaveMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx, string table, string column, string key, byte[] hash, string name, string type, byte[] bytes)
    {
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "UPDATE dbo.__xps_attachments SET owner_table=@table,owner_key_column=@column,owner_key=@key,content_type=@type,size=@size,modified_utc=@modified,data=@data WHERE owner_hash=@owner_hash AND name=@name; IF @@ROWCOUNT=0 INSERT INTO dbo.__xps_attachments(owner_hash,owner_table,owner_key_column,owner_key,name,content_type,size,modified_utc,data) VALUES(@owner_hash,@table,@column,@key,@name,@type,@size,@modified,@data);";
        HashParameter(command, hash); command.Parameters.AddWithValue("@table", table); command.Parameters.AddWithValue("@column", column); command.Parameters.AddWithValue("@key", key); command.Parameters.AddWithValue("@name", name); command.Parameters.AddWithValue("@type", type); command.Parameters.AddWithValue("@size", bytes.LongLength); command.Parameters.AddWithValue("@modified", DateTimeOffset.UtcNow); command.Parameters.Add("@data", System.Data.SqlDbType.VarBinary, -1).Value = bytes;
        command.ExecuteNonQuery(); return true;
    }

    private static byte[] GetMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx, byte[] hash, string name)
    {
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "SELECT data FROM dbo.__xps_attachments WHERE owner_hash=@owner_hash AND name=@name"; HashParameter(command, hash); command.Parameters.AddWithValue("@name", name);
        if (command.ExecuteScalar() is not byte[] bytes) throw new XPScriptRuntimeException(53, "SQL Server attachment was not found.");
        if (bytes.LongLength > XPScriptAttachmentFileRuntime.MaxAttachmentBytes) throw new XPScriptRuntimeException(5, "SQL Server attachment exceeds the 64 MiB XPScript attachment limit.");
        return bytes;
    }

    private static bool DeleteMsSql(Microsoft.Data.SqlClient.SqlConnection c, Microsoft.Data.SqlClient.SqlTransaction? tx, byte[] hash, string name)
    {
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "DELETE FROM dbo.__xps_attachments WHERE owner_hash=@owner_hash AND name=@name"; HashParameter(command, hash); command.Parameters.AddWithValue("@name", name);
        return command.ExecuteNonQuery() == 1;
    }
}
""";

    private const string HttpCode = """
internal static partial class XPScriptDatabaseAttachmentRuntime
{
    private sealed class SupabaseAttachmentConfig { public string Bucket { get; set; } = "attachments"; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XPScriptHttpDbSupabase, SupabaseAttachmentConfig> SupabaseConfigs = new();

    public static void SetSupabaseBucket(XPScriptHttpDbSupabase db, object? bucket)
    {
        var name = XPScriptRuntime.CStr(bucket).Trim();
        if (name.Length is < 1 or > 100 || name.IndexOfAny(['/', '\\', '?', '#', '\0', '\r', '\n']) >= 0)
            throw new XPScriptRuntimeException(5, "Supabase attachment bucket name is invalid.");
        SupabaseConfigs.GetOrCreateValue(db).Bucket = name;
    }

    public static XPScriptAttachmentCollection ForSupabase(XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? keyValue)
    {
        var tableName = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(table, "Supabase attachment owner table");
        var column = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "Supabase attachment key column");
        var ownerKey = XPScriptAttachmentRuntimeHelpers.OwnerKey(keyValue);
        _ = XPScriptHttpDatabaseDataSourceExtensions.GetRow(db, tableName, column, keyValue);
        var bucket = SupabaseConfigs.GetOrCreateValue(db).Bucket;
        var prefix = tableName + "/" + column + "/" + XPScriptAttachmentRuntimeHelpers.Base64Url(ownerKey);
        var baseUrl = db.BaseUrl.TrimEnd('/') + "/storage/v1";
        var apiKey = XPScriptAttachmentRuntimeHelpers.PrivateString(db, "_apiKey");
        var bearer = XPScriptAttachmentRuntimeHelpers.PrivateString(db, "_bearerToken");
        if (bearer.Length == 0) bearer = apiKey;
        Dictionary<string,string> Headers() => new(StringComparer.OrdinalIgnoreCase) { ["apikey"] = apiKey, ["Authorization"] = "Bearer " + bearer, ["Accept"] = "application/json" };
        return new XPScriptAttachmentCollection(
            () => ListSupabase(baseUrl, bucket, prefix, Headers(), db.Timeout),
            (name, type, bytes) => SaveSupabase(baseUrl, bucket, prefix, name, type, bytes, Headers(), db.Timeout),
            name => GetSupabase(baseUrl, bucket, prefix, name, Headers(), db.Timeout),
            name => DeleteSupabase(baseUrl, bucket, prefix, name, Headers(), db.Timeout));
    }

    private static string EncodePath(string path) => string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

    private static XPScriptJsonArray ListSupabase(string baseUrl, string bucket, string prefix, Dictionary<string,string> headers, double timeout)
    {
        var request = new System.Text.Json.Nodes.JsonObject { ["prefix"] = prefix, ["limit"] = 1000, ["offset"] = 0, ["sortBy"] = new System.Text.Json.Nodes.JsonObject { ["column"] = "name", ["order"] = "asc" } };
        var bytes = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post, baseUrl + "/object/list/" + Uri.EscapeDataString(bucket), headers, System.Text.Encoding.UTF8.GetBytes(request.ToJsonString()), "application/json", timeout);
        var doc = XPScriptAttachmentHttpRuntime.ParseJson(bytes, "Supabase attachment list");
        if (doc.Node is not System.Text.Json.Nodes.JsonArray source) throw new XPScriptRuntimeException(13, "Supabase attachment list must return a JSON array.");
        var rows = new System.Text.Json.Nodes.JsonArray();
        foreach (var node in source)
        {
            if (node is not System.Text.Json.Nodes.JsonObject obj) continue;
            var name = obj["name"] is System.Text.Json.Nodes.JsonValue nameValue && nameValue.TryGetValue<string>(out var n) ? n : string.Empty;
            if (name.Length == 0) continue;
            long size = 0; string type = string.Empty;
            if (obj["metadata"] is System.Text.Json.Nodes.JsonObject metadata)
            {
                if (metadata["size"] is System.Text.Json.Nodes.JsonValue sizeValue) sizeValue.TryGetValue<long>(out size);
                if (metadata["mimetype"] is System.Text.Json.Nodes.JsonValue typeValue) typeValue.TryGetValue<string>(out type);
            }
            var modified = obj["updated_at"] is System.Text.Json.Nodes.JsonValue modifiedValue && modifiedValue.TryGetValue<string>(out var m) ? m : string.Empty;
            rows.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = name, ["size"] = size, ["contentType"] = type, ["modified"] = modified });
        }
        return new XPScriptJsonArray(rows);
    }

    private static bool SaveSupabase(string baseUrl, string bucket, string prefix, string name, string type, byte[] bytes, Dictionary<string,string> headers, double timeout)
    {
        headers["x-upsert"] = "true";
        _ = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post, baseUrl + "/object/" + Uri.EscapeDataString(bucket) + "/" + EncodePath(prefix + "/" + name), headers, bytes, type, timeout); return true;
    }

    private static byte[] GetSupabase(string baseUrl, string bucket, string prefix, string name, Dictionary<string,string> headers, double timeout)
        => XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get, baseUrl + "/object/authenticated/" + Uri.EscapeDataString(bucket) + "/" + EncodePath(prefix + "/" + name), headers, null, string.Empty, timeout);

    private static bool DeleteSupabase(string baseUrl, string bucket, string prefix, string name, Dictionary<string,string> headers, double timeout)
    {
        _ = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Delete, baseUrl + "/object/" + Uri.EscapeDataString(bucket) + "/" + EncodePath(prefix + "/" + name), headers, null, string.Empty, timeout); return true;
    }

    public static XPScriptAttachmentCollection ForDomino(XPScriptHttpDbDominoRest db, object? unid) => ForDomino(db, unid, string.Empty);

    public static XPScriptAttachmentCollection ForDomino(XPScriptHttpDbDominoRest db, object? unid, object? fieldName)
    {
        var id = XPScriptRuntime.CStr(unid).Trim().ToUpperInvariant();
        if (id.Length != 32 || !id.All(Uri.IsHexDigit)) throw new XPScriptRuntimeException(5, "Domino attachment owner UNID must contain exactly 32 hexadecimal characters.");
        var field = XPScriptRuntime.CStr(fieldName).Trim();
        if (field.Length > 128 || field.IndexOfAny(['&','?','#','\0','\r','\n']) >= 0) throw new XPScriptRuntimeException(5, "Domino attachment rich-text field name is invalid.");
        var bearer = db.BearerToken;
        if (string.IsNullOrWhiteSpace(bearer)) throw new XPScriptRuntimeException(5, "Domino attachment access requires a bearer token.");
        var baseUrl = db.BaseUrl.TrimEnd('/') + "/api/v1"; var source = db.DataSource;
        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["Authorization"] = "Bearer " + bearer, ["Accept"] = "application/json" };
        return new XPScriptAttachmentCollection(
            () => ListDomino(baseUrl, source, id, headers, db.Timeout),
            (name, type, bytes) => SaveDomino(baseUrl, source, id, field, name, type, bytes, headers, db.Timeout),
            name => GetDomino(baseUrl, source, id, name, headers, db.Timeout),
            name => DeleteDomino(baseUrl, source, id, field, name, headers, db.Timeout));
    }

    private static XPScriptJsonArray ListDomino(string baseUrl, string source, string unid, Dictionary<string,string> headers, double timeout)
    {
        var bytes = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get, baseUrl + "/attachmentnames/" + Uri.EscapeDataString(unid) + "?dataSource=" + Uri.EscapeDataString(source) + "&includeAttachmentMetadata=true", headers, null, string.Empty, timeout);
        var doc = XPScriptAttachmentHttpRuntime.ParseJson(bytes, "Domino attachment list");
        System.Text.Json.Nodes.JsonArray? items = doc.Node as System.Text.Json.Nodes.JsonArray;
        if (items is null && doc.Node is System.Text.Json.Nodes.JsonObject root) items = root["attachments"] as System.Text.Json.Nodes.JsonArray ?? root["files"] as System.Text.Json.Nodes.JsonArray ?? root["$FILES"] as System.Text.Json.Nodes.JsonArray;
        if (items is null) throw new XPScriptRuntimeException(13, "Domino attachment list returned an unsupported JSON shape.");
        var rows = new System.Text.Json.Nodes.JsonArray();
        foreach (var item in items)
        {
            if (item is System.Text.Json.Nodes.JsonValue scalar && scalar.TryGetValue<string>(out var simple)) { rows.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = simple, ["size"] = 0L, ["contentType"] = string.Empty, ["modified"] = string.Empty }); continue; }
            if (item is not System.Text.Json.Nodes.JsonObject obj) continue;
            string name = string.Empty; long size = 0; string type = string.Empty; string modified = string.Empty;
            if (obj["name"] is System.Text.Json.Nodes.JsonValue nv) nv.TryGetValue<string>(out name);
            if (name.Length == 0 && obj["filename"] is System.Text.Json.Nodes.JsonValue fn) fn.TryGetValue<string>(out name);
            if (obj["size"] is System.Text.Json.Nodes.JsonValue sv) sv.TryGetValue<long>(out size);
            if (obj["contentType"] is System.Text.Json.Nodes.JsonValue tv) tv.TryGetValue<string>(out type);
            if (obj["modified"] is System.Text.Json.Nodes.JsonValue mv) mv.TryGetValue<string>(out modified);
            if (name.Length > 0) rows.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = name, ["size"] = size, ["contentType"] = type, ["modified"] = modified });
        }
        return new XPScriptJsonArray(rows);
    }

    private static bool SaveDomino(string baseUrl, string source, string unid, string field, string name, string type, byte[] bytes, Dictionary<string,string> headers, double timeout)
    {
        var multipart = XPScriptAttachmentHttpRuntime.MultipartFile("file", name, type, bytes, out var multipartType);
        var url = baseUrl + "/attachments/" + Uri.EscapeDataString(unid) + "?dataSource=" + Uri.EscapeDataString(source); if (field.Length > 0) url += "&fieldName=" + Uri.EscapeDataString(field);
        _ = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Post, url, headers, multipart, multipartType, timeout); return true;
    }

    private static byte[] GetDomino(string baseUrl, string source, string unid, string name, Dictionary<string,string> headers, double timeout)
        => XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Get, baseUrl + "/attachments/" + Uri.EscapeDataString(unid) + "/" + Uri.EscapeDataString(name) + "?dataSource=" + Uri.EscapeDataString(source), headers, null, string.Empty, timeout);

    private static bool DeleteDomino(string baseUrl, string source, string unid, string field, string name, Dictionary<string,string> headers, double timeout)
    {
        var url = baseUrl + "/attachments/" + Uri.EscapeDataString(unid) + "/" + Uri.EscapeDataString(name) + "?dataSource=" + Uri.EscapeDataString(source); if (field.Length > 0) url += "&fieldName=" + Uri.EscapeDataString(field);
        _ = XPScriptAttachmentHttpRuntime.Send(System.Net.Http.HttpMethod.Delete, url, headers, null, string.Empty, timeout); return true;
    }
}
""";
}
