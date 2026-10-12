using System.Globalization;
using System.Text;
using System.Text.Json;
using BepInEx;

namespace Woden.ControlsTesting;

public sealed partial class ControlsProbe
{
    static CarWorkload? CarWork;
    static readonly Queue<CarPulse> CarPulses=new();
    static CarPulse? CarPulse;
    static CarPlateau CarValues=new();
    static int CarId,CarGear,CarBatch,CarPulseCount;
    static double CarChanged,CarActivated,CarNeutral;
    static bool CarStarted,CarDelivery,CarEnding,CarGearQuery;
    static string CarVerdict="unclassified",CarEpochs="";
    static string CarRecording => Path.Combine(Paths.GameRootPath,"BepInEx","WodenRecordings","request-"+Request!.Nonce,"source.jsonl");
    static CarWorkload ReadCarWorkload()
    {
        string folder=Request!.WorkloadDirectory!;
        foreach(string path in new[]{folder,Request.StageReference!,Path.Combine(Paths.ConfigPath,"dbce-controls.ini")})Plain(path);
        foreach(string path in new[]{Path.Combine(folder,"profile.json"),Path.Combine(folder,"raw-workload.json"),Path.Combine(Request.StageReference!,"stage-context.json")})Plain(path);
        var work=CarWorkload.Read(ReadBounded(Path.Combine(Paths.ConfigPath,"dbce-controls.ini"),1024*1024),
            ReadBounded(Path.Combine(folder,"profile.json"),1024*1024),ReadBounded(Path.Combine(folder,"raw-workload.json"),1024*1024));
        Match(Path.Combine(Request.StageReference!,"stage-context.json"),CarWorkload.StageHash);
        return work;
    }
    static void VerifyCarRecordingRequest(object? launch)
    {
        if(launch==null || Convert.ToInt32(Value(launch,"Version"))!=2 || (Guid)Value(launch,"Id")! != Guid.ParseExact(Request!.Nonce,"N") ||
            (DateTimeOffset)Value(launch,"ExpiresUtc")! != Request.ExpiresUtc || !(bool)Value(launch,"DisableForces")! ||
            (string?)Value(launch,"CaseId")!="woden-shim-car-"+Request.Nonce ||
            !string.Equals(Runtime.GetField("_recordingPath",All)?.GetValue(null) as string,CarRecording,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Schema 5 requires its exact one-shot, force-disabled diagnostic recorder.");
        if(!File.Exists(CarRecording))throw new InvalidDataException("Original diagnostic recorder did not create source.jsonl.");
    }
    static void StartCarTrial()
    {
        CarWork=ReadCarWorkload();
        TrialSession=Path.Combine(Request!.OutputDirectory,"shim-session.txt");TrialBatchFile=Path.Combine(Request.OutputDirectory,"shim-batch.txt");
        File.WriteAllText(TrialSession,"nonce="+Request.Nonce+"\nexpires="+Request.ExpiresUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)+"\n",new UTF8Encoding(false));
        Method(Type("ShimInput"),"ConfigureCarTrial").Invoke(null,new object[]{Request.Nonce,TrialSession,TrialBatchFile,Request.ExpiresUtc,true});
        Method(Type("StageStartup"),"InitializeInputTrial").Invoke(null,new object[]{Request.StageReference!});
        CarPulses.Clear();CarPulses.Enqueue(CarWork.Neutral);
        foreach(var pulse in CarWork.Pulses){CarPulses.Enqueue(pulse);CarPulses.Enqueue(CarWork.Neutral);}
        CarPulses.Enqueue(CarWork.Settle);CarPulses.Enqueue(CarWork.Neutral);
        CarId=CarBatch=CarPulseCount=0;CarStarted=CarDelivery=CarEnding=false;CarPulse=null;CarEpochs="";
        CarChanged=Clock.Elapsed.TotalSeconds;CarActivated=CarNeutral=0;CarVerdict="pending";
        ObserveUntil=Deadline;TrialModules.Clear();
        Row("car-trial","New original Kenya control qualification; temporary player0 Manual; no trajectory/body/background writes; six raw actions only");
    }
    static void VerifyCarDevices()
    {
        var handle=Field(Type("ShimInput"),"_handle")!;
        string report=(string)Method(Type("ShimNative"),"DeviceReports").Invoke(null,new[]{handle})!;
        using var doc=JsonDocument.Parse(report);
        var devices=doc.RootElement.EnumerateArray().ToArray();
        if(devices.Length!=2)throw new InvalidDataException("Car trial needs exactly the two profile devices.");
        var expected=new[]{("{d71b8350-61b7-11f1-8001-444553540000}","{0006346e-0000-0000-0000-504944564944}"),
            ("{d9b1d890-6f3f-11f1-8002-444553540000}","{05310483-0000-0000-0000-504944564944}")};
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
        if(Value(output,"RecordingStatus")?.ToString()!="Recording" || Convert.ToInt64(Value(output,"RecordingDrops"))!=0)
            throw new InvalidOperationException("Original recording is unavailable or dropped samples.");
        ObserveShim();
    }
    static void SendCarBatch(CarPulse? pulse,double now)
    {
        _=ReadCarWorkload();VerifyCarFences();VerifyCarDevices();
        var car=CarNow() ?? throw new InvalidOperationException("Native car disappeared.");
        if(!VerifyNativeCar(car) || car.TransmissionSystem!.Changing)throw new InvalidOperationException("Native car must be racing, unlocked, Manual and not changing before each batch.");
        if(++CarBatch>24)throw new InvalidOperationException("Car batch limit.");
        CarGear=car.MyGear;
        if(pulse?.Action=="shiftUp" && (CarGear<1 || CarGear>=car.Gears))throw new InvalidOperationException("Native gear cannot increment exactly once.");
        CarPulse=pulse;CarActivated=CarNeutral=0;CarValues=new();CarGearQuery=false;CarChanged=now;
        TrialBatchWriter.Replace(TrialBatchFile!,"nonce="+Request!.Nonce+"\nbatch="+CarBatch+"\n"+(pulse?.Command??"end")+"\n");
        Row("car-trial-batch",JsonSerializer.Serialize(new{batch=CarBatch,pulse,gearBefore=CarGear}));
    }
    static void TickCarTrial()
    {
        if(!Protocol.CarTrial(Request)||!Armed)return;
        double now=Clock.Elapsed.TotalSeconds;
        if(now-CarChanged>(CarStarted?8:95))throw new InvalidOperationException("Car trial timed out waiting for "+(CarStarted?"bounded native consumption":"native startup/countdown"));
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
            var output=Field(Runtime,"Output")!;Method(output.GetType(),"StopRecording").Invoke(output,null);
            if(Value(output,"RecordingStatus")?.ToString()!="Completed" || Convert.ToInt64(Value(output,"RecordingDrops"))!=0)
                throw new InvalidOperationException("Original recorder did not complete without drops.");
            CarVerdict="passed: six raw shared-Core actions consumed by native car; Manual gear +1/-1; original producer retained";
            Row("car-trial-verdict",CarVerdict);Close("bounded original car trial completed");return;
        }
        bool injected=CarWorkload.Actions.All(id=>{var a=TrialAction(id);return (bool)Value(a,"Injected")!&&Value(a,"Status")!.ToString()=="Live";});
        if(injected && CarActivated==0){CarActivated=now;Row("car-trial-active","batch="+CarBatch+" seq="+Static("ShimInput","Sequence"));}
        bool neutral=CarWorkload.Actions.All(id=>{var a=TrialAction(id);return !(bool)Value(a,"Injected")!&&Value(a,"Status")!.ToString()=="TestNeutral"&&Convert.ToSingle(Value(a,"Value"))==0;});
        if(CarActivated==0 || !neutral || now-CarActivated<.6){CarNeutral=0;return;}
        if(CarNeutral==0)CarNeutral=now;
        if(now-CarNeutral<.25 || car.TransmissionSystem!.Changing)return;
        if(!CarValues.Passed)throw new InvalidOperationException("Native plateau unavailable/mismatched for "+CarPulse!.Action+"; rows="+CarValues.Samples);
        int expectedGear=CarGear+(CarPulse!.Action=="shiftUp"?1:CarPulse.Action=="shiftDown"?-1:0);
        if(car.MyGear!=expectedGear || CarPulse.Action is "shiftUp" or "shiftDown" && !CarGearQuery)
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
        if(now>=CarActivated+.16 && now<=CarActivated+.55)CarValues.Add(now,CarPulse.Action,CarPulse.Expected,channels);
    }
    static void StopCarTrial()
    {
        if(!Protocol.CarTrial(Request))return;
        if(CarDelivery)Method(Type("ShimInput"),"CarTrialDelivery").Invoke(null,new object[]{false});CarDelivery=false;
        Method(Type("StageStartup"),"StopInputTrial").Invoke(null,null);
        if(!CarVerdict.StartsWith("passed:"))CarVerdict="incomplete: "+CarPulseCount+" native plateaus; "+CarPulse?.Action;
    }
}
