namespace XPScript.Compiler;

internal static class DatabaseUiDataSourceRuntimeSource
{
    public static string Build(bool usesSqlite, bool usesMsSql)
    {
        var code = new System.Text.StringBuilder();
        code.AppendLine(CommonCode);
        if (usesSqlite) code.AppendLine(SqliteCode);
        if (usesMsSql) code.AppendLine(MsSqlCode);
        code.AppendLine(HttpDatabaseCode);
        return code.ToString();
    }

    private const string CommonCode = """
internal static class XPScriptDatabaseDataSourceRuntime
{
    public static XPScriptJsonArray RequireArray(XPScriptJsonDocument document, string operation)
    {
        if (document.Node is System.Text.Json.Nodes.JsonArray array)
            return new XPScriptJsonArray(array);
        throw new XPScriptRuntimeException(13, operation + " must return a JSON array.");
    }

    public static XPScriptJsonObject RequireObject(XPScriptJsonDocument document, string operation)
    {
        if (document.Node is System.Text.Json.Nodes.JsonObject obj)
            return new XPScriptJsonObject(obj);
        throw new XPScriptRuntimeException(13, operation + " must return a JSON object.");
    }

    public static XPScriptJsonObject RequireObject(object? value, string operation)
    {
        return value switch
        {
            XPScriptJsonObject obj => obj,
            XPScriptJsonDocument document when document.Node is System.Text.Json.Nodes.JsonObject obj => new XPScriptJsonObject(obj),
            XPScriptJsonElement element when element.Node is System.Text.Json.Nodes.JsonObject obj => new XPScriptJsonObject(obj),
            _ => throw new XPScriptRuntimeException(13, operation + " requires a JsonObject or an object-root JsonDocument.")
        };
    }

    public static XPScriptJsonObject RequireSingleRow(XPScriptJsonArray rows, string operation)
    {
        if (rows.Count == 0)
            throw new XPScriptRuntimeException(5, operation + " did not find a row/document.");
        if (rows.Count > 1)
            throw new XPScriptRuntimeException(5, operation + " matched more than one row/document.");
        if (rows.Get(0) is XPScriptJsonObject row)
            return row;
        throw new XPScriptRuntimeException(13, operation + " returned an entry that is not a JSON object.");
    }

    public static string RequiredIdentifier(object? value, string label)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length is < 1 or > 128 ||
            !(char.IsAsciiLetter(text[0]) || text[0] == '_') ||
            !text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new XPScriptRuntimeException(5, label + " must contain 1 to 128 ASCII letters, digits or underscores and cannot start with a digit.");
        return text;
    }

    public static string SqliteQuote(object? value, string label)
        => "\"" + RequiredIdentifier(value, label) + "\"";

    public static (string Schema, string Table, string Qualified) MsSqlTable(object? value)
    {
        var raw = XPScriptRuntime.CStr(value).Trim();
        var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2)
            throw new XPScriptRuntimeException(5, "SQL Server table must be 'table' or 'schema.table'.");
        var schema = parts.Length == 2 ? RequiredIdentifier(parts[0], "SQL Server schema") : "dbo";
        var table = RequiredIdentifier(parts[^1], "SQL Server table");
        return (schema, table, "[" + schema + "].[" + table + "]");
    }

    public static object? NodeValue(System.Text.Json.Nodes.JsonNode? node)
        => XPScriptNativeJson.FromNode(node);
}
""";

