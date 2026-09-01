namespace XPScript.Compiler;

internal static class SupabaseDbRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptDbSupabase : XPScriptCaseInsensitiveDynamicObject, IDisposable
{
    private readonly XPScriptHttpDbSupabase? _rest;
    private readonly string _connectionString = "";
    private Npgsql.NpgsqlConnection? _connection;
    private Npgsql.NpgsqlTransaction? _transaction;
    private double _timeout = 30;
    private int _maxRows = 10000;

    public XPScriptDbSupabase(object? postgresConnectionString)
    {
        _connectionString = NormalizePostgresConnectionString(postgresConnectionString);
        Open();
    }

    public XPScriptDbSupabase(object? baseUrl, object? apiKey)
    {
        _rest = new XPScriptHttpDbSupabase(baseUrl, apiKey);
    }

    public string Mode => _rest is null ? "PostgreSQL" : "REST";
    public bool IsOpen => _rest is not null || _connection?.State == System.Data.ConnectionState.Open;
    public string Database => _rest is null ? (_connection?.Database ?? "") : "";
    public string BaseUrl => _rest?.BaseUrl ?? "";
    public string Schema => _rest?.Schema ?? "";

    public double Timeout
    {
        get => _rest is not null ? _rest.Timeout : _timeout;
        set
        {
            if (value < 0.1 || value > 300 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "Supabase Timeout must be between 0.1 and 300 seconds.");
            if (_rest is not null) _rest.Timeout = value;
            else _timeout = value;
        }
    }

    public int MaxRows
    {
        get => _maxRows;
        set
        {
            if (value < 1 || value > 50000) throw new XPScriptRuntimeException(5, "Supabase MaxRows must be between 1 and 50000.");
            _maxRows = value;
        }
    }

    public void SetApiKey(object? apiKey) => RequireRest().SetApiKey(apiKey);
    public void SetBearerToken(object? token) => RequireRest().SetBearerToken(token);
    public void SetSchema(object? schema) => RequireRest().SetSchema(schema);
    public XPScriptJsonDocument Select(object? table) => RequireRest().Select(table);
    public XPScriptJsonDocument Select(object? table, object? query) => RequireRest().Select(table, query);
    public XPScriptJsonDocument Insert(object? table, object? data) => RequireRest().Insert(table, data);
    public XPScriptJsonDocument Upsert(object? table, object? data) => RequireRest().Upsert(table, data);
    public XPScriptJsonDocument Update(object? table, object? filter, object? data) => RequireRest().Update(table, filter, data);
    public XPScriptJsonDocument Delete(object? table, object? filter) => RequireRest().Delete(table, filter);
    public XPScriptJsonDocument Rpc(object? functionName, object? args) => RequireRest().Rpc(functionName, args);
    public string Eq(object? column, object? value) => RequireRest().Eq(column, value);

    public void Open()
    {
        RequirePostgresMode();
        if (_connection?.State == System.Data.ConnectionState.Open) return;
        var connection = new Npgsql.NpgsqlConnection(_connectionString);
        try
        {
            connection.Open();
            _connection = connection;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            connection.Dispose();
            throw ProviderFailure("open", ex);
        }
    }

    public void Close()
    {
        if (_rest is not null) return;
        if (_transaction is not null)
        {
            try { _transaction.Rollback(); } catch { }
            _transaction.Dispose();
            _transaction = null;
        }
        _connection?.Dispose();
        _connection = null;
    }

