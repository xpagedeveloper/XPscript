namespace LSLite.Compiler;

public sealed class LotusTranspiler
{
    public string Transpile(string source, string sourceName)
    {
        var generated = new AdvancedLotusTranspiler().Transpile(source, sourceName);

        // Object-reference checks such as "q Is Nothing" are emitted through the
        // shared LSRef<T> wrapper. Prevent a later member-access rewrite from
        // turning q.IsNothing into q.Value!.IsNothing.
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    public string Transpile(string source) =>
        Transpile(source, "input.ls");
}
