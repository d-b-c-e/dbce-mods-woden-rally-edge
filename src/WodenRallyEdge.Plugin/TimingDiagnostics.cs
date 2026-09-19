using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class TimingDiagnostics
{
    private static double _previousFrame, _nextReport;
    internal static double FrameMs, PollMs, UpdateMs, CarMs, SampleMs, ForceMs, ControlsMs;
    private static double _gear, _shifting;
    internal static long Hitches;
    internal static void Frame(double now)
    {
        FrameMs = _previousFrame > 0 ? (now - _previousFrame) * 1000 : 0;
        _previousFrame = now;
        if (FrameMs < 100 || Runtime.Local == null) return;
        Hitches++;
        if (now < _nextReport) return;
        _nextReport = now + 1;
        Runtime.Log.LogWarning($"Frame gap {FrameMs:F1}ms; prior update={UpdateMs:F1}, devicePoll={PollMs:F1}, controls={ControlsMs:F1}, car={CarMs:F1}, sampler={SampleMs:F1}, force={ForceMs:F1}ms; gear={_gear}, shifting={_shifting}, FFB opens={Runtime.Force.Opens}. Timing correlation, not a cause attribution.");
    }
    internal static void Record(TelemetrySample sample)
    {
        _gear = sample.Get("game.gear"); _shifting = sample.Get("game.shifting");
        sample.Add("timing.frameMs", FrameMs); sample.Add("timing.devicePollMs", PollMs);
        sample.Add("timing.carMs", CarMs); sample.Add("timing.samplerMs", SampleMs);
        sample.Add("timing.forceMs", ForceMs); sample.Add("timing.controlsMs", ControlsMs);
        sample.Add("timing.hitches", Hitches);
    }
}