    private const string SqliteCode = """
internal static class XPScriptSqliteDataSourceExtensions
{
    public static XPScriptJsonArray QueryArray(this XPScriptDbSqlite db, object? sql)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Query(sql), "SQLite QueryArray");

    public static XPScriptJsonArray QueryArray(this XPScriptDbSqlite db, object? sql, object? parameters)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Query(sql, parameters), "SQLite QueryArray");

    public static XPScriptJsonObject GetRow(this XPScriptDbSqlite db, object? table, object? keyColumn, object? keyValue)
    {
        var tableSql = XPScriptDatabaseDataSourceRuntime.SqliteQuote(table, "SQLite table");
        var key = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQLite key column");
        var parameters = XPScriptNativeJson.CreateObject();
        parameters.Set("__xp_key", keyValue);
        var rows = db.QueryArray("SELECT * FROM " + tableSql + " WHERE \"" + key + "\" = $__xp_key LIMIT 2", parameters);
        return XPScriptDatabaseDataSourceRuntime.RequireSingleRow(rows, "SQLite GetRow");
    }

    public static int SaveRow(this XPScriptDbSqlite db, object? table, object? keyColumn, object? data)
    {
        var row = XPScriptDatabaseDataSourceRuntime.RequireObject(data, "SQLite SaveRow");
        var tableName = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(table, "SQLite table");
        var key = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQLite key column");
        if (!row.Contains(key))
            throw new XPScriptRuntimeException(5, "SQLite SaveRow data does not contain key column '" + key + "'.");

        var schema = db.QueryArray("PRAGMA table_xinfo(\"" + tableName + "\")");
        if (schema.Count == 0)
            throw new XPScriptRuntimeException(5, "SQLite SaveRow table does not exist or has no columns.");

        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < schema.Count; i++)
        {
            if (schema.Get(i) is not XPScriptJsonObject column) continue;
            var name = XPScriptRuntime.CStr(column.Get("name"));
            if (name.Length == 0) continue;
            allColumns.Add(name);
            var hidden = column.Contains("hidden") ? XPScriptRuntime.CInt(column.Get("hidden")) : 0;
            if (hidden == 0) writableColumns.Add(name);
        }
        if (!allColumns.Contains(key))
            throw new XPScriptRuntimeException(5, "SQLite SaveRow key column does not exist in the table schema.");

        var assignments = new List<string>();
        var parameters = XPScriptNativeJson.CreateObject();
        var parameterIndex = 0;
        foreach (var pair in row.Node)
        {
            if (!allColumns.Contains(pair.Key))
                throw new XPScriptRuntimeException(5, "SQLite SaveRow data contains unknown table column '" + pair.Key + "'.");
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || !writableColumns.Contains(pair.Key))
                continue;
            var parameterName = "__xp_value_" + parameterIndex++;
            assignments.Add("\"" + pair.Key + "\" = $" + parameterName);
            parameters.Set(parameterName, XPScriptDatabaseDataSourceRuntime.NodeValue(pair.Value));
        }
        if (assignments.Count == 0)
            throw new XPScriptRuntimeException(5, "SQLite SaveRow has no writable columns to update.");

        parameters.Set("__xp_key", row.Get(key));
        var affected = db.Execute(
            "UPDATE \"" + tableName + "\" SET " + string.Join(", ", assignments) + " WHERE \"" + key + "\" = $__xp_key",
            parameters);
        if (affected > 1)
            throw new XPScriptRuntimeException(5, "SQLite SaveRow updated more than one row; the key column is not unique.");
        return affected;
    }
}
""";

    private const string MsSqlCode = """
internal static class XPScriptMsSqlDataSourceExtensions
{
    public static XPScriptJsonArray QueryArray(this XPScriptDbMsSql db, object? sql)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Query(sql), "SQL Server QueryArray");

    public static XPScriptJsonArray QueryArray(this XPScriptDbMsSql db, object? sql, object? parameters)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Query(sql, parameters), "SQL Server QueryArray");

    public static XPScriptJsonObject GetRow(this XPScriptDbMsSql db, object? table, object? keyColumn, object? keyValue)
    {
        var tableInfo = XPScriptDatabaseDataSourceRuntime.MsSqlTable(table);
        var key = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQL Server key column");
        var parameters = XPScriptNativeJson.CreateObject();
        parameters.Set("__xp_key", keyValue);
        var rows = db.QueryArray("SELECT TOP (2) * FROM " + tableInfo.Qualified + " WHERE [" + key + "] = @__xp_key", parameters);
        return XPScriptDatabaseDataSourceRuntime.RequireSingleRow(rows, "SQL Server GetRow");
    }

    public static int SaveRow(this XPScriptDbMsSql db, object? table, object? keyColumn, object? data)
    {
        var row = XPScriptDatabaseDataSourceRuntime.RequireObject(data, "SQL Server SaveRow");
        var tableInfo = XPScriptDatabaseDataSourceRuntime.MsSqlTable(table);
        var key = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "SQL Server key column");
        if (!row.Contains(key))
            throw new XPScriptRuntimeException(5, "SQL Server SaveRow data does not contain key column '" + key + "'.");

        var metadataParameters = XPScriptNativeJson.CreateObject();
        metadataParameters.Set("__xp_schema", tableInfo.Schema);
        metadataParameters.Set("__xp_table", tableInfo.Table);
        var schema = db.QueryArray(
            "SELECT c.name, c.is_identity, c.is_computed FROM sys.columns c " +
            "INNER JOIN sys.tables t ON c.object_id = t.object_id " +
            "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE s.name = @__xp_schema AND t.name = @__xp_table ORDER BY c.column_id",
            metadataParameters);
        if (schema.Count == 0)
            throw new XPScriptRuntimeException(5, "SQL Server SaveRow table does not exist or has no columns.");

        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < schema.Count; i++)
        {
            if (schema.Get(i) is not XPScriptJsonObject column) continue;
            var name = XPScriptRuntime.CStr(column.Get("name"));
            if (name.Length == 0) continue;
            allColumns.Add(name);
            var identity = XPScriptRuntime.CBool(column.Get("is_identity"));
            var computed = XPScriptRuntime.CBool(column.Get("is_computed"));
            if (!identity && !computed) writableColumns.Add(name);
        }
        if (!allColumns.Contains(key))
            throw new XPScriptRuntimeException(5, "SQL Server SaveRow key column does not exist in the table schema.");

        var assignments = new List<string>();
        var parameters = XPScriptNativeJson.CreateObject();
        var parameterIndex = 0;
        foreach (var pair in row.Node)
        {
            if (!allColumns.Contains(pair.Key))
                throw new XPScriptRuntimeException(5, "SQL Server SaveRow data contains unknown table column '" + pair.Key + "'.");
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || !writableColumns.Contains(pair.Key))
                continue;
            var parameterName = "__xp_value_" + parameterIndex++;
            assignments.Add("[" + pair.Key + "] = @" + parameterName);
            parameters.Set(parameterName, XPScriptDatabaseDataSourceRuntime.NodeValue(pair.Value));
        }
        if (assignments.Count == 0)
            throw new XPScriptRuntimeException(5, "SQL Server SaveRow has no writable columns to update.");

        parameters.Set("__xp_key", row.Get(key));
        var affected = db.Execute(
            "UPDATE " + tableInfo.Qualified + " SET " + string.Join(", ", assignments) + " WHERE [" + key + "] = @__xp_key",
            parameters);
        if (affected > 1)
            throw new XPScriptRuntimeException(5, "SQL Server SaveRow updated more than one row; the key column is not unique.");
        return affected;
    }
}
""";

