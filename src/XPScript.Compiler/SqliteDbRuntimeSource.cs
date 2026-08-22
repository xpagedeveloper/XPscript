namespace XPScript.Compiler;

internal static class SqliteDbRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptDbSqlite : IDisposable
{
    private const int MaxSqlBytes = 1024 * 1024;
    private const int MaxParameterCount = 999;
    private const int MaxColumns = 1024;
    private const int MaxJsonNodes = 100_000;
    private const long MaxJsonPayloadBytes = 16L * 1024 * 1024;

    private readonly object _sync = new();
    private readonly string _databasePath;
    private readonly bool _readOnly;
    private Microsoft.Data.Sqlite.SqliteConnection? _connection;
    private Microsoft.Data.Sqlite.SqliteTransaction? _transaction;
    private double _timeout = 30;
    private int _maxRows = 10_000;

    public XPScriptDbSqlite(object? databasePath) : this(databasePath, false)
    {
    }

    public XPScriptDbSqlite(object? databasePath, object? readOnly)
    {
        _databasePath = ResolveDatabasePath(databasePath);
        _readOnly = XPScriptRuntime.CBool(readOnly);
        if (_readOnly && IsMemoryDatabase(_databasePath))
            throw new XPScriptRuntimeException(5, "An in-memory SQLite database cannot be opened read-only.");
        Open();
    }

    public string DatabasePath => _databasePath;
    public bool ReadOnly => _readOnly;

    public bool IsOpen
    {
        get
        {
            lock (_sync)
                return _connection?.State == System.Data.ConnectionState.Open;
        }
    }

    public bool InTransaction
    {
        get
        {
            lock (_sync)
                return _transaction is not null;
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
                throw new XPScriptRuntimeException(5, "SQLite Timeout must be between 0.1 and 300 seconds.");
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
                throw new XPScriptRuntimeException(5, "SQLite MaxRows must be between 1 and 50000.");
            lock (_sync) _maxRows = value;
        }
    }

    public long LastInsertRowId
    {
        get
        {
            lock (_sync)
            {
                using var command = CreateCommand("SELECT last_insert_rowid();");
                try
                {
                    return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    throw ProviderFailure("last insert row lookup", ex);
                }
            }
        }
    }

