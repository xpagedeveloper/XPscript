namespace XPScript.Compiler;

internal static class MySqlDbRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptDbMySql : IDisposable
{
    private const int MaxSqlBytes = 1024 * 1024;
    private const int MaxConnectionStringBytes = 16 * 1024;
    private const int MaxParameterCount = 4096;
    private const int MaxColumns = 1024;
    private const int MaxJsonNodes = 100_000;
    private const long MaxJsonPayloadBytes = 16L * 1024 * 1024;

    private readonly object _sync = new();
    private readonly string _connectionString;
    private MySqlConnector.MySqlConnection? _connection;
    private MySqlConnector.MySqlTransaction? _transaction;
    private double _timeout = 30;
    private int _maxRows = 10_000;

    public XPScriptDbMySql(object? connectionString)
    {
        _connectionString = RequiredConnectionString(connectionString);
        Open();
    }

    public string DataSource
    {
        get
        {
            lock (_sync) return _connection?.DataSource ?? string.Empty;
        }
    }

    public string Database
    {
        get
        {
            lock (_sync) return _connection?.Database ?? string.Empty;
        }
    }

    public string ServerVersion
    {
        get
        {
            lock (_sync) return _connection?.ServerVersion ?? string.Empty;
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (_sync) return _connection?.State == System.Data.ConnectionState.Open;
        }
    }

    public bool InTransaction
    {
        get
        {
            lock (_sync) return _transaction is not null;
        }
    }

