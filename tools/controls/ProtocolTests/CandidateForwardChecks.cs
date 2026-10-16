using System.Text.Json;
using Woden.ControlsTesting;

internal static class CandidateForwardChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string why) { count++;if(!ok)throw new Exception(why); }
        void Refuse(Action action) { count++;try{action();}catch(InvalidDataException){return;}throw new Exception("Invalid candidate accepted"); }
        byte[] Bytes<T>(T value)=>JsonSerializer.SerializeToUtf8Bytes(value,Protocol.Json);
        var now=DateTimeOffset.Parse("2026-10-16T11:00:00Z");string nonce=new('a',32),hash=new('b',64);
        var candidate=new ColdRequest(11,nonce,now.AddMinutes(1),hash,hash,hash,Protocol.CandidateBindingsSha256,
            Protocol.LoadMutedSha256,Path.GetFullPath("candidate-forward-fixture"),Mode:"native-candidate-forward",
            StageReference:Path.GetFullPath("sealed-reference"),PositiveSeconds:10);
        Check(Protocol.CandidateForward(Protocol.Admit(Bytes(candidate),now,nonce))&&Protocol.NativePositive(candidate)&&!Protocol.ShimAdmission(candidate),"candidate is explicit Native schema11 only");
        foreach(var bad in new[]{candidate with{Schema=10},candidate with{Schema=2},candidate with{Mode="native-positive-pedals"},
            candidate with{BindingsSha256=Protocol.OwnerBindingsSha256},candidate with{BindingsSha256=hash},candidate with{PositiveSeconds=15},
            candidate with{PositiveSeconds=22},candidate with{NativeSha256=hash},candidate with{StageReference=null},
            candidate with{ControlsSha256=hash},candidate with{WorkloadDirectory=Path.GetFullPath("other")}})
            Refuse(()=>Protocol.Admit(Bytes(bad),now,nonce));
        var legacy=candidate with{Schema=10,Mode="native-positive-pedals",BindingsSha256=Protocol.OwnerBindingsSha256,PositiveSeconds=22};
        Check(Protocol.Admit(Bytes(legacy),now,nonce)==legacy&&!Protocol.CandidateForward(legacy),"legacy exact EC81/22-second admission remains separate");
        Refuse(()=>Protocol.Admit(Bytes(legacy with{BindingsSha256=Protocol.CandidateBindingsSha256}),now,nonce));
        var raw=new RawRequest(1,nonce,"raw","inject raw axis 2 dev="+PositivePedalPlan.Device+" value=65535 range=0..65535 ms=4000");
        Refuse(()=>Protocol.Command(Bytes(raw),nonce,0,now.AddSeconds(1),now,false,Protocol.NativePositive(candidate)));
        foreach(string operation in new[]{"status","stop"})
            Check(Protocol.Command(Bytes(raw with{Operation=operation,Raw=null}),nonce,0,now.AddSeconds(1),now,false,true).Operation==operation,"only ordinary status/stop remains admitted");
        var windows=PositivePedalPlan.CandidateWindows;
        Check(windows.Length==3&&windows.Sum(w=>w.Seconds)==6&&PositivePedalPlan.CandidateObservationSeconds==10,"one six-second recipe in unchanged ten-second transport");
        var commands=windows.SelectMany(w=>w.Commands(PositivePedalPlan.Device)).ToArray();
        Check(commands.Length==1&&commands[0]==raw.Raw,"only one four-second throttle object; brake and all other objects stay physical");
        Check(!windows[0].Commands(PositivePedalPlan.Device).Any()&&!windows[2].Commands(PositivePedalPlan.Device).Any(),"physical and final release windows submit nothing");
        var c=new Dictionary<string,double>{["wheelRaw.brake"]=927,["wheelInput.brake"]=0,["controls.brake"]=0,["game.brake"]=0,
            ["wheelInput.throttle"]=0,["controls.throttle"]=0,["game.throttle"]=0,["wheelInput.handbrake"]=0,["game.handbrake"]=0,
            ["wheelInput.appliedTicks"]=10,["game.grounded"]=1,["game.trueSpeed"]=0,["motion.velocity.local.z"]=0};
        Check(PositivePedalPlan.CandidateReady(c),"mapped exact-zero neutral physical pedals and grounded near-rest car admit throttle");
        c["game.brake"]=.013;Check(!PositivePedalPlan.CandidateReady(c),"nonzero native brake refuses submission");c["game.brake"]=0;
        c["motion.velocity.local.z"]=-2;Check(!PositivePedalPlan.CandidateReady(c),"moving start refuses submission");c["motion.velocity.local.z"]=0;
        c.Remove("game.grounded");Check(!PositivePedalPlan.CandidateReady(c),"missing grounded state is unavailable");c["game.grounded"]=1;
        c["wheelRaw.throttle"]=65535;c["wheelInput.throttle"]=c["controls.throttle"]=c["game.throttle"]=1;
        Check(PositivePedalPlan.CandidateDelivered(windows[1],c),"actual raw/mapped/applied/native path joins to the production 3% normalization");
        c["controls.brake"]=927/65535d;Check(!PositivePedalPlan.CandidateDelivered(windows[1],c),"unmodified zero-deadzone mapping cannot pass candidate delivery");c["controls.brake"]=0;
        c["game.throttle"]=0;Check(!PositivePedalPlan.CandidateDelivered(windows[1],c),"raw acceptance alone does not prove native delivery");c["game.throttle"]=1;
        c["wheelRaw.brake"]=3000;Check(!PositivePedalPlan.CandidateDelivered(windows[1],c),"above-deadzone physical brake cannot be presented as released");c["wheelRaw.brake"]=927;
        c["wheelInput.appliedTicks"]=double.NaN;Check(!PositivePedalPlan.CandidateDelivered(windows[1],c),"unavailable applied tick refuses delivery");c["wheelInput.appliedTicks"]=10;
        foreach(string wheel in new[]{"fl","fr","rl","rr"}){c["wheel."+wheel+".brakeTorque"]=0;c["wheel."+wheel+".motorTorque"]=0;}
        Check(!PositivePedalPlan.CandidateForwardResponse(c),"throttle and zero torque at rest are not propulsion");
        c["motion.velocity.local.z"]=2;Check(PositivePedalPlan.CandidateForwardResponse(c),"actual forward motion with brake release counts without motor torque");
        c["wheel.fl.motorTorque"]=.01;Check(PositivePedalPlan.CandidateForwardResponse(c),"tiny motor torque neither causes nor blocks the motion witness");
        c["wheel.fl.brakeTorque"]=500;Check(!PositivePedalPlan.CandidateForwardResponse(c),"latched native braking cannot pass");c["wheel.fl.brakeTorque"]=0;
        c["game.handbrake"]=1;Check(!PositivePedalPlan.CandidateForwardResponse(c),"handbrake cannot pass the forward response");c["game.handbrake"]=0;
        c["motion.velocity.local.z"]=-2;Check(!PositivePedalPlan.CandidateForwardResponse(c),"backward motion is not a forward witness");
        c.Remove("wheel.rr.brakeTorque");Check(!PositivePedalPlan.CandidateForwardResponse(c),"missing wheel brake state stays unavailable");
        Console.WriteLine($"PASS {count} focused candidate-forward protocol/recipe checks; production calibration source linked, no baseline/game/device/native.");
    }
}
