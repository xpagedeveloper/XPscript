using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Runtime;

public sealed record XpsWebRouteDescriptor(
    string ProcedureName,
    XpsRoutePolicy Policy,
    string? RouteTemplate = null,
    XpsCorsRule? Cors = null,
    XpsRateLimitRule? RateLimit = null,
    IReadOnlyList<XpsValidationRule>? ValidationRules = null,
    IReadOnlyList<XpsParameterBinding>? ParameterBindings = null);

public sealed record XpsWebRouteParseResult(
    string Source,
    IReadOnlyDictionary<string, XpsWebRouteDescriptor> Routes,
    IReadOnlyList<string> PrecompileTargets,
    string? Platform = null);

public sealed class XpsWebRouteMetadataParser
{
    private static readonly Regex ProcedurePattern = new(
        @"^\s*(?:Public\s+|Private\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ClassPattern = new(
        @"^\s*(?:Public\s+|Private\s+)?Class\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FieldPattern = new(
        @"^\s*(?:Public\s+|Private\s+)?([A-Za-z_]\w*)\s+As\s+[A-Za-z_]\w*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ParameterBindingPattern = new(
        "\\[(FromRoute|FromQuery|FromBody|FromHeader)(?::(?:\"([^\"]+)\"|([^\\]]+)))?\\]\\s*(?:(ByVal|ByRef)\\s+)?([A-Za-z_]\\w*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ShorthandHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "POST", "PUT", "DELETE", "CONNECT", "OPTIONS", "TRACE", "PATCH",
        "ACL", "BASELINE-CONTROL", "BIND", "CHECKIN", "CHECKOUT", "COPY", "LABEL", "LINK", "LOCK",
        "MERGE", "MKACTIVITY", "MKCALENDAR", "MKCOL", "MKREDIRECTREF", "MKWORKSPACE", "MOVE",
        "ORDERPATCH", "PRI", "PROPFIND", "PROPPATCH", "REBIND", "REPORT", "SEARCH", "UNBIND",
        "UNCHECKOUT", "UNLINK", "UNLOCK", "UPDATE", "UPDATEREDIRECTREF", "VERSION-CONTROL"
    };

    public XpsWebRouteParseResult Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder(source.Length);
        var pending = new List<string>();
        var pendingValidation = new List<string>();
        var validationRules = new List<XpsValidationRule>();
        var routes = new Dictionary<string, XpsWebRouteDescriptor>(StringComparer.OrdinalIgnoreCase);
        var precompileTargets = new List<string>();
        string? platform = null;
        string? currentClass = null;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();

            var classMatch = ClassPattern.Match(raw);
            if (classMatch.Success)
            {
                if (pending.Count > 0) throw new XpsWebRouteMetadataException("Web route attributes cannot be applied to a Class declaration.");
                if (pendingValidation.Count > 0) throw new XpsWebRouteMetadataException("Validation attributes must immediately precede a class field.");
                currentClass = classMatch.Groups[1].Value;
                output.AppendLine(raw);
                continue;
            }

            if (trimmed.Equals("End Class", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingValidation.Count > 0) throw new XpsWebRouteMetadataException("Validation attributes are not followed by a class field.");
                currentClass = null;
                output.AppendLine(raw);
                continue;
            }

            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                var attribute = trimmed[1..^1].Trim();
                if (attribute.StartsWith("Platform:", StringComparison.OrdinalIgnoreCase))
                {
                    if (pending.Count > 0) throw new XpsWebRouteMetadataException("Platform metadata must be declared at file level.");
                    var value = attribute[9..].Trim();
                    if (value.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
                    {
                        if (platform is not null) throw new XpsWebRouteMetadataException("A web source may declare [Platform:browser-wasm] only once.");
                        platform = "browser-wasm";
                    }
                    else Console.Error.WriteLine($"error: Unsupported web platform '[Platform:{value}]'. Ignoring rule.");
                    output.AppendLine();
                    continue;
                }

                if (attribute.StartsWith("PreCompile:", StringComparison.OrdinalIgnoreCase))
                {
                    ParsePrecompileTargets(attribute[11..], precompileTargets);
                    output.AppendLine();
                    continue;
                }

                if (currentClass is not null && IsValidationAttribute(attribute))
                {
                    pendingValidation.Add(attribute);
                    output.AppendLine();
                    continue;
                }

                if (!IsKnownRouteAttribute(attribute))
                {
                    Console.Error.WriteLine($"error: Unsupported web route attribute '[{attribute}]'. Ignoring rule.");
                    output.AppendLine();
                    continue;
                }

                if (currentClass is not null) throw new XpsWebRouteMetadataException("Web route attributes cannot be declared inside a Class.");
                pending.Add(attribute);
                output.AppendLine();
                continue;
            }