    public void Open()
    {
        lock (_sync)
        {
            if (_connection?.State == System.Data.ConnectionState.Open) return;
            if (!IsMemoryDatabase(_databasePath)) ValidateDatabaseTarget(_databasePath);

            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = IsMemoryDatabase(_databasePath)
                    ? Microsoft.Data.Sqlite.SqliteOpenMode.Memory
                    : _readOnly
                        ? Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
                        : Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Private
            };

            var connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ToString());
            try
            {
                connection.Open();
                using var pragma = connection.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.CommandTimeout = TimeoutSeconds();
                pragma.ExecuteNonQuery();
                _connection = connection;
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
                    throw new XPScriptRuntimeException(5, "SQLite query result exceeds the 1024-column limit.");

                var rows = new System.Text.Json.Nodes.JsonArray();
                var nodeCount = 1;
                long payloadBytes = 0;
                var rowCount = 0;

                while (reader.Read())
                {
                    if (rowCount >= _maxRows)
                        throw new XPScriptRuntimeException(5, "SQLite query result exceeds MaxRows.");

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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
                throw new XPScriptRuntimeException(5, "A SQLite transaction is already active.");
            try
            {
                _transaction = RequireConnection().BeginTransaction();
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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

    private Microsoft.Data.Sqlite.SqliteCommand CreateCommand(object? sql)
    {
        var command = RequireConnection().CreateCommand();
        command.CommandText = RequiredSql(sql);
        command.CommandTimeout = TimeoutSeconds();
        command.Transaction = _transaction;
        return command;
    }

    private int TimeoutSeconds() => Math.Max(1, (int)Math.Ceiling(_timeout));

    private Microsoft.Data.Sqlite.SqliteConnection RequireConnection()
    {
        if (_connection?.State != System.Data.ConnectionState.Open)
            throw new XPScriptRuntimeException(5, "The SQLite database is closed. Call Open before using it.");
        return _connection;
    }

    private Microsoft.Data.Sqlite.SqliteTransaction RequireTransaction()
        => _transaction ?? throw new XPScriptRuntimeException(5, "No SQLite transaction is active.");

    private static string RequiredSql(object? sql)
    {
        var text = XPScriptRuntime.CStr(sql);
        if (string.IsNullOrWhiteSpace(text))
            throw new XPScriptRuntimeException(5, "SQLite SQL cannot be empty.");
        if (text.Contains('\0'))
            throw new XPScriptRuntimeException(5, "SQLite SQL contains an invalid null character.");
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxSqlBytes)
            throw new XPScriptRuntimeException(5, "SQLite SQL exceeds the 1 MiB limit.");
        return text;
    }

    private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand command, object? parameters)
    {
        if (parameters is null || XPScriptNullRuntime.IsNull(parameters)) return;

        var values = parameters switch
        {
            XPScriptJsonObject jsonObject => jsonObject.Node,
            XPScriptJsonDocument document when document.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            XPScriptJsonElement element when element.Node is System.Text.Json.Nodes.JsonObject jsonObject => jsonObject,
            _ => throw new XPScriptRuntimeException(5, "SQLite parameters must be a JsonObject or a JSON document whose root is an object.")
        };

        if (values.Count > MaxParameterCount)
            throw new XPScriptRuntimeException(5, "SQLite parameters exceed the 999-parameter limit.");

        foreach (var pair in values)
            command.Parameters.AddWithValue(NormalizeParameterName(pair.Key), ToDatabaseValue(pair.Value));
    }

    private static string NormalizeParameterName(string value)
    {
        var name = value.Trim();
        var identifier = name.Length > 0 && name[0] is '$' or '@' or ':' ? name[1..] : name;
        if (identifier.Length is < 1 or > 128 ||
            !(char.IsAsciiLetter(identifier[0]) || identifier[0] == '_') ||
            !identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new XPScriptRuntimeException(5, "SQLite parameter names must contain 1 to 128 ASCII letters, digits or underscores and cannot start with a digit.");
        return name.Length > 0 && name[0] is '$' or '@' or ':' ? name : "$" + name;
    }

    private static object ToDatabaseValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return DBNull.Value;
        if (node is not System.Text.Json.Nodes.JsonValue value)
            throw new XPScriptRuntimeException(5, "SQLite parameter values must be JSON scalar values or null.");

        if (value.TryGetValue<bool>(out var boolean)) return boolean ? 1L : 0L;
        if (value.TryGetValue<byte>(out var byteValue)) return (long)byteValue;
        if (value.TryGetValue<sbyte>(out var signedByte)) return (long)signedByte;
        if (value.TryGetValue<short>(out var shortValue)) return (long)shortValue;
        if (value.TryGetValue<ushort>(out var unsignedShort)) return (long)unsignedShort;
        if (value.TryGetValue<int>(out var integer)) return (long)integer;
        if (value.TryGetValue<uint>(out var unsignedInteger)) return (long)unsignedInteger;
        if (value.TryGetValue<long>(out var longValue)) return longValue;
        if (value.TryGetValue<ulong>(out var unsignedLong))
        {
            if (unsignedLong <= long.MaxValue) return (long)unsignedLong;
            return unsignedLong.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (value.TryGetValue<float>(out var single))
        {
            if (!float.IsFinite(single)) throw new XPScriptRuntimeException(5, "SQLite numeric parameters must be finite.");
            return (double)single;
        }
        if (value.TryGetValue<double>(out var number))
        {
            if (!double.IsFinite(number)) throw new XPScriptRuntimeException(5, "SQLite numeric parameters must be finite.");
            return number;
        }
        if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        if (value.TryGetValue<DateTime>(out var dateTime)) return dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetValue<DateTimeOffset>(out var dateTimeOffset)) return dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetValue<string>(out var text)) return text;
        throw new XPScriptRuntimeException(5, "SQLite parameter value type is not supported.");
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
                    throw new XPScriptRuntimeException(5, "SQLite query result exceeds the 16 MiB JSON payload limit.");
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
                if (!float.IsFinite(single)) throw new XPScriptRuntimeException(5, "SQLite query returned a non-finite number.");
                AddPayload(ref payloadBytes, 32);
                return System.Text.Json.Nodes.JsonValue.Create(single);
            case double number:
                if (!double.IsFinite(number)) throw new XPScriptRuntimeException(5, "SQLite query returned a non-finite number.");
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
            default:
                throw new XPScriptRuntimeException(5, "SQLite query returned an unsupported value type.");
        }
    }

    private static object? ToXPScriptValue(object? value)
    {
        if (value is null || value is DBNull) return null;
        if (value is byte[] bytes)
        {
            if (bytes.LongLength > MaxJsonPayloadBytes)
                throw new XPScriptRuntimeException(5, "SQLite scalar result exceeds the 16 MiB limit.");
            return Convert.ToBase64String(bytes);
        }
        if (value is string text)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxJsonPayloadBytes)
                throw new XPScriptRuntimeException(5, "SQLite scalar result exceeds the 16 MiB limit.");
            return text;
        }
        if (value is float single && !float.IsFinite(single))
            throw new XPScriptRuntimeException(5, "SQLite scalar query returned a non-finite number.");
        if (value is double number && !double.IsFinite(number))
            throw new XPScriptRuntimeException(5, "SQLite scalar query returned a non-finite number.");
        return value;
    }

    private static string UniqueColumnName(System.Text.Json.Nodes.JsonObject row, string value, int columnIndex)
    {
        var baseName = string.IsNullOrWhiteSpace(value) ? "Column" + (columnIndex + 1) : value;
        var name = baseName;
        var suffix = 2;
        while (row.ContainsKey(name)) name = baseName + "_" + suffix++;
        return name;
    }

    private static void AddPayload(ref long payloadBytes, long amount)
    {
        payloadBytes = checked(payloadBytes + amount);
        EnsureResultBudget(0, payloadBytes);
    }

    private static void EnsureResultBudget(int nodeCount, long payloadBytes)
    {
        if (nodeCount > MaxJsonNodes)
            throw new XPScriptRuntimeException(5, "SQLite query result exceeds the 100000-node JSON limit.");
        if (payloadBytes > MaxJsonPayloadBytes)
            throw new XPScriptRuntimeException(5, "SQLite query result exceeds the 16 MiB JSON payload limit.");
    }

    private static XPScriptRuntimeException ProviderFailure(string operation, Microsoft.Data.Sqlite.SqliteException exception)
        => new(5, "SQLite " + operation + " failed with provider error code " + exception.SqliteErrorCode + ".");

    private static bool IsMemoryDatabase(string value)
        => value.Equals(":memory:", StringComparison.OrdinalIgnoreCase);

    private static string ResolveDatabasePath(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Equals(":memory:", StringComparison.OrdinalIgnoreCase)) return ":memory:";
        if (text.Length == 0)
            throw new XPScriptRuntimeException(5, "SQLite database path cannot be empty.");
        if (text.Any(char.IsControl) || text.Contains(':'))
            throw new XPScriptRuntimeException(5, "SQLite database path contains an invalid character.");
        if (Path.IsPathRooted(text))
            throw new XPScriptRuntimeException(5, "SQLite database path must be relative to the application directory.");

        try
        {
            var root = Path.GetFullPath(AppContext.BaseDirectory);
            var fullPath = Path.GetFullPath(Path.Combine(root, text));
            if (!IsInsideRoot(root, fullPath))
                throw new XPScriptRuntimeException(5, "SQLite database path resolves outside the application directory.");
            ValidateDatabaseTarget(fullPath);
            return fullPath;
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            throw new XPScriptRuntimeException(5, "SQLite database path is invalid or inaccessible.");
        }
    }

    private static void ValidateDatabaseTarget(string fullPath)
    {
        try
        {
            var root = Path.GetFullPath(AppContext.BaseDirectory);
            if (!IsInsideRoot(root, fullPath))
                throw new XPScriptRuntimeException(5, "SQLite database path resolves outside the application directory.");

            var parent = Path.GetDirectoryName(fullPath);
            if (parent is null || !Directory.Exists(parent))
                throw new XPScriptRuntimeException(5, "SQLite database parent directory does not exist.");
            ValidateDirectoryChain(root, parent);

            if (Directory.Exists(fullPath))
                throw new XPScriptRuntimeException(5, "SQLite database path points to a directory.");
            var file = new FileInfo(fullPath);
            if (file.LinkTarget is not null || (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint)))
                throw new XPScriptRuntimeException(5, "SQLite database path cannot be a symbolic link or reparse point.");
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            throw new XPScriptRuntimeException(5, "SQLite database path is invalid or inaccessible.");
        }
    }

    private static bool IsInsideRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void ValidateDirectoryChain(string root, string parent)
    {
        var relative = Path.GetRelativePath(root, parent);
        if (relative == ".") return;

        var current = root;
        foreach (var part in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var directory = new DirectoryInfo(current);
            if (!directory.Exists)
                throw new XPScriptRuntimeException(5, "SQLite database parent directory does not exist.");
            if (directory.LinkTarget is not null || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new XPScriptRuntimeException(5, "SQLite database path cannot pass through a symbolic link or reparse point.");
        }
    }
}
""";
}
