using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Mail;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.Web.Runtime;

public sealed record XpsValidationRule(string TypeName, string MemberName, string Rule, string? Argument1 = null, string? Argument2 = null);
public sealed record XpsParameterBinding(string ParameterName, string Source, string? SourceName = null);

public sealed record XpsCorsRule(IReadOnlyList<string> Origins)
{
    public bool AllowsAnyOrigin => Origins.Any(x => x == "*");
    public bool Allows(string origin) => AllowsAnyOrigin || Origins.Contains(origin, StringComparer.OrdinalIgnoreCase);
}

public sealed record XpsRateLimitRule
{
    public XpsRateLimitRule(int permitLimit, TimeSpan window)
    {
        if (permitLimit < 1) throw new ArgumentOutOfRangeException(nameof(permitLimit));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        PermitLimit = permitLimit;
        Window = window;
    }

    public int PermitLimit { get; }
    public TimeSpan Window { get; }
}

public sealed class XpsRequestBody
{
    public const int DefaultMaxJsonBytes = 4 * 1024 * 1024;
    private readonly XpsWebRequest _request;
    internal XpsRequestBody(XpsWebRequest request) => _request = request ?? throw new ArgumentNullException(nameof(request));
    public string Text(int maxBytes = DefaultMaxJsonBytes) => _request.BodyText(maxBytes);
    public byte[] Bytes(int maxBytes = DefaultMaxJsonBytes) => _request.BodyBytes(maxBytes);

    public T Json<T>(int maxBytes = DefaultMaxJsonBytes)
    {
        var value = (T?)Json(typeof(T), maxBytes);
        return value ?? throw new XpsRestBindingException("JSON body cannot be null.");
    }

    public object? Json(Type type, int maxBytes = DefaultMaxJsonBytes)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_request.Body.Length > maxBytes) throw new XpsRestBindingException($"JSON body exceeds the configured {maxBytes} byte limit.");
        if (_request.ContentType is null ||
            (!_request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) && !_request.ContentType.Contains("+json", StringComparison.OrdinalIgnoreCase)))
            throw new XpsRestBindingException("Request Content-Type must be application/json.");
        try
        {
            if (IsXpsObjectReference(type))
            {
                var modelType = type.GetGenericArguments()[0];
                var model = JsonSerializer.Deserialize(_request.Body.Span, modelType, XpsRestJson.Options)
                    ?? throw new XpsRestBindingException("JSON body cannot be null.");
                var create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [modelType])
                    ?? throw new XpsRestBindingException($"Unable to construct XPScript object reference for {modelType.Name}.");
                return create.Invoke(null, [model]);
            }
            return JsonSerializer.Deserialize(_request.Body.Span, type, XpsRestJson.Options);
        }
        catch (JsonException ex)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS"), "1", StringComparison.Ordinal))
                Console.Error.WriteLine($"REST JSON deserialization failed for {type.FullName}: {ex}");
            throw new XpsRestBindingException("Request body contains invalid JSON.", ex);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        { throw new XpsRestBindingException("Unable to construct XPScript request model.", ex.InnerException); }
    }

    internal static bool IsXpsObjectReference(Type type) =>
        type.IsGenericType && string.Equals(type.GetGenericTypeDefinition().Name, "LSRef`1", StringComparison.Ordinal);

    internal static object? UnwrapXpsObjectReference(object? value)
    {
        if (value is null) return null;
        var type = value.GetType();
        if (!IsXpsObjectReference(type)) return value;
        return type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
    }
}

public static class XpsRestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        WriteIndented = false
    };
}

public sealed class XpsRestBindingException : Exception
{
    public XpsRestBindingException(string message) : base(message) { }
    public XpsRestBindingException(string message, Exception innerException) : base(message, innerException) { }
}

public static class XpsRestJsonSchemaValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(XpsWebRequest request, string webRoot, string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = Path.GetFullPath(webRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, schemaPath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsRestBindingException("JSON Schema resolves outside the web root.");
        if (!File.Exists(candidate)) throw new XpsRestBindingException($"JSON Schema '{schemaPath}' was not found.");

        JsonNode schema;
        JsonNode? instance;
        try
        {
            schema = JsonNode.Parse(File.ReadAllText(candidate), documentOptions: new JsonDocumentOptions { MaxDepth = 64 })
                ?? throw new XpsRestBindingException("JSON Schema document cannot be null.");
            instance = JsonNode.Parse(request.Body.Span, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException ex) { throw new XpsRestBindingException("JSON Schema or request body contains invalid JSON.", ex); }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        ValidateNode(schema, instance, "$", errors, 0);
        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateNode(JsonNode schema, JsonNode? value, string path, Dictionary<string, List<string>> errors, int depth)
    {
        if (depth > 64) { Add(errors, path, "Validation nesting exceeds 64 levels."); return; }
        if (schema is JsonValue booleanSchema && booleanSchema.TryGetValue<bool>(out var allowed)) { if (!allowed) Add(errors, path, "Value is rejected by the JSON Schema."); return; }
        if (schema is not JsonObject obj) { Add(errors, path, "JSON Schema node must be an object or boolean."); return; }

        if (obj["type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var type) && !MatchesType(value, type))
        { Add(errors, path, $"Value does not match required JSON type '{type}'."); return; }

        if (value is JsonObject data)
        {
            if (obj["required"] is JsonArray required)
                foreach (var item in required)
                    if (item is JsonValue rv && rv.TryGetValue<string>(out var name) && !data.ContainsKey(name))
                        Add(errors, path + "." + name, $"Required property '{name}' is missing.");

            if (obj["properties"] is JsonObject properties)
                foreach (var property in properties)
                    if (data.TryGetPropertyValue(property.Key, out var child) && property.Value is not null)
                        ValidateNode(property.Value, child, path + "." + property.Key, errors, depth + 1);
        }

        if (value is JsonArray array && obj["items"] is JsonNode items)
            for (var i = 0; i < array.Count; i++) ValidateNode(items, array[i], path + "[" + i + "]", errors, depth + 1);

        if (value is JsonValue scalar)
        {
            if (obj["minLength"] is JsonValue minValue && minValue.TryGetValue<int>(out var min) && scalar.TryGetValue<string>(out var text) && text.Length < min)
                Add(errors, path, $"String length must be at least {min}.");
            if (obj["maxLength"] is JsonValue maxValue && maxValue.TryGetValue<int>(out var max) && scalar.TryGetValue<string>(out text) && text.Length > max)
                Add(errors, path, $"String length must be at most {max}.");
            if (TryNumber(scalar, out var number))
            {
                if (obj["minimum"] is JsonValue minimum && TryNumber(minimum, out var minNumber) && number < minNumber) Add(errors, path, $"Number must be at least {minNumber}.");
                if (obj["maximum"] is JsonValue maximum && TryNumber(maximum, out var maxNumber) && number > maxNumber) Add(errors, path, $"Number must be at most {maxNumber}.");
            }
        }
    }

    private static bool MatchesType(JsonNode? value, string type) => type.ToLowerInvariant() switch
    {
        "null" => value is null,
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue v && v.TryGetValue<string>(out _),
        "boolean" => value is JsonValue v && v.TryGetValue<bool>(out _),
        "integer" => value is JsonValue v && (v.TryGetValue<int>(out _) || v.TryGetValue<long>(out _)),
        "number" => value is JsonValue v && TryNumber(v, out _),
        _ => true
    };

    private static bool TryNumber(JsonValue value, out decimal number)
    {
        if (value.TryGetValue<decimal>(out number)) return true;
        if (value.TryGetValue<long>(out var l)) { number = l; return true; }
        if (value.TryGetValue<double>(out var d)) { number = (decimal)d; return true; }
        number = 0; return false;
    }

    private static void Add(Dictionary<string, List<string>> errors, string path, string message)
    {
        if (!errors.TryGetValue(path, out var list)) errors[path] = list = [];
        list.Add(message);
    }
}

public static class XpsRestBinder
{
    private const string ByValPrefix = "__xps_byval_";
    private const string ByRefPrefix = "__xps_byref_";

    public static bool TryBind(MethodInfo method, XpsWebContext context, XpsWebRouteDescriptor descriptor, out object?[] arguments, out IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);
        var parameters = method.GetParameters();
        arguments = new object?[parameters.Length];
        var validationErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        object? bodyObject = null;

        try
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var name = SourceParameterName(parameter);
                var explicitBinding = descriptor.ParameterBindings?.FirstOrDefault(x => x.ParameterName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (explicitBinding is not null)
                {
                    if (TryExplicitBinding(explicitBinding, parameter, context, ref bodyObject, out var boundValue, out var bindingError))
                        arguments[i] = boundValue;
                    else
                        AddError(validationErrors, name, bindingError!);
                    continue;
                }

                if (context.RouteValues.TryGetValue(name, out var routeValue))
                {
                    arguments[i] = ConvertScalar(routeValue, ValueParameterType(parameter), name);
                    continue;
                }

                var queryValues = context.Request.QueryAll(name);
                if (queryValues.Count > 0)
                {
                    arguments[i] = ConvertScalar(queryValues[0], ValueParameterType(parameter), name);
                    continue;
                }

                var valueType = ValueParameterType(parameter);
                if (name.Equals("body", StringComparison.OrdinalIgnoreCase) || IsComplexBodyType(valueType))
                {
                    bodyObject = new XpsRequestBody(context.Request).Json(valueType);
                    arguments[i] = bodyObject;
                    continue;
                }

                if (parameter.HasDefaultValue) { arguments[i] = parameter.DefaultValue; continue; }
                if (Nullable.GetUnderlyingType(valueType) is not null || !valueType.IsValueType) { arguments[i] = null; continue; }
                AddError(validationErrors, name, $"Required parameter '{name}' is missing.");
            }

            if (bodyObject is not null)
                ValidateObject(bodyObject, descriptor.ValidationRules ?? Array.Empty<XpsValidationRule>(), validationErrors);
        }
        catch (XpsRestBindingException ex) { AddError(validationErrors, "body", ex.Message); }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException) { AddError(validationErrors, "request", ex.Message); }

        errors = validationErrors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        return errors.Count == 0;
    }

    private static bool TryExplicitBinding(XpsParameterBinding binding, ParameterInfo parameter, XpsWebContext context, ref object? bodyObject, out object? value, out string? error)
    {
        value = null;
        error = null;
        var sourceName = string.IsNullOrWhiteSpace(binding.SourceName) ? binding.ParameterName : binding.SourceName!;
        var parameterType = ValueParameterType(parameter);
        try
        {
            switch (binding.Source.ToUpperInvariant())
            {
                case "ROUTE":
                    if (!context.RouteValues.TryGetValue(sourceName, out var routeValue)) { error = $"Route parameter '{sourceName}' is missing."; return false; }
                    value = ConvertScalar(routeValue, parameterType, binding.ParameterName);
                    return true;
                case "QUERY":
                    var queryValues = context.Request.QueryAll(sourceName);
                    if (queryValues.Count == 0) return OptionalOrMissing(parameter, parameterType, binding.ParameterName, out value, out error);
                    value = ConvertScalar(queryValues[0], parameterType, binding.ParameterName);
                    return true;
                case "HEADER":
                    var headerValues = context.Request.HeaderAll(sourceName);
                    if (headerValues.Count == 0) return OptionalOrMissing(parameter, parameterType, binding.ParameterName, out value, out error);
                    value = ConvertScalar(headerValues[0], parameterType, binding.ParameterName);
                    return true;
                case "BODY":
                    bodyObject = new XpsRequestBody(context.Request).Json(parameterType);
                    value = bodyObject;
                    return true;
                default:
                    error = $"Unsupported parameter binding source '{binding.Source}'.";
                    return false;
            }
        }
        catch (XpsRestBindingException ex) { error = ex.Message; return false; }
    }

    private static bool OptionalOrMissing(ParameterInfo parameter, Type valueType, string name, out object? value, out string? error)
    {
        if (parameter.HasDefaultValue) { value = parameter.DefaultValue; error = null; return true; }
        if (Nullable.GetUnderlyingType(valueType) is not null || !valueType.IsValueType) { value = null; error = null; return true; }
        value = null;
        error = $"Required parameter '{name}' is missing.";
        return false;
    }

    private static string SourceParameterName(ParameterInfo parameter)
    {
        var name = parameter.Name ?? string.Empty;
        if (name.StartsWith(ByValPrefix, StringComparison.Ordinal)) return name[ByValPrefix.Length..];
        if (name.StartsWith(ByRefPrefix, StringComparison.Ordinal)) return name[ByRefPrefix.Length..];
        return name;
    }

    private static Type ValueParameterType(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        return type.IsByRef ? type.GetElementType() ?? type : type;
    }

    private static object? ConvertScalar(string value, Type targetType, string parameterName)
    {
        var nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable is not null) { if (string.IsNullOrWhiteSpace(value)) return null; targetType = nullable; }
        try
        {
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(short)) return short.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(byte)) return byte.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(Guid)) return Guid.Parse(value);
            if (targetType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
        { throw new XpsRestBindingException($"Parameter '{parameterName}' is not a valid {targetType.Name}.", ex); }
    }

    private static bool IsComplexBodyType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type != typeof(string) && !type.IsPrimitive && !type.IsEnum && type != typeof(decimal) && type != typeof(Guid) && type != typeof(DateTime) && type != typeof(DateTimeOffset);
    }

    private static void ValidateObject(object value, IReadOnlyList<XpsValidationRule> rules, Dictionary<string, List<string>> errors)
    {
        value = XpsRequestBody.UnwrapXpsObjectReference(value) ?? value;
        var type = value.GetType();
        foreach (var rule in rules.Where(x => x.TypeName.Equals(type.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var member = (MemberInfo?)type.GetProperty(rule.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                         ?? type.GetField(rule.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (member is null) continue;
            var memberValue = member switch { PropertyInfo property => property.GetValue(value), FieldInfo field => field.GetValue(value), _ => null };
            switch (rule.Rule.ToUpperInvariant())
            {
                case "REQUIRED":
                    if (memberValue is null || memberValue is string text && string.IsNullOrWhiteSpace(text)) AddError(errors, rule.MemberName, $"{rule.MemberName} is required.");
                    break;
                case "MAXLENGTH":
                    if (memberValue is string maxText && int.TryParse(rule.Argument1, NumberStyles.None, CultureInfo.InvariantCulture, out var max) && maxText.Length > max) AddError(errors, rule.MemberName, $"{rule.MemberName} cannot exceed {max} characters.");
                    break;
                case "EMAIL":
                    if (memberValue is string email && email.Length > 0 && !IsValidEmail(email)) AddError(errors, rule.MemberName, $"{rule.MemberName} must be a valid email address.");
                    break;
                case "RANGE":
                    if (memberValue is not null && decimal.TryParse(Convert.ToString(memberValue, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && decimal.TryParse(rule.Argument1, NumberStyles.Number, CultureInfo.InvariantCulture, out var min) && decimal.TryParse(rule.Argument2, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxValue) && (number < min || number > maxValue)) AddError(errors, rule.MemberName, $"{rule.MemberName} must be between {min} and {maxValue}.");
                    break;
            }
        }
    }

    private static bool IsValidEmail(string value)
    {
        try { var address = new MailAddress(value); return address.Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var values)) errors[key] = values = [];
        values.Add(message);
    }
}

public sealed class XpsFixedWindowRateLimiter
{
    private readonly ConcurrentDictionary<string, WindowState> _windows = new(StringComparer.Ordinal);
    public bool TryAcquire(string key, XpsRateLimitRule rule, DateTimeOffset now, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(rule);
        var current = _windows.GetOrAdd(key, _ => new WindowState(now, 0));
        lock (current.Gate)
        {
            if (now - current.Start >= rule.Window) { current.Start = now; current.Count = 1; retryAfter = TimeSpan.Zero; return true; }
            if (current.Count < rule.PermitLimit) { current.Count++; retryAfter = TimeSpan.Zero; return true; }
            retryAfter = rule.Window - (now - current.Start);
            if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    private sealed class WindowState(DateTimeOffset start, int count)
    {
        public object Gate { get; } = new();
        public DateTimeOffset Start { get; set; } = start;
        public int Count { get; set; } = count;
    }
}
