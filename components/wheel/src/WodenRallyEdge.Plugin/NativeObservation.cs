using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Transport for one explicit ordinary diagnostic. Reads only the existing GameSampler result;
// no device opens/polls, raw/latched input, body writes, binding changes or shared-input trial.
internal static class NativeObservation
{
    private static bool _active;
    private static double _deadline, _seconds, _firstAt = double.NaN, _lastAt, _exitAt = double.NaN;
    private static long _samples, _firstSequence, _lastSequence;
    private static readonly string[] Required = {
        "wheelRaw.steer", "wheelRaw.throttle", "wheelRaw.brake", "wheelRaw.handbrake",
        "wheelInput.steer", "wheelInput.throttle", "wheelInput.brake", "wheelInput.handbrake",
        "controls.throttle", "controls.brake", "game.throttle", "game.brake", "game.handbrake"
    };

    internal static void Begin(DiagnosticLaunch? request)
    {
        if (request?.NativeObservationSource == null) return;
        _active = true; _deadline = Runtime.Clock.Elapsed.TotalSeconds + 180; _seconds = request.NativeObservationSeconds;
        try
        {
            if (!request.DisableForces || request.InputOrigin != "automated" || Runtime.Settings.InputBackend != "Native" ||
                Runtime.Settings.Player != 0 || Runtime.Settings.TelemetryEnabled || Runtime.Output?.RecordingActive != true)
                throw new IOException("Native observation requires Native/player0, disabled network and its original recorder");
            StagePlayback.MuteNativeObservation();
            StageStartup.InitializeNativeObservation(request.NativeObservationSource);
            Runtime.Log.LogInfo("Native observation phase=menu-entry; no injected gameplay; request=" + request.Id);
        }
        catch (Exception ex) { Finish("refused: " + ex.Message); }
    }

    internal static void Tick(double now)
    {
        if (!_active) return;
        if (!double.IsNaN(_exitAt))
        {
            if (now >= _exitAt) { _active = false; Application.Quit(); }
            return;
        }
        try
        {
            if (now >= _deadline) throw new IOException("180 second startup/observation deadline");
            StageStartup.TickNativeObservation(now);
            var force = Runtime.Force;
            if (force.Attempts != 0 || force.Opens != 0 || force.Connected || Runtime.Output?.ForzaPackets != 0)
                throw new IOException("Physical/network output counters are not zero");
            if (!double.IsNaN(_firstAt) && now - _firstAt >= _seconds)
                Finish(_samples >= 10 && now - _lastAt <= .25
                    ? "captured-current-reader; samples=" + _samples + "; firstSequence=" + _firstSequence + "; lastSequence=" + _lastSequence
                    : "unavailable: original reader samples missing or stale at interval end");
        }
        catch (Exception ex) { Finish("unavailable: " + ex.Message); }
    }

    internal static void Observe(MainCar car, TelemetrySample sample)
    {
        if (!_active || !double.IsNaN(_exitAt)) return;
        try
        {
            if (!Runtime.ControlState(car).WheelAvailable) return; // Wait for the actual countdown/race input phase.
            if (!StageStartup.ValidateRace(car)) return;
            if (Required.Any(k => !sample.Channels.TryGetValue(k, out double value) || !double.IsFinite(value)))
                throw new IOException("Required raw/normalized/delivered channels unavailable at local car");
            if (double.IsNaN(_firstAt))
            {
                _firstAt = Runtime.Clock.Elapsed.TotalSeconds; _firstSequence = sample.Sequence;
                Runtime.Log.LogInfo("Native observation phase=current-physical-reader; firstSequence=" + sample.Sequence +
                    "; automatic=" + car.Aids?.Automatic + "; no injected gameplay; physical rest/reproduction not inferred");
                var wheel = Runtime.Wheel;
                if (wheel != null && wheel.Bindings.Buttons.TryGetValue("Handbrake", out var binding))
                    Runtime.Log.LogInfo("Native observation cached handbrake button=" + binding.Button + "; device=" + binding.DeviceGuid +
                        "; held=" + Runtime.Devices?.Button(binding, false));
            }
            _samples++; _lastSequence = sample.Sequence; _lastAt = Runtime.Clock.Elapsed.TotalSeconds;
        }
        catch (Exception ex) { Finish("unavailable: " + ex.Message); }
    }

    private static void Finish(string reason)
    {
        if (!double.IsNaN(_exitAt)) return;
        Runtime.Log.LogInfo("Native observation phase=ended; " + reason + "; normal exit in 8 seconds");
        Runtime.Output?.StopRecording();
        _exitAt = Runtime.Clock.Elapsed.TotalSeconds + 8;
    }
}
