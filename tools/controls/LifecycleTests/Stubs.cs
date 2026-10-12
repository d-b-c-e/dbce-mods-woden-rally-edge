// Engine/loader boundary only. The addon, protocol and native client are the
// production sources. No game assemblies or device calls in this executable.
namespace BepInEx {
    [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin:Attribute {public BepInPlugin(string id,string name,string version){}}
    [AttributeUsage(AttributeTargets.Class)] public class BepInDependency:Attribute {public BepInDependency(string id,DependencyFlags flags){} public enum DependencyFlags {HardDependency}}
    public static class Paths {public static string ConfigPath="", GameRootPath="";}
}
namespace BepInEx.Unity.IL2CPP {
    public class Logger {public void LogInfo(object value){} public void LogError(object value){}}
    public class BasePlugin {public Logger Log=new(); public virtual void Load(){} public virtual bool Unload()=>true;}
}
namespace HarmonyLib {
    public class Harmony {public Harmony(string id){} public void Patch(System.Reflection.MethodBase target,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null){}}
    public class HarmonyMethod {public HarmonyMethod(Type type,string name){} public int priority;}
    public static class Priority {public const int First=800, Last=0;}
}
namespace UnityEngine {
    public struct Vector2 {public float x,y;}
    public static class ScreenCapture {public static string? LastPath;public static void CaptureScreenshot(string path,int size){LastPath=path;}}
    public static class Time {public static int frameCount=1;}
    public class Transform {public string name="node"; public Transform parent=null!;}
    public class GameObject {public Transform transform=new();}
}
namespace UnityEngine.InputSystem {public static class Gamepad {public static List<object> all=new();}}
public class GamePadSystem {public List<Game_Pad> Game_Pads=new();public class Game_Pad {
    public object? Controller;public UnityEngine.Vector2 LS,RS;public float LT,RT;
    public bool A,B,X,Y,Back,Start,LB,RB,L3,R3,Dpad_Up,Dpad_Down,Dpad_Left,Dpad_Right,AnyKey;
}}
namespace UnityEngine.EventSystems {public class EventSystem {public static EventSystem? current; public UnityEngine.GameObject? currentSelectedGameObject;}}
namespace WodenRallyEdge {
    public static class Runtime {public static DeviceHub Devices=new(); public static bool DiagnosticNoForce; public static bool Focused=>true;
        public static object? Local;public static Force Force=new();public static Output Output=new();public static Settings Settings=new();}
    public class Force {public long Attempts,Opens,Failures;public bool Connected;public float Sent;}
    public class Output {public long ForzaPackets;}
    public class Settings {public string UiPage="Setup";}
    public static class Panel {public static bool Open{get;set;}public static void Toggle(){Open=!Open;}
        private static void Toggle(float x,float y,float width,string text,ref bool value)=>throw new Exception("Renderer widget is not the panel entry point");}
    public static class ShimInput {
        public static GamePadSystem? _producer;public static int _frame;public static IntPtr _handle=new(7);public static int Stops;
        public static bool Owns{get;set;} public static bool Ready{get;set;} public static string Status=>Ready?"active":"refused";
        public static Guid? SteeringGuid=>null;public static string TransmissionStatus=>"Sequential";
        public static void Stop(string reason){Stops++;Ready=false;}
        public static object Action(string id)=>new ActionValue(0,"Live",false);
        public record ActionValue(float Value,string Status,bool Injected);
    }
    public static class ShimNative {public static string ProfileInfo(IntPtr h)=>"ok=1";public static string StartReport(IntPtr h)=>"ordinary";public static string DeviceReports(IntPtr h)=>"physical";}
    public class DeviceHub {
        public System.Collections.IEnumerable Devices=Array.Empty<object>(); public int Closes;
        public void CloseReaders(){Closes++;Devices=Array.Empty<object>();}
        public class Device {public int Slot=1; public Info Info=new(); public bool Ok=true; public int[] Axes=new int[8],Pov=new int[4];public byte[] Physical=new byte[128];}
        public class Info {public Guid? InstanceGuid=Guid.Parse("d71b8350-61b7-11f1-8001-444553540000");}
    }
    public static class MenuNavigation {public static string Status=>"fixture";}
    public static class StagePlayback {public static Adapter Target=new();public class Adapter {public bool Muted;public void MuteOutputs(){Muted=true;}}}
    public class WheelInput {public AppliedInput _last=new();}
    public record AppliedInput {public float Steer{get;set;} public float Throttle{get;set;}public float Brake{get;set;}public float Handbrake{get;set;}public int Car{get;set;}public double At{get;set;}}
}
