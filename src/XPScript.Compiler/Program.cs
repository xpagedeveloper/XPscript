using XPScript.Compiler;

Console.WriteLine("XPScript version 0.9 Beta");
Console.WriteLine("XPageDeveloper.com (c)");
Console.WriteLine();

return await XPScriptCompilerCommandLine.RunAsync(args);
