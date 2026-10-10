using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Dbce.Wheel.Input;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Woden.ControlsTesting;

[BepInPlugin(Id, "Woden developer raw controls probe", "0.1.0")]
[BepInDependency("dbce.wodenrallyedgewheel", BepInDependency.DependencyFlags.HardDependency)]
public sealed class ControlsProbe : BasePlugin
{
    const string Id = "dbce.woden.controls-test";
    static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dbce", "super-woden-rally-edge");
    static string RequestPath => Path.Combine(Root, "controls-request.json");
    static string MarkerPath => Path.Combine(Root, "inject.on");
    static ControlsProbe? Instance;
    static Assembly Mod = null!;
    static Type Runtime = null!, Hub = null!, Wheel = null!;
    static NativeTestInjectionClient? Native;
    static Harmony? Patches;
    static ColdRequest? Request;
    static StreamWriter? Trace;
    static bool Requested, Armed, Closed;
    static int Sequence, RawCommands, Rows;
    static double Deadline, ObserveUntil, NextStatus, NextIdentity;
    static string NativePath = "", NativeHash = "";
    static string? OwnedEvidence;
    static string? PendingStop;
    static IntPtr NativeModule;
    static DateTimeOffset ProcessStart;
    [DllImport("kernel32", CharSet=CharSet.Ansi, ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    static object? Field(Type t, string name, object? instance = null) => t.GetField(name, All)?.GetValue(instance) ?? throw new MissingFieldException(t.FullName, name);
    static MethodInfo Method(Type t, string name) => t.GetMethod(name, All) ?? throw new MissingMethodException(t.FullName, name);
    static Type Type(string name) => Mod.GetType("WodenRallyEdge." + name, true)!;
    static string Hash(string path) { using var f=File.OpenRead(path); using var sha=SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(f)); }
    static void Match(string path, string hash) { if (!string.Equals(Hash(path),hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Artifact differs: " + Path.GetFileName(path)); }
    public override void Load()
    {
        Instance=this;
        if (!File.Exists(RequestPath) && !File.Exists(MarkerPath)) return;
        Requested=true;
        try
        {
            Patches=new Harmony(Id);
            var force=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Dbce.Wheel.Ffb").GetType("Dbce.Wheel.Ffb.WheelFfbNative",true)!;
            var opens=force.GetMethods().Where(m=>m.Name=="Initialise").ToArray();
            if(opens.Length==0) throw new MissingMethodException("Native force-open guard seam missing.");
            foreach(var m in opens) Patch(m,nameof(Refuse));
            // Guard force opening independently of game-specific reflection.
            // A stale mod seam must never prevent this managed refusal patch.
            Mod=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="WodenRallyEdgeWheel");
            Runtime=Type("Runtime"); Hub=Type("DeviceHub"); Wheel=Type("WheelInput");
            HookContract.Verify(Mod,force.Assembly);
            foreach(var name in new[]{"BeginAxis","BeginButton","FinishAxis","TryCommit","RetrySave"}) Patch(Method(Wheel,name),nameof(Refuse));
            // Safety stays latched even when subsequent request validation fails.
            Runtime.GetField("DiagnosticNoForce",All)!.SetValue(null,true);
            Plain(RequestPath); Plain(MarkerPath);
            if(!File.Exists(RequestPath) || !File.Exists(MarkerPath) || new FileInfo(MarkerPath).Length>64) throw new InvalidDataException("Both cold switches required.");
            ProcessStart=Process.GetCurrentProcess().StartTime.ToUniversalTime();
            if(File.GetLastWriteTimeUtc(MarkerPath)>=ProcessStart.UtcDateTime || File.GetLastWriteTimeUtc(RequestPath)>=ProcessStart.UtcDateTime)
                throw new InvalidDataException("Switches must precede the process.");
            Request=Protocol.Admit(ReadBounded(RequestPath,8192),DateTimeOffset.UtcNow,File.ReadAllText(MarkerPath).Trim());
            File.Move(RequestPath,RequestPath+"."+Environment.ProcessId+".taken");
            Match(Mod.Location,Request.PluginSha256);
            var core=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="WodenRallyEdge.Core");
            Match(core.Location,Request.CoreSha256);
            Match(Path.Combine(Paths.ConfigPath,"dbce.wodenrallyedgewheel.cfg"),Request.ConfigSha256);
            Match(Path.Combine(Paths.ConfigPath,"wheel-bindings.json"),Request.BindingsSha256);
            var settings=Field(Runtime,"Settings")!;
            foreach(var flag in new[]{"FfbEnabled","TelemetryEnabled","Record"})
                if((bool)Field(settings.GetType(),flag,settings)!) throw new InvalidDataException("Independent output mute missing: "+flag);
            var dev=Field(Type("DevInput"),"_enabled")!;
            if((bool)dev.GetType().GetProperty("Value")!.GetValue(dev)!) throw new InvalidDataException("Normal DevInput must stay disabled.");
            if(Runtime.GetField("_recordLaunch",All)!.GetValue(null) is not null) throw new InvalidDataException("Diagnostic launch already requested.");
            var stage=Type("StagePlayback");
            var session=Field(stage,"Session")!;
            if((bool)session.GetType().GetProperty("Active")!.GetValue(session)!) throw new InvalidDataException("Stage session already active.");
            NativePath=(string)force.GetProperty("LoadedFrom")!.GetValue(null)!;
            NativeHash=Request.NativeSha256; NativeModule=VerifyModule();
            Native=NativeTestInjectionClient.FromResidentExports(n=>GetProcAddress(NativeModule,n));
            if(!Native.Arm()) throw new InvalidOperationException(Native.Status);
            Mute();
            string output=Path.GetFullPath(Request.OutputDirectory);
            if(output.StartsWith(Path.TrimEndingDirectorySeparator(Paths.GameRootPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Evidence must be outside the game folder.");
            if(Directory.Exists(output) || File.Exists(output)) throw new InvalidDataException("Evidence directory must be new.");
            Plain(output); Directory.CreateDirectory(output); OwnedEvidence=output;
            Trace=new(new FileStream(Path.Combine(output,"observations.tsv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
            Trace.WriteLine("time_s\tframe\tkind\tdata");
            File.WriteAllText(Path.Combine(output,"identity.json"),JsonSerializer.Serialize(new{request=Request,process=Environment.ProcessId,processStart=ProcessStart,
                probeSha256=Hash(typeof(ControlsProbe).Assembly.Location),nativePath=NativePath,physicalOutput=false,kind="native-raw-before-binding"},Protocol.Json));
            Patch(Method(Runtime,"Update"),nameof(BeforeTick),nameof(AfterTick));
            Patch(Method(Runtime,"Stop"),nameof(OnStop));
            Patch(Method(Hub,"Poll"),postfix:nameof(AfterPoll));
            Patch(Method(Hub,"TryAxis"),postfix:nameof(AfterAxis));
            Patch(Method(Wheel,"Button"),postfix:nameof(AfterButton));
            Patch(Method(Wheel,"Apply"),postfix:nameof(AfterApply));
            double remaining=(Request.ExpiresUtc-DateTimeOffset.UtcNow).TotalSeconds;
            if(remaining<=0) throw new InvalidDataException("Cold request expired during admission.");
            Deadline=Clock.Elapsed.TotalSeconds+Math.Min(300,remaining); Armed=true;
            Row("armed",Native.Status); Trace.Flush();
            Log.LogInfo("Raw controls test armed; native force fence and progression/network mute latched. Focus guards unchanged.");
        }
        catch(Exception ex) { Log.LogError("Raw controls refused: "+ex); Close("admission failed: "+ex.Message); }
        finally { if(Requested && Runtime is not null) try { Mute(); } catch(Exception ex) { Log.LogError("Managed mute failed; raw input stopped: "+ex); Close("managed mute failed"); } }
    }
    static void Mute()
    {
        var stage=Type("StagePlayback"); var target=Field(stage,"Target")!;
        Method(target.GetType(),"MuteOutputs").Invoke(target,null);
    }
    static bool Refuse() => !Requested;
    static void Patch(MethodBase target,string? prefix=null,string? postfix=null) => Patches!.Patch(target,
        prefix is null?null:new HarmonyMethod(typeof(ControlsProbe),prefix){priority=Priority.First},
        postfix is null?null:new HarmonyMethod(typeof(ControlsProbe),postfix){priority=Priority.Last});
    static void Plain(string path)
    {
        for(string? p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))
            if((Directory.Exists(p)||File.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Linked evidence path refused.");
    }
    static byte[] ReadBounded(string path,int limit)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length>limit) throw new InvalidDataException("Oversize request.");
        var bytes=new byte[file.Length]; int read=0;
        while(read<bytes.Length) { int n=file.Read(bytes,read,bytes.Length-read); if(n==0) throw new EndOfStreamException(); read+=n; }
        if(file.ReadByte()!=-1) throw new InvalidDataException("Request changed while reading.");
        return bytes;
    }
    static IntPtr VerifyModule()
    {
        using var process=Process.GetCurrentProcess();
        var modules=process.Modules.Cast<ProcessModule>().Where(m=>string.Equals(m.ModuleName,"WheelFfb.dll",StringComparison.OrdinalIgnoreCase)||string.Equals(m.ModuleName,"UnityForceFeedback.dll",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(modules.Length!=1 || modules[0].FileName is not {} modulePath || !string.Equals(Path.GetFullPath(modulePath),Path.GetFullPath(NativePath),StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected one resident reader module.");
        Match(NativePath,NativeHash); return modules[0].BaseAddress;
    }
    static void BeforeTick()
    {
        if(PendingStop is {} pending) { PendingStop=null; Close(pending); }
        if(!Armed) return;
        try
        {
            double now=Clock.Elapsed.TotalSeconds;
            if(now>=Deadline) { Close("duration ended"); return; }
            if(now>=NextIdentity) { NextIdentity=now+1; if(VerifyModule()!=NativeModule || !Native!.RefreshStatus()) throw new InvalidDataException("Native fence identity lost."); }
            string command=Path.Combine(Request!.OutputDirectory,"command.json");
            if(!File.Exists(command)) return;
            Plain(command);
            // Claim the exact file before parsing. A subsequent writer cannot
            // replace bytes between our validation and archival/submission.
            string claimed=Path.Combine(Request.OutputDirectory,"command-claimed.json");
            File.Move(command,claimed);
            var r=Protocol.Command(ReadBounded(claimed,2048),Request.Nonce,Sequence,File.GetLastWriteTimeUtc(claimed),ProcessStart);
            Sequence=r.Sequence; File.Move(claimed,Path.Combine(Request.OutputDirectory,"command-"+Sequence+".json"));
            bool ok=true; string reply=Native!.Status;
            if(r.Operation=="raw")
            {
                if(++RawCommands>256) throw new InvalidDataException("Raw command limit.");
                ok=Native.SubmitRaw(r.Raw!,out reply); ObserveUntil=now+16;
                Row(ok?"request-accepted":"request-refused",r.Raw!+"; "+reply);
                if(!Native.Armed) throw new InvalidDataException("Native fence confirmation lost.");
            }
            string nextReply=Path.Combine(Request.OutputDirectory,"reply.next.json");
            File.WriteAllText(nextReply,JsonSerializer.Serialize(new{r.Sequence,r.Nonce,ok,reply,physicalOutput=false},Protocol.Json));
            File.Move(nextReply,Path.Combine(Request.OutputDirectory,"reply.json"),true);
            if(r.Operation=="stop") Close("stopped by command");
        }
        catch(Exception ex) { Close("command failed: "+ex.Message); }
    }
    static void AfterTick()
    {
        if(!Armed || Clock.Elapsed.TotalSeconds<NextStatus) return;
        try
        {
            NextStatus=Clock.Elapsed.TotalSeconds+.1;
            Row("state","focused="+Runtime.GetProperty("Focused",All)!.GetValue(null)+" menu="+Type("MenuNavigation").GetProperty("Status",All)!.GetValue(null));
            if(Clock.Elapsed.TotalSeconds<=ObserveUntil)
            {
                var selected=EventSystem.current?.currentSelectedGameObject;
                Row("menu-selection",selected==null?"none":ObjectPath(selected.transform));
            }
            Row("native-status",Native!.Status); Trace!.Flush();
        }
        catch(Exception ex) { Close("state observation failed: "+ex.Message); }
    }
    static string ObjectPath(Transform t) { var names=new List<string>(); for(int i=0;t!=null && i<32;t=t.parent,i++) names.Add(Uri.EscapeDataString(t.name)); names.Reverse(); return string.Join("/",names); }
    static bool Observing => Armed && Clock.Elapsed.TotalSeconds<=ObserveUntil;
    static void AfterPoll(object __instance)
    {
        if(!Observing) return;
        try
        {
            foreach(var d in (IEnumerable)Field(Hub,"Devices",__instance)!)
            {
                var t=d.GetType(); int slot=(int)Field(t,"Slot",d)!; var info=Field(t,"Info",d)!;
                var guid=info.GetType().GetField("InstanceGuid")!.GetValue(info);
                bool injected=Native!.LastReadInjected(slot); if(!Native.Armed) throw new InvalidDataException(Native.Status);
                Row("raw-read","slot="+slot+" guid="+guid+" valid="+Field(t,"Ok",d)+" injected="+injected+
                    " axes="+string.Join(",",(int[])Field(t,"Axes",d)!)+" hats="+string.Join(",",(int[])Field(t,"Pov",d)!)+
                    " buttons="+string.Join(",",((byte[])Field(t,"Physical",d)!).Select((v,i)=>(v,i)).Where(x=>x.v!=0).Select(x=>x.i)));
            }
        }
        catch(Exception ex) { RequestStop("raw observation failed: "+ex.Message); }
    }
    static void AfterAxis(object? __0,float __1,bool __result)
    {
        if(!Observing) return;
        try { Row("normalized-axis","binding="+__0+" valid="+__result+" value="+__1.ToString("R",CultureInfo.InvariantCulture)); }
        catch(Exception ex) { RequestStop("axis observation failed: "+ex.Message); }
    }
    static void AfterButton(string __0,bool __1,bool __result)
    { if(Observing) Row("game-button",__0+" edge="+__1+" value="+(__result?1:0)); }
    static void AfterApply(object __instance)
    {
        if(!Observing) return;
        try
        {
            var applied=Wheel.GetField("_last",All)!.GetValue(__instance);
            if(applied is not null) Row("car-input",JsonSerializer.Serialize(applied,applied.GetType()));
        }
        catch(Exception ex) { RequestStop("car observation failed: "+ex.Message); }
    }
    static void Row(string kind,string data)
    {
        if(Trace is null) return;
        if(++Rows>200000) { RequestStop("trace row limit"); return; }
        if(data.Length>4096) { RequestStop("trace value limit"); return; }
        try { Trace.WriteLine(Clock.Elapsed.TotalSeconds.ToString("R",CultureInfo.InvariantCulture)+"\t"+Time.frameCount+"\t"+kind+"\t"+data.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ')); }
        catch(Exception ex) { RequestStop("trace write failed: "+ex.Message); }
    }
    // Observation runs inside device/car enumerations. Do not close their readers
    // or unpatch a method from inside that callback; finish at the next runtime tick.
    static void RequestStop(string reason) { Armed=false; PendingStop ??= reason; }
    static void OnStop() => Close("game stopping");
    static void Close(string reason)
    {
        if(Closed) return; Closed=true; Armed=false;
        try { var hub=Runtime?.GetField("Devices",All)?.GetValue(null); if(hub is not null) Method(Hub,"CloseReaders").Invoke(hub,null); }
        catch(Exception ex) { reason+="; reader close failed: "+ex.Message; }
        try { Trace?.Dispose(); } catch(Exception ex) { reason+="; trace close failed: "+ex.Message; } finally { Trace=null; }
        try { if(OwnedEvidence is not null) File.WriteAllText(Path.Combine(OwnedEvidence,"result.json"),JsonSerializer.Serialize(new{reason,rows=Rows,commands=RawCommands,inputVerdict="unclassified",physicalOutput=false},Protocol.Json)); }
        catch(Exception ex) { Instance?.Log.LogError("Controls result write failed: "+ex.Message); }
        Instance?.Log.LogInfo("Raw controls ended: "+reason+"; process force/progression mute retained");
    }
    // Unloading does not remove safety patches or reset the native process latch.
    public override bool Unload() { Close("addon unload"); return false; }
}
