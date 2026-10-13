using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Dbce.Wheel.Input;
using Woden.ControlsTesting;

// Drive the actual private Harmony callbacks against files/fake native exports.
// Admission metadata is checked separately against the installed interop build.
// No Load(), Unity runtime, device or force export is called. The production
// atomic batch writer is exercised against private temporary files on Windows.
static partial class Test
{
    static readonly Type Probe=typeof(ControlsProbe);
    const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly string Base=Path.Combine(Path.GetTempPath(),"woden-controls-lifecycle-"+Guid.NewGuid().ToString("N"));
    static int Checks, Submitted;static bool NativeOk;static string Dir="";
    static void Check(bool ok,string why){Checks++;if(!ok)throw new Exception(why);}
    static void Set(string field,object? value)=>Probe.GetField(field,Flags)!.SetValue(null,value);
    static T Get<T>(string field)=>(T)Probe.GetField(field,Flags)!.GetValue(null)!;
    static object? Call(string name,params object?[] args){try{return Probe.GetMethod(name,Flags)!.Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException!;}}
    static void CStr(byte[] dst,string text){Array.Clear(dst);Encoding.UTF8.GetBytes(text).CopyTo(dst,0);}
    static double Now=>Get<Stopwatch>("Clock").Elapsed.TotalSeconds;
    static void Reset(string name)
    {
        Call("Close","reset fixture");Dir=Path.Combine(Base,name);Directory.CreateDirectory(Dir);
        foreach(var f in new[]{"Requested","Armed"})Set(f,true);
        Set("Closed",false);Set("PendingStop",null);Set("Instance",new ControlsProbe());
        Set("Runtime",typeof(WodenRallyEdge.Runtime));Set("Hub",typeof(WodenRallyEdge.DeviceHub));Set("Wheel",typeof(WodenRallyEdge.WheelInput));Set("Mod",typeof(WodenRallyEdge.Runtime).Assembly);
        WodenRallyEdge.Runtime.Devices=new();WodenRallyEdge.Runtime.DiagnosticNoForce=true;WodenRallyEdge.StagePlayback.Target.Muted=false;Call("Mute");
        foreach(var f in new[]{"Sequence","RawCommands","Rows"})Set(f,0);
        Get<Dictionary<string,string>>("Observed").Clear();
        Set("Deadline",Now+300);Set("NextIdentity",double.PositiveInfinity);Set("ObserveUntil",Now+300);Set("NextStatus",0d);
        Set("OwnedEvidence",Dir);Set("ProcessStart",DateTimeOffset.UtcNow.AddMinutes(-1));
        Set("Request",new ColdRequest(1,new string('a',32),DateTimeOffset.UtcNow.AddMinutes(5),"", "", "", "", "",Dir));
        Set("ShimIdentityWritten",false);Set("ShimDevices",null);Set("ShimOriginalPage",null);WodenRallyEdge.Runtime.Local=null;WodenRallyEdge.Runtime.Force=new();WodenRallyEdge.Runtime.Output=new();WodenRallyEdge.Runtime.Settings=new();
        UnityEngine.Object.Instances.Clear();UnityEngine.EventSystems.EventSystem.current=null;
        WodenRallyEdge.ShimInput.Run=2;WodenRallyEdge.ShimInput.NativeAllowed=WodenRallyEdge.ShimInput.Neutral=true;WodenRallyEdge.ShimInput.Injected=false;
        WodenRallyEdge.ShimInput.Stops=0;WodenRallyEdge.ShimInput.Ready=true;WodenRallyEdge.ShimInput.Owns=true;
        WodenRallyEdge.ShimInput._producer=new(){Game_Pads=new(){new(){AnyKey=true}}};WodenRallyEdge.ShimInput._frame=21;
        WodenRallyEdge.Panel.Open=false;UnityEngine.ScreenCapture.LastPath=null;
        Set("Trace",new StreamWriter(new FileStream(Path.Combine(Dir,"observations.tsv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)));
        Submitted=0;NativeOk=true;
        var native=new NativeTestInjectionClient(flags=>flags==1?1:0,(cmd,reason,n)=>{Submitted++;Check(File.Exists(Path.Combine(Dir,"command-"+Get<int>("Sequence")+".json")),"archived before native call");CStr(reason,"accepted");return 1;},(text,n)=>{CStr(text,"latched");return NativeOk?1:0;},slot=>NativeOk?1:-1);
        Check(native.Arm(),"fixture client armed");Set("Native",native);
    }
    static void Command(int sequence=1,string operation="raw",string? nonce=null)
    {
        File.WriteAllText(Path.Combine(Dir,"command.json"),JsonSerializer.Serialize(new RawRequest(sequence,nonce??new string('a',32),operation,operation=="raw"?"inject raw instance axis 0 32768 100":null),Protocol.Json));
    }
    static void Tick()=>Call("BeforeTick");
    static void Stopped(string reason)
    {
        Check(!Get<bool>("Armed") && Get<bool>("Closed"),reason+" closed");
        Check(WodenRallyEdge.Runtime.Devices.Closes==1,reason+" reader close once");
        Check(WodenRallyEdge.Runtime.DiagnosticNoForce && WodenRallyEdge.StagePlayback.Target.Muted && !(bool)Call("Refuse")!,reason+" guards latched");
        Check(File.Exists(Path.Combine(Dir,"result.json")),reason+" result saved");
    }
    public static void Main()
    {
        Reset("valid");Command();Tick();Check(Submitted==1 && Get<int>("Sequence")==1,"valid reaches native once");
        using(var reply=JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir,"reply.json"))))Check(reply.RootElement.GetProperty("sequence").GetInt32()==1 && reply.RootElement.GetProperty("ok").GetBoolean(),"correlated atomic reply");
        Check(!File.Exists(Path.Combine(Dir,"command-claimed.json")),"claimed file archived");Tick();Check(Submitted==1,"no implicit replay");Command(2,"stop");Tick();Stopped("explicit stop");
        Command(3);Tick();Check(Submitted==1,"stopped never submits");Call("OnStop");new ControlsProbe().Unload();Check(WodenRallyEdge.Runtime.Devices.Closes==1,"repeated stop/unload idempotent");
        foreach(var bad in new[]{"nonce","sequence","stale","oversize","invalid-json","claimed-exists"}) {
            Reset(bad);Command(sequence:bad=="sequence"?2:1,nonce:bad=="nonce"?new string('b',32):null);
            if(bad=="stale")File.SetLastWriteTimeUtc(Path.Combine(Dir,"command.json"),DateTime.UtcNow.AddMinutes(-2));
            if(bad=="oversize")File.WriteAllText(Path.Combine(Dir,"command.json"),new string('x',2049));
            if(bad=="invalid-json")File.WriteAllText(Path.Combine(Dir,"command.json"),"{");
            if(bad=="claimed-exists")File.WriteAllText(Path.Combine(Dir,"command-claimed.json"),"unresolved previous bytes");
            Tick();Check(Submitted==0,bad+" not submitted");Stopped(bad);
        }
        Reset("duration");Set("Deadline",Now-1);Command();Tick();Check(Submitted==0,"expired no submit");Stopped("duration");
        Reset("native-loss");NativeOk=false;Command();Tick();Check(Submitted==0,"lost native fence no submit");Stopped("native loss");
        Reset("command-limit");Set("RawCommands",256);Command();Tick();Check(Submitted==0,"bounded commands");Stopped("command limit");
        Reset("poll-deferred");WodenRallyEdge.Runtime.Devices.Devices=new ThrowingEnumeration();Call("AfterPoll",WodenRallyEdge.Runtime.Devices);
        Check(!Get<bool>("Armed") && !Get<bool>("Closed") && WodenRallyEdge.Runtime.Devices.Closes==0,"enumeration fault defers closure");Tick();Stopped("deferred poll");
        Reset("axis-deferred");Call("AfterAxis",new BadText(),.5f,true);Check(WodenRallyEdge.Runtime.Devices.Closes==0 && !Get<bool>("Armed"),"formatting fault deferred");Tick();Stopped("deferred axis");
        Reset("rows");Set("Rows",200000);Call("AfterButton","Confirm",true,true);Check(!Get<bool>("Armed") && !Get<bool>("Closed"),"row bound defers close");Tick();Stopped("row bound");
        Reset("write-failure");Get<StreamWriter>("Trace").Dispose();Call("AfterButton","Confirm",true,true);Check(!Get<bool>("Armed") && !Get<bool>("Closed"),"trace error defers close");Tick();Stopped("trace error");
        Reset("changed");Call("AfterButton","Confirm",false,false);int first=Get<int>("Rows");
        for(int i=0;i<100;i++)Call("AfterButton","Confirm",false,false);
        Check(Get<int>("Rows")==first,"identical observed states are not repeated");
        Call("AfterButton","Confirm",false,true);Call("AfterButton","Confirm",false,false);
        Check(Get<int>("Rows")==first+2,"press and release transitions both retained");
        Command();Tick();int commandRows=Get<int>("Rows");Call("AfterButton","Confirm",false,false);
        Check(Get<int>("Rows")==commandRows+1,"new raw command records fresh state even if unchanged");
        Reset("observation-key-limit");for(int i=0;i<=512;i++)Call("AfterButton","unrelated"+i,false,false);
        Check(!Get<bool>("Armed") && !Get<bool>("Closed"),"observation keys bounded with deferred close");Tick();Stopped("key limit");
        Reset("observation");WodenRallyEdge.Runtime.Devices.Devices=new[]{new WodenRallyEdge.DeviceHub.Device()};Call("AfterPoll",WodenRallyEdge.Runtime.Devices);Call("AfterAxis",null,.5f,true);Call("AfterApply",new WodenRallyEdge.WheelInput{_last=new(){Steer=-.5f,Throttle=.5f}});Call("AfterTick");
        Call("Close","done");var trace=File.ReadAllText(Path.Combine(Dir,"observations.tsv"));Check(trace.Contains("injected=True") && trace.Contains("value=0.5") && trace.Contains("\"Steer\":-0.5") && trace.Contains("\"Throttle\":0.5"),"raw normalized and actual applied observations retained");Check(Submitted==0,"observer never submits input");Stopped("normal observation");
        Reset("native-sample");
        var sample=new NativeSample();
        sample.Channels["controls.throttle"]=.125; sample.Channels["game.throttle"]=.1;
        sample.Channels["wheelInput.throttle"]=1; // Deliberately different desired input: never borrowed as native evidence.
        sample.Channels["game.brake"]=double.NaN;
        Call("AfterSample",sample);Call("Close","done");
        var nativeLine=File.ReadAllLines(Path.Combine(Dir,"observations.tsv")).Single(l=>l.Contains("\tnative-car\t"));
        using(var doc=JsonDocument.Parse(nativeLine.Split('\t',4)[3])) {
            var row=doc.RootElement;var ch=row.GetProperty("Channels");
            Check(ch.GetProperty("controls.throttle").GetDouble()==.125 && ch.GetProperty("game.throttle").GetDouble()==.1,"native values independent of intended input");
            Check(!ch.TryGetProperty("wheelInput.throttle",out _) && !ch.TryGetProperty("game.brake",out _),"desired and non-finite values never become native observations");
            var missing=row.GetProperty("Missing").EnumerateArray().Select(x=>x.GetString()).ToArray();
            Check(missing.Contains("game.brake") && missing.Contains("controls.steer"),"missing and invalid native values explicitly unknown");
            Check(row.GetProperty("Car").GetInt32()==42 && row.GetProperty("Sequence").GetInt64()==7 && row.GetProperty("Phase").GetString()=="native-postfix","native identity and phase retained");
        }
        Check(sample.Channels.Count==4 && sample.Channels["wheelInput.throttle"]==1 && double.IsNaN(sample.Channels["game.brake"]),"read-only observer preserves source sample");
        Check(Submitted==0,"native observer never submits input");Stopped("native observation");
        Reset("native-fault");Call("AfterSample",new object());
        Check(!Get<bool>("Armed") && !Get<bool>("Closed") && WodenRallyEdge.Runtime.Devices.Closes==0,"native observer failure defers closure safely");
        Tick();Stopped("native observation fault");
        foreach(var scenario in new[]{"observe","raw-refused","expiry","car","force","network"}) {
            Reset("shim-"+scenario);Set("Request",Get<ColdRequest>("Request") with{Schema=3,Mode="shim-admission"});
            if(scenario=="observe") {
                Call("AfterTick");Check(File.ReadAllText(Path.Combine(Dir,"shim-ProfileInfo.txt"))=="ok=1","actual observer retains native parser report");
                Command(operation:"panel");Tick();Check(WodenRallyEdge.Panel.Open && WodenRallyEdge.Runtime.Settings.UiPage=="Controls","native panel selected without input");
                Command(2,"shot");Tick();Check(UnityEngine.ScreenCapture.LastPath==Path.Combine(Dir,"shim-panel.png"),"bounded native screenshot request");
                Command(3,"stop");Tick();Stopped("shim explicit stop");
                Check(WodenRallyEdge.Runtime.Settings.UiPage=="Setup","temporary panel selection restored before production shutdown/save");
                string rows=File.ReadAllText(Path.Combine(Dir,"observations.tsv"));
                Check(rows.Contains("\"unityGamepads\":0") && rows.Contains("\"AnyKey\":true") && rows.Contains("\"tickFrame\":21"),"native classification/tick observed without clearing keyboard overlay");
                Check(WodenRallyEdge.ShimInput._producer!.Game_Pads[0].AnyKey,"observer preserves keyboard state");
            } else {
                if(scenario=="raw-refused"){Command();Tick();}
                else if(scenario=="expiry"){Set("Deadline",Now-1);Tick();}
                else {
                    if(scenario=="car")WodenRallyEdge.Runtime.Local=new();
                    if(scenario=="force")WodenRallyEdge.Runtime.Force.Attempts=1;
                    if(scenario=="network")WodenRallyEdge.Runtime.Output.ForzaPackets=1;
                    Call("AfterTick");
                }
                Stopped("shim "+scenario);
            }
            Check(Submitted==0,"shim mode never submits raw input");
            Check(WodenRallyEdge.ShimInput.Stops==1 && !WodenRallyEdge.ShimInput.Ready && WodenRallyEdge.ShimInput.Owns,"separate shim stops once with sticky ownership");
        }
        MenuTrialChecks();
        CarTrialChecks();
        Console.WriteLine($"PASS: {Checks} production-addon lifecycle assertions; private fixture {Base}");
    }
    static void MenuTrialChecks()
    {
        const string garage="Menus/MenuTree/World_Map_Screen/Map_Buttons/Garage_button",workshop="Menus/MenuTree/World_Map_Screen/Map_Buttons/WorkShop_button";
        UnityEngine.Transform PathOf(string path){UnityEngine.Transform t=null!;foreach(string part in path.Split('/'))t=new(){name=part,parent=t};return t;}
        void Begin(string name){Reset(name);Set("Request",Get<ColdRequest>("Request") with{Schema=4,Mode="shim-menu-trial"});Call("StartMenuTrial");}
        void TrialTick()=>Call("AfterTick");
        void ReadyPulse(){TrialTick();Set("TrialNeutral",Now-1);TrialTick();}
        void ReleasePulse(){WodenRallyEdge.ShimInput.Run=3;WodenRallyEdge.ShimInput.Injected=true;WodenRallyEdge.ShimInput.Neutral=false;TrialTick();
            WodenRallyEdge.ShimInput.Injected=false;WodenRallyEdge.ShimInput.Neutral=true;TrialTick();Set("TrialNeutral",Now-1);TrialTick();}
        string Step()=>Probe.GetField("Trial",Flags)!.GetValue(null)!.ToString()!;
        Begin("menu-trial-complete");UnityEngine.Object.Instances[typeof(DailyMessage)]=new DailyMessage();
        ReadyPulse();Check(Get<int>("TrialBatch")==1&&Step()=="DailyRelease","fixed first Core Confirm batch");
        ReleasePulse();Check(WodenRallyEdge.ShimInput.Scope==2,"observed injected then neutral narrows to title");
        UnityEngine.Object.Instances[typeof(TitleScreenScript)]=new TitleScreenScript();
        ReadyPulse();Check(Get<int>("TrialBatch")==2&&Step()=="TitleRelease","Core Running accepts bounded second Start batch");
        ReleasePulse();Check(WodenRallyEdge.ShimInput.Scope==3,"title release removes all submit/start delivery");
        var a=new UnityEngine.UI.Selectable{transform=PathOf(garage)};var b=new UnityEngine.UI.Selectable{transform=PathOf(workshop)};a.Right=b;b.Left=a;
        var ga=new UnityEngine.GameObject{transform=a.transform,Selectable=a};var gb=new UnityEngine.GameObject{transform=b.transform,Selectable=b};
        var module=new UnityEngine.EventSystems.BaseInputModule();var events=new UnityEngine.EventSystems.EventSystem{currentSelectedGameObject=ga,Modules=new[]{module}};
        UnityEngine.EventSystems.EventSystem.current=events;
        ReadyPulse();Check(Get<int>("TrialBatch")==3&&!module.enabled,"reciprocal native neighbor verified; competing native module suppressed");
        events.currentSelectedGameObject=gb;ReleasePulse();Check(Step()=="LeftReady","native Workshop selection must follow released right pulse");
        ReadyPulse();Check(Get<int>("TrialBatch")==4,"one bounded reverse batch");
        events.currentSelectedGameObject=ga;ReleasePulse();Check(Get<int>("TrialBatch")==5&&WodenRallyEdge.ShimInput.Scope==4,"return selection read back before end; no more actions");
        WodenRallyEdge.ShimInput.Run=4;TrialTick();Set("TrialNeutral",Now-1);TrialTick();Stopped("fixed menu trial");
        Check(module.enabled&&Get<string>("TrialVerdict").StartsWith("passed:"),"native module restored and actual route verdict retained");
        Check(Submitted==0,"Core action batches never enter old raw injection channel");
        var text=File.ReadAllText(Path.Combine(Dir,"shim-batch.txt"));Check(text.EndsWith("end\n")&&text.Contains("batch=5"),"immutable monotonic five-batch limit ends session");
        foreach(string fault in new[]{"focus","timeout","car","raw","panel"})
        {
            Begin("trial-fault-"+fault);
            if(fault=="focus")WodenRallyEdge.ShimInput.NativeAllowed=false;
            if(fault=="timeout")Set("TrialChanged",Now-40);
            if(fault=="car")WodenRallyEdge.Runtime.Local=new();
            if(fault is "raw" or "panel"){Command(operation:fault);Tick();}else TrialTick();
            Stopped("trial "+fault);Check(Get<int>("TrialBatch")==0&&Submitted==0,"fault cannot inject or launch");
        }
        string target=Path.Combine(Base,"batch-atomic.txt");TrialBatchWriter.Replace(target,"old");
        using(var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
        {TrialBatchWriter.Replace(target,"new");using var reader=new StreamReader(held);Check(reader.ReadToEnd()=="old"&&File.ReadAllText(target)=="new","atomic batch generation survives concurrent Core-style held reader");}
    }
    sealed class BadText {public override string ToString()=>throw new InvalidOperationException("format failed");}
    sealed class ThrowingEnumeration:IEnumerable {public IEnumerator GetEnumerator()=>throw new InvalidOperationException("reader enumeration failed");}
    sealed class NativeSample {
        public int CarInstanceId=>42;public long Sequence=>7;public double ElapsedSeconds=>1;public double SimulationSeconds=>.5;
        public string State=>"driving";public string Phase=>"native-postfix";public Dictionary<string,double> Channels{get;}=new();
    }
}
