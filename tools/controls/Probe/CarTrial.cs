using System.Globalization;
using System.Text;
using System.Text.Json;
using BepInEx;

namespace Woden.ControlsTesting;

public sealed partial class ControlsProbe
{
    static ICarWorkload? CarWork;
    static readonly Queue<CarPulse> CarPulses=new();
    static CarPulse? CarPulse;
    static CarPlateau CarValues=new();
    static int CarId,CarGear,CarBatch,CarPulseCount;
    static double CarChanged,CarActivated,CarNeutral,CarStopped;
    static bool CarStarted,CarDelivery,CarEnding,CarGearQuery;
    static string CarVerdict="unclassified",CarEpochs="";
    static ForceCaptureCoverage ForceCoverage=new();
    static string CarRecording => Path.Combine(Paths.GameRootPath,"BepInEx","WodenRecordings","request-"+Request!.Nonce,"source.jsonl");
    static ICarWorkload ReadCarWorkload()
    {
        string folder=Request!.WorkloadDirectory!;
        if(Protocol.DynamicCarTrial(Request))
        {
            string root=Path.GetDirectoryName(Request.OutputDirectory)!;
            string readback=Path.Combine(root,"private-apply-readback.json");
            string writer=Path.Combine(root,"harness","ConfigurationQualification.dll");
            string receipt=Path.Combine(Paths.GameRootPath,"BepInEx","WodenWheel-install.json");
            foreach(string path in new[]{readback,writer,receipt})Plain(path);
            Match(readback,Request.ApplyReadbackSha256!);
            using var r=JsonDocument.Parse(ReadBounded(readback,8192));
            var row=r.RootElement;
            bool Equal(string field,string expected)=>string.Equals(row.GetProperty(field).GetString(),expected,StringComparison.OrdinalIgnoreCase);
            if(row.GetProperty("schema").GetInt32()!=1 || !Equal("profileRevision",Request.ProfileRevision!) ||
                !Equal("controlsSha256",Request.ControlsSha256!) || !Equal("rawWorkloadSha256",Request.WorkloadSha256!))
                throw new InvalidDataException("Production Apply readback differs from frozen car request.");
            Match(writer,row.GetProperty("writerSha256").GetString()!);
            Match(receipt,row.GetProperty("installedReceiptSha256").GetString()!);
        }
        foreach(string path in new[]{folder,Request.StageReference!,Path.Combine(Paths.ConfigPath,"dbce-controls.ini")})Plain(path);
        foreach(string path in new[]{Path.Combine(folder,"profile.json"),Path.Combine(folder,"raw-workload.json"),Path.Combine(Request.StageReference!,"stage-context.json")})Plain(path);
        var controls=ReadBounded(Path.Combine(Paths.ConfigPath,"dbce-controls.ini"),1024*1024);
        var profile=ReadBounded(Path.Combine(folder,"profile.json"),1024*1024);
        var workload=ReadBounded(Path.Combine(folder,"raw-workload.json"),1024*1024);
        ICarWorkload work=Protocol.DynamicCarTrial(Request) ? AppliedCarWorkload.Read(controls,profile,workload,
            Request.ControlsSha256!,Request.ProfileSha256!,Request.WorkloadSha256!,Request.ProfileRevision!) :
            CarWorkload.Read(controls,profile,workload);
        Match(Path.Combine(Request.StageReference!,"stage-context.json"),CarWorkload.StageHash);
        return work;
    }
    static void VerifyRecordingRequest(object? launch)
    {
        string caseId=(Protocol.OrdinaryForward(Request)?"woden-automated-forward-":Protocol.HudStopOnly(Request)?"woden-shim-hud-":"woden-shim-car-")+Request!.Nonce;
        DateTimeOffset? expiry=launch is null?null:(DateTimeOffset)Value(launch,"ExpiresUtc")!;
        bool expiryMatches=Protocol.OrdinaryForward(Request)
            ? expiry>=Request.ExpiresUtc && expiry<=DateTimeOffset.UtcNow.AddMinutes(15)
            : expiry==Request.ExpiresUtc;
        if(launch==null || Convert.ToInt32(Value(launch,"Version"))!=2 || (Guid)Value(launch,"Id")! != Guid.ParseExact(Request!.Nonce,"N") ||
            !expiryMatches || !(bool)Value(launch,"DisableForces")! ||
            (string?)Value(launch,"CaseId")!=caseId ||
            !string.Equals(Runtime.GetField("_recordingPath",All)?.GetValue(null) as string,CarRecording,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Capture requires its exact one-shot, force-disabled diagnostic recorder.");
        if(!File.Exists(CarRecording))throw new InvalidDataException("Original diagnostic recorder did not create source.jsonl.");
    }
    static void StartCarTrial()
    {
        CarWork=ReadCarWorkload();
        TrialSession=Path.Combine(Request!.OutputDirectory,"shim-session.txt");TrialBatchFile=Path.Combine(Request.OutputDirectory,"shim-batch.txt");
        File.WriteAllText(TrialSession,"nonce="+Request.Nonce+"\nexpires="+Request.ExpiresUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)+"\n",new UTF8Encoding(false));
        Method(Type("ShimInput"),"ConfigureCarTrial").Invoke(null,new object[]{Request.Nonce,TrialSession,TrialBatchFile,Request.ExpiresUtc,true});
        Method(Type("StageStartup"),"InitializeInputTrial").Invoke(null,new object[]{Request.StageReference!});
        CarPulses.Clear();ForceCoverage=new();
        if(Protocol.ForceCapture(Request))foreach(var pulse in CarWork.Forward)CarPulses.Enqueue(pulse);
        else {
            CarPulses.Enqueue(CarWork.Neutral);
            foreach(var pulse in CarWork.Pulses){CarPulses.Enqueue(pulse);CarPulses.Enqueue(CarWork.Neutral);}
            CarPulses.Enqueue(CarWork.Settle);CarPulses.Enqueue(CarWork.Neutral);
        }
        CarId=CarBatch=CarPulseCount=0;CarStarted=CarDelivery=CarEnding=false;CarPulse=null;CarEpochs="";
        CarChanged=Clock.Elapsed.TotalSeconds;CarActivated=CarNeutral=CarStopped=0;CarVerdict="pending";
        ObserveUntil=Deadline;TrialModules.Clear();
        Row("car-trial",Protocol.ForceCapture(Request)?"Synthetic original forward analysis capture: 8s accepted 50% throttle, neutral baseline/end, Manual gear1; no body/focus/force writes":
            Protocol.DynamicCarTrial(Request)?"Frozen production Apply car qualification; temporary player0 Manual; finite raw actions from applied profile; no trajectory/body/background writes":
            "New original Kenya control qualification; temporary player0 Manual; no trajectory/body/background writes; six raw actions only");
    }
    static void VerifyCarDevices()
    {
        var handle=Field(Type("ShimInput"),"_handle")!;
        string report=(string)Method(Type("ShimNative"),"DeviceReports").Invoke(null,new[]{handle})!;
        using var doc=JsonDocument.Parse(report);
        var devices=doc.RootElement.EnumerateArray().ToArray();
        var expected=CarWork!.DevicePins is {} pins ? pins.Select(p=>(p.Key.ToString("B"),p.Value.ToString("B"))).ToArray() :
            new[]{("{d71b8350-61b7-11f1-8001-444553540000}","{0006346e-0000-0000-0000-504944564944}"),
                ("{d9b1d890-6f3f-11f1-8002-444553540000}","{05310483-0000-0000-0000-504944564944}")};
        if(Protocol.DynamicCarTrial(Request) ? devices.Length<expected.Length || devices.Length>8 : devices.Length!=expected.Length)
            throw new InvalidDataException("Car trial device count differs from the bounded profile.");
        var identities=new List<string>();
        foreach(var (instance,product) in expected)
        {
            var d=devices.Single(d=>string.Equals(d.GetProperty("instance").GetString(),instance,StringComparison.OrdinalIgnoreCase));
            string path=d.GetProperty("interfacePath").GetString()!;
            if(!string.Equals(d.GetProperty("product").GetString(),product,StringComparison.OrdinalIgnoreCase) ||
                !d.GetProperty("opened").GetBoolean() || d.GetProperty("status").GetString()!="live" ||
                d.GetProperty("epoch").GetInt64()<1 || string.IsNullOrEmpty(path))throw new InvalidDataException("Exact car-trial device is not live.");
            identities.Add(instance+"|"+path+"|"+d.GetProperty("epoch").GetInt64());
        }
        string identity=string.Join("\n",identities);
        if(CarEpochs.Length!=0 && CarEpochs!=identity)throw new InvalidDataException("Car-trial device path/epoch changed.");
        CarEpochs=identity;
    }
    static MainCar? CarNow() => Runtime.GetField("Local",All)!.GetValue(null) as MainCar;
    static bool VerifyNativeCar(MainCar car)
    {
        if(!(bool)Method(Type("StageStartup"),"ValidateRace").Invoke(null,new object[]{car})!)return false;
        if(car.PlayerIndex!=0 || car.Aids?.Automatic!=false || car.TransmissionSystem==null || car.MyControls==null)
            throw new InvalidOperationException("Native player/Manual/controls identity differs.");
        if(CarId!=0 && CarId!=car.GetInstanceID())throw new InvalidOperationException("Native car instance changed.");
        return (bool)Method(Runtime,"Driving").Invoke(null,new object[]{car})!;
    }
    static void VerifyCarFences()
    {
        if(Native?.Armed!=true || !(bool)Method(Type("ShimInput"),"TrialNativeMenuAllowed").Invoke(null,null)!)
            throw new InvalidOperationException("Car trial requires actual focus, idle and native no-force latch.");
        var session=Field(Type("StagePlayback"),"Session")!;
        if((bool)Value(session,"Active")! || !(bool)Static("StagePlayback","OutputMuted")!)throw new InvalidOperationException("Car trial must remain muted and outside stage replay.");
        var output=Field(Runtime,"Output")!;
        string expected=Protocol.HudOrdinary(Request)&&CarStopped>0 ? "Completed" : "Recording";
        if(Value(output,"RecordingStatus")?.ToString()!=expected || Convert.ToInt64(Value(output,"RecordingDrops"))!=0)
            throw new InvalidOperationException("Original recording is unavailable or dropped samples.");
        ObserveShim();
    }
    static void SendCarBatch(CarPulse? pulse,double now)
    {
        _=ReadCarWorkload();VerifyCarFences();VerifyCarDevices();
        var car=CarNow() ?? throw new InvalidOperationException("Native car disappeared.");
        if(!VerifyNativeCar(car) || car.TransmissionSystem!.Changing)throw new InvalidOperationException("Native car must be racing, unlocked, Manual and not changing before each batch.");
        if(++CarBatch>(Protocol.ForceCapture(Request)?4:24))throw new InvalidOperationException("Car batch limit.");
        CarGear=car.MyGear;
        if(Protocol.ForceCapture(Request)&&CarGear!=1)throw new InvalidOperationException("Forward capture requires unchanged native gear1.");
        if((pulse?.Action is "shiftUp" or "gear2") && (CarGear<1 || CarGear>=car.Gears))throw new InvalidOperationException("Native gear cannot increment exactly once.");
        CarPulse=pulse;CarActivated=CarNeutral=0;CarValues=new();CarGearQuery=false;CarChanged=now;
        TrialBatchWriter.Replace(TrialBatchFile!,"nonce="+Request!.Nonce+"\nbatch="+CarBatch+"\n"+(pulse?.Command??"end")+"\n");
        Row("car-trial-batch",JsonSerializer.Serialize(new{batch=CarBatch,pulse,gearBefore=CarGear}));
    }
    static void TickCarTrial()
    {
        if(!Protocol.CarTrial(Request)||!Armed)return;
        double now=Clock.Elapsed.TotalSeconds;
        if(now-CarChanged>(CarStarted?Math.Max(8,(CarPulse?.DurationMs??700)/1000d+3):95))throw new InvalidOperationException("Car trial timed out waiting for "+(CarStarted?"bounded native consumption":"native startup/countdown"));
        if(!(bool)Static("ShimInput","Ready")!)
        {if((bool)Static("ShimInput","Owns")! || ((string)Static("ShimInput","Status")!).Contains("refused"))throw new InvalidOperationException("Shared host unavailable");return;}
        VerifyCarFences();
        if(!CarStarted)
        {
            SuppressTrialModules();
            Method(Type("StageStartup"),"TickInputTrial").Invoke(null,new object[]{now});
            var start=CarNow();if(start==null||!VerifyNativeCar(start)||start.TransmissionSystem!.Changing)return;
            VerifyCarDevices();CarId=start.GetInstanceID();CarStarted=true;
            Method(Type("ShimInput"),"CarTrialDelivery").Invoke(null,new object[]{true});CarDelivery=true;
            SendCarBatch(CarPulses.Dequeue(),now);return;
        }
        var car=CarNow() ?? throw new InvalidOperationException("Native car disappeared.");
        if(!VerifyNativeCar(car))throw new InvalidOperationException("Native racing state/countdown/focus changed.");
        byte run=Convert.ToByte(Static("ShimInput","Run"));
        if(CarEnding)
        {
            if(run!=4)return;
            if(CarNeutral==0)CarNeutral=now;
            if(now-CarNeutral<.3)return;
            if(CarStopped==0)
            {
                if(Protocol.ForceCapture(Request)&&!ForceCoverage.Passed)throw new InvalidOperationException("Forward capture lacks 2s measured grounded forward speed and qualified production analysis rows.");
                if(Protocol.OrdinaryForward(Request))
                {
                    CarVerdict="passed: bounded automated native forward plateaus ready for installed companion Stop; no physical output";
                    Row("car-forward-ready","installed companion must own request-bound Stop; recorder remains active; utc="+DateTimeOffset.UtcNow.ToString("O"));
                    Close("bounded automated forward input complete; ordinary recorder remains active");return;
                }
                var output=Field(Runtime,"Output")!;Method(output.GetType(),"StopRecording").Invoke(output,null);
                if(Value(output,"RecordingStatus")?.ToString()!="Completed" || Convert.ToInt64(Value(output,"RecordingDrops"))!=0)
                    throw new InvalidOperationException("Original recorder did not complete without drops.");
                CarStopped=now;Row("car-recorder-stop","probe-direct-stop; completed; drops=0; utc="+DateTimeOffset.UtcNow.ToString("O"));
                if(Protocol.HudOrdinary(Request))return;
            }
            if(Protocol.HudOrdinary(Request) && now-CarStopped<5)return;
            if(Protocol.HudOrdinary(Request))
            {
                var output=Field(Runtime,"Output")!;
                var counters=Value(output,"RecordingCounters") ?? throw new InvalidDataException("Completed recording counters unavailable.");
                foreach(string key in new[]{"DroppedSamples","DroppedMarkers","InvalidCount","ErrorCount","LimitCount","QueueFullCount","ContentionCount"})
                    if(Convert.ToInt64(Value(counters,key))!=0)throw new InvalidDataException("HUD recording has nonzero "+key+"; DataWritten unavailable.");
                if(!(bool)Value(output,"RecordingRequiresFinalization")! || output.GetType().GetProperty("RecordingError",All)?.GetValue(output) is not null)
                    throw new InvalidDataException("Ordinary recording cannot show DataWritten before finalization.");
                Row("car-hud-dwell","5s after probe-direct-stop; Completed and DataWritten source state; rendered HUD unverified until WGC review");
            }
            CarVerdict=Protocol.HudOrdinary(Request)?"passed: bounded native forward capture; probe-direct-stop, 5s HUD dwell and completed original source; no physical output":
                Protocol.ForceCapture(Request)?"passed: bounded native forward capture; measured grounded speed/reference and valid independent analysis; no physical output":
                Protocol.DynamicCarTrial(Request)?"passed: applied profile raw actions consumed by native car; Manual transmission and exact applied revision retained; no physical output":
                "passed: six raw shared-Core actions consumed by native car; Manual gear +1/-1; original producer retained";
            Row("car-trial-verdict",CarVerdict);Close("bounded original car trial completed");return;
        }
        bool injected=CarWork!.SelectedActions.All(id=>{var a=TrialAction(id);return (bool)Value(a,"Injected")!&&Value(a,"Status")!.ToString()=="Live";});
        if(injected && CarActivated==0){CarActivated=now;Row("car-trial-active","batch="+CarBatch+" seq="+Static("ShimInput","Sequence"));}
        bool neutral=CarWork!.SelectedActions.All(id=>{var a=TrialAction(id);return !(bool)Value(a,"Injected")!&&Value(a,"Status")!.ToString()=="TestNeutral"&&Convert.ToSingle(Value(a,"Value"))==0;});
        if(CarActivated==0 || !neutral || now-CarActivated<(CarPulse?.DurationMs??700)/1000d-.1){CarNeutral=0;return;}
        if(CarNeutral==0)CarNeutral=now;
        if(now-CarNeutral<.25 || car.TransmissionSystem!.Changing)return;
        if(!CarValues.PassedFor(CarPulse!))throw new InvalidOperationException("Native plateau unavailable/mismatched for "+CarPulse!.Action+"; rows="+CarValues.Samples);
        int expectedGear=CarGear+((CarPulse!.Action is "shiftUp" or "gear2")?1:(CarPulse.Action is "shiftDown" or "gear1")?-1:0);
        if(car.MyGear!=expectedGear || (CarPulse.Action is "shiftUp" or "shiftDown" or "gear1" or "gear2") && !CarGearQuery)
            throw new InvalidOperationException("Native sequential consumption/gear differs for "+CarPulse.Action);
        Row("car-trial-consumed",JsonSerializer.Serialize(new{batch=CarBatch,CarPulse.Action,CarPulse.Expected,CarValues.Samples,gearBefore=CarGear,gearAfter=car.MyGear}));CarPulseCount++;
        if(CarPulses.Count>0){SendCarBatch(CarPulses.Dequeue(),now);return;}
        SendCarBatch(null,now);Method(Type("ShimInput"),"CarTrialDelivery").Invoke(null,new object[]{false});CarDelivery=false;CarEnding=true;
    }
    static void ObserveCarPulse(object carIdentity,string state,IDictionary<string,double> channels)
    {
        if(!Protocol.CarTrial(Request)||!CarStarted||CarPulse==null||CarActivated==0)return;
        double now=Clock.Elapsed.TotalSeconds;
        if(Convert.ToInt32(carIdentity)!=CarId || state!="driving")throw new InvalidOperationException("Native sample identity/state changed.");
        if(now>=CarActivated+.16 && now<=CarActivated+CarPulse.DurationMs/1000d-.15)CarValues.Add(now,CarPulse.Action,CarPulse.Expected,channels);
    }
    static void AfterForceAnalysis(object __0)
    {
        if(!Observing||!Protocol.ForceCapture(Request)||!CarStarted)return;
        // HUD ordinary mode deliberately keeps the game alive after Stop. The
        // recorder has closed; later non-recording rows are outside this source.
        if(Protocol.HudOrdinary(Request)&&CarStopped>0)return;
        try {
            if(Convert.ToInt32(Value(__0,"CarInstanceId"))!=CarId)throw new InvalidDataException("Force capture car changed.");
            var discontinuity=__0.GetType().GetProperty("Discontinuity",All)??throw new MissingMemberException("Sample.Discontinuity");
            ForceCoverage.Observe(Convert.ToDouble(Value(__0,"SimulationSeconds")),Value(__0,"State")?.ToString()=="driving",
                discontinuity.GetValue(__0)!=null,(IDictionary<string,double>)Value(__0,"Channels")!);
        }catch(Exception ex){RequestStop("force capture observation failed: "+ex.Message);}
    }
    static void StopCarTrial()
    {
        if(!Protocol.CarTrial(Request))return;
        if(CarDelivery)Method(Type("ShimInput"),"CarTrialDelivery").Invoke(null,new object[]{false});CarDelivery=false;
        Method(Type("StageStartup"),"StopInputTrial").Invoke(null,null);
        if(!CarVerdict.StartsWith("passed:"))CarVerdict="incomplete: "+CarPulseCount+" native plateaus; "+CarPulse?.Action;
    }
}
