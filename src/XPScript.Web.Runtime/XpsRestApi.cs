using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Mail;
using System.Reflection;
using System.Text.Json;

namespace XPScript.Web.Runtime;

public sealed record XpsValidationRule(string TypeName, string MemberName, string Rule, string? Argument1 = null, string? Argument2 = null);

public sealed record XpsCorsRule(IReadOnlyList<string> Origins)
{
    public bool AllowsAnyOrigin => Origins.Any(x => x == "*");

    public bool Allows(string origin) =>
        AllowsAnyOrigin || Origins.Contains(origin, StringComparer.OrdinalIgnoreCase);
}

public sealed record XpsRateLimitRule(int PermitLimit, TimeSpan Window)
{
    public XpsRateLimitRule
    {
        if (PermitLimit < 1) throw new ArgumentOutOfRangeException(nameof(PermitLimit));
        if (Window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Window));
    }
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
        if (_request.Body.Length > maxBytes)
            throw new XpsRestBindingException($"JSON body exceeds the configured {maxBytes} byte limit.");
        if (_request.ContentType is null ||
            (!_request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) &&
             !_request.ContentType.Contains("+json", StringComparison.OrdinalIgnoreCase)))
            throw new XpsRestBindingException("Request Content-Type must be application/json.");

        try
        {
            return JsonSerializer.Deserialize(_request.Body.Span, type, XpsRestJson.Options);
        }
        catch (JsonException ex)
        {
            throw new XpsRestBindingException("Request body contains invalid JSON.", ex);
        }
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

public static class XpsRestBinder
{
    public static bool TryBind(
        MethodInfo method,
        XpsWebContext context,
        XpsWebRouteDescriptor descriptor,
        out object?[] arguments,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var parameters = method.GetParameters();
        arguments = new object?[parameters.Length];
        var validationErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        object? bodyObject = null;
        Type? bodyType = null;

        try
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var name = parameter.Name ?? string.Empty;

                if (context.RouteValues.TryGetValue(name, out var routeValue))
                {
                    arguments[i] = ConvertScalar(routeValue, parameter.ParameterType, name);
                    continue;
                }

                var queryValue = context.Request.QueryFirst(name);
                if (queryValue.Length > 0)
                {
                    arguments[i] = ConvertScalar(queryValue, parameter.ParameterType, name);
                    continue;
                }

                if (name.Equals("body", StringComparison.OrdinalIgnoreCase) || IsComplexBodyType(parameter.ParameterType))
                {
                    bodyType ??= parameter.ParameterType;
                    if (bodyObject is null || bodyObject.GetType() != bodyType)
                        bodyObject = new XpsRequestBody(context.Request).Json(bodyType);
                    arguments[i] = bodyObject;
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    arguments[i] = parameter.DefaultValue;
                    continue;
                }

                if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null || !parameter.ParameterType.IsValueType)
                {
                    arguments[i] = null;
                    continue;
                }

                AddError(validationErrors, name, $"Required parameter '{name}' is missing.");
            }

            if (bodyObject is not null)
                ValidateObject(bodyObject, descriptor.ValidationRules, validationErrors);
        }
        catch (XpsRestBindingException ex)
        {
            AddError(validationErrors, "body", ex.Message);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            AddError(validationErrors, "request", ex.Message);
        }

        errors = validationErrors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        return errors.Count == 0;
    }

    private static object? ConvertScalar(string value, Type targetType, string parameterName)
    {
        var nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable is not null)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            targetType = nullable;
        }

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
        {
            throw new XpsRestBindingException($"Parameter '{parameterName}' is not a valid {targetType.Name}.", ex);
        }
    }

    private static bool IsComplexBodyType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type != typeof(string) && !type.IsPrimitive && !type.IsEnum &&
               type != typeof(decimal) && type != typeof(Guid) && type != typeof(DateTime) && type != typeof(DateTimeOffset);
    }

    private static void ValidateObject(object value, IReadOnlyList<XpsValidationRule> rules, Dictionary<string, List<string>> errors)
    {
        var type = value.GetType();
        foreach (var rule in rules.Where(x => x.TypeName.Equals(type.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var member = (MemberInfo?)type.GetProperty(rule.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                         ?? type.GetField(rule.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (member is null) continue;
            var memberValue = member switch
            {
                PropertyInfo property => property.GetValue(value),
                FieldInfo field => field.GetValue(value),
                _ => null
            };

            switch (rule.Rule.ToUpperInvariant())
            {
                case "REQUIRED":
                    if (memberValue is null || memberValue is string text && string.IsNullOrWhiteSpace(text))
                        AddError(errors, rule.MemberName, $"{rule.MemberName} is required.");
                    break;
                case "MAXLENGTH":
                    if (memberValue is string maxText && int.TryParse(rule.Argument1, NumberStyles.None, CultureInfo.InvariantCulture, out var max) && maxText.Length > max)
                        AddError(errors, rule.MemberName, $"{rule.MemberName} cannot exceed {max} characters.");
                    break;
                case "EMAIL":
                    if (memberValue is string email && email.Length > 0 && !IsValidEmail(email))
                        AddError(errors, rule.MemberName, $"{rule.MemberName} must be a valid email address.");
                    break;
                case "RANGE":
                    if (memberValue is not null &&
                        decimal.TryParse(Convert.ToString(memberValue, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) &&
                        decimal.TryParse(rule.Argument1, NumberStyles.Number, CultureInfo.InvariantCulture, out var min) &&
                        decimal.TryParse(rule.Argument2, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxValue) &&
                        (number < min || number > maxValue))
                        AddError(errors, rule.MemberName, $"{rule.MemberName} must be between {min} and {maxValue}.");
                    break;
            }
        }
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return address.Address.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
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

        while (true)
        {
            var current = _windows.GetOrAdd(key, _ => new WindowState(now, 0));
            lock (current.Gate)
            {
                if (now - current.Start >= rule.Window)
                {
                    current.Start = now;
                    current.Count = 1;
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                if (current.Count < rule.PermitLimit)
                {
                    current.Count++;
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                retryAfter = rule.Window - (now - current.Start);
                if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
                return false;
            }
        }
    }

    private sealed class WindowState(DateTimeOffset start, int count)
    {
        public object Gate { get; } = new();
        public DateTimeOffset Start { get; set; } = start;
        public int Count { get; set; } = count;
    }
}
