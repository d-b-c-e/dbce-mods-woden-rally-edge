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
public sealed partial class ControlsProbe : BasePlugin
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
    static readonly Dictionary<string,string> Observed = new(StringComparer.Ordinal);
    static bool Requested, Armed, Closed;
    static readonly SessionGate Gate=new();
    static bool NativeEnabled;
    static double NextFocusReceipt;
    static int Sequence, RawCommands, Rows;
    static double Deadline, ObserveUntil, NextStatus, NextIdentity;
    static string NativePath = "", NativeHash = "";
    static string? OwnedEvidence;
    static string? PendingStop;
    static IntPtr NativeModule;
    static DateTimeOffset ProcessStart;
    [DllImport("kernel32", CharSet=CharSet.Ansi, ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool IsIconic(IntPtr window);
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
            if(Protocol.ShimAdmission(Request)) VerifyShimAdmission();
            string configSnapshot=Path.Combine(Path.GetDirectoryName(Request.OutputDirectory)!,"applied.cfg");
            Plain(configSnapshot);
            var addedDefaults=ConfigContract.Verify(ReadBounded(configSnapshot,1024*1024),
                ReadBounded(Path.Combine(Paths.ConfigPath,"dbce.wodenrallyedgewheel.cfg"),1024*1024),Request.ConfigSha256);
            Match(Path.Combine(Paths.ConfigPath,"wheel-bindings.json"),Request.BindingsSha256);
            var settings=Field(Runtime,"Settings")!;
            foreach(var flag in new[]{"FfbEnabled","TelemetryEnabled","Record"})
                if((bool)Field(settings.GetType(),flag,settings)!) throw new InvalidDataException("Independent output mute missing: "+flag);
            var dev=Field(Type("DevInput"),"_enabled")!;
            if((bool)dev.GetType().GetProperty("Value")!.GetValue(dev)!) throw new InvalidDataException("Normal DevInput must stay disabled.");
            var recordLaunch=Runtime.GetField("_recordLaunch",All)!.GetValue(null);
            if(Protocol.NativePositive(Request)) { /* Exact positive launch is verified after its trace is created, below. */ }
            else if(Protocol.RecordingTrial(Request))VerifyRecordingRequest(recordLaunch);
            else if(recordLaunch is not null) throw new InvalidDataException("Diagnostic launch already requested.");
            var stage=Type("StagePlayback");
            var session=Field(stage,"Session")!;
            if((bool)session.GetType().GetProperty("Active")!.GetValue(session)!) throw new InvalidDataException("Stage session already active.");
            NativePath=(string)force.GetProperty("LoadedFrom")!.GetValue(null)!;
            NativeHash=Request.NativeSha256; NativeModule=VerifyModule();
            Native=NativeTestInjectionClient.FromResidentExports(n=>GetProcAddress(NativeModule,n));
            Mute();
            string output=Path.GetFullPath(Request.OutputDirectory);
            if(output.StartsWith(Path.TrimEndingDirectorySeparator(Paths.GameRootPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Evidence must be outside the game folder.");
            if(Directory.Exists(output) || File.Exists(output)) throw new InvalidDataException("Evidence directory must be new.");
            Plain(output); Directory.CreateDirectory(output); OwnedEvidence=output;
            Observed.Clear();
            Trace=new(new FileStream(Path.Combine(output,"observations.tsv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
            Trace.WriteLine("time_s\tframe\tkind\tdata");
            File.WriteAllText(Path.Combine(output,"identity.json"),JsonSerializer.Serialize(new{request=Request,process=Environment.ProcessId,processStart=ProcessStart,
                probeSha256=Hash(typeof(ControlsProbe).Assembly.Location),nativePath=NativePath,physicalOutput=false,
                kind=Protocol.HudStopOnly(Request)?"ordinary-recorder-menu-stop-only":Protocol.CarTrial(Request)?"shared-core-latched-original-car-trial":Protocol.MenuTrial(Request)?"shared-core-latched-menu-trial":Protocol.ShimAdmission(Request)?"ordinary-shim-menu-admission":"native-raw-before-binding",configAddedDefaults=addedDefaults},Protocol.Json));
            Patch(Method(Runtime,"Update"),nameof(BeforeTick),nameof(AfterTick));
            if(Protocol.ForceCapture(Request))Patch(Method(Runtime,"ObserveForceAnalysis"),postfix:nameof(AfterForceAnalysis));
            Patch(Method(Runtime,"Stop"),nameof(OnStop));
            Patch(Method(Hub,"Poll"),postfix:nameof(AfterPoll));
            Patch(Method(Hub,"TryAxis"),postfix:nameof(AfterAxis));
            Patch(Method(Wheel,"Button"),postfix:nameof(AfterButton));
            Patch(Method(Wheel,"Apply"),postfix:nameof(AfterApply));
            Patch(Method(Type("GameSampler"),"Read"),postfix:nameof(AfterSample));
            double remaining=(Request.ExpiresUtc-DateTimeOffset.UtcNow).TotalSeconds;
            if(remaining<=0) throw new InvalidDataException("Cold request expired during admission.");
            Deadline=Clock.Elapsed.TotalSeconds+Math.Min(300,remaining);
            // Install the passive transport admission gate before its first row.
            // It cannot admit a reader interval until the explicit session arm.
            if(Protocol.NativePositive(Request))StartPositivePedals(recordLaunch);
            Row("admitted-unarmed","explicit nonce/sequence arm and exact native/game focus required"); Trace.Flush();
            Log.LogInfo("Raw controls admitted UNARMED; managed force/progression mute latched; zero stimulus before focus acknowledgment.");
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
        if(Closed || Request is null || OwnedEvidence is null) return;
        try
        {
            double now=Clock.Elapsed.TotalSeconds;
            CheckRevocation();
            if(Closed)return;
            if(now>=Deadline) { Close("duration ended"); return; }
            if(now>=NextFocusReceipt){NextFocusReceipt=now+.1;PublishGameFocus();}
            string command=Path.Combine(Request!.OutputDirectory,"command.json");
            if(File.Exists(command))ProcessCommand(command,now);
            // Stop/revoke always precedes another recipe tick or raw pulse.
            if(!Armed || Closed)return;
            RequireSessionFocus();
            if(now>=NextIdentity) { NextIdentity=now+1; if(VerifyModule()!=NativeModule || !Native!.RefreshStatus()) throw new InvalidDataException("Native fence identity lost."); }
            TickPositivePedals(now);
        }
        catch(Exception ex) { Close("command failed: "+ex.Message); }
    }
    static void ProcessCommand(string command,double now)
    {
            Plain(command);
            // Claim the exact file before parsing. A subsequent writer cannot
            // replace bytes between our validation and archival/submission.
            string claimed=Path.Combine(Request!.OutputDirectory,"command-claimed.json");
            File.Move(command,claimed);
            var r=Protocol.Command(ReadBounded(claimed,2048),Request.Nonce,Sequence,File.GetLastWriteTimeUtc(claimed),ProcessStart,Protocol.ShimAdmission(Request),Protocol.LatchedTrial(Request)||Protocol.NativePositive(Request),Protocol.F6Workflow(Request));
            Sequence=r.Sequence; File.Move(claimed,Path.Combine(Request.OutputDirectory,"command-"+Sequence+".json"));
            bool ok=true; string reply=Native!.Status;
            if(r.Operation=="stop")
            {
                Close("stopped by command");
                Publish("reply.json",new{r.Sequence,r.Nonce,ok=true,reply="revoked",revoked=true,physicalOutput=false});
                return;
            }
            if(r.Operation=="arm")
            {
                CheckRevocation();
                if(Closed)throw new InvalidDataException("Arm revoked.");
                bool focused=NativeFocused(r.Client!);
                Gate.Arm(r.Client!,r.Sequence,focused,GameFocused());
                // Never enable the native test lane on mere cold admission.
                if(VerifyModule()!=NativeModule || !Native.Arm())throw new InvalidDataException("Native arm refused: "+Native.Status);
                NativeEnabled=true;Armed=true;RequireSessionFocus();
                if(Protocol.MenuTrial(Request))StartMenuTrial();
                if(Protocol.CarTrial(Request))StartCarTrial();
                if(Protocol.HudStopOnly(Request))StartHudStopOnly();
                Row("armed","nonce/sequence and exact focus acknowledged");
                reply="armed";
            }
            else if(r.Operation!="status")RequireSessionFocus();
            if(r.Operation=="raw")
            {
                if(++RawCommands>256) throw new InvalidDataException("Raw command limit.");
                Observed.Clear();
                ok=Native.SubmitRaw(r.Raw!,out reply); ObserveUntil=now+16;
                Row(ok?"request-accepted":"request-refused",r.Raw!+"; "+reply);
                if(!Native.Armed) throw new InvalidDataException("Native fence confirmation lost.");
            }
            if(r.Operation is "panel" or "shot") ShimVisual(r.Operation);
            if(r.Operation=="workflow"){var result=F6WorkflowCommand(r.Arguments!);ok=result.Accepted;reply=result.Reply;}
            Publish("reply.json",new{r.Sequence,r.Nonce,ok,reply,armed=Gate.Armed,armSequence=Gate.ArmSequence,physicalOutput=false});
    }
    static bool GameFocused() => (bool)Runtime.GetProperty("Focused",All)!.GetValue(null)!;
    static bool NativeFocused(SessionClient client)
    {
        if(client.ProcessId!=Environment.ProcessId || client.StartedUtcTicks!=ProcessStart.UtcDateTime.Ticks ||
            !string.Equals(Path.GetFullPath(client.ExecutablePath),Path.GetFullPath(Environment.ProcessPath!),StringComparison.OrdinalIgnoreCase))return false;
        var window=new IntPtr(client.Window);var name=new StringBuilder(256);
        return GetWindowThreadProcessId(window,out uint owner)!=0 && owner==Environment.ProcessId &&
            GetClassName(window,name,name.Capacity)>0 && name.ToString()==client.Class && client.Class=="UnityWndClass" &&
            IsWindowVisible(window) && !IsIconic(window) && GetForegroundWindow()==window;
    }
    static void Publish(string file,object value)
    {
        string target=Path.Combine(OwnedEvidence!,file),temp=target+".next";
        Plain(target);Plain(temp);
        File.WriteAllText(temp,JsonSerializer.Serialize(value,Protocol.Json));File.Move(temp,target,true);
    }
    static void PublishGameFocus() => Publish("game-focus.json",new{processId=Environment.ProcessId,
        startedUtcTicks=ProcessStart.UtcDateTime.Ticks,focused=GameFocused(),checkedUtc=DateTimeOffset.UtcNow,armed=Gate.Armed,revoked=Gate.Revoked});
    static void CheckRevocation()
    {
        string path=Path.Combine(Path.GetDirectoryName(Request!.OutputDirectory)!,"revoke.json");
        if(!File.Exists(path))return;
        Plain(path);var revoke=Protocol.Decode<RevokeRequest>(ReadBounded(path,2048),2048);
        if(revoke.Schema!=1 || revoke.Nonce!=Request.Nonce)throw new InvalidDataException("Invalid revocation identity; fail closed.");
        Close("terminal nonce revocation");
    }
    static void RequireSessionFocus()
    {
        CheckRevocation();
        if(Closed || !Armed || Clock.Elapsed.TotalSeconds>=Deadline || Gate.Client is not {} client || !Gate.AdmitRow(NativeFocused(client),GameFocused()))
            throw new InvalidDataException("Session unarmed/revoked or native/game focus lost; zero further stimulus.");
    }
    static void AfterTick()
    {
        if(!Armed) return;
        try
        {
            RequireSessionFocus();
            TickMenuTrial();
            TickCarTrial();
            TickHudStopOnly();
            if(!Armed || Clock.Elapsed.TotalSeconds<NextStatus)return;
            NextStatus=Clock.Elapsed.TotalSeconds+.1;
            if(Protocol.ShimAdmission(Request)) ObserveShim();
            Row("state","focused="+Runtime.GetProperty("Focused",All)!.GetValue(null)+" menu="+Type("MenuNavigation").GetProperty("Status",All)!.GetValue(null));
            if(Clock.Elapsed.TotalSeconds<=ObserveUntil)
            {
                var selected=EventSystem.current?.currentSelectedGameObject;
                Changed("menu-selection","selected",selected==null?"none":ObjectPath(selected.transform));
            }
            Row("native-status",Native!.Status); Trace!.Flush();
        }
        catch(Exception ex) { var cause=ex is TargetInvocationException {InnerException: {} inner} ? inner : ex;
            Close("state observation failed: "+cause.GetType().Name+": "+cause.Message); }
    }
    static string ObjectPath(Transform t) { var names=new List<string>(); for(int i=0;t!=null && i<32;t=t.parent,i++) names.Add(Uri.EscapeDataString(t.name)); names.Reverse(); return string.Join("/",names); }
    static bool Observing {
        get { if(!Armed || Clock.Elapsed.TotalSeconds>ObserveUntil)return false;
            try{RequireSessionFocus();return true;}catch(Exception ex){RequestStop(ex.Message);return false;} }
    }
    static void AfterPoll(object __instance)
    {
        if(!Observing) return;
        try
        {
            foreach(var d in (IEnumerable)Field(Hub,"Devices",__instance)!)
            {
                var t=d.GetType(); int slot=(int)Field(t,"Slot",d)!; var info=Field(t,"Info",d)!;
                var guid=info.GetType().GetField("InstanceGuid")!.GetValue(info);
                bool injected=Native!.LastReadInjected(slot); ObservePositiveRead(guid,injected); if(!Native.Armed) throw new InvalidDataException(Native.Status);
                Changed("raw-read",slot.ToString(CultureInfo.InvariantCulture),"slot="+slot+" guid="+guid+" valid="+Field(t,"Ok",d)+" injected="+injected+
                    " axes="+string.Join(",",(int[])Field(t,"Axes",d)!)+" hats="+string.Join(",",(int[])Field(t,"Pov",d)!)+
                    " buttons="+string.Join(",",((byte[])Field(t,"Physical",d)!).Select((v,i)=>(v,i)).Where(x=>x.v!=0).Select(x=>x.i)));
            }
        }
        catch(Exception ex) { RequestStop("raw observation failed: "+ex.Message); }
    }
    static void AfterAxis(object? __0,float __1,bool __result)
    {
        if(!Observing) return;
        try { Changed("normalized-axis",__0?.ToString()??"none","binding="+__0+" valid="+__result+" value="+__1.ToString("R",CultureInfo.InvariantCulture)); }
        catch(Exception ex) { RequestStop("axis observation failed: "+ex.Message); }
    }
    static void AfterButton(string __0,bool __1,bool __result)
    { if(Observing) {
        if(Protocol.CarTrial(Request) && __result && (CarPulse?.Action=="shiftUp" && __0=="Gear up" ||
            CarPulse?.Action=="shiftDown" && __0=="Gear down" || CarPulse?.Action=="gear1" && __0=="Gear 1" ||
            CarPulse?.Action=="gear2" && __0=="Gear 2"))CarGearQuery=true;
        Changed("game-button",__0+"/"+__1,__0+" edge="+__1+" value="+(__result?1:0)); } }
    static void Changed(string kind,string key,string data)
    {
        string id=kind+"/"+key;
        if(Observed.TryGetValue(id,out var previous) && previous==data) return;
        if(Observed.Count>=512 && !Observed.ContainsKey(id)) { RequestStop("observation key limit"); return; }
        Observed[id]=data; Row(kind,data);
    }
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
    // Observe the independently sampled native Controls/MainCar fields after
    // MainCar.FixedUpdate, not WheelInput._last or the desired action values.
    // Missing/non-finite channels are explicit unknowns, never zero-filled.
    static readonly string[] NativeChannels = {
        "wheelRaw.throttle","wheelRaw.brake","wheelRaw.handbrake","wheelInput.throttle","wheelInput.brake","wheelInput.handbrake","wheelInput.appliedTicks",
        "game.actualPower","game.enginePower","game.fuel","game.gears","game.speedMomentum","game.gearMomentum","game.grounded",
        "motion.orientation.x","motion.orientation.y","motion.orientation.z","motion.orientation.w",
        "motion.velocity.local.x","motion.velocity.local.y","motion.velocity.local.z",
        "controls.steer","controls.throttle","controls.brake","game.handbrake",
        "game.steer","game.throttle","game.brake","game.trueSpeed","game.gear",
        "game.status","game.paused","game.locked","game.respawning","game.replay","game.automatic","game.shifting",
        "camera.mode","camera.stockPreset","camera.mountedView","camera.changing",
        "motion.position.world.x","motion.position.world.y","motion.position.world.z",
        "wheel.fl.steerAngle","wheel.fr.steerAngle","wheel.fl.motorTorque","wheel.fr.motorTorque",
        "wheel.rl.motorTorque","wheel.rr.motorTorque","wheel.fl.brakeTorque","wheel.fr.brakeTorque",
        "wheel.rl.brakeTorque","wheel.rr.brakeTorque"
    };
    static void AfterSample(object __0,object __result)
    {
        if(!Observing) return;
        try
        {
            var t=__result.GetType();
            object Get(string name)=>t.GetProperty(name,All)?.GetValue(__result) ?? throw new MissingMemberException("Sample."+name);
            var source=Get("Channels") as IDictionary<string,double> ?? throw new InvalidDataException("Sample channels changed type.");
            var channels=new Dictionary<string,double>(StringComparer.Ordinal); var missing=new List<string>();
            foreach(string name in NativeChannels)
                if(source.TryGetValue(name,out double v) && double.IsFinite(v)) channels.Add(name,v); else missing.Add(name);
            ReadPositiveNativeFields(__0,channels,missing);
            ReadDriveFields(source,Get("SimulationSeconds"),channels,missing);
            ObservePositiveSample(Get("CarInstanceId"),Convert.ToInt64(Get("Sequence")),Get("State").ToString()!,channels);
            ObserveCarPulse(Get("CarInstanceId"),Get("State").ToString()!,channels);
            Row("native-car",JsonSerializer.Serialize(new {
                Car=Get("CarInstanceId"),Sequence=Get("Sequence"),At=Get("ElapsedSeconds"),
                Simulation=Get("SimulationSeconds"),State=Get("State"),Phase=Get("Phase"),Channels=channels,Missing=missing
            }));
        }
        catch(Exception ex) { RequestStop("native car observation failed: "+ex.Message); }
    }
    static void Row(string kind,string data)
    {
        if(Trace is null) return;
        if(Armed){try{RequireSessionFocus();}catch(Exception ex){RequestStop(ex.Message);return;}}
        if(++Rows>200000) { RequestStop("trace row limit"); return; }
        if(data.Length>4096) { RequestStop("trace value limit"); return; }
        try { Trace.WriteLine(Clock.Elapsed.TotalSeconds.ToString("R",CultureInfo.InvariantCulture)+"\t"+Time.frameCount+"\t"+kind+"\t"+data.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ')); if(kind is "car-trial-active" or "car-recorder-stop" or "hud-recorder-stop")Trace.Flush(); }
        catch(Exception ex) { RequestStop("trace write failed: "+ex.Message); }
    }
    // Observation runs inside device/car enumerations. Do not close their readers
    // or unpatch a method from inside that callback; finish at the next runtime tick.
    static void RequestStop(string reason) { Gate.Revoke();Armed=false; PendingStop ??= reason; }
    static void OnStop() => Close("game stopping");
    static void Close(string reason)
    {
        if(Closed) return; Closed=true; Gate.Revoke();Armed=false;
        bool neutral=true;
        // The ordinary shim owns a separate reader. Closing DeviceHub alone
        // cannot neutralize it at timeout/failure; ownership remains sticky.
        if(Protocol.ShimAdmission(Request))
        {
            try { StopCarTrial(); }
            catch(Exception ex) { reason+="; car trial stop failed: "+ex.Message; }
            try { RestoreTrialModules(); }
            catch(Exception ex) { reason+="; native module restore failed: "+ex.Message; }
            try { RestoreShimPresentation(); }
            catch(Exception ex) { reason+="; presentation restore failed: "+ex.Message; }
            try { Method(Type("ShimInput"),"Stop").Invoke(null,new object[]{"admission test ended: "+reason}); }
            catch(Exception ex) { neutral=false;reason+="; shim stop failed: "+ex.Message; }
        }
        try { var hub=Runtime?.GetField("Devices",All)?.GetValue(null); if(hub is not null) Method(Hub,"CloseReaders").Invoke(hub,null); }
        catch(Exception ex) { neutral=false;reason+="; reader close failed: "+ex.Message; }
        // Reuse the existing transport's recorder Stop and eight-second normal
        // Quit on abort. Do not leave a cold/pending workload alive until180s.
        if(Protocol.NativePositive(Request) && !PositiveComplete)
            try{Method(Type("NativeObservation"),"Finish").Invoke(null,new object[]{"refused: controls session revoked: "+reason});}
            catch(Exception ex){neutral=false;reason+="; observation stop failed: "+ex.Message;}
        // CloseReadDevices drops pending native samples. Verify its resident
        // readback rather than treating a stop request as a neutral receipt.
        try{if(NativeEnabled)neutral=neutral && Native!.RefreshStatus() && Native.Status.Contains("no sample running",StringComparison.Ordinal);}
        catch(Exception ex){neutral=false;reason+="; neutral readback failed: "+ex.Message;}
        try{if(OwnedEvidence is not null)Publish("stop-ack.json",new{schema=1,nonce=Request!.Nonce,sequence=Sequence,
            processId=Environment.ProcessId,startedUtcTicks=ProcessStart.UtcDateTime.Ticks,revoked=true,probeNeutral=neutral,checkedUtc=DateTimeOffset.UtcNow});}
        catch(Exception ex){reason+="; stop acknowledgment failed: "+ex.Message;}
        try { Trace?.Dispose(); } catch(Exception ex) { reason+="; trace close failed: "+ex.Message; } finally { Trace=null; }
        try { if(OwnedEvidence is not null) File.WriteAllText(Path.Combine(OwnedEvidence,"result.json"),JsonSerializer.Serialize(new{reason,rows=Rows,commands=RawCommands,inputVerdict=Protocol.HudStopOnly(Request)?HudVerdict:Protocol.CarTrial(Request)?CarVerdict:Protocol.MenuTrial(Request)?TrialVerdict:"unclassified",physicalOutput=false,
            positiveEvidence=Protocol.NativePositive(Request)?new{verdict=PositiveVerdict,complete=PositiveComplete,physicalReturned=PositivePhysicalReturned,releaseRows=PositiveReleaseRows,seconds=Request!.PositiveSeconds,recipe=PositiveRecipe,pzDataAvailable=!Protocol.CandidateTuple(Request)&&PositivePzDataAvailable,windowStarts=PositiveWindowStarts,nativeEffects=PositiveEffects,handbrakeEffectObserved=PositiveEffects.TryGetValue("handbrake",out int hbEffects)&&hbEffects>0,driveResponse=PositiveDriveReport(),causalVerdict=Protocol.DriveResponse(Request)?"driveResponse phase verdicts; delivery and vehicle response separate, INCONCLUSIVE is not a pass":Protocol.CandidateForward(Request)?"requires actual forward motion trend and brake release; no repeated P/Z comparison":PositivePzDataAvailable?"requires joined P/Z native analysis; A/B start states not controlled":"unavailable P/Z grounding/orientation/timer/torque fields"}:null,
            carEvidence=Protocol.CarTrial(Request)?new{recording=CarRecording,kind="new-original-not-reference-replay",nativePlateaus=CarPulseCount,
                recipe=Protocol.AppliedForwardCapture(Request)?"applied-forward-50pct-8s@1":Protocol.ForceCapture(Request)?"forward-50pct-8s@1":"six-actions@1",forceCapture=Protocol.ForceCapture(Request)?ForceCoverage:null,
                forceProducer="existing diagnostic source.jsonl; inspect actual ffb validity/gates",pressure="unavailable: no native pressure channel qualified",
                torque="native wheel torque channels/missing list retained; proportional handbrake and physical torque not qualified",opponents="none",temporaryTransmission="player0 Manual"}:null},Protocol.Json)); }
        catch(Exception ex) { Instance?.Log.LogError("Controls result write failed: "+ex.Message); }
        Instance?.Log.LogInfo("Raw controls ended: "+reason+"; process force/progression mute retained");
    }
    // Unloading does not remove safety patches or reset the native process latch.
    public override bool Unload() { Close("addon unload"); return false; }
}
