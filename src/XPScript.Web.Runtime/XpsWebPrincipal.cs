namespace XPScript.Web.Runtime;

public sealed class XpsWebPrincipal
{
    private readonly HashSet<string> _rules;

    public XpsWebPrincipal(bool isAuthenticated, string? userId = null, string? name = null, IEnumerable<string>? rules = null)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
        Name = name;
        _rules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rules is null) return;
        foreach (var rule in rules)
        {
            var normalized = NormalizeRule(rule);
            if (normalized.Length > 0) _rules.Add(normalized);
        }
    }

    public bool IsAuthenticated { get; }
    public string? UserId { get; }
    public string? Name { get; }
    public IReadOnlyCollection<string> Rules => _rules;

    public bool HasRule(string rule) => _rules.Contains(NormalizeRule(rule));

    public XpsWebPrincipal MergeSession(IXpsSession? session)
    {
        if (session is null || !session.Started) return this;
        var rules = new HashSet<string>(_rules, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in session.Rules) rules.Add(NormalizeRule(rule));
        return new XpsWebPrincipal(
            IsAuthenticated || session.IsAuthenticated,
            UserId ?? session.UserId,
            Name ?? session.UserName,
            rules);
    }

    private static string NormalizeRule(string? rule)
    {
        var value = (rule ?? string.Empty).Trim();
        if (value.Length > 128) throw new ArgumentException("Rule name exceeds 128 characters.", nameof(rule));
        foreach (var c in value)
        {
            if (char.IsControl(c)) throw new ArgumentException("Rule name contains a control character.", nameof(rule));
        }
        return value;
    }
}

public sealed record XpsRoutePolicy(
    bool AllowAnonymous,
    IReadOnlySet<string> Methods,
    IReadOnlyList<string> RequiredRules,
    IReadOnlyList<string> ForbiddenRules)
{
    public static XpsRoutePolicy Authenticated(params string[] methods) =>
        new(false, new HashSet<string>(methods.Select(x => x.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase), [], []);

    public XpsRouteAuthorizationResult Authorize(XpsWebRequest request, XpsWebPrincipal principal, IXpsSession? session = null)
    {
        var effectivePrincipal = principal.MergeSession(session);
        if (Methods.Count > 0 && !IsMethodAllowed(request.Method)) return XpsRouteAuthorizationResult.MethodNotAllowed;
        if (!AllowAnonymous && !effectivePrincipal.IsAuthenticated) return XpsRouteAuthorizationResult.AuthenticationRequired;
        if (RequiredRules.Any(rule => !effectivePrincipal.HasRule(rule))) return XpsRouteAuthorizationResult.Forbidden;
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
