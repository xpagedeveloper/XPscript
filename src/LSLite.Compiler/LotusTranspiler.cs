namespace LSLite.Compiler;

public sealed class LotusTranspiler
{
    public string Transpile(string source, string sourceName) =>
        new AdvancedLotusTranspiler().Transpile(source, sourceName);

    public string Transpile(string source) =>
        Transpile(source, "input.ls");
}
