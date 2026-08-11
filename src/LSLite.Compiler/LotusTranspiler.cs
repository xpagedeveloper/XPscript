namespace LSLite.Compiler;

public sealed class LotusTranspiler
{
    public string Transpile(string source, string sourceName)
    {
        var generated = new CoreCompatibilityTranspiler().Transpile(source, sourceName);
        generated += "\n\n" + CoreControlRuntimeSource.Code + "\n";

        // The active error statement is recorded by LSControlRuntime.Capture when an
        // exception actually occurs. Statements executed by an error handler must not
        // replace that position before Resume or Resume Next executes.
        generated = System.Text.RegularExpressions.Regex.Replace(
            generated,
            @"(?m)^\s*__lsErrCtx\.Statement\s*=\s*\d+;\s*\r?$\n?",
            "");

        // Object-reference checks such as "q Is Nothing" are emitted through the
        // shared LSRef<T> wrapper. Prevent a later member-access rewrite from
        // turning q.IsNothing into q.Value!.IsNothing.
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    public string Transpile(string source) =>
        Transpile(source, "input.ls");
}
