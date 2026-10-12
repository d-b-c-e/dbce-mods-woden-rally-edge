using System.Reflection;

namespace Woden.ControlsTesting;

public static class HookContract
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    public static int VerifyShim(Assembly mod)
    {
        int count=0;
        void Require(bool ok,string what){count++;if(!ok)throw new MissingMemberException("Shim admission seam changed: "+what);}
        Type T(string name)=>mod.GetType("WodenRallyEdge."+name,true)!;
        // The real renderer also has Toggle(x,y,width,text,ref bool). Require
        // the exact panel entry point; name-only lookup is ambiguous at runtime.
        Require(T("Panel").GetMethod("Toggle",All,null,Type.EmptyTypes,null)?.ReturnType==typeof(void),"Panel.Toggle()");
        Require(T("Panel").GetProperty("Open",All)?.PropertyType==typeof(bool),"Panel.Open");
        Require(T("Settings").GetField("UiPage",All)?.FieldType==typeof(string),"Settings.UiPage");
        Require(T("Settings").GetField("InputBackend",All)?.FieldType==typeof(string),"Settings.InputBackend");
        foreach(var name in new[]{"Owns","Ready"})Require(T("ShimInput").GetProperty(name,All)?.PropertyType==typeof(bool),"ShimInput."+name);
        Require(T("ShimInput").GetProperty("Sequence",All)?.PropertyType==typeof(ulong),"ShimInput.Sequence");
        Require(T("ShimInput").GetProperty("Run",All)?.PropertyType==typeof(byte),"ShimInput.Run");
        Require(T("ShimInput").GetMethod("ConfigureMenuTrial",All,null,new[]{typeof(string),typeof(string),typeof(string),typeof(DateTimeOffset),typeof(bool)},null)?.ReturnType==typeof(void),"ShimInput.ConfigureMenuTrial");
        Require(T("ShimInput").GetMethod("MenuTrialScope",All,null,new[]{typeof(int)},null)?.ReturnType==typeof(void),"ShimInput.MenuTrialScope");
        foreach(var name in new[]{"KnownNeutral","TrialNativeMenuAllowed"})Require(T("ShimInput").GetMethod(name,All)?.ReturnType==typeof(bool),"ShimInput."+name);
        foreach(var name in new[]{"Status","TransmissionStatus"})Require(T("ShimInput").GetProperty(name,All)?.PropertyType==typeof(string),"ShimInput."+name);
        Require(T("ShimInput").GetProperty("SteeringGuid",All)?.PropertyType==typeof(Guid?),"ShimInput.SteeringGuid");
        Require(T("ShimInput").GetField("_frame",All)?.FieldType==typeof(int),"ShimInput._frame");
        Require(T("ShimInput").GetField("_handle",All)?.FieldType==typeof(IntPtr),"ShimInput._handle");
        Require(T("ShimInput").GetField("_producer",All)?.FieldType.FullName=="GamePadSystem","ShimInput._producer");
        Require(T("ShimInput").GetMethod("Stop",All,null,new[]{typeof(string)},null)?.ReturnType==typeof(void),"ShimInput.Stop(string)");
        Require(T("ShimInput").GetMethod("Action",All,null,new[]{typeof(string)},null)?.ReturnType.FullName=="WodenRallyEdge.Core.ShimAction","ShimInput.Action(string)");
        foreach(var name in new[]{"ProfileInfo","StartReport","DeviceReports"})Require(T("ShimNative").GetMethod(name,All,null,new[]{typeof(IntPtr)},null)?.ReturnType==typeof(string),"ShimNative."+name);
        foreach(var name in new[]{"Attempts","Opens","Failures"})Require(T("ForceController").GetProperty(name,All)?.PropertyType==typeof(long),"ForceController."+name);
        Require(T("ForceController").GetProperty("Connected",All)?.PropertyType==typeof(bool),"ForceController.Connected");
        Require(T("ForceController").GetProperty("Sent",All)?.PropertyType==typeof(float),"ForceController.Sent");
        Require(T("Runtime").GetField("Output",All)?.FieldType.GetProperty("ForzaPackets",All)?.PropertyType==typeof(long),"Output.ForzaPackets");
        Require(T("Runtime").GetField("Local",All)?.FieldType.FullName=="MainCar","Runtime.Local");
        return count;
    }
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
        var button=T("WheelInput").GetMethod("Button",All);
        var buttonArgs=button?.GetParameters().Select(p=>p.ParameterType.FullName ?? "").ToArray();
        Require(button?.ReturnType==typeof(bool) && buttonArgs is not null &&
            (buttonArgs.SequenceEqual(new[]{"System.String","System.Boolean"}) ||
             buttonArgs.SequenceEqual(new[]{"System.String","System.Boolean","MainCar"})),"WheelInput.Button (legacy or exact-car context)");
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
