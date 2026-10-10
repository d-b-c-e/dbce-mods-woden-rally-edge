using System.Reflection;
using System.Runtime.Loader;
using Woden.ControlsTesting;
if(args.Length!=1) throw new ArgumentException("Supply the Woden repository root; this checks metadata only.");
string root=Path.GetFullPath(args[0]);
string[] dirs=["components/wheel/src/WodenRallyEdge.Plugin/bin/Release/net6.0", "components/wheel/src/WodenRallyEdge.Core/bin/Release/net6.0", "components/wheel/lib/core", "components/wheel/lib/interop", "components/wheel/lib/toolkit/dotnet", "components/wheel/lib/recording", "vendor/playback"];
AssemblyLoadContext.Default.Resolving+=(_,name)=> {
    foreach(var d in dirs) { string p=Path.Combine(root,d,name.Name+".dll"); if(File.Exists(p)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(p); }
    return null;
};
var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,dirs[0],"WodenRallyEdgeWheel.dll"));
var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"components/wheel/lib/toolkit/dotnet/Dbce.Wheel.Ffb.dll"));
Console.WriteLine($"PASS: {HookContract.Verify(mod,native)} exact metadata seams. No static initialization, hooks installed, engine, device or native calls.");
