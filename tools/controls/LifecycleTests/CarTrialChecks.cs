using System.Text.Json;
using Woden.ControlsTesting;

static partial class Test
{
    static void CarTrialChecks()
    {
        string? original=Environment.GetEnvironmentVariable("WODEN_CAR_ORIGINAL"),controls=Environment.GetEnvironmentVariable("WODEN_CORE_MENU_PROFILE");
        if(original==null||controls==null)return;
        string reference=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dbce","StagePlayback","references","woden-kenya-20261005");
        void Begin(string name)
        {
            Reset(name);BepInEx.Paths.GameRootPath=Path.Combine(Dir,"game");BepInEx.Paths.ConfigPath=Path.Combine(Dir,"config");Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            File.Copy(controls,Path.Combine(BepInEx.Paths.ConfigPath,"dbce-controls.ini"));
            string workload=Path.Combine(Dir,"workload");Directory.CreateDirectory(workload);File.Copy(Path.Combine(original,"raw-workload.json"),Path.Combine(workload,"raw-workload.json"));File.Copy(Path.Combine(original,"apply","profile.json"),Path.Combine(workload,"profile.json"));
            Set("Request",Get<ColdRequest>("Request") with{Schema=5,Mode="shim-car-trial",WorkloadDirectory=workload,StageReference=reference});
            WodenRallyEdge.StagePlayback.Session.Active=false;WodenRallyEdge.StageStartup.Valid=true;WodenRallyEdge.Runtime.RaceReady=true;WodenRallyEdge.ShimInput.CarDelivery=false;
            WodenRallyEdge.ShimNative.Report="[{\"instance\":\"{d71b8350-61b7-11f1-8001-444553540000}\",\"product\":\"{0006346e-0000-0000-0000-504944564944}\",\"interfacePath\":\"exact-wheel\",\"epoch\":1,\"opened\":true,\"status\":\"live\"},{\"instance\":\"{d9b1d890-6f3f-11f1-8002-444553540000}\",\"product\":\"{05310483-0000-0000-0000-504944564944}\",\"interfacePath\":\"exact-shifter\",\"epoch\":3,\"opened\":true,\"status\":\"live\"}]";
            Call("StartCarTrial");
        }
        Dictionary<string,double> Channels(CarPulse pulse)=>new(){
            ["game.automatic"]=0,["game.locked"]=0,["game.paused"]=0,["game.respawning"]=0,["game.replay"]=0,
            ["controls.steer"]=pulse.Action=="steer"?pulse.Expected:0,["game.steer"]=pulse.Action=="steer"?pulse.Expected:0,
            ["controls.throttle"]=pulse.Action=="throttle"?pulse.Expected:0,["game.throttle"]=pulse.Action=="throttle"?pulse.Expected:0,
            ["controls.brake"]=pulse.Action=="brake"?pulse.Expected:pulse.Action=="settle"?1:0,["game.brake"]=pulse.Action=="brake"?pulse.Expected:pulse.Action=="settle"?1:0,
            ["game.handbrake"]=pulse.Action=="handbrake"&&pulse.Expected>0?1:0};
        void FinishPulse(bool values=true,bool gear=true,bool query=true)
        {
            var pulse=Get<CarPulse>("CarPulse");var car=(MainCar)WodenRallyEdge.Runtime.Local!;
            WodenRallyEdge.ShimInput.Injected=true;Call("AfterTick");
            Set("CarActivated",Now-1);var plateau=Get<CarPlateau>("CarValues");
            if(values)for(int i=0;i<8;i++)plateau.Add(i*.025,pulse.Action,pulse.Expected,Channels(pulse));
            if(pulse.Action is "shiftUp" or "shiftDown")
            {if(query)Call("AfterButton",pulse.Action=="shiftUp"?"Gear up":"Gear down",false,true);if(gear)car.MyGear+=pulse.Action=="shiftUp"?1:-1;}
            WodenRallyEdge.ShimInput.Injected=false;Set("CarNeutral",Now-1);Call("AfterTick");
        }
        Begin("car-original-complete");Call("AfterTick");Check(Get<int>("CarBatch")==0&&!WodenRallyEdge.ShimInput.CarDelivery,"native startup has no injected input before car/countdown");
        var live=new MainCar();WodenRallyEdge.Runtime.Local=live;WodenRallyEdge.Runtime.RaceReady=false;Call("AfterTick");Check(Get<int>("CarBatch")==0,"countdown cannot start raw workload");
        WodenRallyEdge.Runtime.RaceReady=true;Call("AfterTick");Check(Get<int>("CarBatch")==1&&WodenRallyEdge.ShimInput.CarDelivery,"native car identity arms a neutral baseline first");
        while(!Get<bool>("CarEnding")&&Get<bool>("Armed"))FinishPulse();
        Check(Get<int>("CarPulseCount")==23&&Get<int>("CarBatch")==24&&live.MyGear==1&&!WodenRallyEdge.ShimInput.CarDelivery,"bounded original six-action workload plus settle/neutral ends delivery");
        WodenRallyEdge.ShimInput.Run=4;Call("AfterTick");Set("CarNeutral",Now-1);Call("AfterTick");
        Stopped("car original complete");Check(Get<string>("CarVerdict").StartsWith("passed:")&&WodenRallyEdge.Runtime.Output.RecordingStatus=="Completed","Core completion and original recorder completion both required");
        Check(Submitted==0,"original car batches never enter legacy raw injector");
        Begin("car-recorder-fails");WodenRallyEdge.Runtime.Local=new MainCar();Call("AfterTick");
        while(!Get<bool>("CarEnding")&&Get<bool>("Armed"))FinishPulse();
        WodenRallyEdge.Runtime.Output.Complete=false;WodenRallyEdge.ShimInput.Run=4;Call("AfterTick");Set("CarNeutral",Now-1);Call("AfterTick");
        Stopped("car recorder failure");Check(!Get<string>("CarVerdict").StartsWith("passed:"),"input plateaus cannot hide recorder completion failure");
        foreach(string fault in new[]{"focus","force","network","epoch","missing-device","automatic","changing","no-plateau","no-query","no-gear","recording-drop","replay-active","profile-changed"})
        {
            Begin("car-fault-"+fault);WodenRallyEdge.Runtime.Local=new MainCar();Call("AfterTick");
            var car=(MainCar)WodenRallyEdge.Runtime.Local;
            if(fault=="focus")WodenRallyEdge.ShimInput.NativeAllowed=false;
            if(fault=="force")WodenRallyEdge.Runtime.Force.Attempts=1;
            if(fault=="network")WodenRallyEdge.Runtime.Output.ForzaPackets=1;
            if(fault=="epoch")WodenRallyEdge.ShimNative.Report=WodenRallyEdge.ShimNative.Report.Replace("\"epoch\":3","\"epoch\":4");
            if(fault=="missing-device")WodenRallyEdge.ShimNative.Report="[]";
            if(fault=="automatic")car.Aids!.Automatic=true;
            if(fault=="changing")car.TransmissionSystem!.Changing=true;
            if(fault=="recording-drop")WodenRallyEdge.Runtime.Output.RecordingDrops=1;
            if(fault=="replay-active")WodenRallyEdge.StagePlayback.Session.Active=true;
            if(fault=="profile-changed")File.AppendAllText(Path.Combine(BepInEx.Paths.ConfigPath,"dbce-controls.ini"),"# changed\n");
            if(fault is "no-query" or "no-gear") {while(Get<CarPulse>("CarPulse").Action!="shiftUp")FinishPulse();FinishPulse(query:fault!="no-query",gear:fault!="no-gear");}
            else if(fault=="changing"){Set("CarChanged",Now-9);Call("AfterTick");}
            else FinishPulse(values:fault!="no-plateau");
            Stopped("car "+fault);Check(!Get<string>("CarVerdict").StartsWith("passed:")&&!WodenRallyEdge.ShimInput.CarDelivery,"car fault stays incomplete/neutral: "+fault);
        }
        WodenRallyEdge.ShimNative.Report="physical";
        Begin("car-recorder-identity");
        var request=Get<ColdRequest>("Request");string recording=Path.Combine(BepInEx.Paths.GameRootPath,"BepInEx","WodenRecordings","request-"+request.Nonce,"source.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(recording)!);File.WriteAllText(recording,"fixture producer");WodenRallyEdge.Runtime._recordingPath=recording;
        var launch=new RecordingLaunch(2,Guid.ParseExact(request.Nonce,"N"),request.ExpiresUtc,true,"woden-shim-car-"+request.Nonce);
        Call("VerifyCarRecordingRequest",launch);Check(true,"exact diagnostic request accepted");
        foreach(var bad in new[]{launch with{Version=1},launch with{Id=Guid.NewGuid()},launch with{ExpiresUtc=request.ExpiresUtc.AddSeconds(1)},launch with{DisableForces=false},launch with{CaseId="unrelated"}})
        {bool refused=false;try{Call("VerifyCarRecordingRequest",bad);}catch(InvalidDataException){refused=true;}Check(refused,"mismatched ordinary recorder refused");}
        WodenRallyEdge.Runtime._recordingPath=recording+"other";bool wrongPath=false;try{Call("VerifyCarRecordingRequest",launch);}catch(InvalidDataException){wrongPath=true;}Check(wrongPath,"different recorder output refused");Call("Close","fixture complete");
    }
    sealed record RecordingLaunch(int Version,Guid Id,DateTimeOffset ExpiresUtc,bool DisableForces,string CaseId);
}
