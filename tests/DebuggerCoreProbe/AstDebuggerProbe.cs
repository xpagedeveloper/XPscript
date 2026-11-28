using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using XPScript.Compiler;

internal static class AstDebuggerProbe
{
    public static async Task VerifyAsync()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "out", "debugger-core-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "root.xps");
        await File.WriteAllTextAsync(sourcePath, """
Sub Main()
    Dim rootValue As Integer
    rootValue = 1
    Call ErrorFlowProbe()
    Call RangeProbe()
    Call Child()
End Sub
Sub ErrorFlowProbe()
    On Error Resume Next
    Dim errorFlowValue As Integer = 7
End Sub
Sub RangeProbe()
    Dim loopValue As Integer
    For loopValue = 1 To 2
        Print loopValue
    Next
End Sub
Include "child.xps"
""");
        await File.WriteAllTextAsync(Path.Combine(root, "child.xps"), """
Sub Child()
    Dim includedValue As Integer
    includedValue = 2
    includedValue = includedValue + 1
    Error 5
End Sub
""");
        await VerifyAstMutationTrackingAsync(root);

        using var compileTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var executable = await AstCompiler.CompileAsync(sourcePath, Path.Combine(root, "app"), cancellationToken: compileTimeout.Token);
        var port = ReservePort();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(executable);
        process.StartInfo.Environment["XPSCRIPT_DEBUG_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        process.StartInfo.Environment["XPSCRIPT_DEBUG_STOP_ON_ENTRY"] = "1";
        process.StartInfo.Environment["XPSCRIPT_RUNTIME_DEBUG"] = "1";

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Could not launch the AST debugger fixture.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var client = await ConnectAsync(port, process, connectTimeout.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var eventTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "hello");
            var entry = await ReadEventAsync(reader, eventTimeout.Token);
            RequireStopped(entry, "entry", "root.xps", 2);
            await SendAsync(writer, new { command = "setBreakpoints", source = "child.xps", lines = new[] { 3 } });
            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "breakpoints");
            await SendAsync(writer, new { command = "continue" });
            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "continued");
            RequireStopped(await ReadEventAsync(reader, eventTimeout.Token), "breakpoint", "child.xps", 3);
            await SendAsync(writer, new { command = "valueHistory", name = "errorFlowValue" });
            RequireValueHistory(await ReadEventAsync(reader, eventTimeout.Token), "errorFlowValue", "7");
            await SendAsync(writer, new { command = "valueHistory", name = "loopValue" });
            RequireValueHistory(await ReadEventAsync(reader, eventTimeout.Token), "loopValue", "2");

            await SendAsync(writer, new { command = "next" });
            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "continued");
            RequireStopped(await ReadEventAsync(reader, eventTimeout.Token), "step", "child.xps", 4);
            await SendAsync(writer, new { command = "valueHistory", name = "includedValue" });
            RequireValueHistory(await ReadEventAsync(reader, eventTimeout.Token), "includedValue", "2");

            await SendAsync(writer, new { command = "continue" });
            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "continued");
            RequireType(await ReadEventAsync(reader, eventTimeout.Token), "complete");
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(exitTimeout.Token);
            var errorOutput = await stderr;
            if (process.ExitCode == 0 || !System.Text.RegularExpressions.Regex.IsMatch(errorOutput, @"child\.xps:(?:line )?5"))
                throw new InvalidOperationException($"AST debugger runtime error did not retain child.xps line 5 (exit {process.ExitCode}).\n{await stdout}\n{errorOutput}");
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            throw;
        }
    }

    private static async Task VerifyAstMutationTrackingAsync(string root)
    {
        var sourcePath = Path.Combine(root, "mutation-tracking.xps");
        await File.WriteAllTextAsync(sourcePath, """
Class DebugBox
    Public Value As Integer
End Class
Sub Main()
    Dim values(1) As Integer
    values(NextIndex()) = ComputeValue()
    Dim box As DebugBox
    box = New DebugBox
    box.Value = ComputeValue()
    Dim answer As Integer
    Mutate(answer)
End Sub
Function NextIndex() As Integer
    NextIndex = 0
End Function
Function ComputeValue() As Integer
    ComputeValue = 1
End Function
Sub Mutate(ByRef answer As Integer)
    answer = answer + 1
End Sub
""");
        var generated = await AstCompiler.GenerateSourceAsync(sourcePath, Path.Combine(root, "mutation-source"));
        if (generated.Contains("TrackValue(\"values\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Array element changes must not be reported as direct variable writes.");
        if (generated.Contains("TrackValue(\"box.Value\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Object member writes must not be auto-inspected by the debugger.");
        if (generated.Contains("TrackValue(\"__lsv\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated ByRef copy-back variables must not be exposed by the debugger.");
        if (Count(generated, "NextIndex()") != 2)
            throw new InvalidOperationException("AST debugger instrumentation changed array index evaluation.");
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static async Task<TcpClient> ConnectAsync(int port, Process process, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !process.HasExited)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return client;
            }
            catch (SocketException)
            {
                client.Dispose();
                await Task.Delay(50, cancellationToken);
            }
        }
        throw new InvalidOperationException("AST debugger runtime did not open its local endpoint.");
    }

    private static async Task SendAsync<T>(StreamWriter writer, T command) =>
        await writer.WriteLineAsync(JsonSerializer.Serialize(command));

    private static async Task<JsonDocument> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("AST debugger closed its connection before the expected event.");
        return JsonDocument.Parse(line);
    }

    private static void RequireType(JsonDocument message, string expected)
    {
        using (message)
            if (message.RootElement.GetProperty("type").GetString() != expected)
                throw new InvalidOperationException($"Expected AST debugger event '{expected}', received {message.RootElement.GetRawText()}.");
    }

    private static void RequireStopped(JsonDocument message, string reason, string source, int line)
    {
        using (message)
        {
            var root = message.RootElement;
            if (root.GetProperty("type").GetString() != "stopped" || root.GetProperty("reason").GetString() != reason ||
                root.GetProperty("source").GetString() != source || root.GetProperty("line").GetInt32() != line)
                throw new InvalidOperationException($"Expected AST debugger stop {reason} at {source}:{line}, received {root.GetRawText()}.");
        }
    }

    private static void RequireValueHistory(JsonDocument message, string name, string expectedValue)
    {
        using (message)
        {
            var root = message.RootElement;
            if (root.GetProperty("type").GetString() != "valueHistory" ||
                !root.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("Name").GetString() == name && item.GetProperty("NewValue").GetString() == expectedValue))
                throw new InvalidOperationException($"AST debugger did not record {name}={expectedValue}: {root.GetRawText()}");
        }
    }
}
