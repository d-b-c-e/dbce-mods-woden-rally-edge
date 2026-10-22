using System.Text.Json;
using Woden.ControlsTesting;

internal static class DriveResponseChecks
{
    // Real rows: candidate-forward run-01 source samples 573-647 (first 1.25 s of full throttle from rest, crash.count 0):
    // simulationSeconds, motion.velocity.local.z, motion.angularVelocity.local.y. R9 service-brake-after-B native-car
    // rows (game.brake 1, every wheel brake torque 0): Simulation, motion.velocity.local.z. No recorded run has
    // commanded steering yet, so steering rows are synthetic.
    static readonly double[] ForwardThrottle={24.4166,-0.0001,0.0,24.4332,-0.0001,0.0,24.4499,0.1279,0.0009,24.4666,0.1631,0.0032,24.4832,0.2903,0.0171,24.4999,0.361,-0.0015,24.5166,0.5138,0.0072,24.5332,0.5898,0.0045,24.5499,0.7126,-0.0217,24.5666,0.8046,-0.0076,24.5832,0.9209,-0.0151,24.5999,1.0142,0.0153,24.6166,1.1439,-0.0086,24.6332,1.2467,-0.0036,24.6499,1.3753,-0.0014,24.6666,1.4409,-0.0024,24.6832,1.6101,0.0165,24.6999,1.7262,-0.014,24.7166,1.8221,0.0244,24.7332,1.9476,0.0001,24.7499,2.0539,-0.0151,24.7666,2.1972,0.0141,24.7832,2.3279,0.0027,24.7999,2.4561,-0.0008,24.8166,2.5479,-0.0105,24.8332,2.7193,0.0112,24.8499,2.8514,0.0102,24.8666,2.9862,0.0089,24.8832,3.1181,0.01,24.8999,3.256,0.0101,24.9166,3.3923,0.0104,24.9332,3.5282,0.0108,24.9499,3.6689,0.0111,24.9666,3.8086,0.0115,24.9832,3.954,0.0118,24.9999,4.0996,0.0122,25.0166,4.2445,0.0124,25.0332,4.3846,0.0119,25.0499,4.4872,0.0128,25.0666,4.6351,0.0134,25.0832,4.8196,0.0195,25.0999,4.9708,0.0057,25.1166,5.1452,0.0234,25.1332,5.1265,-0.0948,25.1499,5.5085,0.0816,25.1666,5.6128,-0.1201,25.1832,6.0843,0.0964,25.1999,5.8557,0.0882,25.2166,6.34,-0.133,25.2332,6.44,0.0701,25.2499,6.5745,0.064,25.2666,6.6282,-0.1057,25.2832,7.1205,0.1231,25.2999,6.8131,0.1535,25.3166,7.1973,-0.0263,25.3332,7.41,0.148,25.3499,7.8555,-0.0451,25.3666,7.6167,-0.0454,25.3832,8.062,0.1843,25.3999,8.1694,-0.0275,25.4166,8.2912,-0.0281,25.4332,8.3524,0.1923,25.4499,8.7894,-0.0116,25.4666,8.4263,-0.0836,25.4832,9.1851,-0.105,25.4999,9.1122,-0.0031,25.5166,9.1016,-0.0753,25.5332,9.5011,-0.0791,25.5499,9.8931,0.0831,25.5666,9.5414,0.1401,25.5832,10.0416,-0.0046,25.5999,10.0333,0.1742,25.6166,10.4996,-0.0447,25.6332,10.3963,0.0397,25.6499,10.6249,-0.04};
    static readonly double[] R9ServiceBrake={28.1165,14.5334,28.1332,14.4869,28.1499,13.8344,28.1665,13.5849,28.1832,13.264,28.1999,13.0437,28.2165,12.2785,28.2332,12.4805,28.2499,11.7024,28.2665,11.8764,28.2832,11.0608,28.2999,11.4445,28.3165,10.8296,28.3332,10.2895,28.3499,10.3038,28.3665,10.0044,28.3832,9.6728,28.3999,10.1008,28.4165,9.3308,28.4332,9.3563,28.4499,8.7046,28.4665,8.7085,28.4832,8.2619,28.4999,7.8873,28.5165,7.5563,28.5332,7.8848,28.5499,7.1832,28.5665,7.1577,28.5832,6.905,28.5999,6.9433,28.6165,6.2507,28.6332,6.271,28.6499,6.0291,28.6665,6.0655,28.6832,5.4447,28.6999,5.236,28.7165,4.8922,28.7332,5.0664,28.7499,4.6624,28.7665,4.65,28.7832,4.5133,28.7999,4.2946,28.8165,4.1107,28.8332,3.9559,28.8499,3.7527,28.8665,3.5915,28.8832,3.4249,28.8999,3.2568,28.9165,3.0617,28.9332,2.902,28.9499,2.7395,28.9665,2.5785,28.9832,2.4184,28.9999,2.2545,29.0165,2.1207,29.0332,1.9354,29.0499,1.8119,29.0665,1.662,29.0832,1.4798,29.0999,1.3566,29.1165,1.176,29.1332,1.0531,29.1499,0.9025,29.1665,0.768,29.1832,0.6427,29.1999,0.4915,29.2165,0.3818};
    const int PhysicalSteer=34142; // real owner wheel at rest; wheelInput.steer 0.02506093867123127 in run-01

    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string why) { count++;if(!ok)throw new Exception(why); }
        void Refuse(Action action) { count++;try{action();}catch(InvalidDataException){return;}throw new Exception("Invalid drive request accepted"); }
        byte[] Bytes<T>(T value)=>JsonSerializer.SerializeToUtf8Bytes(value,Protocol.Json);
        var now=DateTimeOffset.Parse("2026-10-17T22:00:00Z");string nonce=new('a',32),hash=new('b',64);
        var drive=new ColdRequest(12,nonce,now.AddMinutes(1),hash,hash,hash,Protocol.CandidateBindingsSha256,
            Protocol.LoadMutedSha256,Path.GetFullPath("drive-response-fixture"),Mode:"native-drive-response",
            StageReference:Path.GetFullPath("sealed-reference"),PositiveSeconds:10);
        var admitted=Protocol.Admit(Bytes(drive),now,nonce);
        Check(admitted==drive&&Protocol.DriveResponse(admitted)&&Protocol.CandidateTuple(admitted)&&Protocol.NativePositive(admitted)&&
            !Protocol.CandidateForward(admitted)&&!Protocol.ShimAdmission(admitted),"drive response is explicit Native schema12 on the candidate tuple");
        foreach(var bad in new[]{drive with{Schema=11},drive with{Mode="native-candidate-forward"},drive with{BindingsSha256=Protocol.OwnerBindingsSha256},
            drive with{PositiveSeconds=15},drive with{PositiveSeconds=22},drive with{NativeSha256=hash},drive with{StageReference=null},
            drive with{ControlsSha256=hash},drive with{WorkloadDirectory=Path.GetFullPath("other")}})
            Refuse(()=>Protocol.Admit(Bytes(bad),now,nonce));
        var candidate=drive with{Schema=11,Mode="native-candidate-forward"};
        Check(Protocol.CandidateForward(Protocol.Admit(Bytes(candidate),now,nonce))&&!Protocol.DriveResponse(candidate),"schema11 candidate stays admitted and separate");
        var raw=new RawRequest(1,nonce,"raw","inject raw axis 0 dev="+PositivePedalPlan.Device+" value=40048 range=0..65535 ms=1000");
        Refuse(()=>Protocol.Command(Bytes(raw),nonce,0,now.AddSeconds(1),now,false,Protocol.NativePositive(drive)));
        foreach(string operation in new[]{"status","stop"})
            Check(Protocol.Command(Bytes(raw with{Operation=operation,Raw=null}),nonce,0,now.AddSeconds(1),now,false,true).Operation==operation,"only ordinary status/stop remain admitted");

        var w=PositivePedalPlan.DriveWindows;
        Check(w.Length==6&&w.Sum(x=>x.Seconds)==7.75&&w.Sum(x=>x.Seconds)<PositivePedalPlan.CandidateObservationSeconds,"one 7.75-second recipe inside the unchanged ten-second transport");
        string Command(int axis,int value,int ms)=>$"inject raw axis {axis} dev={PositivePedalPlan.Device} value={value} range=0..65535 ms={ms}";
        var expected=new[]{Array.Empty<string>(),new[]{Command(2,65535,1250)},new[]{Command(0,26659,1000)},new[]{Command(0,40048,1000)},
            new[]{Command(2,0,100),Command(5,65535,100)},Array.Empty<string>()};
        for(int i=0;i<w.Length;i++)Check(w[i].Commands(PositivePedalPlan.Device).SequenceEqual(expected[i]),"exact drive commands for "+w[i].Name);
        Check(w[4].TtlMilliseconds==100&&w[4].Throttle==0&&w[4].Brake==65535,"brake reuses the 100 ms pulses that stop at the native reverse boundary");
        Check(PositivePedalPlan.Windows.Concat(PositivePedalPlan.CandidateWindows).All(x=>x.Steer is null),"existing schema10/11 recipes gain no steering object");
        var steer=PositivePedalPlan.DriveSteerCalibration;
        Check(steer.Normalize(PositivePedalPlan.DriveSteerLeft)==-.25&&steer.Normalize(PositivePedalPlan.DriveSteerRight)==.25,"production normalization maps the steer raws to -0.25/+0.25");
        Check(Math.Abs(steer.Normalize(PhysicalSteer)-0.02506093867123127)<1e-7,"pinned 04EB steer calibration reproduces the real run-01 rest value (recorded as float)");

        var c=new Dictionary<string,double>{["wheelRaw.steer"]=40048,["wheelInput.steer"]=.25,["wheelInput.appliedTicks"]=12,
            ["wheel.fl.steerAngle"]=4,["wheel.fr.steerAngle"]=4};
        Check(PositivePedalPlan.DriveDelivered(w[3],c),"raw and production-normalized steer count as live delivery");
        Check(!PositivePedalPlan.DriveDelivered(w[2],c),"right-hand values cannot satisfy the left window");
        c["wheelInput.steer"]=.2;Check(!PositivePedalPlan.DriveDelivered(w[3],c),"wrong normalized steer refuses delivery");c["wheelInput.steer"]=.25;
        c["wheelInput.appliedTicks"]=double.NaN;Check(!PositivePedalPlan.DriveDelivered(w[3],c),"unavailable applied tick refuses delivery");c["wheelInput.appliedTicks"]=12;
        Check(PositivePedalPlan.DriveEffect(w[3],c)&&!PositivePedalPlan.DriveEffect(w[2],c),"front wheel steer angle sign follows the command");
        c.Remove("wheel.fr.steerAngle");Check(!PositivePedalPlan.DriveEffect(w[3],c),"missing wheel angle is not an effect");

        DriveReport Classify(Action<int,int,Dictionary<string,double>>? edit=null,bool complete=true,bool released=true,int lastWindow=5)=>
            DriveClassifier.Classify(Rows(edit,lastWindow),complete,released);
        DrivePhase Phase(DriveReport report,string name)=>report.Phases.Single(p=>p.Window==name);
        var nominal=Classify();
        Check(nominal.Outcome==DriveClassifier.Pass&&nominal.Phases.Count==5&&nominal.Phases.All(p=>p.Delivery.Verdict=="PASS"&&p.Response.Verdict=="PASS"),
            "real throttle and brake rows with synthetic opposite steering pass every phase");
        Check(Phase(nominal,"drive-steer-left").Response.Values["straightBaseline"]==.1923,"steering baseline is the real straight-line maximum yaw rate under throttle");
        Check(Phase(nominal,"drive-brake").Response.Values["decel"]>Phase(nominal,"drive-brake").Response.Values["coastDecel"],"real R9 braking exceeds coasting");
        string json=JsonSerializer.Serialize(nominal,Protocol.Json);
        Check(json.Contains("\"outcome\":\"PASS\"")&&json.Contains("\"straightBaseline\":0.1923"),"report serializes with its values");

        var contact=Classify((i,n,r)=>{if(i>=3&&n>=0&&(i>3||n>=30))r["drive.crashCount"]=1;});
        Check(Phase(contact,"drive-steer-right").Response.Verdict=="INCONCLUSIVE"&&Phase(contact,"drive-brake").Response.Verdict=="INCONCLUSIVE"&&
            Phase(contact,"drive-steer-left").Response.Verdict=="PASS"&&contact.Outcome=="INCONCLUSIVE","a contact makes its phase and the coast baseline inconclusive, not failed");
        var lateContact=Classify((i,n,r)=>{if(i>=3)r["drive.crashCount"]=1;});
        Check(Phase(lateContact,"drive-steer-left").Response.Verdict=="INCONCLUSIVE","a contact first seen on the next phase's first row belongs to the earlier phase");
        var noWitness=Classify((i,n,r)=>{if(i==1)r.Remove("drive.crashCount");});
        Check(Phase(noWitness,"drive-throttle").Response.Reason=="contact witness unavailable","missing crash counter is inconclusive");
        var inverted=Classify((i,n,r)=>{if(i is 2 or 3)r["motion.angularVelocity.local.y"]*=-1;});
        Check(Phase(inverted,"drive-steer-left").Response.Verdict=="FAIL"&&Phase(inverted,"drive-steer-right").Response.Verdict=="FAIL"&&inverted.Outcome=="FAIL",
            "yaw clearly opposite to the command fails");
        var weak=Classify((i,n,r)=>{if(i is 2 or 3)r["motion.angularVelocity.local.y"]*=.2;});
        Check(Phase(weak,"drive-steer-right").Response.Verdict=="INCONCLUSIVE"&&weak.Outcome=="INCONCLUSIVE","yaw within the straight-line baseline is insufficient, not a pass or failure");
        var slow=Classify((i,n,r)=>{if(i==2)r["motion.velocity.local.z"]=.5;});
        Check(Phase(slow,"drive-steer-left").Response.Reason=="not moving forward through the whole phase","steering needs forward motion");
        var smoothed=Classify((i,n,r)=>{if(i==2){double v=-.25*(1-Math.Pow(.9,n+1));r["controls.steer"]=r["game.steer"]=v>-.2499?v:-.2499;}});
        Check(Phase(smoothed,"drive-steer-left").Delivery.Verdict=="INCONCLUSIVE"&&Phase(smoothed,"drive-steer-left").Response.Verdict=="PASS",
            "native steer approaching but never equal to the mapped value is inconclusive delivery, separate from the yaw response");
        var ignored=Classify((i,n,r)=>{if(i==2)r["controls.steer"]=r["game.steer"]=steer.Normalize(PhysicalSteer);});
        Check(Phase(ignored,"drive-steer-left").Delivery.Verdict=="FAIL","native steer that stays physical fails delivery");
        var noMod=Classify((i,n,r)=>{if(i==3)r["wheelInput.steer"]=0;});
        Check(Phase(noMod,"drive-steer-right").Delivery.Reason=="raw/normalized steer not delivered","mod path failure is its own delivery failure");
        var noBrake=Classify((i,n,r)=>{if(i==4)r["motion.velocity.local.z"]=9;});
        Check(Phase(noBrake,"drive-brake").Response.Verdict=="FAIL","deceleration no greater than coasting fails");
        var stopped=Classify((i,n,r)=>{if(i==4){r["motion.velocity.local.z"]=.2;r["game.trueSpeed"]=.6;}});
        Check(Phase(stopped,"drive-brake").Delivery.Verdict=="INCONCLUSIVE"&&Phase(stopped,"drive-brake").Response.Verdict=="INCONCLUSIVE","no brake pulse at rest is inconclusive");
        var stuck=Classify((i,n,r)=>{if(i==5)r["game.brake"]=1;});
        Check(Phase(stuck,"final-release").Response.Verdict=="FAIL","a native brake still applied after release fails");
        var reverse=Classify((i,n,r)=>{if(i==5)r["motion.velocity.local.z"]=-.05*n;});
        Check(Phase(reverse,"final-release").Response.Verdict=="FAIL","reversing after release fails");
        var notReturned=Classify(released:false);
        Check(Phase(notReturned,"final-release").Delivery.Verdict=="FAIL","raw axes that never return fail release delivery");
        var stoppedEarly=Classify(complete:false,released:false,lastWindow:2);
        Check(Phase(stoppedEarly,"drive-throttle").Response.Verdict=="PASS"&&stoppedEarly.Phases.Skip(1).All(p=>p.Response.Verdict=="INCONCLUSIVE")&&
            stoppedEarly.Outcome=="INCONCLUSIVE","a run stopped during steering keeps the throttle pass and leaves later phases inconclusive");
        var noThrottle=Classify((i,n,r)=>{if(i==1)r["motion.velocity.local.z"]=0;});
        Check(Phase(noThrottle,"drive-throttle").Response.Verdict=="FAIL","no forward motion under throttle fails");
        Check(Phase(noThrottle,"drive-steer-left").Response.Reason=="no straight-line yaw baseline from the throttle phase","no moving throttle rows leaves no steering baseline");
        Console.WriteLine($"PASS {count} focused drive-response protocol/recipe/classifier checks; real throttle and brake rows, synthetic steering; no baseline/game/device/native.");
    }

    // Nominal run: physical rest, the real throttle rows, synthetic left then right steering while coasting, the real
    // R9 brake rows, then a settled release. Physical brake 900 maps to exact 0 through the 3% deadzone.
    static List<DriveRow> Rows(Action<int,int,Dictionary<string,double>>? edit,int lastWindow)
    {
        var rows=new List<DriveRow>();long sequence=500;double clock=24.4,heading=0;
        double rest=PositivePedalPlan.DriveSteerCalibration.Normalize(PhysicalSteer);
        void Add(int window,int n,double t,double forward,double yaw,int steerRaw,double steerValue,int throttle,int brake)
        {
            heading+=yaw/60;
            double throttleValue=throttle/65535d,brakeValue=new WodenRallyEdge.Core.AxisCalibration(0,65535,Deadzone:.03).Normalize(brake);
            var c=new Dictionary<string,double>(StringComparer.Ordinal){
                ["wheelRaw.steer"]=steerRaw,["wheelInput.steer"]=steerValue,["controls.steer"]=steerValue,["game.steer"]=steerValue,
                ["wheelRaw.throttle"]=throttle,["wheelInput.throttle"]=throttleValue,["controls.throttle"]=throttleValue,["game.throttle"]=throttleValue,
                ["wheelRaw.brake"]=brake,["wheelInput.brake"]=brakeValue,["controls.brake"]=brakeValue,["game.brake"]=brakeValue,
                ["wheelRaw.handbrake"]=0,["wheelInput.handbrake"]=0,["game.handbrake"]=0,["wheelInput.appliedTicks"]=sequence-400,
                ["motion.velocity.local.z"]=forward,["motion.angularVelocity.local.y"]=yaw,["game.trueSpeed"]=forward*3,
                ["motion.orientation.x"]=0,["motion.orientation.y"]=Math.Sin(heading/2),["motion.orientation.z"]=0,["motion.orientation.w"]=Math.Cos(heading/2),
                ["drive.simulationSeconds"]=t,["drive.crashCount"]=0};
            edit?.Invoke(window,n,c);
            rows.Add(new(window,sequence++,c));
        }
        for(int n=0;n<60;n++)Add(0,n,clock+=1/60d,0,0,PhysicalSteer,rest,0,900);
        if(lastWindow>=1)for(int n=0;n<ForwardThrottle.Length/3;n++)
            Add(1,n,clock=24.4+1+ForwardThrottle[3*n]-ForwardThrottle[0],ForwardThrottle[3*n+1],ForwardThrottle[3*n+2],PhysicalSteer,rest,65535,900);
        double speed=ForwardThrottle[^2];
        foreach(var (window,raw,from,to) in new[]{(2,PositivePedalPlan.DriveSteerLeft,0d,-.5),(3,PositivePedalPlan.DriveSteerRight,-.5,.5)})
            if(lastWindow>=window)for(int n=0;n<60;n++)
                Add(window,n,clock+=1/60d,speed-=1/60d,n<20?from+(to-from)*(n+1)/20:to,raw,PositivePedalPlan.DriveSteerCalibration.Normalize(raw),0,900);
        if(lastWindow>=4){double t0=clock+1/60d;for(int n=0;n<R9ServiceBrake.Length/2;n++)
            Add(4,n,clock=t0+R9ServiceBrake[2*n]-R9ServiceBrake[0],R9ServiceBrake[2*n+1],0,PhysicalSteer,rest,0,65535);}
        if(lastWindow>=5)for(int n=0;n<90;n++)Add(5,n,clock+=1/60d,.3*Math.Pow(.9,n),0,PhysicalSteer,rest,0,900);
        return rows;
    }
}
