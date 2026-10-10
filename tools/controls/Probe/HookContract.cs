using System.Reflection;

namespace Woden.ControlsTesting;

public static class HookContract
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    public static int Verify(Assembly mod, Assembly force)
    {
        int count=0;
        void Require(bool ok,string what) { count++; if(!ok) throw new MissingMemberException("Controls probe seam changed: "+what); }
        Type T(string name)=>mod.GetType("WodenRallyEdge."+name,true)!;
        MethodInfo M(string type,string method,Type returns,params string[] args)
        {
            var m=T(type).GetMethod(method,All);
            Require(m is not null && m.ReturnType==returns && m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(args),type+"."+method);
            return m!;
        }
        foreach(var n in new[]{"Update","Stop"}) M("Runtime",n,typeof(void));
        M("DeviceHub","Poll",typeof(void)); M("DeviceHub","CloseReaders",typeof(void));
        M("DeviceHub","TryAxis",typeof(bool),"WodenRallyEdge.Core.AxisBinding","System.Single&");
        M("WheelInput","Button",typeof(bool),"System.String","System.Boolean");
        var apply=T("WheelInput").GetMethod("Apply",All);
        Require(apply?.GetParameters().Length==1 && apply.GetParameters()[0].ParameterType.Name=="Controls" && apply.ReturnType.Name=="InputLease","WheelInput.Apply");
        foreach(var n in new[]{"BeginAxis","BeginButton","FinishAxis","TryCommit","RetrySave"}) Require(T("WheelInput").GetMethod(n,All)!=null,"WheelInput."+n);
        foreach(var n in new[]{"Steer","Throttle","Brake","Handbrake","Car","At"}) Require(T("AppliedInput").GetProperty(n,All)!=null,"AppliedInput."+n);
        Require(T("WheelInput").GetField("_last",All)?.FieldType==T("AppliedInput"),"WheelInput._last");
        var sampler=T("GameSampler").GetMethod("Read",All);
        Require(sampler?.ReturnType.FullName=="WodenRallyEdge.Core.TelemetrySample" &&
            sampler.GetParameters().Select(p=>p.ParameterType.Name).SequenceEqual(new[]{"MainCar","AppliedInput"}),"GameSampler.Read");
        var sample=sampler!.ReturnType;
        Require(typeof(IDictionary<string,double>).IsAssignableFrom(sample.GetProperty("Channels")?.PropertyType),"sample.Channels");
        foreach(var (name,type) in new[]{("CarInstanceId",typeof(int)),("Sequence",typeof(long)),("ElapsedSeconds",typeof(double)),
            ("SimulationSeconds",typeof(double)),("State",typeof(string)),("Phase",typeof(string))})
            Require(sample.GetProperty(name)?.PropertyType==type,"sample."+name);
        foreach(var n in new[]{"Settings","Devices","DiagnosticNoForce","_recordLaunch"}) Require(T("Runtime").GetField(n,All)!=null,"Runtime."+n);
        Require(T("Runtime").GetProperty("Focused",All)?.PropertyType==typeof(bool),"Runtime.Focused");
        foreach(var n in new[]{"FfbEnabled","TelemetryEnabled","Record"}) Require(T("Settings").GetField(n,All)?.FieldType==typeof(bool),"Settings."+n);
        var device=T("DeviceHub").GetNestedType("Device",All)!;
        foreach(var (name,type) in new[]{("Slot",typeof(int)),("Axes",typeof(int[])),("Pov",typeof(int[])),("Physical",typeof(byte[])),("Ok",typeof(bool))})
            Require(device.GetField(name,All)?.FieldType==type,"Device."+name);
        Require(device.GetField("Info",All)?.FieldType.GetField("InstanceGuid")?.FieldType==typeof(Guid?),"Device.Info.InstanceGuid");
        Require(T("DeviceHub").GetField("Devices",All)!=null,"DeviceHub.Devices");
        Require(T("MenuNavigation").GetProperty("Status",All)?.PropertyType==typeof(string),"MenuNavigation.Status");
        var session=T("StagePlayback").GetField("Session",All)?.FieldType;
        Require(session?.GetProperty("Active")?.PropertyType==typeof(bool),"StagePlayback.Session.Active");
        var adapter=T("StagePlayback").GetField("Target",All)?.FieldType;
        Require(adapter?.GetMethod("MuteOutputs",All)?.ReturnType==typeof(void),"StagePlayback.Target.MuteOutputs");
        Require(T("DevInput").GetField("_enabled",All)?.FieldType.GetProperty("Value")?.PropertyType==typeof(bool),"DevInput._enabled");
        var native=force.GetType("Dbce.Wheel.Ffb.WheelFfbNative",true)!;
        var opens=native.GetMethods().Where(m=>m.Name=="Initialise").ToArray();
        Require(opens.Length>0 && opens.All(m=>m.ReturnType==typeof(bool)),"native.Initialise");
        Require(native.GetProperty("LoadedFrom")?.PropertyType==typeof(string),"native.LoadedFrom");
        return count;
    }
}
