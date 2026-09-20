using System.Diagnostics;

if(args.Length!=1){Console.Error.WriteLine("Usage: CompilerRunWarmBenchmark <repo-root>");return 2;}
var root=Path.GetFullPath(args[0]);
var source=Path.Combine(root,"samples","application-is-debugging.xps");
async Task<long> RunAsync(bool debug, bool noDaemon = false)
{
    var psi=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var a in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","run",source,"--security=off"}) psi.ArgumentList.Add(a);
    if(debug) psi.ArgumentList.Add("--debug");
    if(noDaemon) psi.ArgumentList.Add("--no-daemon");
    var sw=Stopwatch.StartNew(); using var p=Process.Start(psi)!; var o=p.StandardOutput.ReadToEndAsync(); var e=p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); sw.Stop();
    if(p.ExitCode!=0) throw new Exception("run failed: "+await e+" "+await o);
    return sw.ElapsedMilliseconds;
}
long Median(IEnumerable<long> x){var a=x.Order().ToArray();return a[a.Length/2];}
var localCold=await RunAsync(false, true);
var localDebug=await RunAsync(true, true);
var cold=await RunAsync(false);
var warmSamples=new List<long>(); for(var i=0;i<3;i++) warmSamples.Add(await RunAsync(false));
var debugSamples=new List<long>(); for(var i=0;i<3;i++) debugSamples.Add(await RunAsync(true));
Console.WriteLine($"localColdMs={localCold} localDebugFreshMs={localDebug} daemonColdMs={cold} daemonWarmMs={string.Join(",",warmSamples)} daemonWarmMedianMs={Median(warmSamples)} daemonDebugFreshMs={string.Join(",",debugSamples)} daemonDebugFreshMedianMs={Median(debugSamples)}");
return 0;
