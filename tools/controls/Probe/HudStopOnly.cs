using System.Globalization;

namespace Woden.ControlsTesting;

// Menu-only recorder observation. No action batch, synthetic device event,
// stage replay, or player-car input is submitted by this recipe.
public sealed partial class ControlsProbe
{
    static double HudRecordingSince, HudStopped;
    static string HudVerdict="unclassified";

    static void StartHudStopOnly()
    {
        HudRecordingSince=HudStopped=0;
        HudVerdict="pending";
        ObserveUntil=Deadline;
        Row("hud-stop-only","ordinary one-shot recorder; menu only; 2s Recording, probe-direct Stop, 5s DataWritten dwell; no injected input");
    }

    static void TickHudStopOnly()
    {
        if(!Protocol.HudStopOnly(Request) || !Armed)return;
        double now=Clock.Elapsed.TotalSeconds;
        ObserveShim(); // Includes no player car, zero force and zero network output.
        var output=Field(Runtime,"Output")!;
        string status=Value(output,"RecordingStatus")?.ToString() ?? "missing";
        if(Convert.ToInt64(Value(output,"RecordingDrops"))!=0)
            throw new InvalidOperationException("Ordinary menu recording dropped samples.");
        if(HudStopped==0)
        {
            if(status!="Recording")throw new InvalidOperationException("Ordinary menu recording is not Recording: "+status);
            if(HudRecordingSince==0)
            {
                HudRecordingSince=now;
                Row("hud-recording","Recording; utc="+DateTimeOffset.UtcNow.ToString("O"));
            }
            if(now-HudRecordingSince<2)return;
            Method(output.GetType(),"StopRecording").Invoke(output,null);
            if(Value(output,"RecordingStatus")?.ToString()!="Completed")
                throw new InvalidOperationException("Probe-direct menu recorder Stop did not complete.");
            HudStopped=now;
            Row("hud-recorder-stop","probe-direct-stop; completed; drops=0; utc="+DateTimeOffset.UtcNow.ToString("O"));
            return;
        }
        if(status!="Completed")throw new InvalidOperationException("Stopped menu recording changed state: "+status);
        if(now-HudStopped<5)return;
        var counters=Value(output,"RecordingCounters") ?? throw new InvalidDataException("Menu recording counters unavailable.");
        foreach(string key in new[]{"DroppedSamples","DroppedMarkers","InvalidCount","ErrorCount","LimitCount","QueueFullCount","ContentionCount"})
            if(Convert.ToInt64(Value(counters,key))!=0)throw new InvalidDataException("Menu recording has nonzero "+key+"; DataWritten unavailable.");
        if(!(bool)Value(output,"RecordingRequiresFinalization")! ||
            output.GetType().GetProperty("RecordingError",All)?.GetValue(output) is not null ||
            !File.Exists(CarRecording) || new FileInfo(CarRecording).Length==0)
            throw new InvalidDataException("Ordinary menu source did not reach DataWritten before finalization.");
        HudVerdict="passed: 2s menu Recording, probe-direct Stop, 5s Completed/DataWritten dwell; no injected input or physical output";
        Row("hud-poststop-dwell","5s Completed/DataWritten source state; rendered HUD unverified until WGC review");
        Row("hud-stop-only-verdict",HudVerdict);
        Close("bounded menu recorder stop and dwell completed");
    }
}
