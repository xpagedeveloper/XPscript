using System.Diagnostics;
using System.Text;

namespace XPScript.Compiler;

public static class CompilerMcpInstaller
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { WriteHelp(); return args.Length == 0 ? 1 : 0; }
        var client = args[0].ToLowerInvariant();
        var scope = "user";
        var force = false;
        for (var i=1;i<args.Length;i++)
        {
            if (args[i] == "--scope" && i+1 < args.Length) scope=args[++i].ToLowerInvariant();
            else if (args[i] == "--force") force=true;
            else { Console.Error.WriteLine($"Unknown argument: {args[i]}"); return 1; }
        }
        if (scope is not ("user" or "project")) { Console.Error.WriteLine("--scope must be user or project."); return 1; }
        return client switch
        {
            "codex" => await InstallCodexAsync(scope, force).ConfigureAwait(false),
            "claude" or "claude-code" => await InstallClaudeAsync(scope, force).ConfigureAwait(false),
            _ => Fail($"Unsupported MCP client '{client}'. Supported clients: codex, claude.")
        };
    }

    private static async Task<int> InstallCodexAsync(string scope, bool force)
    {
        if (!FindCommand("codex", out var codex)) return Fail("Codex CLI was not found on PATH.");
        var command=CurrentExecutable();
        var check=await RunAsync(codex!, "mcp", "get", "xpscript").ConfigureAwait(false);
        if (check.ExitCode != 0 || force)
        {
            if (check.ExitCode == 0) await RunAsync(codex!, "mcp", "remove", "xpscript").ConfigureAwait(false);
            var add=await RunAsync(codex!, "mcp", "add", "xpscript", "--", command, "mcp").ConfigureAwait(false);
            if (add.ExitCode != 0) return Fail("Could not register XPScript MCP in Codex: " + add.Error);
        }
        var skillRoot = scope == "project" ? Path.Combine(Environment.CurrentDirectory, ".codex", "skills", "xpscript-development") : Path.Combine(UserHome(), ".codex", "skills", "xpscript-development");
        InstallSkill(skillRoot);
        Console.WriteLine($"XPScript MCP and skill are installed for Codex ({scope}). MCP command: {command} mcp");
        return 0;
    }

    private static async Task<int> InstallClaudeAsync(string scope, bool force)
    {
        if (!FindCommand("claude", out var claude)) return Fail("Claude Code CLI was not found on PATH.");
        var command=CurrentExecutable();
        var get=await RunAsync(claude!, "mcp", "get", "xpscript").ConfigureAwait(false);
        if (get.ExitCode != 0 || force)
        {
            if (get.ExitCode == 0) await RunAsync(claude!, "mcp", "remove", "xpscript", "--scope", scope).ConfigureAwait(false);
            var add=await RunAsync(claude!, "mcp", "add", "xpscript", "--scope", scope, "--", command, "mcp").ConfigureAwait(false);
            if (add.ExitCode != 0) return Fail("Could not register XPScript MCP in Claude Code: " + add.Error);
        }
        var skillRoot = scope == "project" ? Path.Combine(Environment.CurrentDirectory, ".claude", "skills", "xpscript-development") : Path.Combine(UserHome(), ".claude", "skills", "xpscript-development");
        InstallSkill(skillRoot);
        Console.WriteLine($"XPScript MCP and skill are installed for Claude Code ({scope}). MCP command: {command} mcp");
        return 0;
    }

    private static void InstallSkill(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"SKILL.md"), CompilerMcpSkill.Content, new UTF8Encoding(false));
    }
    private static string CurrentExecutable() => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the XPScript compiler executable path.");
    private static string UserHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static bool FindCommand(string name, out string? path)
    {
        var names=OperatingSystem.IsWindows()?new[]{name+".exe",name+".cmd",name+".bat",name}:new[]{name};
        foreach(var dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries)) foreach(var n in names){var p=Path.Combine(dir,n);if(File.Exists(p)){path=p;return true;}}
        path=null;return false;
    }
    private static async Task<(int ExitCode,string Output,string Error)> RunAsync(string fileName, params string[] arguments)
    {
        var psi=new ProcessStartInfo(fileName){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        foreach(var arg in arguments) psi.ArgumentList.Add(arg);
        using var p=Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync().ConfigureAwait(false);
        return (p.ExitCode,await stdout.ConfigureAwait(false),await stderr.ConfigureAwait(false));
    }
    private static int Fail(string message){Console.Error.WriteLine(message);return 1;}
    private static void WriteHelp()=>Console.WriteLine("Usage: xpscript mcp install codex|claude [--scope user|project] [--force]");
}

internal static class CompilerMcpSkill
{
    public const string Content = "---\nname: xpscript-development\ndescription: Develop, validate and compile XPScript using the official compiler machine interface.\n---\n\n# XPScript development\n\nUse the XPScript compiler as the source of truth. Do not infer language validity from generated C# or from another language server.\n\n## Use MCP during the edit loop\n\nPrefer the XPScript MCP tools for fast, non-executing development feedback:\n\n- Use `xpscript_validate` after creating or changing XPScript. It uses the reusable warm compiler and returns structured compiler diagnostics.\n- Use `xpscript_symbols` to discover public XPScript APIs instead of guessing names.\n- Use `xpscript_describe` before using an unfamiliar XPScript symbol or signature.\n- Use `xpscript_explain` for stable XPS diagnostic codes when a diagnostic needs semantic explanation.\n- Correct compiler diagnostics and validate again until validation succeeds.\n\nMCP validation is intentionally not a replacement for final compilation. It does not publish an executable/package and must not be treated as proof that packaging, target publishing or deployment succeeds.\n\n## Use the CLI for authoritative full builds\n\nRun a full `xpscript compile` when the user asks to build/package/release, before claiming a change is fully buildable, when output artifacts are required, when target-specific publishing must be verified, or after an edit sequence before handing off a change whose acceptance criteria include compilation.\n\nUse the target/runtime options required by the project. Treat the full compile result and exit code as authoritative for artifact generation. Do not execute the resulting program unless execution was requested or is required by an explicitly requested test.\n\n## Recommended agent loop\n\n1. Inspect existing XPScript and project conventions.\n2. Use symbol/description tools when API knowledge is uncertain.\n3. Edit the XPScript source.\n4. Call `xpscript_validate` and repair structured diagnostics.\n5. Repeat until validation returns `result: ok`.\n6. If the task requires a complete build or build verification, run the normal XPScript CLI compile with the project's target/runtime settings.\n7. Report validation and full-build status separately.\n\nNever replace compiler validation with an LLM-only judgment.\n";
}
