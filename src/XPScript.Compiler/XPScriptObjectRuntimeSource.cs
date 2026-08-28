namespace XPScript.Compiler;

public static class XPScriptObjectRuntimeSource
{
    public const string Code = """
internal interface IXPScriptIterable
{
    System.Collections.IEnumerable XPScriptItems();
}

[System.Text.Json.Serialization.JsonConverter(typeof(LSObjectJsonConverterFactory))]
internal abstract class LSObjectBase : IXPScriptIterable, System.Collections.IEnumerable
{
    private bool _deleted;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool __IsDeleted => _deleted;

    public virtual void __Delete()
    {
        _deleted = true;
    }

    public System.Collections.IEnumerable XPScriptItems()
    {
        var method = GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(candidate =>
                candidate.Name.Equals("Iterator", StringComparison.OrdinalIgnoreCase) &&
                candidate.GetParameters().Length == 0);

        if (method is null)
            throw new XPScriptRuntimeException(13, "ForAll requires an iterable value. XPscript classes must expose Public Function Iterator().");

        object? value;
        try
        {
            value = method.Invoke(this, null);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        if (ReferenceEquals(value, this))
            throw new XPScriptRuntimeException(13, "Iterator() must return another iterable value, not the object itself.");

        return LSForAllRuntime.Enumerate(value);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        XPScriptItems().GetEnumerator();
}

internal interface ILSObjectReference
{
    bool IsNothing { get; }
    object? ObjectValue { get; }
}

internal static class LSObjectIdentityRuntime
{
    public static bool IsNothing(object? value) =>
        value is ILSObjectReference reference ? reference.IsNothing : value is null;

    public static bool IsNotNothing(object? value) => !IsNothing(value);

    public static bool IsNullOrNothing(object? value) =>
        ReferenceEquals(value, System.DBNull.Value) ||
        value is ILSObjectReference reference && reference.IsNothing;
}

[System.Text.Json.Serialization.JsonConverter(typeof(LSRefJsonConverterFactory))]
internal sealed class LSRef<T> : ILSObjectReference, IXPScriptIterable, System.Collections.IEnumerable where T : LSObjectBase
{
    public T? Value { get; private set; }

    public bool IsNothing => Value is null;
    object? ILSObjectReference.ObjectValue => Value;

    public LSRef()
    {
    }

    private LSRef(T value)
    {
        Value = value;
    }

    public static LSRef<T> Create(T value) => new(value);

    public System.Collections.IEnumerable XPScriptItems()
    {
        var value = Value;
        if (value is null)
            throw new XPScriptRuntimeException(13, "ForAll cannot iterate Nothing.");
        return value.XPScriptItems();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        XPScriptItems().GetEnumerator();

    public void Delete()
    {
        var value = Value;
        if (value is null)
            return;

        try
        {
            value.__Delete();
        }
        finally
        {
            Value = null;
        }
    }

    public bool IsSameReference(LSRef<T>? other) =>
        other is not null && ReferenceEquals(this, other);

    public override string ToString() =>
        throw new InvalidCastException("Object references cannot be converted to String implicitly.");
}

public sealed class LSRefJsonConverterFactory : System.Text.Json.Serialization.JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition().Name.Equals("LSRef`1", StringComparison.Ordinal);

    public override System.Text.Json.Serialization.JsonConverter CreateConverter(Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(LSRefJsonConverter<>).MakeGenericType(itemType);
        return (System.Text.Json.Serialization.JsonConverter)(Activator.CreateInstance(converterType, nonPublic: true)
            ?? throw new InvalidOperationException("Unable to create XPscript object-reference JSON converter."));
    }
}

internal sealed class LSRefJsonConverter<T> : System.Text.Json.Serialization.JsonConverter<LSRef<T>> where T : LSObjectBase
{
    public override LSRef<T>? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return new LSRef<T>();
        var value = new LSObjectJsonConverter<T>().Read(ref reader, typeof(T), options);
        return value is null ? new LSRef<T>() : LSRef<T>.Create(value);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, LSRef<T> value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value is null || value.IsNothing)
        {
            writer.WriteNullValue();
            return;
        }
        new LSObjectJsonConverter<T>().Write(writer, value.Value!, options);
    }
}

public sealed class LSObjectJsonConverterFactory : System.Text.Json.Serialization.JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(LSObjectBase).IsAssignableFrom(typeToConvert) && !typeToConvert.IsAbstract;

    public override System.Text.Json.Serialization.JsonConverter CreateConverter(Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var converterType = typeof(LSObjectJsonConverter<>).MakeGenericType(typeToConvert);
        return (System.Text.Json.Serialization.JsonConverter)(Activator.CreateInstance(converterType, nonPublic: true)
            ?? throw new InvalidOperationException("Unable to create XPscript class JSON converter."));
    }
}

internal sealed class LSObjectJsonConverter<T> : System.Text.Json.Serialization.JsonConverter<T> where T : LSObjectBase
{
    public override T? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return null;
        using var document = System.Text.Json.JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new System.Text.Json.JsonException($"JSON value for {typeToConvert.Name} must be an object.");

        T instance;
        try
        {
            instance = (T)(Activator.CreateInstance(typeToConvert, nonPublic: true)
                ?? throw new System.Text.Json.JsonException($"Unable to create XPscript class {typeToConvert.Name}."));
        }
        catch (MissingMethodException ex)
        {
            throw new System.Text.Json.JsonException($"XPscript class {typeToConvert.Name} requires a parameterless Sub New for JSON deserialization.", ex);
        }

        var fields = typeToConvert.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(field => !field.IsInitOnly && !field.Name.StartsWith("__", StringComparison.Ordinal))
            .ToArray();
        var properties = typeToConvert.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.SetMethod?.IsPublic == true && !property.Name.StartsWith("__", StringComparison.Ordinal))
            .Where(property => property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: true).Length == 0)
            .ToArray();

        foreach (var item in document.RootElement.EnumerateObject())
        {
            var property = properties.FirstOrDefault(candidate => JsonName(candidate.Name, options).Equals(item.Name, NameComparison(options)));
            if (property is not null)
            {
                var converted = System.Text.Json.JsonSerializer.Deserialize(item.Value.GetRawText(), property.PropertyType, options);
                try
                {
                    property.SetValue(instance, converted);
                }
                catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
                continue;
            }

            var field = fields.FirstOrDefault(candidate => JsonName(candidate.Name, options).Equals(item.Name, NameComparison(options)));
            if (field is null) continue;
            var fieldValue = System.Text.Json.JsonSerializer.Deserialize(item.Value.GetRawText(), field.FieldType, options);
            field.SetValue(instance, fieldValue);
        }

        return instance;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, T value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in value.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).OrderBy(field => field.MetadataToken))
        {
            if (field.Name.StartsWith("__", StringComparison.Ordinal)) continue;
            var jsonName = JsonName(field.Name, options);
            if (!written.Add(jsonName)) continue;
            writer.WritePropertyName(jsonName);
            System.Text.Json.JsonSerializer.Serialize(writer, field.GetValue(value), field.FieldType, options);
        }

        foreach (var property in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).OrderBy(property => property.MetadataToken))
        {
            if (property.Name.StartsWith("__", StringComparison.Ordinal) || property.GetIndexParameters().Length != 0 || property.GetMethod?.IsPublic != true) continue;
            if (property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: true).Length != 0) continue;
            var jsonName = JsonName(property.Name, options);
            if (!written.Add(jsonName)) continue;

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            writer.WritePropertyName(jsonName);
            System.Text.Json.JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
        }

        writer.WriteEndObject();
    }

    private static string JsonName(string memberName, System.Text.Json.JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(memberName) ?? memberName;

    private static StringComparison NameComparison(System.Text.Json.JsonSerializerOptions options) =>
        options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

internal static class LSObjectRuntime
{
    public static void AssignRef<T>(ref LSRef<T> target, LSRef<T> source) where T : LSObjectBase
    {
        target = source;
    }
}
""";
}