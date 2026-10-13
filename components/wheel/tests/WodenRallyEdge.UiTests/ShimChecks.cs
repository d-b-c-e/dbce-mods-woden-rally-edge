using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Source-linked production backend, ABI decoder, WheelInput and scoped native
// action lease. Only native handle calls are replaced. No game, device open,
// worker, OS input or force call occurs. An explicitly supplied private DLL is
// loaded for ABI checks only; ordinary builds run the managed contract fixture.
internal static partial class ShimChecks
{
    private static readonly ShimAction[] FrameActions = new ShimAction[ShimActionMap.Count];
    private static byte[] _profile = Array.Empty<byte>();
    private static int _opens, _ticks, _stops, _tickResult, _openedWindow;
    private static string _unsupported = "";
    private static bool _badHash, _invalidateAtOpen;
    private static bool _latched;
    private static byte[]? _coreFrame;
    private static bool _latchedArgs;
    private static string _mode = "Sequential";
    private static void Set(string field, object value) => typeof(ShimInput).GetField(field, BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,value);
    private static void Replace(string field, string method)
    {
        var f=typeof(ShimNative).GetField(field,BindingFlags.Static|BindingFlags.NonPublic)!;
        f.SetValue(null,Delegate.CreateDelegate(f.FieldType,typeof(ShimChecks).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!));
    }
    private static int Open(string folder,string nonce,byte[] profile,int length,string unsupported,IntPtr window,out IntPtr handle,byte[] why,int whyLength)
    { _opens++;_openedWindow=window.ToInt32();_profile=profile;_unsupported=unsupported;handle=new IntPtr(77);if(_invalidateAtOpen)GameWindow.Valid=false;return 0; }
    private static int Tick(IntPtr handle,byte[] frame)
    {
        _ticks++; if(_tickResult!=0)return _tickResult;
        BitConverter.GetBytes(ShimActionMap.Abi).CopyTo(frame,4);
        BitConverter.GetBytes((ulong)_ticks).CopyTo(frame,8);
        frame[40]=(byte)(_latched?2:0);frame[41]=frame[42]=(byte)(_latched?1:0);
        for(int i=0;i<FrameActions.Length;i++)
        {int at=48+i*8;BitConverter.GetBytes(FrameActions[i].Value).CopyTo(frame,at);frame[at+4]=(byte)FrameActions[i].Status;frame[at+5]=(byte)(FrameActions[i].Injected?1:0);}
        return 0;
    }
    private static int CoreTick(IntPtr handle,byte[] frame) { _ticks++;_coreFrame!.CopyTo(frame,0);return 0; }
    private static int Open2(ref ShimNative.OpenArgs args,out IntPtr handle,byte[] why,int whyLength)
    {
        _opens++;_profile=new byte[args.ProfileLength];Marshal.Copy(args.Profile,_profile,0,_profile.Length);
        _latchedArgs=args.StructSize==128&&args.Flags==ShimNative.Latched&&args.FencesVerified==1&&args.ForceAbsent==1&&
            args.CaptureFolder==IntPtr.Zero&&args.GameId==IntPtr.Zero&&args.Hz==0&&args.Seconds==0&&
            Marshal.PtrToStringUTF8(args.AdapterVersion)==(GetCarTrial()?"woden-car-trial@1":"woden-menu-trial@1")&&File.Exists(Marshal.PtrToStringUni(args.SessionFile))&&
            !File.Exists(Marshal.PtrToStringUni(args.BatchFile));
        handle=new IntPtr(77);return 0;
    }
    private static int Text(string text,byte[] bytes,out int needed)
    { var b=Encoding.UTF8.GetBytes(text);needed=b.Length+1;Array.Clear(bytes);Array.Copy(b,bytes,Math.Min(b.Length,bytes.Length-1));return 0; }
    private static int Profile(IntPtr handle,byte[] bytes,int size,out int needed) => Text("ok=1\ntransmission="+_mode+"\nsha256="+(_badHash?"incorrect":Convert.ToHexString(SHA256.HashData(_profile)))+"\n",bytes,out needed);
    private static int Report(IntPtr handle,byte[] bytes,int size,out int needed) => Text("device-free fixture",bytes,out needed);
    private static int Stop(IntPtr handle,byte[] bytes,int size,out int needed) {_stops++;return Text("resolved fixture",bytes,out needed);}
    private static void Frame(string id,float value,ShimStatus status=ShimStatus.Live) => FrameActions[ShimActionMap.Index(id)]=new(value,status);
    private static void Next() { Time.frameCount++;ShimInput.Poll(); }
    private static void Reset(string dir)
    {
        Set("_requested",false);Set("_refused",false);Set("_terminal",false);Set("_producer",null!);Set("_handle",IntPtr.Zero);Set("_frame",-1);Set("_context",-1);
        Set("_window",0);
        Set("_sequence",0UL);Set("_trialNonce",null!);Set("_trialScope",0);
        Set("_carTrial",false);Set("_carTrialDelivery",false);Set("_carTrialEnded",false);
        Set("<Owns>k__BackingField",false);Set("<SteeringGuid>k__BackingField",null!);
        _opens=_ticks=_stops=_tickResult=_openedWindow=0;_latched=_badHash=_invalidateAtOpen=false;_mode="Sequential";Gamepad.all.Clear();GameWindow.Window=123;GameWindow.Valid=GameWindow.UnityFocused=true;Runtime.Focused=true;Runtime.Local=null;Pause.Paused=false;Runtime.DiagnosticNoForce=false;
        for(int i=0;i<FrameActions.Length;i++)FrameActions[i]=new(0,ShimActionMap.UnsupportedNames.Contains(ShimActionMap.Names[i])?ShimStatus.Unsupported:ShimStatus.Live);
        Keyboard.current?.Clear();
        Runtime.Settings=new(new ConfigFile(Path.Combine(dir,Guid.NewGuid()+".cfg"),false)){InputBackend="Shim",WheelEnabled=true};
        Runtime.Wheel=new(Path.Combine(dir,Guid.NewGuid()+".json"));Runtime.Devices=new();
        ShimInput.Start(AppContext.BaseDirectory,dir,Path.Combine(dir,"reports"));
    }
    internal static void Run(string dir,Action<bool,string> check)
    {
        int first=0;
        void Check(bool ok,string reason){first++;check(ok,"shim: "+reason);}
        var badDll=Path.Combine(dir,"bad-shim");Directory.CreateDirectory(badDll);
        Check(ShimNative.Load(badDll)!=null,"missing DLL refuses before load");
        File.WriteAllText(Path.Combine(badDll,"dbce_shim.dll"),"not the reviewed binary");
        Check(ShimNative.Load(badDll)!.Contains("reviewed provisional"),"different binary refuses before LoadLibrary");
#if PRIVATE_SHIM_ABI_TESTS
        Check(ShimNative.Load(AppContext.BaseDirectory)==null,"pinned native DLL exports/layout/action order match ABI 2 (no open)");
        Console.WriteLine("Private native ABI check: PASS; no native handle opened.");
#else
        Check(Marshal.SizeOf<ShimNative.OpenArgs>()==ShimNative.ArgsSize,"managed open-arguments layout is ABI 2 size (native DLL not qualified)");
        // All handle calls below are fixture delegates. Do not add a production
        // bypass just to make the public managed fixture independent of the DLL.
        typeof(ShimNative).GetField("<Loaded>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
        Console.WriteLine("Private native ABI check: NOT RUN; managed shim consumer fixture only.");
#endif
        Replace("_open",nameof(Open));Replace("_tick",nameof(Tick));Replace("_profile",nameof(Profile));Replace("_startReport",nameof(Report));Replace("_devices",nameof(Report));Replace("_stop",nameof(Stop));
        var steering=Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
        string section="[Controls]\r\nsteer=axis 0 dev="+steering.ToString("B")+" prod={bbbbbbbb-1111-2222-3333-444444444444} range=0..65535 rest=32768 travel=+1 calibrated\r\n";
        byte[] exact=Encoding.UTF8.GetBytes(section);
        File.WriteAllText(Path.Combine(dir,"dbce-controls.ini"),"[Other]\r\nignored=1\r\n"+section+"[After]\r\nignored=2\r\n",new UTF8Encoding(true));
        Check(ShimProfile.Section(File.ReadAllBytes(Path.Combine(dir,"dbce-controls.ini"))).SequenceEqual(exact),"BOM and unrelated sections preserve exact Controls bytes");
        bool duplicate=false;try{ShimProfile.Section(Encoding.UTF8.GetBytes(section+section));}catch(InvalidDataException){duplicate=true;}
        Check(duplicate,"duplicate section refused");
        Check(new ShimAction(float.NaN,ShimStatus.Live).Applied==0 && new ShimAction(1,ShimStatus.Stale).Applied==0,"nonfinite/unavailable cannot deliver");
        Check(ShimActionMap.Compose(float.NaN,float.PositiveInfinity,0,1,0,.5f)==(1,0,.5f),"finite keyboard composition");
        // The cold car-trial seam exposes only one transmission's bounded
        // shift labels; clutch, reverse and menu actions remain unavailable.
        Reset(dir);Set("_trialNonce",new string('a',32));Set("_carTrial",true);Set("_carTrialDelivery",true);
        var delivers=typeof(ShimInput).GetMethod("TrialDelivers",BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Delivers(string label)=>(bool)delivers.Invoke(null,new object[]{label})!;
        Set("_transmission","HPattern");
        Check(Delivers("Gear 1")&&Delivers("Gear 2")&&!Delivers("Gear up")&&!Delivers("Gear down")&&
            !Delivers("Gear R")&&!Delivers("Confirm"),"H-pattern car trial delivers only the bounded forward gates");
        Set("_transmission","Sequential");
        Check(Delivers("Gear up")&&Delivers("Gear down")&&!Delivers("Gear 1")&&!Delivers("Gear 2")&&
            !Delivers("Clutch")&&!Delivers("Pause"),"Sequential car trial delivers only shift edges");

        // The force wheel never follows a stale native Steer binding while shared input is requested and has not
        // actually been refused (pending, or stopped before opening). An explicit FFB wheel always wins.
        var legacy=Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        Reset(dir);Runtime.Wheel!.Bindings.Steer=new(legacy,0,new(0,65535,32768));
        Runtime.Devices!.Devices.Add(new DeviceHub.Device{Info=new(){InstanceGuid=legacy,Name="Legacy wheel"}});
        Runtime.Devices.Devices.Add(new DeviceHub.Device{Info=new(){InstanceGuid=steering,Name="Shared wheel"}});
        var pendingTarget=Runtime.Devices.ResolveForceTarget(true,"",Runtime.Wheel.SteeringGuid);
        Check(ShimInput.PendingShared&&Runtime.Wheel.SteeringGuid==null&&!pendingTarget.Ready&&pendingTarget.Reason.StartsWith("Waiting for the shared controls profile"),
            "pending shared profile: a stale legacy Steer is not a follow-steering FFB target");
        Check(Runtime.Devices.ResolveForceTarget(false,legacy.ToString(),Runtime.Wheel.SteeringGuid).Guid==legacy,"pending shared profile: an explicit FFB wheel still wins");
        Set("_terminal",true);
        var stopped=Runtime.Devices.ResolveForceTarget(true,"",Runtime.Wheel.SteeringGuid);
        Check(ShimInput.StoppedBeforeOpen&&!stopped.Ready&&stopped.Reason.StartsWith("Shared input stopped before opening"),"stopped before opening (not refused): the legacy Steer stays blocked");
        Set("_terminal",false);Set("_refused",true);
        Check(!ShimInput.PendingShared&&Runtime.Devices.ResolveForceTarget(true,"",Runtime.Wheel.SteeringGuid).Guid==legacy,"an actual refusal (Native retained) follows the native Steer binding");
        Reset(dir);Runtime.Settings.InputBackend="Native";ShimInput.Start(AppContext.BaseDirectory,dir,Path.Combine(dir,"reports"));
        Runtime.Wheel!.Bindings.Steer=new(legacy,0,new(0,65535,32768));
        Check(!ShimInput.PendingShared&&Runtime.Wheel.SteeringGuid==legacy&&Runtime.Wheel.SteeringReason==null,"Native backend follows the native Steer binding as before");
        Set("<Owns>k__BackingField",true);Set("<SteeringGuid>k__BackingField",null!);
        Check(Runtime.Wheel.SteeringGuid==null&&Runtime.Wheel.SteeringReason!.StartsWith("The shared controls profile has no usable steering"),"owning without a steering identity names the shared profile, never the native Steer");

        var producer=new GamePadSystem();var pad=new GamePadSystem.Game_Pad();producer.Game_Pads.Add(pad);
        Reset(dir);Gamepad.all.Add(new());ShimInput.BeforeStock(producer);Next();
        Check(!ShimInput.Owns&&_opens==0&&Gamepad.all.Count==1,"unrelated current gamepad refuses before open and remains enabled/present");
        Reset(dir);pad.Controller=new();ShimInput.BeforeStock(producer);Next();
        Check(!ShimInput.Owns&&_opens==0&&pad.Controller!=null,"cached controller refuses without altering native identity");pad.Controller=null;
        Reset(dir);pad.LS=new(.2f,0);ShimInput.BeforeStock(producer);Next();
        Check(!ShimInput.Owns&&_opens==0&&pad.LS.x==.2f,"cached raw axes refuse without mutating native producer");pad.LS=default;
        Reset(dir);ShimInput.Poll();Check(_opens==0,"producer must be observed before native open");
        GameWindow.Window=0;ShimInput.BeforeStock(producer);Next();Check(_opens==0,"owned game window required");
        GameWindow.Window=123;Frame("steer",.4f);Frame("throttle",.7f);Frame("brake",.2f);Next();
        Check(ShimInput.Owns&&ShimInput.Ready&&_opens==1&&_profile.SequenceEqual(exact),"ordinary open owns exact bytes once");
        Check(Runtime.Wheel!.SteeringGuid==steering,"FFB follows shared profile exact GUID, not legacy JSON or product/name");
        Runtime.Wheel.Bindings.Steer=new(legacy,0,new(0,65535,32768));
        Check(Runtime.Wheel.SteeringGuid==steering&&Runtime.Wheel.SteeringReason==null,"owning: a stale legacy Steer never replaces the shared steering identity");
        Set("_terminal",true);
        Check(ShimInput.Owns&&!ShimInput.Ready&&!ShimInput.PendingShared&&Runtime.Wheel.SteeringGuid==steering,"stopped after owning: ownership stays sticky and never returns to the legacy Steer");
        Set("_terminal",false);
        Check(ShimActionMap.UnsupportedNames.All(id=>_unsupported.Contains(id+"=")),"unsupported action reasons supplied to Core");
        MenuCameraScript.Available=true;MenuCameraScript.Exiting=false;
        var daily=new DailyMessage();StartupWheelControls.Daily(daily)?.Restore();Runtime.Clock.Advance(.11);Next();StartupWheelControls.Daily(daily)?.Restore();
        Frame("confirm",1);Next();var startup=StartupWheelControls.Daily(daily);
        Check(startup!=null&&daily.MyControls!.ButtonStart,"shim Confirm reaches existing native startup consumer without a legacy binding");startup?.Restore();
        Check(!daily.MyControls!.ButtonStart,"native startup field restores after shim delivery");
        Frame("confirm",0);Next();var arcade=new ArcadeCarSelect();CustomWheelMenu.Arcade(arcade)?.Restore();Runtime.Clock.Advance(.11);Next();CustomWheelMenu.Arcade(arcade)?.Restore();
        Frame("navRight",1);Next();var custom=CustomWheelMenu.Arcade(arcade);
        Check(custom!=null&&arcade.field_Private_MenuControls_0!.DPadRight,"shim navigation reaches native car selection without legacy bindings");custom?.Restore();arcade.isActiveAndEnabled=false;Frame("navRight",0);Next();
        int ticked=_ticks;ShimInput.Poll();Runtime.Wheel.Button("Confirm");Check(_ticks==ticked,"one tick per Unity frame across consumers");
        Runtime.Wheel.Bindings.Buttons["Camera"]=new(steering,1);Runtime.Devices!.Pressed.Add(new(steering,1));
        Check(!Runtime.Wheel.Button("Camera",false),"active shim never delivers legacy DeviceHub button");
        Runtime.Wheel.Bindings.Buttons["Gear 1"]=new(steering,2);Check(!Runtime.Wheel.HPatternBound,"legacy gates cannot select H-pattern for a Sequential shared profile");

        var values=Enumerable.Range(0,18).Select(i=>new GamePadSystem.Actions("action"+i,0,false)).ToArray();
        values[16]=values[16] with {value=1,Pressed=true};values[7]=values[7] with {value=1,Pressed=true};values[11]=values[11] with {value=1,Pressed=true};
        var saved=values.ToArray();
        var car=new MainCar();var controls=new Controls{field_Private_MainCar_0=car,field_Private_Game_Pad_0=new(){PadActions=values}};car.MyControls=controls;Runtime.Local=car;
        Frame("handbrake",.35f);Next();var lease=Runtime.Wheel.Apply(controls);
        Check(lease!=null&&values[16].value==1&&values[17].value==0&&values[7].value==1&&values[6].value==.2f,"real WheelInput preserves native keyboard steering/throttle with shim brake");
        Check(values[11].Pressed&&lease!.Handbrake==.35f,"native keyboard horn and progressive shim handbrake retained");
        lease!.Restore();Check(values.SequenceEqual(saved),"production value-array lease restores native values byte-for-value");
        car.Aids!.Automatic=true;Frame("shiftUp",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[3].value==0&&ShimInput.TransmissionStatus.Contains("native gearbox is Automatic"),"Sequential profile cannot shift native Automatic; mismatch is explicit");lease!.Restore();
        car.Aids.Automatic=false;Next();lease=Runtime.Wheel.Apply(controls);Check(values[3].value==0,"held shift cannot cross Automatic-to-Manual transition");lease!.Restore();
        Frame("shiftUp",0);Next();Frame("shiftUp",1);Next();lease=Runtime.Wheel.Apply(controls);Check(values[3].value==1,"fresh sequential press reaches actual Manual car gearbox");lease!.Restore();
        car.TransmissionSystem!.Changing=true;Next();lease=Runtime.Wheel.Apply(controls);Check(values[3].value==0,"gear change in progress blocks repeated shift");lease!.Restore();
        car.TransmissionSystem.Changing=false;Next();lease=Runtime.Wheel.Apply(controls);Check(values[3].value==0,"still-held button cannot repeat after gear change finishes");lease!.Restore();
        Frame("shiftUp",0);Next();
        Frame("camera",1);Next();lease=Runtime.Wheel.Apply(controls);Check(values[5].Pressed,"fresh mapped camera reaches native action table without legacy binding dependency");lease!.Restore();
        Check(Runtime.Wheel.Button("Camera")&&!Runtime.Wheel.Button("Camera"),"button edge consumed once per action");
        Runtime.Focused=false;Next();Runtime.Focused=true;Next();Check(!Runtime.Wheel.Button("Camera",false),"held button cannot fire on focus return");
        Frame("camera",0);Next();Frame("camera",1);Next();Check(Runtime.Wheel.Button("Camera"),"neutral then press rearms after focus return");
        GameWindow.UnityFocused=false;Next();lease=Runtime.Wheel.Apply(controls);
        Check(Runtime.Focused&&ShimInput.Ready&&ShimInput.Axis("steer")==0&&!ShimInput.AxesLive&&!Runtime.Wheel.Button("Camera",false),"same-process console foreground keeps host alive but neutralizes shared actions and force samples");
        Check(lease!=null&&Runtime.Wheel.LastFor(car)==null,"console foreground cannot create a live shared driving sample");lease!.Restore();
        GameWindow.UnityFocused=true;Next();Check(!Runtime.Wheel.Button("Camera",false),"console-to-Unity focus requires fresh neutral before held button delivery");
        Frame("camera",0);Next();Frame("camera",1);Next();Check(Runtime.Wheel.Button("Camera"),"fresh camera press rearms after Unity focus return");
        Pause.Paused=true;Next();Check(Runtime.Wheel.Apply(controls)==null&&values.SequenceEqual(saved),"native pause gate leaves the car action table untouched");
        Frame("back",0);Next();Frame("back",1);Next();Check(Runtime.Wheel.Button("Back"),"paused context still permits a fresh menu Back press");
        Pause.Paused=false;Next();Check(!Runtime.Wheel.Button("Camera",false)&&!Runtime.Wheel.Button("Back",false),"held actions cannot leak from pause back into driving");
        Frame("camera",0,ShimStatus.ReadFailed);Next();Frame("camera",1);Next();Check(!Runtime.Wheel.Button("Camera",false),"unavailable then held is not a fresh release/press");
        Frame("steer",1,ShimStatus.ReadFailed);Next();lease=Runtime.Wheel.Apply(controls);Check(lease!=null&&Runtime.Wheel.LastFor(car)==null,"missing live driving axis blocks force sample rather than substituting legacy reader");lease!.Restore();
        var connected=new Gamepad();Gamepad.all.Add(connected);Next();
        Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1&&ReferenceEquals(Gamepad.all[0],connected),"new unrelated pad terminates shim while preserving native pad");
        Check(!Runtime.Wheel.Button("Camera",false)&&Runtime.Wheel.Apply(controls)==null,"terminal session cannot fall back to legacy wheel actions");
        Gamepad.all.Clear();Next();ShimInput.Stop("again");Check(_opens==1&&_stops==1,"terminal state neither reopens nor stops handle twice");

        Reset(dir);GameWindow.UnityFocused=false;ShimInput.BeforeStock(producer);Frame("steer",.7f);Next();Next();Next();
        Check(ShimInput.Ready&&_opens==1&&_openedWindow==123&&_ticks==3&&ShimInput.Axis("steer")==0&&ShimInput.Status.Contains("unfocused"),"owned main window permits ordinary open/ticks behind console without action delivery");
        int beforeInvalid=_ticks;GameWindow.Valid=false;ShimInput.Poll();
        Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1&&_ticks==beforeInvalid&&ShimInput.Axis("steer")==0,"window liveness/identity failure stops before another tick, including in the same frame");
        GameWindow.Valid=true;GameWindow.Window=456;Next();Check(_opens==1&&_stops==1,"a replacement Unity window cannot reopen a terminal host or fall back");
        Reset(dir);_invalidateAtOpen=true;ShimInput.BeforeStock(producer);Next();
        Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1&&_ticks==0,"window changing during native open closes the acquired host before its first tick");

