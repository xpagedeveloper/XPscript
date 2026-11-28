namespace XPScript.Compiler.Syntax;

public enum DeclarationVisibility
{
    Private,
    Public
}

public static class DeclarationVisibilityResolver
{
    public static DeclarationVisibility ResolveClass(SyntaxToken? visibility, bool optionPublic = false)
        => Resolve(visibility, optionPublic ? DeclarationVisibility.Public : DeclarationVisibility.Private);

    public static DeclarationVisibility ResolveField(SyntaxToken? visibility, bool optionPublic = false)
        => Resolve(visibility, optionPublic ? DeclarationVisibility.Public : DeclarationVisibility.Private);

    public static DeclarationVisibility ResolveClassMember(SyntaxToken? visibility)
        => Resolve(visibility, DeclarationVisibility.Public);

    private static DeclarationVisibility Resolve(SyntaxToken? visibility, DeclarationVisibility defaultVisibility)
        => visibility?.Kind switch
        {
            SyntaxKind.PublicKeyword => DeclarationVisibility.Public,
            SyntaxKind.PrivateKeyword => DeclarationVisibility.Private,
            _ => defaultVisibility
        };
}
