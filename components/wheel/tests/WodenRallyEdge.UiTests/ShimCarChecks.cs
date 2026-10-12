using System.Reflection;
using UnityEngine;

namespace WodenRallyEdge;

internal static partial class ShimChecks
{
    static bool GetCarTrial()=>(bool)typeof(ShimInput).GetField("_carTrial",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
    static void TrialCarCore(string dir,Action<bool,string> check)
    {
        string? source=Environment.GetEnvironmentVariable("WODEN_CORE_CAR_FRAMES"),profile=Environment.GetEnvironmentVariable("WODEN_CORE_MENU_PROFILE");
        if(source==null||profile==null)return;
        Panel.Close(false);Reset(dir);Runtime.DiagnosticNoForce=true;Runtime.Settings.FfbEnabled=Runtime.Settings.TelemetryEnabled=Runtime.Settings.Record=false;
        File.WriteAllBytes(Path.Combine(dir,"dbce-controls.ini"),File.ReadAllBytes(profile));
        string session=Path.Combine(dir,"car-session.txt"),batch=Path.Combine(dir,"car-batch.txt");File.WriteAllText(session,"fixture");
        ShimInput.ConfigureCarTrial(new string('a',32),session,batch,DateTimeOffset.UtcNow.AddMinutes(2),true);
        Replace("_open2",nameof(Open2));Replace("_tick",nameof(CoreTick));
        var producer=new GamePadSystem();var values=Enumerable.Range(0,18).Select(i=>new GamePadSystem.Actions("native"+i,0,false)).ToArray();
        var saved=values.ToArray();var pad=new GamePadSystem.Game_Pad{PadActions=values};producer.Game_Pads.Add(pad);
        var car=new MainCar();var controls=new Controls{field_Private_MainCar_0=car,field_Private_GamePadSystem_0=producer};car.MyControls=controls;Runtime.Local=car;
        ShimInput.BeforeStock(producer);Input.Held=false;Input.Keys.Clear();Input.Down.Clear();
        int frames=0,active=0,shiftUp=0,shiftDown=0,previousPhase=-1;bool armed=false;var covered=new HashSet<string>();
        using var input=new BinaryReader(File.OpenRead(source));
        while(input.BaseStream.Position<input.BaseStream.Length)
        {
            int phase=input.ReadInt32();_coreFrame=input.ReadBytes(296);check(_coreFrame.Length==296,"complete car Core ABI frame");
            Runtime.Clock.Advance(.01);Next();frames++;
            if(!armed){check(ShimInput.Ready&&!ShimInput.AxesLive,"car trial starts owned but cannot deliver before native admission");ShimInput.CarTrialDelivery(true);armed=true;}
            if(phase!=previousPhase){car.TransmissionSystem!.Changing=false;previousPhase=phase;}
            var lease=Runtime.Wheel!.Apply(controls);check(lease!=null,"production car consumer has a scoped native table");
            check(!Runtime.Wheel.Button("Confirm",false)&&!Runtime.Wheel.Button("Pause",false)&&!Runtime.Wheel.Button("Camera",false),"car mode cannot submit/start/pause/camera");
            if(ShimInput.AxesLive)
            {
                active++;
                check(Runtime.Wheel.LastFor(car)!=null,"all raw axes delivered Live retain production force-producer input sample");
                check(Math.Abs(values[16].value-values[17].value-ShimInput.Axis("steer"))<.0001 && values[7].value==ShimInput.Axis("throttle")&&values[6].value==ShimInput.Axis("brake"),"actual Core normalization reaches native axes");
                check(lease!.Handbrake==ShimInput.Axis("handbrake")&&values[0].Pressed==(lease.Handbrake>0),"progressive handbrake amount and native bool remain distinct");
                foreach(string id in new[]{"steer","throttle","brake","handbrake"})if(Math.Abs(ShimInput.Axis(id))>.1)covered.Add(id);
            }
            if(values[3].Pressed){shiftUp++;car.MyGear++;car.TransmissionSystem!.Changing=true;covered.Add("shiftUp");}
            if(values[1].Pressed){shiftDown++;car.MyGear--;car.TransmissionSystem!.Changing=true;covered.Add("shiftDown");}
            // Native game remains an engine fixture; this is consumer delivery,
            // not a claim about the real gearbox or physical output.
            lease!.Restore();check(values.SequenceEqual(saved),"car scoped action lease restores every native value");
        }
        check(_latchedArgs&&_opens==1&&frames>2000&&active>500,"actual pinned Core raw workload consumed by production Open2/decoder/input lease");
        check(covered.Count==6&&shiftUp==1&&shiftDown==1&&car.MyGear==1,"six actual consumer actions, once-only sequential +1/-1 fixture");
        check(ShimInput.Run==4&&!ShimInput.AxesLive&&ShimInput.Axis("throttle")==0,"actual Core Completed retains neutral state");
        Runtime.Focused=false;Time.frameCount++;ShimInput.Poll();check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1,"car latched mode stops on actual focus loss with sticky ownership");
        ShimInput.CarTrialDelivery(false);bool rearmed=false;try{ShimInput.CarTrialDelivery(true);}catch(InvalidOperationException){rearmed=true;}check(rearmed,"cleanup can neutralize a failed host, and car trial cannot rearm after terminal delivery");
        Replace("_tick",nameof(Tick));_coreFrame=null;
    }
}
