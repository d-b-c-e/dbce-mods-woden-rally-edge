using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Dbce.Wheel.Input;
using Woden.ControlsTesting;

// Drive the actual private Harmony callbacks against files/fake native exports.
// Admission metadata is checked separately against the installed interop build.
// No Load(), Unity runtime, P/Invoke or force export is called in this fixture.
static class Test
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
        Console.WriteLine($"PASS: {Checks} production-addon lifecycle assertions; private fixture {Base}");
    }
    sealed class BadText {public override string ToString()=>throw new InvalidOperationException("format failed");}
    sealed class ThrowingEnumeration:IEnumerable {public IEnumerator GetEnumerator()=>throw new InvalidOperationException("reader enumeration failed");}
}