    public int Execute(object? sql) => Execute(sql, null);
    public int Execute(object? sql, object? parameters)
    {
        using var command = CreateCommand(sql);
        AddParameters(command, parameters);
        try { return command.ExecuteNonQuery(); }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("execute", ex); }
    }

    public object? Scalar(object? sql) => Scalar(sql, null);
    public object? Scalar(object? sql, object? parameters)
    {
        using var command = CreateCommand(sql);
        AddParameters(command, parameters);
        try { return ToXPScriptValue(command.ExecuteScalar()); }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("scalar query", ex); }
    }

    public XPScriptJsonDocument Query(object? sql) => Query(sql, null);
    public XPScriptJsonDocument Query(object? sql, object? parameters)
    {
        using var command = CreateCommand(sql);
        AddParameters(command, parameters);
        try
        {
            using var reader = command.ExecuteReader();
            var rows = new System.Text.Json.Nodes.JsonArray();
            var count = 0;
            while (reader.Read())
            {
                if (count >= _maxRows) throw new XPScriptRuntimeException(5, "Supabase PostgreSQL query result exceeds MaxRows.");
                var row = new System.Text.Json.Nodes.JsonObject();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[UniqueColumnName(row, reader.GetName(i), i)] = reader.IsDBNull(i) ? null : ToJsonNode(reader.GetValue(i));
                rows.Add(row);
                count++;
            }
            return new XPScriptJsonDocument(rows);
        }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("query", ex); }
    }

    public void BeginTransaction()
    {
        RequirePostgresMode();
        if (_transaction is not null) throw new XPScriptRuntimeException(5, "A Supabase PostgreSQL transaction is already active.");
        try { _transaction = RequireConnection().BeginTransaction(); }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("begin transaction", ex); }
    }

    public void Commit()
    {
        var transaction = _transaction ?? throw new XPScriptRuntimeException(5, "No Supabase PostgreSQL transaction is active.");
        try { transaction.Commit(); }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("commit", ex); }
        finally { transaction.Dispose(); _transaction = null; }
    }

    public void Rollback()
    {
        var transaction = _transaction ?? throw new XPScriptRuntimeException(5, "No Supabase PostgreSQL transaction is active.");
        try { transaction.Rollback(); }
        catch (Npgsql.NpgsqlException ex) { throw ProviderFailure("rollback", ex); }
        finally { transaction.Dispose(); _transaction = null; }
    }

    public void Dispose() => Close();

    private XPScriptHttpDbSupabase RequireRest()
        => _rest ?? throw new XPScriptRuntimeException(5, "This XPDbSupabase instance uses PostgreSQL. REST operation is unavailable.");

    private void RequirePostgresMode()
    {
        if (_rest is not null) throw new XPScriptRuntimeException(5, "This XPDbSupabase instance uses REST. PostgreSQL operation is unavailable.");
    }

    private Npgsql.NpgsqlConnection RequireConnection()
    {
        RequirePostgresMode();
        if (_connection?.State != System.Data.ConnectionState.Open)
            throw new XPScriptRuntimeException(5, "The Supabase PostgreSQL connection is closed. Call Open before using it.");
        return _connection;
    }

    private Npgsql.NpgsqlCommand CreateCommand(object? sql)
    {
        var text = XPScriptRuntime.CStr(sql);
        if (string.IsNullOrWhiteSpace(text)) throw new XPScriptRuntimeException(5, "Supabase PostgreSQL SQL cannot be empty.");
        if (text.Contains('\0')) throw new XPScriptRuntimeException(5, "Supabase PostgreSQL SQL contains an invalid null character.");
        var command = RequireConnection().CreateCommand();
        command.CommandText = text;
        command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(_timeout));
        command.Transaction = _transaction;
        return command;
    }

    private static void AddParameters(Npgsql.NpgsqlCommand command, object? parameters)
    {
        if (parameters is null || XPScriptNullRuntime.IsNull(parameters)) return;
        var values = parameters switch
        {
            XPScriptJsonObject jsonObject => jsonObject.Node,
            XPScriptJsonDocument document when document.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            XPScriptJsonElement element when element.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            _ => throw new XPScriptRuntimeException(5, "Supabase PostgreSQL parameters must be a JsonObject or JSON object document.")
        };
        foreach (var pair in values)
        {
            var name = pair.Key.Trim().TrimStart('@', ':', '$');
            if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new XPScriptRuntimeException(5, "Supabase PostgreSQL parameter name is invalid.");
            command.Parameters.AddWithValue(name, ToDatabaseValue(pair.Value));
        }
    }

    private static object ToDatabaseValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return DBNull.Value;
        if (node is not System.Text.Json.Nodes.JsonValue value)
            throw new XPScriptRuntimeException(5, "Supabase PostgreSQL parameter values must be JSON scalar values or null.");
        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<int>(out var intValue)) return intValue;
        if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        if (value.TryGetValue<double>(out var number)) return number;
        if (value.TryGetValue<DateTime>(out var dateTime)) return dateTime;
        if (value.TryGetValue<DateTimeOffset>(out var dateTimeOffset)) return dateTimeOffset;
        if (value.TryGetValue<string>(out var text)) return text;
        throw new XPScriptRuntimeException(5, "Supabase PostgreSQL parameter value type is not supported.");
    }

    private static System.Text.Json.Nodes.JsonNode? ToJsonNode(object value) => value switch
    {
        string text => System.Text.Json.Nodes.JsonValue.Create(text),
        bool boolean => System.Text.Json.Nodes.JsonValue.Create(boolean),
        byte or sbyte or short or ushort or int or uint or long => System.Text.Json.Nodes.JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)),
        float single => System.Text.Json.Nodes.JsonValue.Create(single),
        double number => System.Text.Json.Nodes.JsonValue.Create(number),
        decimal decimalValue => System.Text.Json.Nodes.JsonValue.Create(decimalValue),
        DateTime dateTime => System.Text.Json.Nodes.JsonValue.Create(dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
        DateTimeOffset dateTimeOffset => System.Text.Json.Nodes.JsonValue.Create(dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
        Guid guid => System.Text.Json.Nodes.JsonValue.Create(guid.ToString("D")),
        byte[] bytes => System.Text.Json.Nodes.JsonValue.Create(Convert.ToBase64String(bytes)),
        _ => System.Text.Json.Nodes.JsonValue.Create(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
    };

    private static object? ToXPScriptValue(object? value)
    {
        if (value is null || value is DBNull) return null;
        return value switch
        {
            byte or sbyte or short or ushort or int or uint or long => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
            float or double or decimal => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D"),
            DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value
        };
    }

    private static string UniqueColumnName(System.Text.Json.Nodes.JsonObject row, string name, int index)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "column" + (index + 1) : name;
        if (!row.ContainsKey(candidate)) return candidate;
        var suffix = 2;
        while (row.ContainsKey(candidate + "_" + suffix)) suffix++;
        return candidate + "_" + suffix;
    }

    private static string NormalizePostgresConnectionString(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new XPScriptRuntimeException(5, "Supabase PostgreSQL connection string cannot be empty.");
        try
        {
            if (text.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(text);
                var userInfo = uri.UserInfo.Split(':', 2);
                var builder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                    Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres",
                    Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                    Timeout = 20,
                    SslMode = Npgsql.SslMode.Require
                };
                return builder.ConnectionString;
            }
            return new Npgsql.NpgsqlConnectionStringBuilder(text).ConnectionString;
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException)
        {
            throw new XPScriptRuntimeException(5, "Supabase PostgreSQL connection string is invalid.");
        }
    }

    private static XPScriptRuntimeException ProviderFailure(string operation, Exception ex)
        => new(5, "Supabase PostgreSQL " + operation + " failed: " + ex.Message);
}
""";
}
