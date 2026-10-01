using BepInEx.Configuration;
using System.Net;
using System.Net.Sockets;
using WodenRallyEdge.Core;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge;
internal static class UxChecks
{
    internal static void SettingsAndBindings(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"woden-ux-"+Guid.NewGuid());Directory.CreateDirectory(dir);string path=Path.Combine(dir,"config.cfg");
        var guid=Guid.NewGuid();
        File.WriteAllText(path,$"[ForceFeedback]\nDeviceGuid = {guid}\nEnabled = false\nStrengthPercent = 62\nSmoothingMs = 57\n[Camera]\nAutoFitBonnet = false\nDefaultsVersion = 2\nHeight = 1.27\n");
        var s=new Settings(new ConfigFile(path,false));var pose=s.GetCameraPose(false);var force=s.ForceOptions;
        check(s.UiView=="Simple"&&s.UiPage=="Setup"&&!s.FfbFollowSteering,"legacy config defaults Simple while preserving explicit wheel");
        s.SetPresentation("Advanced","Driving");check(s.GetCameraPose(false)==pose&&s.ForceOptions==force&&!s.FfbEnabled,"presentation cannot mutate runtime options");s.Save();
        s=new(new ConfigFile(path,false));check(s.UiView=="Advanced"&&s.UiPage=="Driving"&&s.FfbGuid==guid.ToString()&&!s.FfbFollowSteering,"explicit view and wheel persist");
        s.SetPresentation("Simple","Driving");check(s.UiPage=="Setup","Advanced-only page maps to Setup");s.SetPresentation("typo","Controls");check(s.UiView=="Simple"&&s.UiPage=="Controls"&&!s.FfbEnabled,"invalid view keeps common page and Off");
        var fresh=new Settings(new ConfigFile(Path.Combine(dir,"fresh.cfg"),false));check(fresh.FfbFollowSteering&&fresh.UiView=="Simple"&&fresh.FfbStrength==50,"fresh settings follow Steering with 50% default");
        path=Path.Combine(dir,"bindings.json");File.WriteAllText(path,"{\"Version\":1}");var legacy=Bindings.Load(path);
        check(legacy.CameraKeys["Camera pitch up"]=="Numpad1"&&new Bindings().CameraKeys["Camera pitch up"]=="Numpad3","legacy implicit key meaning retained; new family tilt defaults");
        legacy.CameraKeys["Camera"]="C";legacy.CameraKeys["Camera up"]="U";legacy.Buttons["Camera down"]=new(guid,7);CameraTuning.RestoreAdjustmentKeys(legacy);
        check(legacy.CameraKeys["Camera"]=="C"&&legacy.CameraKeys["Camera down"]=="Numpad2"&&!legacy.Buttons.ContainsKey("Camera down"),"restore scope preserves cycle key while replacing adjustment button");
        legacy.Handbrake=new(guid,4,new(60000,0,null,.02)){Inverted=true};legacy.Buttons["Handbrake"]=new(guid,5);legacy.Save(path);var loaded=Bindings.Load(path);
        check(loaded.Handbrake!.Inverted&&loaded.Handbrake.Normalize(0)==0&&loaded.Handbrake.Normalize(60000)==1,"explicit inversion saved independently of captured direction and deadzone");
        check(loaded.Conflict("Camera",loaded.Buttons["Handbrake"])=="Handbrake","shared button conflict points to existing action");
        check(loaded.Buttons["Handbrake"].Button==5,"axis calibration preserves handbrake button");
    }
    internal static void SelectionAndRepeat(Action<bool,string> check)
    {
        Guid a=Guid.NewGuid(),b=Guid.NewGuid();var list=new[]{new ForceCandidate(a,"Same name",true),new ForceCandidate(b,"Same name",true)};
        check(ForceSelection.Resolve(true,b.ToString(),a,list).Guid==a,"follow uses saved Steering GUID even with explicit history");
        check(ForceSelection.Resolve(false,b.ToString(),a,list.Reverse()).Guid==b,"override survives enumeration reorder and Steering change");
        check(!ForceSelection.Resolve(true,b.ToString(),null,list).Ready,"unbound steering cannot fall back to old override");
        check(!ForceSelection.Resolve(false,a.ToString(),b,list.Skip(1)).Ready,"missing exact override cannot use different wheel");
        check(!ForceSelection.Resolve(true,"",a,new[]{list[0],list[0]}).Ready,"duplicate identity is ambiguous");
        check(!ForceSelection.Resolve(true,"",a,new[]{list[0] with {ForceFeedback=false}}).Ready,"non-FFB Steering is inactive");
        check(!ForceSelection.Resolve(true,"",a,new[]{list[0] with {Virtual=true}}).Ready,"virtual target is inactive");
        var (controller,device)=ForceControllerChecks.Create();Runtime.Devices!.Candidates=list;Runtime.Settings.FfbFollowSteering=true;Runtime.Wheel=new();Runtime.Wheel.Bindings.Steer=new(a,0,new(0,65535,32768));
        controller.Prepare();check(device.Opens==1,"actual controller opens valid following target at zero");
        Runtime.Wheel.Bindings.Steer=null;controller.Prepare();check(device.Closes==1&&!controller.Connected&&controller.Sent==0,"clearing followed Steering releases existing output");
        for(int i=0;i<10;i++)controller.Prepare();check(device.Opens==1&&device.Closes==1,"invalid selection never reconnect loops");
        Runtime.Wheel.Bindings.Steer=new(b,0,new(0,65535,32768));controller.Prepare();check(device.Opens==2,"new exact follow target opens through normal lifecycle");controller.Panic();Runtime.Wheel.Bindings.Steer=new(a,0,new(0,65535,32768));controller.Prepare();check(device.Opens==2&&!Runtime.Settings.FfbEnabled,"Steering change cannot clear saved Off");
        var repeat=new ShortcutRepeat();check(!repeat.Tick(true,false,0)&&!repeat.Tick(true,true,1),"held capture/focus key cannot act on resume");repeat.Tick(false,true,2);check(repeat.Tick(true,true,3),"new press after release acts once");check(!repeat.Tick(true,true,3.1)&&repeat.Tick(true,true,3.35),"held repeat has initial delay");check(repeat.Tick(true,true,8)&&!repeat.Tick(true,true,8.01),"stalls do not replay missed adjustments");repeat.Tick(false,true,9);check(repeat.Tick(true,true,10,false)&&!repeat.Tick(true,true,20,false),"reset never repeats");
        check(HandbrakeInput.Amount(true,true,.4f,true,false)==1&&HandbrakeInput.Amount(false,true,.4f,false,false)==.4f,"independent additive handbrake contributions");
    }
    internal static void NetworkCapture(Action<bool,string> check)
    {
        using var a=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));using var b=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        a.Client.ReceiveTimeout=1500;b.Client.ReceiveTimeout=1500;
        int pa=((IPEndPoint)a.Client.LocalEndPoint!).Port,pb=((IPEndPoint)b.Client.LocalEndPoint!).Port;
        string path=Path.Combine(Path.GetTempPath(),"woden-ux-network-"+Guid.NewGuid()+".jsonl");
        using var output=new TelemetryOutput(new(pa,0,20,path),"ux-fixture","synthetic");
        TelemetrySample Sample(int n)=>new(){SessionId="ux-fixture",ElapsedSeconds=n,SimulationSeconds=n,State="driving",Sequence=n};
        output.Publish(Sample(1));IPEndPoint endpoint=new(IPAddress.Loopback,0);check(a.Receive(ref endpoint).Length==324,"initial real loopback destination");
        var old=output.ActiveOptions;bool rejected=false;try{output.ConfigureNetwork(new(pb,pb,20));}catch(ArgumentException){rejected=true;}
        check(rejected&&output.ActiveOptions==old&&output.RecordingActive,"invalid Apply leaves network and recorder intact");
        output.ConfigureNetwork(new(pb,0,20));output.Publish(Sample(2));check(b.Receive(ref endpoint).Length==324,"atomic retarget sends to new loopback port");
        check(output.ActiveOptions.RecordingPath==path&&output.RecordingActive,"destination change retains same capture");
        output.ConfigureNetwork(new(pb,0,20,Enabled:false));long sent=output.ForzaPackets;output.Publish(Sample(3));Thread.Sleep(120);check(output.ForzaPackets==sent&&output.RecordingActive&&!output.Sending,"Telemetry Off stops UDP but retains recording");
        output.StopRecording();check(!output.RecordingActive&&!output.Stopped,"Stop recording keeps dashboard worker alive");output.Dispose();
        var result=SessionReader.Read(path).ToArray();check(result.Last().Kind==SessionRecordKind.Footer && result.Count(r=>r.Kind==SessionRecordKind.Sample)==3,"capture finalizes across connection/Off changes");
    }
}