        Reset(dir);ShimInput.BeforeStock(producer);_badHash=true;Next();Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1,"accepted-byte identity disagreement closes the owned handle, no legacy fallback");
        Reset(dir);ShimInput.BeforeStock(producer);Next();_tickResult=-4;Next();Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1,"native tick failure is sticky neutral");
        Reset(dir);ShimInput.BeforeStock(producer);Next();
        controls.field_Private_Game_Pad_0!.Controller=new();
        Check(ControlActions.For(controls)==null&&!ShimInput.Ready&&_stops==1,"production lookup rejects cached Controls fallback pad outside observed producer");
        controls.field_Private_Game_Pad_0.Controller=null;
        foreach(string mode in new[]{"Automatic","HPattern","Unavailable"})
        {
            Reset(dir);_mode=mode;ShimInput.BeforeStock(producer);Runtime.Local=car;Next();Frame("shiftUp",1);Frame("shiftDown",1);Next();lease=Runtime.Wheel!.Apply(controls);
            Check(lease!=null&&values[1].value==0&&values[3].value==0,"shared gear actions neutral for Core-reported "+mode+" profile");lease!.Restore();
            Check(ShimInput.Action("shiftUp").Status==ShimStatus.Unsupported&&ShimInput.TransmissionStatus.Contains(mode=="HPattern"?"H-pattern":mode),"sequential actions withheld for other transmission: "+mode);
            ShimInput.Stop("mode fixture complete");
        }
        // Shared H-pattern uses the same native gearbox emulator, but applies
        // pedal effects only to the wheel before native keyboard composition.
        Reset(dir);_mode="HPattern";ShimInput.BeforeStock(producer);Runtime.Local=car;
        car.Aids!.Automatic=false;car.TransmissionSystem!.Changing=false;car.MyGear=1;car.Gears=6;
        for(int i=0;i<values.Length;i++)values[i]=new GamePadSystem.Actions("hpattern"+i,0,false);
        Next();Frame("throttle",.7f);Frame("brake",.2f);Next();lease=Runtime.Wheel!.Apply(controls);
        Check(Runtime.Wheel.HPatternBound&&lease!=null&&values[7].value==0&&values[6].value==.2f,"H-pattern out of gear cuts wheel drive and keeps brake");lease!.Restore();
        Frame("gear3",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(lease!=null&&values[7].value==.7f&&values[3].value==1&&values[1].value==0,"held gear3 uses the production upward gearbox pulse");lease!.Restore();
        Check(values.All(a=>a.value==0&&!a.Pressed),"H-pattern synthesized shift and pedals restore the native action table");
        Frame("gear3",0);Next();lease=Runtime.Wheel.Apply(controls);lease!.Restore();
        Frame("reverse",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==.2f&&values[6].value==.7f,"shared reverse swaps wheel throttle and brake");lease!.Restore();
        Frame("gear1",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==0&&values[6].value==.2f,"conflicting H-pattern gates resolve to neutral rather than choosing reverse");lease!.Restore();
        Frame("reverse",0);Next();Frame("neutral",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==0&&values[3].value==0,"explicit neutral overrides a held forward gate");lease!.Restore();
        Frame("neutral",0);Frame("gear1",0);Next();car.Aids.Automatic=true;Frame("reverse",1);Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==.7f&&values[6].value==.2f&&ShimInput.TransmissionStatus.Contains("Automatic"),"native Automatic keeps wheel pedals unswapped and reports the mismatch");lease!.Restore();
        car.Aids.Automatic=false;Next();lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==.2f&&values[6].value==.7f,"Manual selection uses the physical reverse position without a release ritual");lease!.Restore();
        Frame("reverse",0);Next();Frame("reverse",1);Next();
        values[7]=values[7] with{value=1,Pressed=true};values[3]=values[3] with{value=1,Pressed=true};var heldKeyboard=values.ToArray();
        lease=Runtime.Wheel.Apply(controls);
        Check(values[7].value==1&&values[6].value==.7f&&values[3].value==1,"native keyboard throttle and shift remain independent of the wheel reverse gate");lease!.Restore();
        Check(values.SequenceEqual(heldKeyboard),"native keyboard restores exactly after shared H-pattern");
        Frame("gear1",1);Frame("reverse",0);Next();Runtime.Focused=false;Next();lease=Runtime.Wheel.Apply(controls);
        Check(!ShimInput.HPatternReady(car)&&values.SequenceEqual(heldKeyboard),"focus loss cancels wheel H-pattern without changing native keyboard state");lease?.Restore();
        Runtime.Focused=true;Next();lease=Runtime.Wheel.Apply(controls);
        Check(Runtime.Wheel.HeldGate(car)==1,"a gear held across focus loss remains engaged on return");lease?.Restore();
        Pause.Paused=true;Next();Pause.Paused=false;Next();
        Check(Runtime.Wheel.HeldGate(car)==1,"a gear held through pause remains engaged without moving the stick");
        Panel.Toggle();Next();Panel.CompleteClose(true);Next();
        Check(Runtime.Wheel.HeldGate(car)==1,"a gear held through the F6 panel remains engaged on close");
        Runtime.Local=null;Next();Runtime.Local=car;Next();
        Check(Runtime.Wheel.HeldGate(car)==1,"a gear held during loading is present on player spawn");
        foreach(string id in ShimActionMap.Names.Where(ShimActionMap.IsGate))Frame(id,0,ShimStatus.DeviceMissing);
        Next();Check(!ShimInput.HPatternGatesAvailable&&ShimInput.TransmissionStatus.Contains("Shifter gates unavailable"),"missing shifter differs from an available stick in neutral");
        lease=Runtime.Wheel.Apply(controls);Check(values[7].value==1&&values[3].value==1,"missing shifter leaves native keyboard throttle and shift available");lease?.Restore();
        _tickResult=-4;Next();Check(Runtime.Wheel.Apply(controls)==null&&values.SequenceEqual(heldKeyboard),"terminal shim loss does not fall back to legacy H-pattern");
        for(int i=0;i<values.Length;i++)values[i]=new GamePadSystem.Actions("action"+i,0,false);

        // The observed producer's keyboard overlay is independent of its raw
        // pad cache. Exercise that exact source, not only Controls' fallback.
        Reset(dir);_mode="Automatic";Runtime.Local=car;
        var keyboardValues=Enumerable.Range(0,18).Select(i=>new GamePadSystem.Actions("keyboard"+i,0,false)).ToArray();
        foreach(int i in new[]{0,3,6,11,17})keyboardValues[i]=keyboardValues[i] with{value=1,Pressed=true};
        var keyboardSaved=keyboardValues.ToArray();pad.AnyKey=true;pad.PadActions=keyboardValues;
        controls.field_Private_GamePadSystem_0=producer;controls.Controller_Int=0;
        ShimInput.BeforeStock(producer);Frame("steer",.4f);Frame("throttle",.7f);Frame("brake",.2f);Frame("handbrake",.35f);Frame("shiftUp",1);Next();
        Check(ShimInput.Ready&&_opens==1&&pad.AnyKey&&keyboardValues.SequenceEqual(keyboardSaved),"native AnyKey/PadActions keyboard overlay admits unchanged when raw gamepad fields are neutral");
        lease=Runtime.Wheel!.Apply(controls);
        Check(lease!=null&&keyboardValues[17].value==1&&keyboardValues[16].value==0&&keyboardValues[6].value==1&&keyboardValues[7].value==.7f,
            "observed native keyboard left/brake coexist with opposite shared steer and shared throttle");
        Check(lease!.Handbrake==1&&keyboardValues[0].Pressed&&keyboardValues[11].Pressed,"keyboard handbrake/horn pass through the production lease");
        Check(ShimInput.Action("shiftUp").Status==ShimStatus.Unsupported&&keyboardValues[3].value==1,
            "unsupported shared transmission does not erase the native keyboard shift action");
        lease.Restore();Check(keyboardValues.SequenceEqual(keyboardSaved)&&pad.AnyKey,"observed producer keyboard values and AnyKey restore unchanged");
        _tickResult=-4;Next();
        Check(ShimInput.Owns&&!ShimInput.Ready&&Runtime.Wheel.Apply(controls)==null&&keyboardValues.SequenceEqual(keyboardSaved)&&pad.AnyKey,
            "terminal shared transport leaves native keyboard table intact while blocking legacy wheel fallback");
        controls.field_Private_GamePadSystem_0=null;pad.AnyKey=false;pad.PadActions=null;
        TrialCore(dir,Check);
        TrialCarCore(dir,Check);
        TrialAppliedHPattern(dir,Check);
        Reset(dir);Runtime.Settings.InputBackend="Native";ShimInput.Start(AppContext.BaseDirectory,dir,dir);Next();
        Check(!ShimInput.Owns&&_opens==0,"default standalone route never loads/opens an input host");
        Runtime.Log.Errors.Clear();Runtime.Log.Warnings.Clear();
        Console.WriteLine($"PASS {first} production shim/lease checks; fake handle calls, no device/game/force. Native ABI result reported separately.");
    }
    private static void TrialCore(string dir,Action<bool,string> check)
    {
        // Regress the exact release-arm failure even without the optional native
        // Core fixture. TestNeutral does not become ordinary Live input.
        var gate=new ShimButtonGate();var actions=new ShimAction[31];int confirm=ShimActionMap.Index("confirm");
        actions[confirm]=new(0,ShimStatus.TestNeutral);gate.Observe(actions,0,true);
        actions[confirm]=new(1,ShimStatus.Live,true);gate.Observe(actions,0,true);
        check(!gate.Read("Confirm",false),"ordinary TestNeutral cannot arm injection");
        actions[confirm]=new(0,ShimStatus.TestNeutral);gate.Observe(actions,0,true,1UL<<confirm);
        actions[confirm]=new(1,ShimStatus.Live,true);gate.Observe(actions,0,true,1UL<<confirm);
        check(gate.Read("Confirm",true)&&!gate.Read("Confirm",true),"owned known-neutral arms exactly one first injected edge");
        string? source=Environment.GetEnvironmentVariable("WODEN_CORE_MENU_FRAMES"),profile=Environment.GetEnvironmentVariable("WODEN_CORE_MENU_PROFILE");
        if(source==null||profile==null)return;
        Panel.Close(false);Reset(dir);Runtime.DiagnosticNoForce=true;Runtime.Settings.FfbEnabled=Runtime.Settings.TelemetryEnabled=Runtime.Settings.Record=false;
        File.WriteAllBytes(Path.Combine(dir,"dbce-controls.ini"),File.ReadAllBytes(profile));
        string session=Path.Combine(dir,"trial-session.txt"),batch=Path.Combine(dir,"trial-batch.txt");File.WriteAllText(session,"fixture");
        ShimInput.ConfigureMenuTrial(new string('a',32),session,batch,DateTimeOffset.UtcNow.AddMinutes(2),true);
        Replace("_open2",nameof(Open2));Replace("_tick",nameof(CoreTick));
        var producer=new GamePadSystem();producer.Game_Pads.Add(new());ShimInput.BeforeStock(producer);
        var daily=new DailyMessage();var title=new TitleScreenScript{FramesToDemo=100000};
        var garage=new GameObject();var workshop=new GameObject();garage.Right=workshop;workshop.Left=garage;
        var events=new UnityEngine.EventSystems.EventSystem{currentSelectedGameObject=garage};
        UnityEngine.EventSystems.EventSystem.current=null;
        Input.Held=false;Input.Keys.Clear();Input.Down.Clear();StartupWheelControls.Reset();MenuCameraScript.Available=true;MenuCameraScript.Exiting=false;
        int phaseBefore=-1,right=0,left=0;long delivered=MenuNavigation.Delivered;
        using var input=new BinaryReader(File.OpenRead(source));
        while(input.BaseStream.Position<input.BaseStream.Length)
        {
            int phase=input.ReadInt32();_coreFrame=input.ReadBytes(296);check(_coreFrame.Length==296,"complete actual-Core ABI frame");
            if(phase!=phaseBefore)
            {
                if(phase<3)ShimInput.MenuTrialScope(phase+1);
                if(phase==4)ShimInput.MenuTrialScope(4);
                if(phase==2)UnityEngine.EventSystems.EventSystem.current=events;
                phaseBefore=phase;
            }
            Runtime.Clock.Advance(.01);Next();
            if(phase==0){bool original=DailyMessageInputHook.Prefix(daily,out var lease);if(original)daily.Update();DailyMessageInputHook.Postfix(lease);}
            if(phase==1){bool original=TitleScreenInputHook.Prefix(title,out var lease);if(original)title.FixedUpdate();TitleScreenInputHook.Postfix(lease);}
            if(phase is 2 or 3)
            {
                var before=events.currentSelectedGameObject;MenuNavigation.Update();
                if(before!=events.currentSelectedGameObject){if(phase==2)right++;else left++;}
            }
            check(!Runtime.Wheel!.Button("Back",false)&&!Runtime.Wheel.Button("Camera",false)&&ShimInput.Axis("throttle")==0,"trial excludes unrelated actions/driving");
            if(phase>=2)check(!Runtime.Wheel.Button("Confirm",false)&&!Runtime.Wheel.Button("Pause",false),"map cannot submit/start");
        }
        check(_latchedArgs&&_opens==1&&_ticks==500,"real 500 Core frames consume verified latched-only Open2 arguments");
        check(daily.Loads==1&&title.StartLoads==1&&!daily.MyControls!.ButtonStart&&!title.MyControls!.ButtonStart,"actual startup prefixes drive each native fixture once and restore fields");
        check(right==1&&left==1&&MenuNavigation.Delivered-delivered==2&&events.currentSelectedGameObject==garage,"actual WheelInput and MenuNavigation move native handler selection right then left once");
        check(ShimInput.Run==4&&ShimInput.KnownNeutral("confirm")==false&&!Runtime.Wheel!.Button("Menu right",false),"Core Completed is terminal neutral after narrowing scope");
        bool widened=false;try{ShimInput.MenuTrialScope(3);}catch(InvalidOperationException){widened=true;}check(widened,"completed scope cannot widen");
        Input.Held=true;check(!ShimInput.TrialNativeMenuAllowed()&&!ShimInput.Ready,"physical keyboard interrupts diagnostic trial");Input.Held=false;
        UnityEngine.EventSystems.EventSystem.current=null;Replace("_tick",nameof(Tick));_coreFrame=null;
    }
    internal static void PanelCheck(string dir,Action<bool,string> check)
    {
        Panel.Close(false);Reset(dir);
        Runtime.Settings.UiPage="Controls";Panel.Toggle();GUI.Commands.Clear();Event.current=new();Panel.Draw();
        check(GUI.Commands.Any(c=>c.Text.Contains("waiting for native input producer")),"pending shim request is visible on Controls before ownership");
        check(GUI.Commands.Any(c=>c.Text.Contains("Native bindings below remain active"))&&GUI.Commands.Any(c=>c.Text=="Bind"),"pre-admission Controls clearly retains usable standalone bindings");
        var refusedProducer=new GamePadSystem();refusedProducer.Game_Pads.Add(new(){Controller=new()});ShimInput.BeforeStock(refusedProducer);
        GUI.Commands.Clear();Panel.Draw();check(GUI.Commands.Any(c=>c.Text.Contains("Shim refused; Native retained")),"refused shim reason remains visible on Controls before ownership");
        Panel.Close(false);Reset(dir);
        var producer=new GamePadSystem();producer.Game_Pads.Add(new());ShimInput.BeforeStock(producer);Next();
        Panel.Toggle();GUI.Commands.Clear();Event.current=new();int ticks=_ticks;Panel.Draw();
        check(GUI.Commands.Any(c=>c.Text.Contains("Shim pilot active")),"shim F6 shows current backend");
        check(GUI.Commands.Any(c=>c.Text.Contains("dbce-controls.ini")),"shim F6 names profile and restart boundary");
        check(!GUI.Commands.Any(c=>c.Text=="Bind Steer"||c.Text=="Save calibration"),"shim F6 does not offer inactive legacy calibration");
        check(ticks==_ticks,"drawing shim status never polls native input");
        File.WriteAllText(Path.Combine(dir,"shim-panel.json"),System.Text.Json.JsonSerializer.Serialize(GUI.Commands,new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Panel.Close(false);ShimInput.Stop("fixture complete");
    }
}
