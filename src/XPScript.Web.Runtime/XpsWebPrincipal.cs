namespace XPScript.Web.Runtime;

public sealed class XpsWebPrincipal
{
    private readonly HashSet<string> _rules;
    private readonly HashSet<string> _roles;

    public XpsWebPrincipal(
        bool isAuthenticated,
        string? userId = null,
        string? name = null,
        IEnumerable<string>? rules = null,
        IEnumerable<string>? roles = null)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
        Name = name;
        _rules = NormalizeValues(rules);
        _roles = NormalizeValues(roles);
    }

    public bool IsAuthenticated { get; }
    public string? UserId { get; }
    public string? Name { get; }
    public IReadOnlyCollection<string> Rules => _rules;
    public IReadOnlyCollection<string> Roles => _roles;

    public bool HasRule(string rule) => _rules.Contains(NormalizeValue(rule, "Rule"));
    public bool HasRole(string role) => _roles.Contains(NormalizeValue(role, "Role"));

    public XpsWebPrincipal MergeSession(IXpsSession? session)
    {
        if (session is null || !session.Started) return this;
        var rules = new HashSet<string>(_rules, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in session.Rules) rules.Add(NormalizeValue(rule, "Rule"));
        var roles = new HashSet<string>(_roles, StringComparer.OrdinalIgnoreCase);
        foreach (var role in session.Roles) roles.Add(NormalizeValue(role, "Role"));
        return new XpsWebPrincipal(
            IsAuthenticated || session.IsAuthenticated,
            UserId ?? session.UserId,
            Name ?? session.UserName,
            rules,
            roles);
    }

    private static HashSet<string> NormalizeValues(IEnumerable<string>? values)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values is null) return result;
        foreach (var value in values)
        {
            var normalized = NormalizeValue(value, "Authorization value");
            if (normalized.Length > 0) result.Add(normalized);
        }
        return result;
    }

    private static string NormalizeValue(string? value, string kind)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > 128) throw new ArgumentException($"{kind} name exceeds 128 characters.");
        foreach (var c in normalized)
            if (char.IsControl(c)) throw new ArgumentException($"{kind} name contains a control character.");
        return normalized;
    }
}

public sealed record XpsRoutePolicy(
    bool AllowAnonymous,
    IReadOnlySet<string> Methods,
    IReadOnlyList<string> RequiredRules,
    IReadOnlyList<string> ForbiddenRules)
{
    public IReadOnlyList<string> RequiredRoles { get; init; } = Array.Empty<string>();

    public static XpsRoutePolicy Authenticated(params string[] methods) =>
        new(false, new HashSet<string>(methods.Select(x => x.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase), [], []);

    public XpsRouteAuthorizationResult Authorize(XpsWebRequest request, XpsWebPrincipal principal, IXpsSession? session = null)
    {
        var effectivePrincipal = principal.MergeSession(session);
        if (Methods.Count > 0 && !IsMethodAllowed(request.Method)) return XpsRouteAuthorizationResult.MethodNotAllowed;
        if (!AllowAnonymous && !effectivePrincipal.IsAuthenticated) return XpsRouteAuthorizationResult.AuthenticationRequired;
        if (RequiredRules.Any(rule => !effectivePrincipal.HasRule(rule))) return XpsRouteAuthorizationResult.Forbidden;
        if (RequiredRoles.Count > 0 && !RequiredRoles.Any(effectivePrincipal.HasRole)) return XpsRouteAuthorizationResult.Forbidden;
        if (ForbiddenRules.Any(effectivePrincipal.HasRule)) return XpsRouteAuthorizationResult.Forbidden;
        return XpsRouteAuthorizationResult.Allowed;
    }

    private bool IsMethodAllowed(string method)
    {
        if (Methods.Contains(method)) return true;
        return method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) && Methods.Contains("GET");
    }
}

public enum XpsRouteAuthorizationResult
{
    Allowed,
    MethodNotAllowed,
    AuthenticationRequired,
    Forbidden
}