using BepInEx.Configuration;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

public sealed class Settings
{
    private readonly ConfigFile _config;
    public int Player, ForzaPort = 8000, DetailPort = 8001, DetailHz = 20;
    public bool Record, WheelEnabled, Bonnet = true, Bumper = true, FfbEnabled = true, FfbInvert;
    public bool CountdownAssistEnabled;
    public float CountdownSpeed = 75;
    public float CameraHeight = CameraPose.Bonnet.Height, CameraForward = CameraPose.Bonnet.Forward, CameraPitch = 8, CameraSide, CameraFov = 70;
    public float BumperHeight = .35f, BumperForward = 2.2f, BumperPitch, BumperSide, BumperFov = 70, BumperAhead = CameraPose.BumperAheadDefault;
    public bool CameraAutoFit = true, ShowFrameRate;
    public float CameraMoveStep = CameraSteps.Default.Move, CameraTiltStep = CameraSteps.Default.Tilt, CameraFovStep = CameraSteps.Default.Fov;
    public CameraSteps CameraSteps => new CameraSteps(CameraMoveStep, CameraTiltStep, CameraFovStep).Bounded();
    private int _cameraDefaultsVersion;
    public float FfbStrength = 50, FfbPeak = 25, FfbLoadReference = 6000, FfbSlipScale = .35f, FfbSmoothing = 35, FfbDamping = .05f;
    public bool CrashEnabled = true;
    public string FfbModel = "Grip";
    public float FfbLoadRatio = 2, FfbGripSmoothing = .2f;
    public float CrashStrength = 50;
    public string FfbGuid = "";
    public bool FfbFollowSteering = true, TelemetryEnabled = true;
    private int _selectionVersion;
    public string UiView = "Simple", UiPage = "Setup";
    public float UiScale = 100;
    public bool CustomFfb => FfbModel != "Grip" || FfbLoadRatio != 2 || FfbGripSmoothing != .2f || FfbPeak != 25 || FfbLoadReference != 6000 || FfbSlipScale != .35f || FfbSmoothing != 35 || FfbDamping != .05f || FfbInvert;
    public void SetPresentation(string view, string page)
    {
        UiView = SettingsPresentation.View(view); UiPage = SettingsPresentation.Page(page, UiView);
        _config.Bind("Interface", "View", "Simple").Value = UiView;
        _config.Bind("Interface", "Page", "Setup").Value = UiPage;
        _config.Save();
    }
    public ForceOptions ForceOptions => new(FfbStrength, FfbPeak, FfbLoadReference, FfbSlipScale, FfbSmoothing, FfbDamping, FfbInvert,
        FfbModel == "Classic" ? 3 : GripSignal.ModelVersion, FfbLoadRatio, FfbGripSmoothing);
    public Settings(ConfigFile config)
    {
        _config = config; _config.SaveOnConfigSet = false; Sync(true);
        if (_cameraDefaultsVersion < 1)
        {
            bool oldDefault = CameraHeight == .85f && CameraForward == .75f && CameraPitch == 3;
            CameraAutoFit = oldDefault || CameraHeight == 1.1f && CameraForward == .1f && CameraPitch == 8 || GetCameraPose(false) == CameraPose.Bonnet;
            if (oldDefault) SetCameraPose(false, CameraPose.Bonnet);
            _cameraDefaultsVersion = 1;
        }
        if (_cameraDefaultsVersion < 2)
        {
            if (CameraAutoFit && CameraHeight == 1.1f && CameraForward == .1f && CameraPitch == 8) SetCameraPose(false, CameraPose.Bonnet);
            _cameraDefaultsVersion = 2;
        }
        if (_selectionVersion < 1) { FfbFollowSteering = string.IsNullOrWhiteSpace(FfbGuid); _selectionVersion = 1; }
        Validate();
    }
    public CameraPose GetCameraPose(bool bumper) => bumper ? new(BumperSide, BumperHeight, BumperForward, BumperPitch, BumperFov) : new(CameraSide, CameraHeight, CameraForward, CameraPitch, CameraFov);
    public void SetCameraPose(bool bumper, CameraPose pose)
    {
        pose = pose.Bounded();
        if (bumper) { BumperSide = pose.Side; BumperHeight = pose.Height; BumperForward = pose.Forward; BumperPitch = pose.Pitch; BumperFov = pose.Fov; }
        else { CameraSide = pose.Side; CameraHeight = pose.Height; CameraForward = pose.Forward; CameraPitch = pose.Pitch; CameraFov = pose.Fov; }
    }
    public void ResetCamera(bool bumper)
    { SetCameraPose(bumper, bumper ? WodenRallyEdge.Core.CameraPose.Bumper : WodenRallyEdge.Core.CameraPose.Bonnet); if (bumper) BumperAhead = CameraPose.BumperAheadDefault; else CameraAutoFit = true; }
    public void Save() { Validate(); Sync(false); _config.Save(); }
    public void Validate()
    {
        static float Bound(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        Player = Math.Clamp(Player, 0, 3); ForzaPort = Math.Clamp(ForzaPort, 0, 65535); DetailPort = Math.Clamp(DetailPort, 0, 65535); DetailHz = Math.Clamp(DetailHz, 1, 60);
        SetCameraPose(false, GetCameraPose(false)); SetCameraPose(true, GetCameraPose(true));
        FfbStrength = Bound(FfbStrength, 0, 100, 50); FfbPeak = Bound(FfbPeak, 0, 50, 25); CrashStrength = Bound(CrashStrength, 0, 100, 50);
        FfbLoadReference = Bound(FfbLoadReference, 100, 50000, 6000); FfbSlipScale = Bound(FfbSlipScale, .02f, 3, .35f);
        FfbSmoothing = Bound(FfbSmoothing, 0, 200, 35); FfbDamping = Bound(FfbDamping, 0, .5f, .05f);
        FfbModel = FfbModel == "Classic" ? "Classic" : "Grip"; FfbLoadRatio = Bound(FfbLoadRatio, .2f, 10, 2); FfbGripSmoothing = Bound(FfbGripSmoothing, 0, .95f, .2f);
        BumperAhead = Bound(BumperAhead, -1, 2, CameraPose.BumperAheadDefault);
        CountdownSpeed = Bound(CountdownSpeed, 25, 100, 75);
        var steps = CameraSteps; CameraMoveStep = steps.Move; CameraTiltStep = steps.Tilt; CameraFovStep = steps.Fov;
        UiView = SettingsPresentation.View(UiView); UiPage = SettingsPresentation.Page(UiPage, UiView);
        UiScale = Bound(UiScale, 85, 150, 100);
    }
    private void Sync(bool read)
    {
        void Item<T>(string group, string name, ref T value, string help)
        { var entry = _config.Bind(group, name, value, help); if (read) value = entry.Value; else entry.Value = value; }
        Item("Interface", "View", ref UiView, "Simple or Advanced; presentation only.");
        Item("Interface", "Page", ref UiPage, "Last settings page.");
        Item("Interface", "ScalePercent", ref UiScale, "Settings text and target scale, 85..150%.");
        Item("Telemetry", "Enabled", ref TelemetryEnabled, "Send dashboard UDP; does not start or stop diagnostic recording.");
        Item("ForceFeedback", "FollowSteering", ref FfbFollowSteering, "Use the saved Steering device GUID. An explicit selection remains independent.");
        Item("ForceFeedback", "SelectionVersion", ref _selectionVersion, "Preserve legacy explicit selections during migration.");
        Item("General", "PlayerIndex", ref Player, "Exact local player index; change from the F6 panel.");
        Item("Wheel", "Enabled", ref WheelEnabled, "Use calibrated direct wheel input. F6 provides binding and calibration.");
        Item("Difficulty", "CountdownAssistEnabled", ref CountdownAssistEnabled, "Optional single-player countdown/time-limit assist. Default Off; does not change lap/stage timing or game speed.");
        Item("Difficulty", "CountdownSpeedPercent", ref CountdownSpeed, "Countdown speed when enabled, 25..100%. 75% gives about 80 seconds of driving per 60 countdown seconds.");
        Item("Telemetry", "ForzaPort", ref ForzaPort, "Loopback Forza Horizon 5 UDP; 0 disables. Apply outputs in F6 after edits.");
        Item("Telemetry", "DetailPort", ref DetailPort, "Loopback detailed JSON UDP; 0 disables. Must differ from Forza.");
        Item("Telemetry", "DetailHz", ref DetailHz, "Detailed UDP maximum frequency, 1..60 Hz.");
        Item("Diagnostics", "RecordSession", ref Record, "Legacy diagnostic capture preference. Use tools/Start-RecordedGame.ps1 for a single requested launch.");
        Item("Camera", "BonnetEnabled", ref Bonnet, "Include bonnet in the normal camera-button cycle. Does not force this view.");
        Item("Camera", "BumperEnabled", ref Bumper, "Include bumper in the normal camera-button cycle.");
        Item("Camera", "Height", ref CameraHeight, "Car-local vertical offset."); Item("Camera", "Forward", ref CameraForward, "Car-local forward offset."); Item("Camera", "PitchDegrees", ref CameraPitch, "Downward pitch.");
        Item("Camera", "BumperHeight", ref BumperHeight, "Bumper car-local vertical offset."); Item("Camera", "BumperForward", ref BumperForward, "Bumper car-local forward offset; used only when the car body cannot be measured."); Item("Camera", "BumperPitchDegrees", ref BumperPitch, "Bumper downward pitch.");
        Item("Camera", "Side", ref CameraSide, "Bonnet car-local side offset."); Item("Camera", "Fov", ref CameraFov, "Bonnet vertical field of view, degrees.");
        Item("Camera", "BumperSide", ref BumperSide, "Bumper car-local side offset."); Item("Camera", "BumperFov", ref BumperFov, "Bumper vertical field of view, degrees.");
        Item("Camera", "BumperAheadMetres", ref BumperAhead, "Bumper distance in front of the car body, -1..2 m. The view follows each car's length.");
        Item("Camera", "MoveStepMetres", ref CameraMoveStep, "Camera shortcut move per press, 0.005..0.25 m.");
        Item("Camera", "TiltStepDegrees", ref CameraTiltStep, "Camera shortcut tilt per press, 0.1..10 degrees.");
        Item("Camera", "FovStepDegrees", ref CameraFovStep, "Camera shortcut field-of-view change per press, 0.5..10 degrees.");
        Item("Camera", "AutoFitBonnet", ref CameraAutoFit, "Fit the bonnet view to the car body. Adjusting an offset switches to manual placement.");
        Item("Camera", "DefaultsVersion", ref _cameraDefaultsVersion, "Camera defaults migration marker; custom offsets are preserved.");
        Item("Display", "ShowFrameRate", ref ShowFrameRate, "Draw the frame rate (current fps and the 1% low of the last 10 s) at the top right of the centre screen (STD-023). Default Off.");
        Item("ForceFeedback", "Enabled", ref FfbEnabled, "Saved FFB On/Off preference. Feedback starts only during valid player driving. F8 saves Off.");
        Item("ForceFeedback", "DeviceGuid", ref FfbGuid, "Exact FFB wheel GUID selected in F6; no fallback.");
        Item("ForceFeedback", "StrengthPercent", ref FfbStrength, "Overall strength, default 50%. Original output gain restored; no additional reduction.");
        Item("ForceFeedback", "CrashEnabled", ref CrashEnabled, "Crash kick: art of rally's cue (a short push plus a 25 Hz rattle) when the car hits something. Default On.");
        Item("ForceFeedback", "CrashStrengthPercent", ref CrashStrength, "Crash kick strength, percent of full force for the hardest hit; independent of Strength and the peak cap, as in art of rally. Default 50.");
        Item("ForceFeedback", "Model", ref FfbModel, "Grip (default): art of rally's force model from the toolkit on the front tyres' lateral force, rebuilt from the game's tyre friction; the wheel lightens as the front slides and Strength 50 means the same as art of rally's 50. Classic: the earlier contact-weighted slip estimate with its peak cap.");
        Item("ForceFeedback", "GripLoadRatio", ref FfbLoadRatio, "Grip: full scale as a multiple of the front load measured at rest, 0.2..10; higher is lighter. Default 2 (art of rally: 11,500 N for about 5,600 N of front load).");
        Item("ForceFeedback", "GripSmoothing", ref FfbGripSmoothing, "Grip: smoothing per update, 0..0.95; default 0.2 as in art of rally.");
        Item("ForceFeedback", "PeakPercent", ref FfbPeak, "Classic only: hard peak cap, 0..50% of the device nominal range.");
        Item("ForceFeedback", "LoadReference", ref FfbLoadReference, "Estimated tyre-signal normalization reference in Unity force units; uncalibrated.");
        Item("ForceFeedback", "SlipScale", ref FfbSlipScale, "Unity sideways-slip scale for the provisional aligning estimate.");
        Item("ForceFeedback", "SmoothingMs", ref FfbSmoothing, "Shared toolkit output smoothing time constant.");
        Item("ForceFeedback", "Damping", ref FfbDamping, "Damping from calibrated steering movement. No input binding means no damping component.");
        Item("ForceFeedback", "Invert", ref FfbInvert, "Reverse output sign; stop the car before changing.");
    }
}
