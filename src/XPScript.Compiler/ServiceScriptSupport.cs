using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed record XpsServiceJob(string ProcedureName, TimeSpan Interval);

internal sealed record XpsServiceDefinition(
    bool IsService,
    string Source,
    TimeSpan StopTimeout,
    IReadOnlyList<XpsServiceJob> Jobs)
{
    public static XpsServiceDefinition None(string source) =>
        new(false, source, TimeSpan.FromSeconds(30), Array.Empty<XpsServiceJob>());
}

internal static class XpsServiceScriptParser
{
    private static readonly Regex ServiceRule = new(@"^\s*\[Service\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex StopTimeoutRule = new(@"^\s*\[StopTimeout\s*:\s*([^\]]+)\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex IntervalRule = new(@"^\s*\[Interval\s*:\s*([^\]]+)\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcedureHeader = new(@"^\s*(?:(?:Public|Private)\s+)?Sub\s+([A-Za-z_]\w*)\s*(?:\(([^)]*)\))?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static XpsServiceDefinition Parse(string source, string sourceName)
    {
        var normalized = (source ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var first = normalized.Split('\n').FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        if (!ServiceRule.IsMatch(first)) return XpsServiceDefinition.None(source);

        var lines = normalized.Split('\n');
        var output = new List<string>(lines.Length);
        var jobs = new List<XpsServiceJob>();
        var stopTimeout = TimeSpan.FromSeconds(30);
        TimeSpan? pendingInterval = null;
        var serviceSeen = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (ServiceRule.IsMatch(line))
            {
                if (serviceSeen) throw Error(sourceName, i + 1, "[Service] may only be declared once.");
                serviceSeen = true;
                output.Add(string.Empty);
                continue;
            }

            var stopMatch = StopTimeoutRule.Match(line);
            if (stopMatch.Success)
            {
                stopTimeout = ParseDuration(stopMatch.Groups[1].Value, sourceName, i + 1, "StopTimeout");
                output.Add(string.Empty);
                continue;
            }

            var intervalMatch = IntervalRule.Match(line);
            if (intervalMatch.Success)
            {
                if (pendingInterval is not null) throw Error(sourceName, i + 1, "An [Interval] rule must be followed by a Sub before another [Interval] rule.");
                pendingInterval = ParseDuration(intervalMatch.Groups[1].Value, sourceName, i + 1, "Interval");
                output.Add(string.Empty);
                continue;
            }

            if (pendingInterval is not null)
            {
                if (string.IsNullOrWhiteSpace(line) || IsComment(line))
                {
                    output.Add(line);
                    continue;
                }

                var procedure = ProcedureHeader.Match(line);
                if (!procedure.Success) throw Error(sourceName, i + 1, "[Interval] must be followed by a parameterless module-level Sub procedure.");
                if (!string.IsNullOrWhiteSpace(procedure.Groups[2].Value)) throw Error(sourceName, i + 1, "An [Interval] Sub cannot declare parameters.");

                var name = procedure.Groups[1].Value;
                if (name.Equals("ServiceStart", StringComparison.OrdinalIgnoreCase) || name.Equals("ServiceStop", StringComparison.OrdinalIgnoreCase))
                    throw Error(sourceName, i + 1, "ServiceStart and ServiceStop cannot have an [Interval] rule.");
                if (jobs.Any(x => x.ProcedureName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    throw Error(sourceName, i + 1, $"Service job '{name}' has more than one [Interval] rule.");

                jobs.Add(new XpsServiceJob(name, pendingInterval.Value));
                pendingInterval = null;
            }

            output.Add(line);
        }

        if (pendingInterval is not null) throw Error(sourceName, lines.Length, "[Interval] must be followed by a Sub procedure.");

        var stripped = string.Join("\n", output);
        ValidateHook(stripped, sourceName, "ServiceStart");
        ValidateHook(stripped, sourceName, "ServiceStop");
        return new XpsServiceDefinition(true, stripped, stopTimeout, jobs.AsReadOnly());
    }

    private static void ValidateHook(string source, string sourceName, string hook)
    {
        var matches = Regex.Matches(source, $@"(?im)^\s*(?:(?:Public|Private)\s+)?Sub\s+{hook}\s*(?:\(([^)]*)\))?\s*$");
        if (matches.Count > 1) throw new CompilerException($"{sourceName}: service hook '{hook}' may only be declared once.");
        if (matches.Count == 1 && !string.IsNullOrWhiteSpace(matches[0].Groups[1].Value))
            throw new CompilerException($"{sourceName}: service hook '{hook}' cannot declare parameters.");
    }

    private static TimeSpan ParseDuration(string raw, string sourceName, int line, string rule)
    {
        var match = Regex.Match(raw.Trim(), @"^(\d+)\s*([smhd])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var amount) || amount <= 0)
            throw Error(sourceName, line, $"[{rule}] must use a positive duration such as 30s, 4m, 1h or 1d.");
        try
        {
            return match.Groups[2].Value.ToLowerInvariant() switch
            {
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "h" => TimeSpan.FromHours(amount),
                "d" => TimeSpan.FromDays(amount),
                _ => throw new InvalidOperationException()
            };
        }
        catch (OverflowException)
        {
            throw Error(sourceName, line, $"[{rule}] duration is too large.");
        }
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("'", StringComparison.Ordinal) || trimmed.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase);
    }

    private static CompilerException Error(string sourceName, int line, string message) => new($"{sourceName}({line}): {message}");
}

internal static class XpsServiceGeneratedCodePostProcessor
{
    private static readonly Regex ScriptEntryCall = new(@"(?m)^\s*Script\.[A-Za-z_]\w*\(\);\s*$", RegexOptions.CultureInvariant);

    public static string Transform(string generated, XpsServiceDefinition definition)
    {
        if (!definition.IsService) return generated;
        var result = ScriptEntryCall.Replace(generated, "        XPScriptServiceRuntime.Run(typeof(Script));", 1);
        if (result == generated) throw new CompilerException("Unable to generate service entry point.");
        return result;
    }
}

internal static class XpsServiceRuntimeSource
{
    public static string Build(XpsServiceDefinition definition)
    {
        if (!definition.IsService) return string.Empty;
        var jobs = string.Join(",\n", definition.Jobs.Select(job => $"        new ServiceJob(\"{Escape(job.ProcedureName)}\", TimeSpan.FromTicks({job.Interval.Ticks}L))"));

        return $$"""
internal static class XPScriptServiceRuntime
{
    private sealed record ServiceJob(string ProcedureName, TimeSpan Interval);
    private static readonly ServiceJob[] Jobs =
    [
{{jobs}}
    ];
    private static readonly TimeSpan StopTimeout = TimeSpan.FromTicks({{definition.StopTimeout.Ticks}}L);
    private static readonly ManualResetEventSlim StopSignal = new(false);
    private static readonly object ActiveLock = new();
    private static readonly HashSet<Task> ActiveRuns = [];
    private static int _stopping;

    public static bool Stopping => Volatile.Read(ref _stopping) != 0;

    public static void Run(Type scriptType)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XPSCRIPT_NAVIGATION_FILE")))
        {
            Console.Error.WriteLine("error: service scripts cannot be run directly. Use 'xpscript compile <file>.xps'.");
            Environment.ExitCode = 2;
            return;
        }

        using var schedulerCancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            RequestStop(schedulerCancellation);
        };
        Console.CancelKeyPress += cancelHandler;

        System.Runtime.InteropServices.PosixSignalRegistration? sigTerm = null;
        System.Runtime.InteropServices.PosixSignalRegistration? sigInt = null;
        if (!OperatingSystem.IsWindows())
        {
            sigTerm = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                RequestStop(schedulerCancellation);
            });
            sigInt = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                RequestStop(schedulerCancellation);
            });
        }

        try
        {
            InvokeOptional(scriptType, "ServiceStart");
            var schedulers = Jobs.Select(job => RunSchedulerAsync(scriptType, job, schedulerCancellation.Token)).ToArray();
            StopSignal.Wait();
            RequestStop(schedulerCancellation);

            var deadline = DateTime.UtcNow + StopTimeout;
            if (!WaitUntil(Task.WhenAll(schedulers), deadline) || !WaitForActiveRuns(deadline))
            {
                Environment.ExitCode = 3;
                return;
            }

            InvokeOptional(scriptType, "ServiceStop");
        }
        finally
        {
            Volatile.Write(ref _stopping, 1);
            try { schedulerCancellation.Cancel(); } catch (ObjectDisposedException) { }
            Console.CancelKeyPress -= cancelHandler;
            sigTerm?.Dispose();
            sigInt?.Dispose();
        }
    }

    private static async Task RunSchedulerAsync(Type scriptType, ServiceJob job, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var run = Task.Run(() => InvokeRequired(scriptType, job.ProcedureName), CancellationToken.None);
            lock (ActiveLock) ActiveRuns.Add(run);
            try { await run.ConfigureAwait(false); }
            catch (Exception ex) { Console.Error.WriteLine("service job '" + job.ProcedureName + "' failed: " + SafeMessage(ex)); }
            finally { lock (ActiveLock) ActiveRuns.Remove(run); }

            try { await Task.Delay(job.Interval, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }

    private static bool WaitForActiveRuns(DateTime deadline)
    {
        Task[] active;
        lock (ActiveLock) active = ActiveRuns.ToArray();
        return active.Length == 0 || WaitUntil(Task.WhenAll(active), deadline);
    }

    private static bool WaitUntil(Task task, DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) return task.IsCompleted;
        try { return task.Wait(remaining); }
        catch { return task.IsCompleted; }
    }

    private static void RequestStop(CancellationTokenSource cancellation)
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
        StopSignal.Set();
    }

    private static void InvokeOptional(Type scriptType, string name)
    {
        var method = FindMethod(scriptType, name);
        if (method is not null) Invoke(method);
    }

    private static void InvokeRequired(Type scriptType, string name)
    {
        var method = FindMethod(scriptType, name) ?? throw new InvalidOperationException("Service procedure was not found: " + name);
        Invoke(method);
    }

    private static System.Reflection.MethodInfo? FindMethod(Type scriptType, string name) =>
        scriptType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .SingleOrDefault(method => method.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && method.GetParameters().Length == 0);

    private static void Invoke(System.Reflection.MethodInfo method)
    {
        try { method.Invoke(null, null); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private static string SafeMessage(Exception ex)
    {
        var text = ex.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length == 0 ? ex.GetType().Name : text;
    }
}
""";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
