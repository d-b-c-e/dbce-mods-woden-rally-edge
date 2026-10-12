using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Source-linked production backend, ABI decoder, WheelInput and scoped native
// action lease. Only native handle calls are replaced. No game, device open,
// worker, OS input or force call occurs; the DLL is loaded for ABI checks only.
internal static class ShimChecks
{
    private static readonly ShimAction[] FrameActions = new ShimAction[ShimActionMap.Count];
    private static byte[] _profile = Array.Empty<byte>();
    private static int _opens, _ticks, _stops, _tickResult;
    private static string _unsupported = "";
    private static bool _badHash;
    private static string _mode = "Sequential";
    private static void Set(string field, object value) => typeof(ShimInput).GetField(field, BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,value);
    private static void Replace(string field, string method)
    {
        var f=typeof(ShimNative).GetField(field,BindingFlags.Static|BindingFlags.NonPublic)!;
        f.SetValue(null,Delegate.CreateDelegate(f.FieldType,typeof(ShimChecks).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!));
    }
    private static int Open(string folder,string nonce,byte[] profile,int length,string unsupported,IntPtr window,out IntPtr handle,byte[] why,int whyLength)
    { _opens++;_profile=profile;_unsupported=unsupported;handle=new IntPtr(77);return 0; }
    private static int Tick(IntPtr handle,byte[] frame)
    {
        _ticks++; if(_tickResult!=0)return _tickResult;
        BitConverter.GetBytes(ShimActionMap.Abi).CopyTo(frame,4);
        BitConverter.GetBytes((ulong)_ticks).CopyTo(frame,8);
        for(int i=0;i<FrameActions.Length;i++)
        {int at=48+i*8;BitConverter.GetBytes(FrameActions[i].Value).CopyTo(frame,at);frame[at+4]=(byte)FrameActions[i].Status;frame[at+5]=0;}
        return 0;
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
        Set("<Owns>k__BackingField",false);Set("<SteeringGuid>k__BackingField",null!);
        _opens=_ticks=_stops=_tickResult=0;_badHash=false;_mode="Sequential";Gamepad.all.Clear();GameWindow.Window=123;Runtime.Focused=true;Runtime.Local=null;Pause.Paused=false;
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
        Check(ShimNative.Load(AppContext.BaseDirectory)==null,"pinned native DLL exports/layout/action order match ABI 2 (no open)");
        Replace("_open",nameof(Open));Replace("_tick",nameof(Tick));Replace("_profile",nameof(Profile));Replace("_startReport",nameof(Report));Replace("_stop",nameof(Stop));
        var steering=Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
        string section="[Controls]\r\nsteer=axis 0 dev="+steering.ToString("B")+" prod={bbbbbbbb-1111-2222-3333-444444444444} range=0..65535 rest=32768 travel=+1 calibrated\r\n";
        byte[] exact=Encoding.UTF8.GetBytes(section);
        File.WriteAllText(Path.Combine(dir,"dbce-controls.ini"),"[Other]\r\nignored=1\r\n"+section+"[After]\r\nignored=2\r\n",new UTF8Encoding(true));
        Check(ShimProfile.Section(File.ReadAllBytes(Path.Combine(dir,"dbce-controls.ini"))).SequenceEqual(exact),"BOM and unrelated sections preserve exact Controls bytes");
        bool duplicate=false;try{ShimProfile.Section(Encoding.UTF8.GetBytes(section+section));}catch(InvalidDataException){duplicate=true;}
        Check(duplicate,"duplicate section refused");
        Check(new ShimAction(float.NaN,ShimStatus.Live).Applied==0 && new ShimAction(1,ShimStatus.Stale).Applied==0,"nonfinite/unavailable cannot deliver");
        Check(ShimActionMap.Compose(float.NaN,float.PositiveInfinity,0,1,0,.5f)==(1,0,.5f),"finite keyboard composition");

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
        Runtime.Wheel.Bindings.Buttons["Gear 1"]=new(steering,2);Check(!Runtime.Wheel.HPatternBound,"legacy gate emulation cannot override unsupported shim H-pattern");

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
        Frame("camera",0,ShimStatus.ReadFailed);Next();Frame("camera",1);Next();Check(!Runtime.Wheel.Button("Camera",false),"unavailable then held is not a fresh release/press");
        Frame("steer",1,ShimStatus.ReadFailed);Next();lease=Runtime.Wheel.Apply(controls);Check(lease!=null&&Runtime.Wheel.LastFor(car)==null,"missing live driving axis blocks force sample rather than substituting legacy reader");lease!.Restore();
        var connected=new Gamepad();Gamepad.all.Add(connected);Next();
        Check(ShimInput.Owns&&!ShimInput.Ready&&_stops==1&&ReferenceEquals(Gamepad.all[0],connected),"new unrelated pad terminates shim while preserving native pad");
        Check(!Runtime.Wheel.Button("Camera",false)&&Runtime.Wheel.Apply(controls)==null,"terminal session cannot fall back to legacy wheel actions");
        Gamepad.all.Clear();Next();ShimInput.Stop("again");Check(_opens==1&&_stops==1,"terminal state neither reopens nor stops handle twice");

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
            Check(ShimInput.Action("shiftUp").Status==ShimStatus.Unsupported&&ShimInput.TransmissionStatus.Contains(mode),"unsupported transmission is visible: "+mode);
            ShimInput.Stop("mode fixture complete");
        }
        Reset(dir);Runtime.Settings.InputBackend="Native";ShimInput.Start(AppContext.BaseDirectory,dir,dir);Next();
        Check(!ShimInput.Owns&&_opens==0,"default standalone route never loads/opens an input host");
        Runtime.Log.Errors.Clear();Runtime.Log.Warnings.Clear();
        Console.WriteLine($"PASS {first} production shim/lease checks; native ABI only, fake handle calls, no device/game/force.");
    }
    internal static void PanelCheck(string dir,Action<bool,string> check)
    {
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
