using System.Diagnostics;
using System.Text.Json;

namespace XPScript.Compiler;

/// <summary>Optional phase timing for AST diagnostics and benchmark runs.</summary>
internal sealed class AstPhaseTiming
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, double> _phases = new(StringComparer.Ordinal);
    private double _last;

    public void Mark(string phase)
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        _phases[phase] = now - _last;
        _last = now;
    }

    public void Write(string sourcePath)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_AST_PHASE_TIMINGS"), "1", StringComparison.Ordinal)) return;
        var payload = _phases.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
        payload["source"] = Path.GetFileName(sourcePath);
        payload["total_ms"] = _clock.Elapsed.TotalMilliseconds;
        Console.Error.WriteLine("AST_PHASE_TIMINGS=" + JsonSerializer.Serialize(payload));
    }
}
