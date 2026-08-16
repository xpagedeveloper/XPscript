using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Runtime;

public sealed record XpsWebRouteDescriptor(string ProcedureName, XpsRoutePolicy Policy);

public sealed record XpsWebRouteParseResult(string Source, IReadOnlyDictionary<string, XpsWebRouteDescriptor> Routes);

public sealed class XpsWebRouteMetadataParser
{
    private static readonly Regex ProcedurePattern = new(
        @"^\s*(?:Public\s+|Private\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public XpsWebRouteParseResult Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder(source.Length);
        var pending = new List<string>();
        var routes = new Dictionary<string, XpsWebRouteDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[', StringComparison.Ordinal) && trimmed.EndsWith(']', StringComparison.Ordinal))
            {
                pending.Add(trimmed[1..^1].Trim());
                output.AppendLine();
                continue;
            }

            if (pending.Count > 0)
            {
                if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal))
                {
                    output.AppendLine(raw);
                    continue;
                }

                var procedure = ProcedurePattern.Match(raw);
                if (!procedure.Success)
                    throw new XpsWebRouteMetadataException("Web route attributes must immediately precede a Sub or Function declaration.");

                var name = procedure.Groups[1].Value;
                var policy = BuildPolicy(pending);
                if (!routes.TryAdd(name, new XpsWebRouteDescriptor(name, policy)))
                    throw new XpsWebRouteMetadataException($"Duplicate web route metadata for procedure '{name}'.");
                pending.Clear();
            }

            output.AppendLine(raw);
        }

        if (pending.Count > 0)
            throw new XpsWebRouteMetadataException("Web route attributes are not followed by a Sub or Function declaration.");

        return new XpsWebRouteParseResult(output.ToString().TrimEnd('\r', '\n'), routes);
    }

    private static XpsRoutePolicy BuildPolicy(IReadOnlyList<string> attributes)
    {
        var allowAnonymous = false;
        var explicitAuthentication = false;
        var methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiredRules = new List<string>();
        var forbiddenRules = new List<string>();

        foreach (var attribute in attributes)
        {
            if (attribute.Equals("Anonymous", StringComparison.OrdinalIgnoreCase))
            {
                if (explicitAuthentication)
                    throw new XpsWebRouteMetadataException("A route cannot be both Anonymous and Authenticated.");
                allowAnonymous = true;
                continue;
            }
            if (attribute.Equals("Authenticated", StringComparison.OrdinalIgnoreCase))
            {
                if (allowAnonymous)
                    throw new XpsWebRouteMetadataException("A route cannot be both Anonymous and Authenticated.");
                explicitAuthentication = true;
                continue;
            }

            var method = attribute.ToUpperInvariant();
            if (method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")
            {
                methods.Add(method);
                continue;
            }

            if (attribute.StartsWith("Rule:", StringComparison.OrdinalIgnoreCase))
            {
                var rule = attribute[5..].Trim();
                if (rule.Length == 0) throw new XpsWebRouteMetadataException("Rule attribute requires a rule name.");
                if (rule.StartsWith('!'))
                {
                    var forbidden = NormalizeRule(rule[1..]);
                    forbiddenRules.Add(forbidden);
                }
                else
                {
                    requiredRules.Add(NormalizeRule(rule));
                }
                continue;
            }

            throw new XpsWebRouteMetadataException($"Unsupported web route attribute '[{attribute}]'.");
        }

        if (methods.Count == 0)
            throw new XpsWebRouteMetadataException("A web route must declare at least one HTTP method attribute.");

        return new XpsRoutePolicy(allowAnonymous, methods, requiredRules, forbiddenRules);
    }

    private static string NormalizeRule(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 or > 128)
            throw new XpsWebRouteMetadataException("Rule name must contain 1 to 128 characters.");
        if (normalized.Any(char.IsControl))
            throw new XpsWebRouteMetadataException("Rule name contains a control character.");
        return normalized;
    }
}

public sealed class XpsWebRouteMetadataException : Exception
{
    public XpsWebRouteMetadataException(string message) : base(message) { }
}
