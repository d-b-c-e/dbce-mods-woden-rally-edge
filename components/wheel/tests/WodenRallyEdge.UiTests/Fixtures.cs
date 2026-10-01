#pragma warning disable CS0649
using System.Diagnostics;
using WodenRallyEdge.Core;

// Minimal managed fixture for the exact production panel/capture/shortcut source.
// No game, IL2CPP, hardware DLL, device enumeration or output adapter is loaded.
namespace UnityEngine
{
    public sealed class GameObject { public bool activeInHierarchy=true; }
    public record struct Color(float r,float g,float b,float a=1) { public static Color white => new(1,1,1); }
    public record struct Rect(float x,float y,float width,float height);
    public record struct Vector2(float x,float y);
    public enum EventType { Repaint, MouseDown, MouseDrag, MouseUp, Used, KeyDown, KeyUp, ScrollWheel }
    public enum KeyCode { None, Tab, Return, Space, LeftArrow, RightArrow, PageDown, PageUp, F6, Escape }
    public static class Time {public static int frameCount;}
    public static class Input
    {
        public static bool FailRead,Held;
        public static readonly HashSet<KeyCode> Keys=new(),Down=new();
        public static bool anyKey=>FailRead?throw new InvalidOperationException("fixture legacy keyboard failed"):Held||Keys.Count>0;
        public static bool GetKeyDown(KeyCode key)=>FailRead?throw new InvalidOperationException("fixture legacy keyboard failed"):Down.Contains(key);
        public static bool GetKey(KeyCode key)=>Keys.Contains(key);
        public static bool GetMouseButton(int button)=>false;
    }
    public sealed class Event
    { public static Event current=new(); public EventType type=EventType.Repaint; public int button; public Vector2 mousePosition,delta; public KeyCode keyCode; public bool shift; public void Use()=>type=EventType.Used; }
    public sealed class GUIContent(string text) { public string text=text; }
    public sealed class GUIStyle { public int fontSize; public bool wordWrap; public State normal=new(); public sealed class State { public Color textColor; } }
    public static class Screen { public static int width=1280,height=720; }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static bool visible; public static CursorLockMode lockState; }
    public static class GUIUtility { public static int keyboardControl; }
    public sealed class Texture2D { public static Texture2D whiteTexture=new(); }
    public sealed record DrawCommand(string Kind,Rect Rect,string Text,Color Color,int FontSize=0,bool Wrap=false);
    public static class GUI
    {
        public static Color color=Color.white;public static bool enabled=true;
        public static readonly List<DrawCommand> Commands=new();
        public static string? TextReplacement;
        public static void DrawTexture(Rect r,Texture2D t)=>Commands.Add(new("rect",r,"",color));
        public static void Label(Rect r,GUIContent text,GUIStyle style)=>Commands.Add(new("text",r,text.text,style.normal.textColor,style.fontSize,style.wordWrap));
        public static string TextArea(Rect r,string text,int max) { Commands.Add(new("text",r,text,Color.white,18));if(TextReplacement is { } replacement){TextReplacement=null;return replacement;}return text; }
    }
}
namespace UnityEngine.EventSystems
{
    public sealed class EventSystem { public static EventSystem? current; public bool enabled=true,isFocused=true; public UnityEngine.GameObject? currentSelectedGameObject,firstSelectedGameObject; public void SetSelectedGameObject(UnityEngine.GameObject g)=>currentSelectedGameObject=g; }
    public class BaseEventData(EventSystem events) { public EventSystem Events=events; }
    public enum MoveDirection {Up,Down,Left,Right}
    public sealed class AxisEventData(EventSystem events):BaseEventData(events) {public MoveDirection moveDir;public UnityEngine.Vector2 moveVector;}
    public interface IMoveHandler {} public interface ISubmitHandler {} public interface ICancelHandler {}
    public static class ExecuteEvents
    {
        public static readonly object submitHandler=new(),cancelHandler=new(),moveHandler=new();public static readonly List<string> Delivered=new();
        public static UnityEngine.GameObject ExecuteHierarchy<T>(UnityEngine.GameObject g,BaseEventData data,object handler){Delivered.Add(typeof(T).Name);return g;}
    }
}
namespace UnityEngine.InputSystem
{
    public enum Key {None,F6,F8,Escape,A,B,C,U,J,LeftShift,RightShift,LeftCtrl,RightCtrl,LeftAlt,RightAlt,LeftMeta,RightMeta,Numpad8,Numpad2,Numpad9,Numpad7,Numpad4,Numpad6,Numpad1,Numpad3,NumpadPlus,NumpadMinus,Numpad0}
    public sealed class KeyControl { public bool wasPressedThisFrame,isPressed; }
    public sealed class Keyboard
    { public static Keyboard? current=new();public bool FailF8;private readonly Dictionary<Key,KeyControl> keys=new();public KeyControl anyKey=>new(){isPressed=keys.Values.Any(k=>k.isPressed)};public KeyControl this[Key key] {get{if(key==Key.F8&&FailF8)throw new InvalidOperationException("fixture F8 read failed");if(!keys.ContainsKey(key))keys[key]=new();return keys[key];}}public void Clear(){foreach(var k in keys.Values){k.wasPressedThisFrame=false;k.isPressed=false;}} }
    public sealed class Mouse {public static Mouse? current; public KeyControl leftButton=new(),rightButton=new(),middleButton=new();}
}
namespace WodenRallyEdge
{
    internal static class Plugin {internal const string Version="0.2.12";}
    internal static class TimingDiagnostics {internal static double PollMs;}
    // Native-behavior fixtures from guarded GameAssembly: DailyMessage.Update
    // RVA 0x456090 (ready + flags/legacy anyKey), title FixedUpdate 0xAFB560
    // (autonomous frame timeout, Start/legacy Return with Escape exclusion).
    internal sealed class DailyMessage
    {
        internal bool Ready=true,Confirm,Start;internal int Loads;
        public void Update(){if(Ready&&(Confirm||Start||UnityEngine.Input.anyKey)){Ready=false;Loads++;}}
    }
    internal sealed class TitleScreenScript
    {
        internal int FrameCount,FramesToDemo=3,DemoLoads,StartLoads;internal bool DemoStarted,Starting,Start,Back;
        public void FixedUpdate()
        {
            if(!DemoStarted)FrameCount++;
            if(FrameCount>FramesToDemo){FrameCount=0;DemoStarted=true;DemoLoads++;}
            if((Start||UnityEngine.Input.GetKey(UnityEngine.KeyCode.Return))&&!UnityEngine.Input.GetKey(UnityEngine.KeyCode.Escape)&&!Back&&!Starting){Starting=true;StartLoads++;}
        }
    }
    internal sealed class GamePadSystem
    {
        internal readonly List<Game_Pad> Game_Pads=new();
        internal sealed class Game_Pad
        {
            internal bool AnyKey,A,B,X,Y,Back,Start,LB,RB,L3,R3,Dpad_Up,Dpad_Down,Dpad_Left,Dpad_Right;
            internal UnityEngine.Vector2 LS,RS;internal float LT,RT;
            internal float[]? InputFloats;
            internal Actions[]? PadActions;
        }
        internal record struct Actions(string name,float value,bool Pressed,int AssignedFloat=0,UnityEngine.KeyCode KeyBoard_Key=UnityEngine.KeyCode.None,bool Available=true);
    }
    internal static class UiNative
    {internal static bool CursorVisible=>UnityEngine.Cursor.visible;internal static void CursorLock(UnityEngine.CursorLockMode mode)=>UnityEngine.Cursor.lockState=mode;internal static void Style(UnityEngine.GUIStyle s,int size,bool wrap){s.fontSize=size;s.wordWrap=wrap;}}
    internal sealed class Pause
    {internal static bool Paused;internal bool PhotomodeActive;internal void SetPause()=>Paused=true;internal void UnsetPause()=>Paused=false;}
    internal sealed class MainCar
    {internal enum CarStatus {WARMING,RACE,END,DESTROYED} internal CarStatus Status=CarStatus.RACE;internal Controls? MyControls;internal int GetInstanceID()=>1;}
    internal sealed class Controls {internal MainCar? field_Private_MainCar_0;internal Pause? PauseScript;}
    internal sealed class InputLease {internal float Handbrake;internal InputLease(GameAction[] a,float s,float t,float b,WheelInput i,MainCar car){i.HandbrakeCar=null;i.HandbrakeAmount=0;} }
    internal sealed class GameAction {internal string name="fixture";}
    internal static class ControlActions {internal static GameAction[]? For(Controls c)=>null;}
    internal static class CountdownTimerAssist {internal static string Status=>"Fixture: assist waiting for gameplay";}
    internal static class MountedCamera
    {internal static bool PlayerOwned=true;internal static CameraCycle Cycle=new();internal static string Status=>"Fixture mounted view";internal static CameraPose Pose(bool bumper)=>Runtime.Settings.GetCameraPose(bumper);}
    internal sealed class ForceController
    {
        internal int Reconnects,Changes;internal long Attempts=>0;internal long Failures=>0;internal float Sent=>0;
        internal string Status=>Runtime.Settings.FfbEnabled?"FFB inactive — settings open":"FFB off";
        internal void Suspend(string reason){}internal void Reconnect(string reason)=>Reconnects++;
        internal void SetEnabled(bool enabled){Runtime.Settings.FfbEnabled=enabled;Runtime.Settings.Save();Changes++;}internal void Panic()=>SetEnabled(false);
    }
    internal sealed class DeviceHub
    {
        internal sealed class Info {internal Guid? InstanceGuid;internal string Name="MOZA R12";internal bool ForceFeedback=true;}
        internal sealed class Device {internal Info Info=new();internal int[] Axes=new int[8];internal bool Ok=true;}
        internal readonly List<Device> Devices=new();internal int Refreshes,Polls;internal Action? OnPoll;internal readonly List<ButtonBinding> Pressed=new();
        internal string Status=>"Fixture devices only";
        internal void Poll(){Polls++;OnPoll?.Invoke();}internal void Refresh()=>Refreshes++;internal void ClearPresses()=>Pressed.Clear();
        internal string Describe(Guid guid)=>Devices.FirstOrDefault(d=>d.Info.InstanceGuid==guid)?.Info.Name??"Disconnected "+guid.ToString()[..8];
        internal Dictionary<(Guid,int),int> AxesSnapshot()=>Devices.SelectMany(d=>Enumerable.Range(0,8).Select(i=>new KeyValuePair<(Guid,int),int>((d.Info.InstanceGuid!.Value,i),d.Axes[i]))).ToDictionary();
        internal bool TryAxis(AxisBinding? b,out float value){value=0;var d=Devices.FirstOrDefault(d=>d.Info.InstanceGuid==b?.DeviceGuid);if(b==null||d?.Ok!=true)return false;value=(float)b.Normalize(d.Axes[b.Axis]);return true;}
        internal IEnumerable<ButtonBinding> PressedButtons()=>Pressed;
          internal bool Button(ButtonBinding b,bool edge,string action="default")=>Pressed.Contains(b);
        internal ForceTarget ResolveForceTarget(bool f,string id,Guid? s)=>ForceSelection.Resolve(f,id,s,Devices.Select(d=>new ForceCandidate(d.Info.InstanceGuid!.Value,d.Info.Name,d.Info.ForceFeedback)));
    }
    internal static class Runtime
    {
        internal static Settings Settings=null!;internal static WheelInput? Wheel;internal static DeviceHub? Devices=new();internal static ForceController? Force=new();internal static TelemetryOutput? Output;
        internal static bool Focused=true;internal static MainCar? Local;internal static readonly TestClock Clock=new();internal static Log Log=new();
        internal static PlayerControlState ControlState(MainCar c)=>new(PlayerPhase.Racing,true,true,false,false,false,false,false,false);
        internal static bool CameraAvailable(MainCar c)=>true;
    }
    internal sealed class TestClock {private readonly Stopwatch watch=Stopwatch.StartNew();private double offset;internal TimeSpan Elapsed=>watch.Elapsed+TimeSpan.FromSeconds(offset);internal void Advance(double seconds)=>offset+=seconds;}
    internal sealed class Log {internal readonly List<string> Errors=new(),Warnings=new(),Infos=new();internal void LogInfo(string s)=>Infos.Add(s);internal void LogWarning(string s)=>Warnings.Add(s);internal void LogError(string s)=>Errors.Add(s);}
}
