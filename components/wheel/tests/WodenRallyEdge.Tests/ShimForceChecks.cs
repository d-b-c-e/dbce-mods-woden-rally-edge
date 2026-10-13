using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class ShimForceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var (controller,device)=ForceControllerChecks.Create();
        var steering=Guid.NewGuid();var legacy=Guid.NewGuid();
        Runtime.Wheel=new();Runtime.Wheel.Bindings.Steer=new(legacy,0,new(0,65535,32768));
        Runtime.Settings.FfbFollowSteering=true;
        var savedOptions=Runtime.Settings.ForceOptions;
        Runtime.Devices!.Candidates=new[]{new ForceCandidate(legacy,"same name",true),new ForceCandidate(steering,"same name",true)};
        try
        {
            // Shared input requested, not refused and not yet owning: the stale legacy wheel is never a target.
            ShimInput.PendingShared=true;
            controller.Prepare();controller.Prepare();
            check(device.Opens==0&&controller.Status.StartsWith("Waiting for the shared controls profile"),"pending shared profile: the stale legacy wheel never opens");
            ShimInput.StoppedBeforeOpen=true;controller.Prepare();
            check(device.Opens==0&&controller.Status.StartsWith("Shared input stopped before opening"),"stopped before opening (not refused): the legacy wheel stays closed");
            ShimInput.StoppedBeforeOpen=false;ShimInput.PendingShared=false;
            ShimInput.Owns=true;ShimInput.Ready=true;ShimInput.SteeringGuid=steering;
            controller.Prepare();
            check(device.OpenedGuid==steering&&device.Opens==1,"production force selection follows shim instance GUID, never same product/name or legacy binding");
            check(Runtime.Devices.Closes==1&&Runtime.Devices.Refreshes==1,"force open retains original reader close/refresh lifecycle");
            Runtime.Devices.Readable=false;controller.Prepare();
            check(device.Writes.Last()==0&&device.Opens==1&&Runtime.Devices.Reads>0,"shim steering retains existing force reader reacquire with zero");
            ShimInput.Ready=false;controller.Prepare();controller.Prepare();
            check(device.Closes==1&&device.Opens==1,"terminal shim releases force and never reopens");
            check(Runtime.Settings.FfbEnabled&&Runtime.Settings.Saves==0&&Runtime.Settings.ForceOptions==savedOptions,"shim terminal state does not rewrite saved force preference or tune");
        }
        finally {controller.Shutdown();ShimInput.Owns=false;ShimInput.Ready=true;ShimInput.PendingShared=ShimInput.StoppedBeforeOpen=false;ShimInput.SteeringGuid=null;Runtime.Wheel=new();}
    }
}
