using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler.Syntax;

/// <summary>Opt-in parsed-syntax reuse. Production compilation does not use this cache.</summary>
public static class ParsedSyntaxCache
{
    private static readonly ConcurrentDictionary<string, ParsedSyntaxResult> Entries = new(StringComparer.Ordinal);

    public static ParsedSyntaxResult Parse(string source, string parserVersion = "v1", int baseOffset = 0, bool classMemberContext = false, bool classHasBase = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = BuildKey(source, parserVersion, baseOffset, classMemberContext, classHasBase);
        return Entries.GetOrAdd(key, _ => ParseUncached(source, baseOffset, classMemberContext, classHasBase));
    }

    public static void Clear() => Entries.Clear();

    private static ParsedSyntaxResult ParseUncached(string source, int baseOffset, bool classMemberContext, bool classHasBase)
    {
        var parser = new DeclarationParser(source, baseOffset, classMemberContext, classHasBase);
        var tree = parser.ParseCompilationUnit();
        return new ParsedSyntaxResult(tree, parser.Diagnostics.ToArray());
    }

    private static string BuildKey(string source, string parserVersion, int baseOffset, bool classMemberContext, bool classHasBase)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"{parserVersion}|{baseOffset}|{classMemberContext}|{classHasBase}|{Convert.ToHexString(bytes)}";
    }
}

public sealed record ParsedSyntaxResult(CompilationUnitSyntax Tree, IReadOnlyList<SyntaxDiagnostic> Diagnostics);
