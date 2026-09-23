using System.Diagnostics;

var repo=Path.GetFullPath(args.Length>0?args[0]:".");
var root=Path.Combine(Path.GetTempPath(),"xpscript-mcp-installer-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var home=Path.Combine(root,"home");
    var bin=Path.Combine(root,"bin");
    Directory.CreateDirectory(home); Directory.CreateDirectory(bin);
    var fake=Path.Combine(bin,OperatingSystem.IsWindows()?"codex.cmd":"codex");
    var state=Path.Combine(root,"state.txt");
    var log=Path.Combine(root,"calls.txt");
    if(OperatingSystem.IsWindows())
        await File.WriteAllTextAsync(fake,"@echo off\r\necho %*>>\""+log+"\"\r\nif \"%1 %2 %3\"==\"mcp get xpscript\" (if exist \""+state+"\" (type \""+state+"\" & exit /b 0) else exit /b 1)\r\nif \"%1 %2 %3\"==\"mcp add xpscript\" (echo command=C:/fake/xpscript.exe mcp>\""+state+"\" & exit /b 0)\r\nif \"%1 %2 %3\"==\"mcp remove xpscript\" (del /q \""+state+"\" 2>nul & exit /b 0)\r\nexit /b 1\r\n");
    else {
        await File.WriteAllTextAsync(fake,"#!/bin/sh\necho \"$@\" >> '"+log+"'\nif [ \"$1 $2 $3\" = \"mcp get xpscript\" ]; then [ -f '"+state+"' ] && cat '"+state+"' && exit 0; exit 1; fi\nif [ \"$1 $2 $3\" = \"mcp add xpscript\" ]; then echo 'command=C:/fake/xpscript.exe mcp' > '"+state+"'; exit 0; fi\nif [ \"$1 $2 $3\" = \"mcp remove xpscript\" ]; then rm -f '"+state+"'; exit 0; fi\nexit 1\n");
        File.SetUnixFileMode(fake,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    }
    async Task Run()
    {
        var psi=new ProcessStartInfo("dotnet"){WorkingDirectory=repo,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","mcp","install","codex","--scope","user"}) psi.ArgumentList.Add(a);
        psi.Environment["PATH"]=bin+Path.PathSeparator+(Environment.GetEnvironmentVariable("PATH")??"");
        psi.Environment["XPSCRIPT_MCP_INSTALL_HOME"]=home;
        psi.Environment["XPSCRIPT_MCP_INSTALL_COMMAND"]="C:/fake/xpscript.exe";
        using var p=Process.Start(psi)!; var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
        if(p.ExitCode!=0) throw new Exception("Installer failed: "+await stderr+" "+await stdout);
    }
    await Run();
    var skill=Path.Combine(home,".codex","skills","xpscript-development","SKILL.md");
    if(!File.Exists(skill)||!(await File.ReadAllTextAsync(skill)).Contains("xpscript_validate")) throw new Exception("Skill was not installed.");
    var first=(await File.ReadAllLinesAsync(log)).Length;
    await Run();
    var calls=await File.ReadAllLinesAsync(log);
    if(calls.Length!=first+1 || !calls[^1].StartsWith("mcp get xpscript")) throw new Exception("Second install was not idempotent.");
    await File.WriteAllTextAsync(state,"command=C:/wrong/xpscript.exe mcp");
    await Run();
    calls=await File.ReadAllLinesAsync(log);
    if(!calls.Any(x=>x.StartsWith("mcp remove xpscript")) || calls.Count(x=>x.StartsWith("mcp add xpscript"))<2) throw new Exception("Mismatched registration was not repaired.");
    Console.WriteLine("MCP installer probe passed.");
}
finally { try{Directory.Delete(root,true);}catch{} }
