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
    public static class Time {public static int frameCount=1;}
    public class Transform {public string name="node"; public Transform parent=null!;}
    public class GameObject {public Transform transform=new();}
}
namespace UnityEngine.EventSystems {public class EventSystem {public static EventSystem? current; public UnityEngine.GameObject? currentSelectedGameObject;}}
namespace WodenRallyEdge {
    public static class Runtime {public static DeviceHub Devices=new(); public static bool DiagnosticNoForce; public static bool Focused=>true;}
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