    public double Timeout
    {
        get
        {
            lock (_sync) return _timeout;
        }
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.1 || value > 300)
                throw new XPScriptRuntimeException(5, "MySQL Timeout must be between 0.1 and 300 seconds.");
            lock (_sync) _timeout = value;
        }
    }

    public int MaxRows
    {
        get
        {
            lock (_sync) return _maxRows;
        }
        set
        {
            if (value < 1 || value > 50_000)
                throw new XPScriptRuntimeException(5, "MySQL MaxRows must be between 1 and 50000.");
            lock (_sync) _maxRows = value;
        }
    }

    public void Open()
    {
        lock (_sync)
        {
            if (_connection?.State == System.Data.ConnectionState.Open) return;
            var connection = new MySqlConnector.MySqlConnection(_connectionString);
            try
            {
                connection.Open();
                _connection = connection;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                connection.Dispose();
                throw ProviderFailure("open", ex);
            }
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            if (_transaction is not null)
            {
                try { _transaction.Rollback(); } catch { }
                _transaction.Dispose();
                _transaction = null;
            }

            _connection?.Dispose();
            _connection = null;
        }
    }

    public int Execute(object? sql) => Execute(sql, null);

    public int Execute(object? sql, object? parameters)
    {
        lock (_sync)
        {
            using var command = CreateCommand(sql);
            AddParameters(command, parameters);
            try
            {
                return command.ExecuteNonQuery();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("execute", ex);
            }
        }
    }

    public XPScriptJsonDocument Query(object? sql) => Query(sql, null);

    public XPScriptJsonDocument Query(object? sql, object? parameters)
    {
        lock (_sync)
        {
            using var command = CreateCommand(sql);
            AddParameters(command, parameters);
            try
            {
                using var reader = command.ExecuteReader();
                if (reader.FieldCount > MaxColumns)
                    throw new XPScriptRuntimeException(5, "MySQL query result exceeds the 1024-column limit.");

                var rows = new System.Text.Json.Nodes.JsonArray();
                var nodeCount = 1;
                long payloadBytes = 0;
                var rowCount = 0;

                while (reader.Read())
                {
                    if (rowCount >= _maxRows)
                        throw new XPScriptRuntimeException(5, "MySQL query result exceeds MaxRows.");

                    var row = new System.Text.Json.Nodes.JsonObject();
                    nodeCount = checked(nodeCount + 1);
                    EnsureResultBudget(nodeCount, payloadBytes);

                    for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                    {
                        var name = UniqueColumnName(row, reader.GetName(columnIndex), columnIndex);
                        payloadBytes = checked(payloadBytes + System.Text.Encoding.UTF8.GetByteCount(name));
                        var value = reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex);
                        var node = ToJsonNode(value, ref payloadBytes);
                        if (node is not null) nodeCount = checked(nodeCount + 1);
                        EnsureResultBudget(nodeCount, payloadBytes);
                        row[name] = node;
                    }

                    rows.Add(row);
                    rowCount++;
                }

                return new XPScriptJsonDocument(rows);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("query", ex);
            }
        }
    }

    public object? Scalar(object? sql) => Scalar(sql, null);

    public object? Scalar(object? sql, object? parameters)
    {
        lock (_sync)
        {
            using var command = CreateCommand(sql);
            AddParameters(command, parameters);
            try
            {
                return ToXPScriptValue(command.ExecuteScalar());
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("scalar query", ex);
            }
        }
    }

    public void BeginTransaction()
    {
        lock (_sync)
        {
            if (_transaction is not null)
                throw new XPScriptRuntimeException(5, "A MySQL transaction is already active.");
            try
            {
                _transaction = RequireConnection().BeginTransaction();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("begin transaction", ex);
            }
        }
    }

    public void Commit()
    {
        lock (_sync)
        {
            var transaction = RequireTransaction();
            try
            {
                transaction.Commit();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("commit", ex);
            }
            finally
            {
                transaction.Dispose();
                _transaction = null;
            }
        }
    }

    public void Rollback()
    {
        lock (_sync)
        {
            var transaction = RequireTransaction();
            try
            {
                transaction.Rollback();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                throw ProviderFailure("rollback", ex);
            }
            finally
            {
                transaction.Dispose();
                _transaction = null;
            }
        }
    }

    public void Dispose() => Close();

    private MySqlConnector.MySqlCommand CreateCommand(object? sql)
    {
        var command = RequireConnection().CreateCommand();
        command.CommandText = RequiredSql(sql);
        command.CommandTimeout = TimeoutSeconds();
        command.Transaction = _transaction;
        return command;
    }

    private int TimeoutSeconds() => Math.Max(1, (int)Math.Ceiling(_timeout));

    private MySqlConnector.MySqlConnection RequireConnection()
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            throw new XPScriptRuntimeException(5, "The MySQL database is closed. Call Open before using it.");
        return _connection;
    }

    private MySqlConnector.MySqlTransaction RequireTransaction()
        => _transaction ?? throw new XPScriptRuntimeException(5, "No MySQL transaction is active.");

    private static string RequiredSql(object? sql)
    {
        var text = XPScriptRuntime.CStr(sql);
        if (string.IsNullOrWhiteSpace(text))
            throw new XPScriptRuntimeException(5, "MySQL SQL cannot be empty.");
        if (text.Contains('\0'))
            throw new XPScriptRuntimeException(5, "MySQL SQL contains an invalid null character.");
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxSqlBytes)
            throw new XPScriptRuntimeException(5, "MySQL SQL exceeds the 1 MiB limit.");
        return text;
    }

    private static string RequiredConnectionString(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (string.IsNullOrWhiteSpace(text))
            throw new XPScriptRuntimeException(5, "MySQL connection string cannot be empty.");
        if (text.Contains('\0'))
            throw new XPScriptRuntimeException(5, "MySQL connection string contains an invalid null character.");
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxConnectionStringBytes)
            throw new XPScriptRuntimeException(5, "MySQL connection string exceeds the 16 KiB limit.");
        return text;
    }

    private static void AddParameters(MySqlConnector.MySqlCommand command, object? parameters)
    {
        if (parameters is null || XPScriptNullRuntime.IsNull(parameters)) return;

        var values = parameters switch
        {
            XPScriptJsonObject jsonObject => jsonObject.Node,
            XPScriptJsonDocument document when document.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            XPScriptJsonElement element when element.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            _ => throw new XPScriptRuntimeException(5, "MySQL parameters must be a JsonObject or a JSON document whose root is an object.")
        };

        if (values.Count > MaxParameterCount)
            throw new XPScriptRuntimeException(5, "MySQL parameters exceed the 4096-parameter limit.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            var name = NormalizeParameterName(pair.Key);
            if (!names.Add(name))
                throw new XPScriptRuntimeException(5, "MySQL parameters contain a duplicate normalized name.");
            command.Parameters.AddWithValue(name, ToDatabaseValue(pair.Value));
        }
    }

    private static string NormalizeParameterName(string value)
    {
        var name = value.Trim();
        var identifier = name.Length > 0 && name[0] is '$' or '@' or ':' ? name[1..] : name;
        if (identifier.Length is < 1 or > 128 ||
            !(char.IsAsciiLetter(identifier[0]) || identifier[0] == '_') ||
            !identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new XPScriptRuntimeException(5, "MySQL parameter names must contain 1 to 128 ASCII letters, digits or underscores and cannot start with a digit.");
        return "@" + identifier;
    }

    private static object ToDatabaseValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return DBNull.Value;
        if (node is not System.Text.Json.Nodes.JsonValue value)
            throw new XPScriptRuntimeException(5, "MySQL parameter values must be JSON scalar values or null.");

        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<ulong>(out var unsignedLong)) return unsignedLong <= long.MaxValue ? (long)unsignedLong : unsignedLong.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        if (value.TryGetValue<double>(out var number))
        {
            if (!double.IsFinite(number)) throw new XPScriptRuntimeException(5, "MySQL numeric parameters must be finite.");
            return number;
        }
        if (value.TryGetValue<DateTime>(out var dateTime)) return dateTime;
        if (value.TryGetValue<DateTimeOffset>(out var dateTimeOffset)) return dateTimeOffset.UtcDateTime;
        if (value.TryGetValue<string>(out var text)) return text;
        throw new XPScriptRuntimeException(5, "MySQL parameter value type is not supported.");
    }

    private static System.Text.Json.Nodes.JsonNode? ToJsonNode(object? value, ref long payloadBytes)
    {
        if (value is null || value is DBNull) return null;

        switch (value)
        {
            case string text:
                AddPayload(ref payloadBytes, System.Text.Encoding.UTF8.GetByteCount(text));
                return System.Text.Json.Nodes.JsonValue.Create(text);
            case byte[] bytes:
                if (bytes.LongLength > MaxJsonPayloadBytes)
                    throw new XPScriptRuntimeException(5, "MySQL query result exceeds the 16 MiB JSON payload limit.");
                var base64 = Convert.ToBase64String(bytes);
                AddPayload(ref payloadBytes, base64.Length);
                return System.Text.Json.Nodes.JsonValue.Create(base64);
            case bool boolean:
                AddPayload(ref payloadBytes, 5);
                return System.Text.Json.Nodes.JsonValue.Create(boolean);
            case byte or sbyte or short or ushort or int or uint or long:
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
            case ulong unsignedLong when unsignedLong <= long.MaxValue:
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create((long)unsignedLong);
            case ulong largeUnsigned:
                var unsignedText = largeUnsigned.ToString(System.Globalization.CultureInfo.InvariantCulture);
                AddPayload(ref payloadBytes, unsignedText.Length);
                return System.Text.Json.Nodes.JsonValue.Create(unsignedText);
            case float single:
                if (!float.IsFinite(single)) throw new XPScriptRuntimeException(5, "MySQL query returned a non-finite number.");
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create(single);
            case double number:
                if (!double.IsFinite(number)) throw new XPScriptRuntimeException(5, "MySQL query returned a non-finite number.");
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create(number);
            case decimal decimalValue:
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create(decimalValue);
            case DateTime dateTime:
                var dateText = dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                AddPayload(ref payloadBytes, dateText.Length);
                return System.Text.Json.Nodes.JsonValue.Create(dateText);
            case DateTimeOffset dateTimeOffset:
                var offsetText = dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                AddPayload(ref payloadBytes, offsetText.Length);
                return System.Text.Json.Nodes.JsonValue.Create(offsetText);
            case Guid guid:
                var guidText = guid.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
                AddPayload(ref payloadBytes, guidText.Length);
                return System.Text.Json.Nodes.JsonValue.Create(guidText);
            case TimeSpan timeSpan:
                var timeText = timeSpan.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
                AddPayload(ref payloadBytes, timeText.Length);
                return System.Text.Json.Nodes.JsonValue.Create(timeText);
            default:
                var fallback = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                AddPayload(ref payloadBytes, System.Text.Encoding.UTF8.GetByteCount(fallback));
                return System.Text.Json.Nodes.JsonValue.Create(fallback);
        }
    }

    private static object? ToXPScriptValue(object? value)
    {
        if (value is null || value is DBNull) return null;
        if (value is byte[] bytes)
        {
            if (bytes.LongLength > MaxJsonPayloadBytes)
                throw new XPScriptRuntimeException(5, "MySQL scalar result exceeds the 16 MiB limit.");
            return Convert.ToBase64String(bytes);
        }
        if (value is DateTime dateTime) return dateTime;
        if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset.DateTime;
        if (value is string text)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxJsonPayloadBytes)
                throw new XPScriptRuntimeException(5, "MySQL scalar result exceeds the 16 MiB limit.");
            return text;
        }
        if (value is float single && !float.IsFinite(single))
            throw new XPScriptRuntimeException(5, "MySQL scalar query returned a non-finite number.");
        if (value is double number && !double.IsFinite(number))
            throw new XPScriptRuntimeException(5, "MySQL scalar query returned a non-finite number.");
        return value;
    }

    private static string UniqueColumnName(System.Text.Json.Nodes.JsonObject row, string name, int index)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "column" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) : name;
        if (!row.ContainsKey(candidate)) return candidate;
        var suffix = 2;
        while (row.ContainsKey(candidate + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture))) suffix++;
        return candidate + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddPayload(ref long payloadBytes, long amount)
    {
        payloadBytes = checked(payloadBytes + amount);
        EnsureResultBudget(0, payloadBytes);
    }

    private static void EnsureResultBudget(int nodeCount, long payloadBytes)
    {
        if (nodeCount > MaxJsonNodes)
            throw new XPScriptRuntimeException(5, "MySQL query result exceeds the JSON node limit.");
        if (payloadBytes > MaxJsonPayloadBytes)
            throw new XPScriptRuntimeException(5, "MySQL query result exceeds the 16 MiB JSON payload limit.");
    }

    private static XPScriptRuntimeException ProviderFailure(string operation, Exception exception)
        => new(5, "MySQL " + operation + " failed: " + exception.Message);
}
""";
}
