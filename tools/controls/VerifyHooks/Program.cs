using System.Reflection;
using System.Runtime.Loader;
using Woden.ControlsTesting;
if(args.Length is <1 or >2) throw new ArgumentException("Supply the Woden repository root and optionally an exact candidate plugin directory; this checks metadata only.");
string root=Path.GetFullPath(args[0]);
string? candidate=args.Length==2 ? Path.GetFullPath(args[1]) : null;
string[] dirs=[..(candidate is null ? Array.Empty<string>() : new[]{candidate}), "components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0", "components/wheel/src/WodenRallyEdge.Core/bin/Release/net6.0", "components/wheel/lib/core", "components/wheel/lib/interop", "components/wheel/lib/toolkit/dotnet", "components/wheel/lib/recording", "vendor/playback"];
AssemblyLoadContext.Default.Resolving+=(_,name)=> {
    foreach(var d in dirs) { string p=Path.Combine(Path.IsPathRooted(d) ? d : Path.Combine(root,d),name.Name+".dll"); if(File.Exists(p)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(p); }
    return null;
};
var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate is null ? Path.Combine(root,dirs[0],"WodenRallyEdgeWheel.dll") : Path.Combine(candidate,"WodenRallyEdgeWheel.dll"));
var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate is null ? Path.Combine(root,"components/wheel/lib/toolkit/dotnet/Dbce.Wheel.Ffb.dll") : Path.Combine(candidate,"Dbce.Wheel.Ffb.dll"));
Console.WriteLine($"PASS: {HookContract.Verify(mod,native)} exact metadata seams. No static initialization, hooks installed, engine, device or native calls.");
Console.WriteLine($"PASS: {HookContract.VerifyShim(mod)} exact ordinary shim admission metadata seams, including overloaded Panel.Toggle().");
