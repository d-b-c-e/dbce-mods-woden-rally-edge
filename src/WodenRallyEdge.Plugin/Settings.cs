using BepInEx.Configuration;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

public sealed class Settings
{
    private readonly ConfigFile _config;
    public int Player, ForzaPort = 8000, DetailPort = 8001, DetailHz = 20;
    public bool Record, WheelEnabled, Bonnet = true, Bumper = true, FfbEnabled, FfbInvert;
    public float CameraHeight = .85f, CameraForward = .75f, CameraPitch = 3;
    public float BumperHeight = .35f, BumperForward = 2.2f, BumperPitch;
    public float FfbStrength = 10, FfbPeak = 25, FfbLoadReference = 6000, FfbSlipScale = .35f, FfbSmoothing = 35, FfbDamping = .05f;
    public string FfbGuid = "";
    public ForceOptions ForceOptions => new(FfbStrength, FfbPeak, FfbLoadReference, FfbSlipScale, FfbSmoothing, FfbDamping, FfbInvert);
    public Settings(ConfigFile config) { _config = config; _config.SaveOnConfigSet = false; Sync(true); Validate(); }
    public void Save() { Validate(); Sync(false); _config.Save(); }
    public void Validate()
    {
        static float Bound(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        Player = Math.Clamp(Player, 0, 3); ForzaPort = Math.Clamp(ForzaPort, 0, 65535); DetailPort = Math.Clamp(DetailPort, 0, 65535); DetailHz = Math.Clamp(DetailHz, 1, 60);
        CameraHeight = Bound(CameraHeight, .1f, 3, .85f); CameraForward = Bound(CameraForward, -2, 4, .75f); CameraPitch = Bound(CameraPitch, -30, 30, 3);
        BumperHeight = Bound(BumperHeight, .1f, 3, .35f); BumperForward = Bound(BumperForward, -2, 4, 2.2f); BumperPitch = Bound(BumperPitch, -30, 30, 0);
        FfbStrength = Bound(FfbStrength, 0, 100, 10); FfbPeak = Bound(FfbPeak, 0, 50, 25);
        FfbLoadReference = Bound(FfbLoadReference, 100, 50000, 6000); FfbSlipScale = Bound(FfbSlipScale, .02f, 3, .35f);
        FfbSmoothing = Bound(FfbSmoothing, 0, 200, 35); FfbDamping = Bound(FfbDamping, 0, .5f, .05f);
    }
    private void Sync(bool read)
    {
        void Item<T>(string group, string name, ref T value, string help)
        { var entry = _config.Bind(group, name, value, help); if (read) value = entry.Value; else entry.Value = value; }
        Item("General", "PlayerIndex", ref Player, "Exact local player index; change from the F6 panel.");
        Item("Wheel", "Enabled", ref WheelEnabled, "Use calibrated direct wheel input. F6 provides binding and calibration.");
        Item("Telemetry", "ForzaPort", ref ForzaPort, "Loopback Forza Horizon 5 UDP; 0 disables. Apply outputs in F6 after edits.");
        Item("Telemetry", "DetailPort", ref DetailPort, "Loopback detailed JSON UDP; 0 disables. Must differ from Forza.");
        Item("Telemetry", "DetailHz", ref DetailHz, "Detailed UDP maximum frequency, 1..60 Hz.");
        Item("Diagnostics", "RecordSession", ref Record, "Record numeric sessions; F6 can start/stop a bounded capture live.");
        Item("Camera", "BonnetEnabled", ref Bonnet, "Include bonnet in the normal camera-button cycle. Does not force this view.");
        Item("Camera", "BumperEnabled", ref Bumper, "Include bumper in the normal camera-button cycle.");
        Item("Camera", "Height", ref CameraHeight, "Car-local vertical offset."); Item("Camera", "Forward", ref CameraForward, "Car-local forward offset."); Item("Camera", "PitchDegrees", ref CameraPitch, "Downward pitch.");
        Item("Camera", "BumperHeight", ref BumperHeight, "Bumper car-local vertical offset."); Item("Camera", "BumperForward", ref BumperForward, "Bumper car-local forward offset."); Item("Camera", "BumperPitchDegrees", ref BumperPitch, "Bumper downward pitch.");
        Item("ForceFeedback", "Enabled", ref FfbEnabled, "Allow experimental force output; also requires Arm in F6 each game launch.");
        Item("ForceFeedback", "DeviceGuid", ref FfbGuid, "Exact FFB wheel GUID selected in F6; no fallback.");
        Item("ForceFeedback", "StrengthPercent", ref FfbStrength, "Overall output percentage; first-test default 10.");
        Item("ForceFeedback", "PeakPercent", ref FfbPeak, "Hard peak cap, 0..50% of the device nominal range.");
        Item("ForceFeedback", "LoadReference", ref FfbLoadReference, "Estimated tyre-signal normalization reference in Unity force units; uncalibrated.");
        Item("ForceFeedback", "SlipScale", ref FfbSlipScale, "Unity sideways-slip scale for the provisional aligning estimate.");
        Item("ForceFeedback", "SmoothingMs", ref FfbSmoothing, "Shared toolkit output smoothing time constant.");
        Item("ForceFeedback", "Damping", ref FfbDamping, "Damping from calibrated steering movement. No input binding means no damping component.");
        Item("ForceFeedback", "Invert", ref FfbInvert, "Reverse output sign; stop the car before changing.");
    }
}