    private const string HttpDatabaseCode = """
internal static class XPScriptHttpDatabaseDataSourceExtensions
{
    public static XPScriptJsonArray QueryArray(this XPScriptHttpDbSupabase db, object? table)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Select(table), "Supabase QueryArray");

    public static XPScriptJsonArray QueryArray(this XPScriptHttpDbSupabase db, object? table, object? query)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Select(table, query), "Supabase QueryArray");

    public static XPScriptJsonObject GetRow(this XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? keyValue)
    {
        var filter = db.Eq(keyColumn, keyValue);
        var rows = XPScriptDatabaseDataSourceRuntime.RequireArray(
            db.Select(table, "select=*&" + filter + "&limit=2"),
            "Supabase GetRow");
        return XPScriptDatabaseDataSourceRuntime.RequireSingleRow(rows, "Supabase GetRow");
    }

    public static int SaveRow(this XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? data)
    {
        var row = XPScriptDatabaseDataSourceRuntime.RequireObject(data, "Supabase SaveRow");
        var key = XPScriptDatabaseDataSourceRuntime.RequiredIdentifier(keyColumn, "Supabase key column");
        if (!row.Contains(key))
            throw new XPScriptRuntimeException(5, "Supabase SaveRow data does not contain key column '" + key + "'.");
        var result = XPScriptDatabaseDataSourceRuntime.RequireArray(
            db.Update(table, db.Eq(key, row.Get(key)), row),
            "Supabase SaveRow");
        if (result.Count > 1)
            throw new XPScriptRuntimeException(5, "Supabase SaveRow updated more than one row; the key column is not unique.");
        return result.Count;
    }

    public static XPScriptJsonArray GetViewArray(this XPScriptHttpDbDominoRest db, object? viewName)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.GetView(viewName), "Domino GetViewArray");

    public static XPScriptJsonArray GetViewArray(this XPScriptHttpDbDominoRest db, object? viewName, object? query)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.GetView(viewName, query), "Domino GetViewArray");

    public static XPScriptJsonArray QueryArray(this XPScriptHttpDbDominoRest db, object? queryPayload)
        => XPScriptDatabaseDataSourceRuntime.RequireArray(db.Query(queryPayload), "Domino QueryArray");

    public static XPScriptJsonObject GetRow(this XPScriptHttpDbDominoRest db, object? unid)
        => XPScriptDatabaseDataSourceRuntime.RequireObject(db.GetDocument(unid), "Domino GetRow");

    public static bool SaveRow(this XPScriptHttpDbDominoRest db, object? unid, object? data)
    {
        var row = XPScriptDatabaseDataSourceRuntime.RequireObject(data, "Domino SaveRow");
        db.UpdateDocument(unid, row);
        return true;
    }
}
""";
}