            if (pendingValidation.Count > 0)
            {
                if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal))
                {
                    output.AppendLine(raw);
                    continue;
                }
                if (currentClass is null) throw new XpsWebRouteMetadataException("Validation attributes may only be used on fields inside a Class.");
                var field = FieldPattern.Match(raw);
                if (!field.Success) throw new XpsWebRouteMetadataException("Validation attributes must immediately precede a class field declaration.");
                var memberName = field.Groups[1].Value;
                foreach (var validation in pendingValidation) validationRules.Add(ParseValidationRule(currentClass, memberName, validation));
                pendingValidation.Clear();
            }

            if (pending.Count > 0)
            {
                if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal))
                {
                    output.AppendLine(raw);
                    continue;
                }

                var procedure = ProcedurePattern.Match(raw);
                if (!procedure.Success) throw new XpsWebRouteMetadataException("Web route attributes must immediately precede a Sub or Function declaration.");
                var name = procedure.Groups[1].Value;
                var parameterBindings = ParseParameterBindings(raw, out var sanitizedDeclaration);
                var descriptor = BuildDescriptor(name, pending) with { ParameterBindings = parameterBindings };
                if (!routes.TryAdd(name, descriptor)) throw new XpsWebRouteMetadataException($"Duplicate web route metadata for procedure '{name}'.");
                pending.Clear();
                output.AppendLine(sanitizedDeclaration);
                continue;
            }

            output.AppendLine(raw);
        }

        if (pending.Count > 0) throw new XpsWebRouteMetadataException("Web route attributes are not followed by a Sub or Function declaration.");
        if (pendingValidation.Count > 0) throw new XpsWebRouteMetadataException("Validation attributes are not followed by a class field.");

        var frozenValidation = (IReadOnlyList<XpsValidationRule>)validationRules.AsReadOnly();
        foreach (var key in routes.Keys.ToArray()) routes[key] = routes[key] with { ValidationRules = frozenValidation };
        ValidateExplicitRouteDuplicates(routes.Values);

        return new XpsWebRouteParseResult(output.ToString().TrimEnd('\r', '\n'), routes, precompileTargets.AsReadOnly(), platform);
    }

    private static IReadOnlyList<XpsParameterBinding> ParseParameterBindings(string declaration, out string sanitizedDeclaration)
    {
        var bindings = new List<XpsParameterBinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ParameterBindingPattern.Matches(declaration))
        {
            var annotation = match.Groups[1].Value;
            var parameterName = match.Groups[5].Value;
            if (!seen.Add(parameterName)) throw new XpsWebRouteMetadataException($"Parameter '{parameterName}' has more than one binding attribute.");
            var source = annotation[4..].ToUpperInvariant();
            var sourceName = match.Groups[2].Success ? match.Groups[2].Value.Trim() : match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            if (source == "BODY" && !string.IsNullOrWhiteSpace(sourceName)) throw new XpsWebRouteMetadataException("FromBody does not accept a source name.");
            if (sourceName is not null && (sourceName.Length is < 1 or > 256 || sourceName.Any(char.IsControl))) throw new XpsWebRouteMetadataException("Parameter binding source name is invalid.");
            bindings.Add(new XpsParameterBinding(parameterName, source, sourceName));
        }

        sanitizedDeclaration = ParameterBindingPattern.Replace(declaration, match =>
        {
            var mode = match.Groups[4].Success ? match.Groups[4].Value + " " : string.Empty;
            return mode + match.Groups[5].Value;
        });
        return bindings.AsReadOnly();
    }

    private static void ParsePrecompileTargets(string value, List<string> targets)
    {
        foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var target = raw.Trim();
            if (target.Length is 0 or > 1024) throw new XpsWebRouteMetadataException("PreCompile target must contain 1 to 1024 characters.");
            if (target.Any(char.IsControl)) throw new XpsWebRouteMetadataException("PreCompile target contains a control character.");
            if (string.IsNullOrEmpty(Path.GetExtension(target))) target += ".xps";
            else if (target.EndsWith(".xsp", StringComparison.OrdinalIgnoreCase))
            {
                var corrected = target[..^4] + ".xps";
                Console.Error.WriteLine($"error: PreCompile target '{target}' uses the misspelled .xsp extension. Trying '{corrected}'.");
                target = corrected;
            }
            else if (!target.EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"error: PreCompile target '{target}' does not use the .xps extension. Ignoring target.");
                continue;
            }
            if (!targets.Contains(target, StringComparer.OrdinalIgnoreCase)) targets.Add(target);
            if (targets.Count > 128) throw new XpsWebRouteMetadataException("A script may declare at most 128 PreCompile targets.");
        }
        if (targets.Count == 0) throw new XpsWebRouteMetadataException("PreCompile requires at least one valid target.");
    }

    private static bool IsKnownRouteAttribute(string attribute)
    {
        if (attribute.Equals("Anonymous", StringComparison.OrdinalIgnoreCase) ||
            attribute.Equals("Authenticated", StringComparison.OrdinalIgnoreCase) ||
            attribute.StartsWith("Rule:", StringComparison.OrdinalIgnoreCase) ||
            attribute.StartsWith("Role:", StringComparison.OrdinalIgnoreCase) ||
            attribute.StartsWith("Route:", StringComparison.OrdinalIgnoreCase) ||
            attribute.Equals("Cors", StringComparison.OrdinalIgnoreCase) ||
            attribute.StartsWith("Cors:", StringComparison.OrdinalIgnoreCase) ||
            attribute.StartsWith("RateLimit:", StringComparison.OrdinalIgnoreCase)) return true;
        return TryParseHttpMethodAttribute(attribute, out _);
    }

    private static bool IsValidationAttribute(string attribute) =>
        attribute.Equals("Required", StringComparison.OrdinalIgnoreCase) ||
        attribute.Equals("Email", StringComparison.OrdinalIgnoreCase) ||
        attribute.StartsWith("MaxLength:", StringComparison.OrdinalIgnoreCase) ||
        attribute.StartsWith("Range:", StringComparison.OrdinalIgnoreCase);

    private static XpsValidationRule ParseValidationRule(string typeName, string memberName, string attribute)
    {
        if (attribute.Equals("Required", StringComparison.OrdinalIgnoreCase)) return new XpsValidationRule(typeName, memberName, "Required");
        if (attribute.Equals("Email", StringComparison.OrdinalIgnoreCase)) return new XpsValidationRule(typeName, memberName, "Email");
        if (attribute.StartsWith("MaxLength:", StringComparison.OrdinalIgnoreCase))
        {
            var raw = attribute[10..].Trim();
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var max) || max < 1 || max > 1_000_000) throw new XpsWebRouteMetadataException("MaxLength must contain an integer between 1 and 1000000.");
            return new XpsValidationRule(typeName, memberName, "MaxLength", max.ToString(CultureInfo.InvariantCulture));
        }
        if (attribute.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
        {
            var values = attribute[6..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length != 2 || !decimal.TryParse(values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var min) || !decimal.TryParse(values[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var max) || min > max) throw new XpsWebRouteMetadataException("Range must use [Range:min;max] with numeric values and min <= max.");
            return new XpsValidationRule(typeName, memberName, "Range", min.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture));
        }
        throw new XpsWebRouteMetadataException($"Unsupported validation attribute '[{attribute}]'.");
    }

    private static XpsWebRouteDescriptor BuildDescriptor(string procedureName, IReadOnlyList<string> attributes)
    {
        var hasAnonymous = attributes.Any(x => x.Equals("Anonymous", StringComparison.OrdinalIgnoreCase));
        var hasAuthenticated = attributes.Any(x => x.Equals("Authenticated", StringComparison.OrdinalIgnoreCase));
        if (hasAnonymous && hasAuthenticated) Console.Error.WriteLine("error: Route declares both [Anonymous] and [Authenticated]. [Authenticated] takes precedence.");
        var allowAnonymous = hasAnonymous && !hasAuthenticated;
        var methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiredRules = new List<string>();
        var forbiddenRules = new List<string>();
        var requiredRoles = new List<string>();
        var forbiddenRoles = new List<string>();
        string? routeTemplate = null;
        XpsCorsRule? cors = null;
        XpsRateLimitRule? rateLimit = null;

        foreach (var attribute in attributes)
        {
            if (attribute.Equals("Anonymous", StringComparison.OrdinalIgnoreCase) || attribute.Equals("Authenticated", StringComparison.OrdinalIgnoreCase)) continue;
            if (TryParseHttpMethodAttribute(attribute, out var method)) { methods.Add(method); continue; }
            if (attribute.StartsWith("Rule:", StringComparison.OrdinalIgnoreCase))
            {
                var rule = attribute[5..].Trim();
                if (rule.Length == 0) throw new XpsWebRouteMetadataException("Rule attribute requires a rule name.");
                if (rule.StartsWith('!')) forbiddenRules.Add(NormalizeRule(rule[1..])); else requiredRules.Add(NormalizeRule(rule));
                continue;
            }
            if (attribute.StartsWith("Role:", StringComparison.OrdinalIgnoreCase)) { ParseRoles(attribute[5..], requiredRoles, forbiddenRoles); continue; }
            if (attribute.StartsWith("Route:", StringComparison.OrdinalIgnoreCase))
            {
                if (routeTemplate is not null) throw new XpsWebRouteMetadataException("A web route may declare only one [Route:...] attribute.");
                routeTemplate = NormalizeRouteTemplate(attribute[6..]);
                continue;
            }
            if (attribute.Equals("Cors", StringComparison.OrdinalIgnoreCase) || attribute.StartsWith("Cors:", StringComparison.OrdinalIgnoreCase))
            {
                if (cors is not null) throw new XpsWebRouteMetadataException("A web route may declare only one [Cors] rule.");
                cors = ParseCors(attribute);
                continue;
            }
            if (attribute.StartsWith("RateLimit:", StringComparison.OrdinalIgnoreCase))
            {
                if (rateLimit is not null) throw new XpsWebRouteMetadataException("A web route may declare only one [RateLimit:...] rule.");
                rateLimit = ParseRateLimit(attribute[10..]);
            }
        }
        if (methods.Count == 0) throw new XpsWebRouteMetadataException("A web route must declare at least one HTTP method attribute.");
        return new XpsWebRouteDescriptor(procedureName, new XpsRoutePolicy(allowAnonymous, methods, requiredRules, forbiddenRules, requiredRoles, forbiddenRoles), routeTemplate, cors, rateLimit);
    }

    private static XpsCorsRule ParseCors(string attribute)
    {
        if (attribute.Equals("Cors", StringComparison.OrdinalIgnoreCase)) return new XpsCorsRule(["*"]);
        var value = attribute[5..].Trim();
        var origins = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (origins.Length == 0) throw new XpsWebRouteMetadataException("Cors requires at least one origin or '*'.");
        if (origins.Length > 32) throw new XpsWebRouteMetadataException("Cors may contain at most 32 origins.");
        foreach (var origin in origins)
        {
            if (origin == "*") continue;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || uri.AbsolutePath != "/") throw new XpsWebRouteMetadataException($"Cors origin '{origin}' must be an absolute http/https origin without a path.");
        }
        return new XpsCorsRule(origins);
    }

    private static XpsRateLimitRule ParseRateLimit(string value)
    {
        var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var permits) || permits is < 1 or > 1_000_000) throw new XpsWebRouteMetadataException("RateLimit must use [RateLimit:requests;window], for example [RateLimit:100;1m].");
        return new XpsRateLimitRule(permits, ParseDuration(parts[1]));
    }

    private static TimeSpan ParseDuration(string value)
    {
        if (value.Length < 2) throw new XpsWebRouteMetadataException("RateLimit window is invalid.");
        var suffix = char.ToLowerInvariant(value[^1]);
        if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount <= 0) throw new XpsWebRouteMetadataException("RateLimit window is invalid.");
        var result = suffix switch { 's' => TimeSpan.FromSeconds(amount), 'm' => TimeSpan.FromMinutes(amount), 'h' => TimeSpan.FromHours(amount), 'd' => TimeSpan.FromDays(amount), _ => throw new XpsWebRouteMetadataException("RateLimit window suffix must be s, m, h or d.") };
        if (result < TimeSpan.FromSeconds(1) || result > TimeSpan.FromDays(30)) throw new XpsWebRouteMetadataException("RateLimit window must be between 1 second and 30 days.");
        return result;
    }

    private static string NormalizeRouteTemplate(string value)
    {
        var route = value.Trim().Replace('\\', '/');
        if (route.Length is < 1 or > 2048 || !route.StartsWith('/', StringComparison.Ordinal)) throw new XpsWebRouteMetadataException("Route must be an absolute path beginning with '/'.");
        if (route.Contains("//", StringComparison.Ordinal) || route.Any(char.IsControl) || route.Contains('?') || route.Contains('#')) throw new XpsWebRouteMetadataException("Route contains an invalid path character or empty segment.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in route.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!segment.StartsWith('{') && !segment.EndsWith('}')) continue;
            if (!(segment.StartsWith('{') && segment.EndsWith('}'))) throw new XpsWebRouteMetadataException("Route parameters must occupy a complete path segment, for example {id}.");
            var name = segment[1..^1].Trim();
            if (name.Length == 0 || !Regex.IsMatch(name, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant)) throw new XpsWebRouteMetadataException($"Invalid route parameter '{{{name}}}'.");
            if (!names.Add(name)) throw new XpsWebRouteMetadataException($"Duplicate route parameter '{{{name}}}'.");
        }
        return route.Length > 1 ? route.TrimEnd('/') : route;
    }

    private static void ValidateExplicitRouteDuplicates(IEnumerable<XpsWebRouteDescriptor> routes)
    {
        var explicitRoutes = routes.Where(x => x.RouteTemplate is not null).ToArray();
        for (var i = 0; i < explicitRoutes.Length; i++)
            for (var j = i + 1; j < explicitRoutes.Length; j++)
                if (string.Equals(explicitRoutes[i].RouteTemplate, explicitRoutes[j].RouteTemplate, StringComparison.OrdinalIgnoreCase) && explicitRoutes[i].Policy.Methods.Intersect(explicitRoutes[j].Policy.Methods, StringComparer.OrdinalIgnoreCase).Any()) throw new XpsWebRouteMetadataException($"Duplicate explicit route '{explicitRoutes[i].RouteTemplate}' has overlapping HTTP methods.");
    }

    private static void ParseRoles(string value, List<string> requiredRoles, List<string> forbiddenRoles)
    {
        var found = false;
        foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            found = true;
            var role = raw.Trim();
            if (role.StartsWith('!')) AddUnique(forbiddenRoles, NormalizeRole(role[1..])); else AddUnique(requiredRoles, NormalizeRole(role));
        }
        if (!found) throw new XpsWebRouteMetadataException("Role attribute requires at least one role name.");
    }

    private static void AddUnique(List<string> values, string value) { if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value); }

    private static bool TryParseHttpMethodAttribute(string attribute, out string method)
    {
        method = string.Empty;
        var candidate = attribute.Trim();
        if (candidate.StartsWith("Method:", StringComparison.OrdinalIgnoreCase)) candidate = candidate[7..].Trim();
        else if (!ShorthandHttpMethods.Contains(candidate)) return false;
        if (!IsValidHttpToken(candidate)) throw new XpsWebRouteMetadataException($"Invalid HTTP method '{candidate}'.");
        method = candidate.ToUpperInvariant();
        return true;
    }

    private static bool IsValidHttpToken(string value)
    {
        if (value.Length is 0 or > 64) return false;
        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c)) continue;
            if (c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~') continue;
            return false;
        }
        return true;
    }

    private static string NormalizeRule(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 or > 128) throw new XpsWebRouteMetadataException("Rule name must contain 1 to 128 characters.");
        if (normalized.Any(char.IsControl)) throw new XpsWebRouteMetadataException("Rule name contains a control character.");
        return normalized;
    }

    private static string NormalizeRole(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 or > 128) throw new XpsWebRouteMetadataException("Role name must contain 1 to 128 characters.");
        if (normalized.Any(char.IsControl)) throw new XpsWebRouteMetadataException("Role name contains a control character.");
        return normalized;
    }
}

public sealed class XpsWebRouteMetadataException : Exception
{
    public XpsWebRouteMetadataException(string message) : base(message) { }
}
