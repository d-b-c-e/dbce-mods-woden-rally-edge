using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Woden strips GUI.Button, GUILayout and sliders. Fixed rectangles, Label,
// DrawTexture, TextArea and IMGUI events are enough for a complete native panel.
internal static class Panel
{
    internal static bool Open { get; private set; }
    internal static string Message = "F6 settings / F8 stops FFB";
    private static readonly string[] Pages = { "Setup", "Controls", "FFB", "Cameras", "Telemetry", "Help" };
    private static readonly string[] Axes = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
    private static int _page, _buttonPage, _devicePage, _drag = -1;
    private static float _scale, _left, _top;
    private static GUIStyle? _label, _title, _help;
    private static bool _cursorVisible, _dirty;
    private static CursorLockMode _cursorLock;
    private static Pause? _ownedPause;
    private static EventSystem? _events;
    private static bool _eventsEnabled;
    private static double _changedAt, _nextError;
    private static string _forza = "", _detail = "";
    private static bool _editBumper, _controlsButtons, _advancedFfb, _deviceDetails;
    private static bool _cameraBindings;
    private static bool _difficulty;
    private static int _cameraBindingPage;
    private static string _saveStatus = "Saved";
    private static string ActionLabel(string action) => action switch
    {
        "Steer" => "Steering", "Handbrake" => "E-Brake", "Gear up" => "Shift up", "Gear down" => "Shift down", "Camera" => "Change camera",
        "Rear view" => "Look behind", "Respawn" => "Reset car", "Settings panel" => "Settings", "Panic stop" => "Stop FFB", _ => action
    };
    private static readonly Color Accent = new(.95f, .73f, .3f, 1);
    private static Rect R(float x, float y, float w, float h) => new(_left + x * _scale, _top + y * _scale, w * _scale, h * _scale);
    private static bool Inside(Rect r, Vector2 p) => p.x >= r.x && p.y >= r.y && p.x < r.x + r.width && p.y < r.y + r.height;
    internal static void Update()
    {
        if (!Runtime.Focused) return;
        try
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.F8].wasPressedThisFrame || Runtime.Wheel?.Button("Panic stop") == true) Runtime.Force?.Panic();
            bool capturing = Runtime.Wheel?.Capture != null || Runtime.Wheel?.CaptureButton != null;
            if (kb != null && kb[Key.F6].wasPressedThisFrame || !capturing && Runtime.Wheel?.Button("Settings panel") == true) Toggle();
            if (Open && kb != null && kb[Key.Escape].wasPressedThisFrame) { if (capturing) Runtime.Wheel?.Cancel(); else Toggle(); }
            if (!Open && Runtime.Wheel?.Button("Pause") == true && Runtime.Local != null)
            {
                var pause = Runtime.Local.MyControls?.PauseScript;
                if (pause != null) { Runtime.Force?.Suspend("paused"); if (Pause.Paused) pause.UnsetPause(); else pause.SetPause(); }
            }
        }
        catch (Exception ex) { Message = "Hotkeys: " + ex.Message; }
        if (Open)
        {
            Cursor.visible = true; UiNative.CursorLock(CursorLockMode.None);
            Runtime.Wheel?.UpdateCapture();
        }
        if (_dirty && Runtime.Clock.Elapsed.TotalSeconds - _changedAt > .7) Save();
    }
    internal static void Toggle()
    {
        if (Open) { Close(true); return; }
        Open = true; Runtime.Force?.Suspend("Settings open");
        _forza = Runtime.Settings.ForzaPort.ToString(); _detail = Runtime.Settings.DetailPort.ToString();
        _cursorVisible = UiNative.CursorVisible; _cursorLock = Cursor.lockState;
        Cursor.visible = true; UiNative.CursorLock(CursorLockMode.None);
        try
        {
            var car = Runtime.Local;
            var pause = car?.MyControls?.PauseScript;
            if (car != null && car.Status == MainCar.CarStatus.RACE && !Pause.Paused && pause != null) { pause.SetPause(); _ownedPause = pause; }
            _events = EventSystem.current;
            if (_events != null) { _eventsEnabled = _events.enabled; _events.enabled = false; }
        }
        catch (Exception ex) { Message = "Panel open; pause manually if needed: " + ex.Message; }
        Runtime.Log.LogInfo("F6 panel opened");
    }
    internal static void Close(bool resume)
    {
        if (!Open) return;
        Open = false; Runtime.Wheel?.Cancel(); _drag = -1; Save();
        try
        {
            if (_events != null) _events.enabled = _eventsEnabled;
            if (resume && _ownedPause != null && Pause.Paused && !_ownedPause.PhotomodeActive) _ownedPause.UnsetPause();
            Cursor.visible = _cursorVisible; UiNative.CursorLock(_cursorLock);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Panel restore: " + ex.Message); }
        _events = null; _ownedPause = null;
        Runtime.Log.LogInfo("F6 panel closed");
    }
    internal static void SettingsChanged() => Dirty();
    private static void Dirty() { _dirty = true; _saveStatus = "Saving…"; _changedAt = Runtime.Clock.Elapsed.TotalSeconds; }
    private static void Save() { try { Runtime.Settings.Save(); _dirty = false; _saveStatus = "Saved"; } catch (Exception ex) { _saveStatus = "Save failed"; Message = "Could not save settings: " + ex.Message; } }
    private static void Fill(Rect r, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = previous;
    }
    private static void Label(float x, float y, float width, string text, bool help = false, float height = 28)
        => GUI.Label(R(x, y, width, height), new GUIContent(text), help ? _help! : _label!);
    private static bool Button(float x, float y, float width, string text, bool active = false)
    {
        var r = R(x, y, width, 34); var e = Event.current;
        bool hover = Inside(r, e.mousePosition);
        Fill(r, active ? new Color(.37f, .28f, .12f, 1) : hover ? new Color(.23f, .28f, .34f, 1) : new Color(.13f, .17f, .23f, 1));
        Label(x + 10, y + 4, width - 15, text);
        if (e.type == EventType.MouseDown && e.button == 0 && hover) { e.Use(); GUIUtility.keyboardControl = 0; return true; }
        return false;
    }
    private static void Toggle(float x, float y, float width, string text, ref bool value)
    {
        Label(x, y + 4, width - 125, text + ":");
        if (Button(x + width - 120, y, 55, "Off", !value) && value) { value = false; Dirty(); }
        if (Button(x + width - 60, y, 60, "On", value) && !value) { value = true; Dirty(); }
    }
    private static void Slider(float y, string label, ref float value, float min, float max, string format = "F2")
    {
        Label(250, y, 245, label); Label(835, y, 90, value.ToString(format, CultureInfo.InvariantCulture));
        var r = R(510, y + 3, 300, 22); var e = Event.current; int id = (int)y;
        if (e.type == EventType.MouseDown && e.button == 0 && Inside(r, e.mousePosition)) { _drag = id; e.Use(); }
        if (_drag == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.Used))
        {
            value = min + Math.Clamp((e.mousePosition.x - r.x) / r.width, 0, 1) * (max - min); Dirty();
            if (e.type != EventType.Used) e.Use();
        }
        if (_drag == id && e.type == EventType.MouseUp) { _drag = -1; e.Use(); }
        Fill(r, new Color(.08f, .1f, .15f, 1));
        Fill(new Rect(r.x, r.y + r.height * .35f, r.width * Math.Clamp((value - min) / (max - min), 0, 1), r.height * .3f), Accent);
        float knob = r.x + r.width * Math.Clamp((value - min) / (max - min), 0, 1);
        Fill(new Rect(knob - 3 * _scale, r.y, 6 * _scale, r.height), Color.white);
    }
    private static void Bar(float x, float y, float width, float value, bool steering)
    {
        var r = R(x, y, width, 12); Fill(r, new Color(.06f, .08f, .12f, 1));
        float origin = steering ? .5f : 0; float length = steering ? Math.Clamp(value, -1, 1) * .5f : Math.Clamp(value, 0, 1);
        Fill(new Rect(r.x + r.width * (origin + Math.Min(0, length)), r.y, r.width * Math.Abs(length), r.height), Accent);
    }
    internal static void Draw()
    {
        if (!Open) return;
        var previousColor = GUI.color; bool previousEnabled = GUI.enabled;
        try
        {
            GUI.color = Color.white; GUI.enabled = true;
            _scale = Math.Min(1.6f, Math.Min((Screen.width - 32f) / 960, (Screen.height - 32f) / 740));
            _left = (Screen.width - 960 * _scale) / 2; _top = (Screen.height - 740 * _scale) / 2;
            _label ??= new GUIStyle(); _title ??= new GUIStyle(); _help ??= new GUIStyle();
            UiNative.Style(_label, (int)(18 * _scale), true); _label.normal.textColor = Color.white;
            UiNative.Style(_title, (int)(27 * _scale), false); _title.normal.textColor = Accent;
            UiNative.Style(_help, (int)(15 * _scale), true); _help.normal.textColor = new Color(.69f, .76f, .84f, 1);
            Fill(R(0, 0, 960, 740), new Color(.035f, .05f, .075f, .99f));
            Fill(R(0, 0, 220, 740), new Color(.06f, .08f, .12f, 1));
            GUI.Label(R(24, 20, 850, 45), new GUIContent("Woden / Wheel settings"), _title);
            Label(25, 65, 900, "F6 close   |   F8 stops FFB   |   " + _saveStatus, true);
            for (int i = 0; i < Pages.Length; i++) if (Button(18, 120 + i * 47, 185, Pages[i], _page == i)) { Runtime.Wheel?.Cancel(); _page = i; _drag = -1; }
            if (Button(18, 595, 185, "Stop FFB")) Runtime.Force?.Panic();
            if (Button(18, 642, 185, "Close")) Close(true);
            Label(250, 112, 280, Pages[_page]);
            switch (_page) { case 0: SetupPage(); break; case 1: ControlsPage(); break; case 2: ForcePage(); break; case 3: CameraPage(); break; case 4: TelemetryPage(); break; case 5: HelpPage(); break; }
            Fill(R(238, 681, 700, 1), new Color(.25f, .3f, .36f, 1));
            Label(250, 695, 680, Message, true, 35);
            if (Event.current.type == EventType.MouseDown && Inside(R(0, 0, 960, 740), Event.current.mousePosition)) Event.current.Use();
        }
        catch (Exception ex)
        {
            if (Runtime.Clock.Elapsed.TotalSeconds > _nextError) { Runtime.Log.LogError("F6 draw: " + ex); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 5; }
        }
        finally { GUI.color = previousColor; GUI.enabled = previousEnabled; }
    }
    private static void SetupPage()
    {
        if (Button(620, 104, 292, "Difficulty", _difficulty)) _difficulty = !_difficulty;
        if (_difficulty) { DifficultyPage(); return; }
        var cfg = Runtime.Settings;
        bool previous = cfg.WheelEnabled; Toggle(250, 150, 320, "Wheel controls", ref cfg.WheelEnabled);
        if (previous != cfg.WheelEnabled) Runtime.Force?.Suspend("Wheel controls changed");
        if (Button(640, 150, 272, "Refresh devices")) { Runtime.Force?.Reconnect("Device refresh"); Runtime.Devices?.Refresh(); }
        Label(250, 207, 662, "Check your controls, then bind buttons and set up feedback. This panel applies no force.", true, 48);
        int row = 0;
        foreach (string name in new[] { "Steer", "Throttle", "Brake" })
        {
            float y = 279 + row++ * 70;
            bool available = Runtime.Devices!.TryAxis(Runtime.Wheel!.Bindings.Axis(name), out float value);
            Label(250, y, 150, ActionLabel(name));
            Bar(405, y + 8, 355, available ? value : 0, name == "Steer");
            Label(782, y - 2, 135, available ? name == "Steer" ? value < -.02 ? "Left" : value > .02 ? "Right" : "Centre" : value.ToString("P0") : "Not available", true);
        }
        Label(250, 480, 662, "These bars show device input. Confirm the car responds during your first drive.", true, 42);
        if (Button(250, 533, 310, "1. Bind / calibrate controls")) { _page = 1; _controlsButtons = false; }
        if (Button(580, 533, 332, "2. Bind driving buttons")) { _page = 1; _controlsButtons = true; }
        if (Button(250, 585, 310, "3. Set up FFB")) _page = 2;
        Label(580, 585, 332, "Close settings, then enter a level to check your controls.", true, 64);
    }
    private static void DifficultyPage()
    {
        var cfg = Runtime.Settings;
        Label(250, 165, 662, "More time to finish", false, 36);
        Toggle(250, 215, 550, "Countdown assist", ref cfg.CountdownAssistEnabled);
        Slider(280, "Countdown speed (%)", ref cfg.CountdownSpeed, 25, 100, "F0");
        Label(250, 330, 662, $"At {cfg.CountdownSpeed:F0}%, 60 seconds on the timer lasts about {6000 / cfg.CountdownSpeed:F0} seconds of driving.", true, 50);
        if (Button(250, 404, 190, "Normal / 100%")) { cfg.CountdownSpeed = 100; Dirty(); }
        if (Button(455, 404, 190, "Gentler / 75%")) { cfg.CountdownSpeed = 75; Dirty(); }
        if (Button(660, 404, 190, "Relaxed / 50%")) { cfg.CountdownSpeed = 50; Dirty(); }
        Label(250, 471, 662, "Slows the single-player time limit. Car speed, force feedback and elapsed lap/stage clocks run normally. Checkpoint time bonuses keep their normal value.", true, 76);
        Label(250, 569, 662, cfg.CountdownAssistEnabled ? CountdownTimerAssist.Status : "Off — normal countdown speed", true, 48);
        Label(250, 630, 662, "Saved automatically. Changes affect future countdown ticks; they do not undo a timeout.", true, 44);
    }
    private static void ControlsPage()
    {
        if (Button(600, 104, 145, "Axes", !_controlsButtons)) { Runtime.Wheel?.Cancel(); _controlsButtons = false; }
        if (Button(760, 104, 152, "Buttons", _controlsButtons)) { Runtime.Wheel?.Cancel(); _controlsButtons = true; }
        if (_controlsButtons) ButtonsPage(); else WheelPage();
    }
    private static void WheelPage()
    {
        var cfg = Runtime.Settings; var input = Runtime.Wheel!;
        bool was = cfg.WheelEnabled; Toggle(250, 150, 320, "Wheel input", ref cfg.WheelEnabled);
        if (was != cfg.WheelEnabled) Runtime.Force?.Suspend("Wheel route changed");
        Label(605, 153, 180, "Local player: " + cfg.Player);
        if (Button(790, 150, 48, "-")) { cfg.Player = Math.Max(0, cfg.Player - 1); Dirty(); }
        if (Button(850, 150, 48, "+")) { cfg.Player = Math.Min(3, cfg.Player + 1); Dirty(); }
        Label(250, 197, 660, "Release pedals / E-Brake first. Centre the wheel; turn right first when calibrating.", true, 30);
        int row = 0;
        foreach (string name in new[] { "Steer", "Throttle", "Brake", "Handbrake" })
        {
            float y = 228 + row++ * 91; var binding = input.Bindings.Axis(name);
            Label(250, y, 140, ActionLabel(name));
            Label(385, y, 385, binding == null ? "Not bound" : (name == "Handbrake" && !input.Bindings.HandbrakeUsesAxis ? "Inactive / " : "") + Runtime.Devices!.Describe(binding.DeviceGuid) + " / " + Axes[binding.Axis], true, 30);
            if (Button(772, y - 3, 140, "Calibrate")) input.BeginAxis(name);
            float value = 0; bool available = Runtime.Devices!.TryAxis(binding, out value);
            Bar(250, y + 33, 430, available ? value : 0, name == "Steer");
            Label(698, y + 24, 75, available ? value.ToString("F2") : "--", true);
            if (binding != null)
            {
                if (Button(772, y + 39, 66, "Invert")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Rest = binding.Calibration.End, End = binding.Calibration.Rest } }); input.Save(); }
                if (Button(846, y + 39, 66, "Clear")) { input.Bindings.SetAxis(name, null); if (name == "Handbrake") input.Bindings.HandbrakeUsesAxis = false; input.Save(); }
                Label(250, y + 52, 210, "Deadzone: " + binding.Calibration.Deadzone.ToString("P0"), true);
                if (Button(470, y + 52, 40, "-")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Deadzone = Math.Max(0, binding.Calibration.Deadzone - .01) } }); input.Save(); }
                if (Button(520, y + 52, 40, "+")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Deadzone = Math.Min(.25, binding.Calibration.Deadzone + .01) } }); input.Save(); }
                if (name == "Handbrake" && !input.Bindings.HandbrakeUsesAxis && Button(590, y + 52, 140, "Use axis")) { input.Bindings.HandbrakeUsesAxis = true; input.Save(); }
            }
        }
        if (input.Capture != null)
        {
            Fill(R(238, 605, 700, 72), new Color(.17f, .15f, .08f, 1));
            Label(250, 610, 380, ActionLabel(input.CaptureAxis!) + ": " + input.Capture.Status, true, 60);
            if (Button(640, 620, 172, "Save calibration")) input.FinishAxis();
            if (Button(820, 620, 92, "Cancel")) input.Cancel();
        }
        else Label(250, 625, 660, input.Status, true, 40);
    }
    private static void ButtonsPage()
    {
        var input = Runtime.Wheel!;
        Label(250, 154, 670, "Bind a wheel, shifter or stalk button. F6 and F8 always remain available on the keyboard.", true, 48);
        for (int i = 0; i < 7; i++)
        {
            int index = _buttonPage * 7 + i; if (index >= WheelInput.ButtonActions.Length) break;
            string action = WheelInput.ButtonActions[index]; float y = 222 + i * 51;
            Label(250, y, 160, ActionLabel(action));
            string text = BindingLabel(action);
            if (action == "Handbrake" && input.Bindings.HandbrakeUsesAxis) text = "Inactive / " + text;
            Label(410, y, 320, text, true, 42);
            if (Button(742, y - 4, 90, "Bind")) input.BeginButton(action);
            if (Button(842, y - 4, 70, "Clear")) { ClearBinding(action); }
        }
        if (Button(250, 591, 130, "Previous")) _buttonPage = 0;
        if (Button(393, 591, 130, "Next")) _buttonPage = 1;
        if (input.CaptureButton != null)
        { Label(250, 637, 540, "Press a control for " + ActionLabel(input.CaptureButton) + "…", true); if (Button(820, 631, 92, "Cancel")) input.Cancel(); }
        else Label(250, 638, 660, "Driving buttons follow the stock actions. H-pattern and menu navigation are not added yet.", true, 38);
    }
    private static void ForcePage()
    {
        var cfg = Runtime.Settings; var force = Runtime.Force!;
        Label(250, 154, 165, "FFB:");
        if (Button(425, 150, 95, "Off", !cfg.FfbEnabled)) force.SetEnabled(false);
        if (Button(530, 150, 95, "On", cfg.FfbEnabled)) force.SetEnabled(true);
        Label(650, 154, 262, "Remembers your choice", true);
        var wheels = Runtime.Devices!.Devices.Where(x => x.Info.ForceFeedback).ToArray();
        string selected = Guid.TryParse(cfg.FfbGuid, out var id) ? Runtime.Devices.Describe(id) : "Choose a wheel";
        Label(250, 202, 490, "Output: " + selected);
        if (Button(752, 194, 160, "Next wheel"))
        {
            force.Reconnect("Output device changed");
            int current = Array.FindIndex(wheels, x => x.Info.InstanceGuid?.ToString() == cfg.FfbGuid);
            if (wheels.Length > 0) cfg.FfbGuid = wheels[(current + 1) % wheels.Length].Info.InstanceGuid!.Value.ToString();
            Dirty();
        }
        Slider(253, "Strength (%)", ref cfg.FfbStrength, 0, 100, "F0");
        Slider(298, "Peak output cap (%)", ref cfg.FfbPeak, 0, 50, "F0");
        Slider(343, "Smoothing (ms)", ref cfg.FfbSmoothing, 0, 200, "F0");
        Slider(388, "Steering damping", ref cfg.FfbDamping, 0, .5f);
        if (Button(250, 430, 245, "Advanced", _advancedFfb)) _advancedFfb = !_advancedFfb;
        if (_advancedFfb)
        { Slider(473, "Reference front load", ref cfg.FfbLoadReference, 100, 50000, "F0"); Slider(517, "Slip response scale", ref cfg.FfbSlipScale, .02f, 3); }
        bool invert = cfg.FfbInvert; Toggle(552, 430, 360, "Invert FFB", ref cfg.FfbInvert); if (invert != cfg.FfbInvert) force.Suspend("Direction changed; close settings to drive");
        Label(250, 570, 662, force.Status + $". Accepted output {force.Sent:P1}; failed calls {force.Failures}.", true, 45);
        Label(250, 625, 662, "FFB starts while driving when On. Default strength: 50%. Settings and pauses stop force; F8 saves Off until you choose On.", true, 46);
    }
    private static string BindingLabel(string action)
    {
        var bindings = Runtime.Wheel!.Bindings;
        var text = bindings.Buttons.TryGetValue(action, out var b) ? Runtime.Devices!.Describe(b.DeviceGuid) + " / button " + (b.Button + 1) : "";
        if (bindings.CameraKeys.TryGetValue(action, out var key) && key != "None") text += (text.Length > 0 ? " + " : "") + key;
        return text.Length == 0 ? "Not bound" : text;
    }
    private static void ClearBinding(string action)
    {
        var input = Runtime.Wheel!; input.Bindings.Buttons.Remove(action);
        if (input.Bindings.CameraKeys.ContainsKey(action)) input.Bindings.CameraKeys[action] = "None";
        input.Save();
    }
    private static void CameraPage()
    {
        if (Button(590, 104, 150, "Position", !_cameraBindings)) { Runtime.Wheel?.Cancel(); _cameraBindings = false; }
        if (Button(755, 104, 157, "Bindings", _cameraBindings)) { Runtime.Wheel?.Cancel(); _cameraBindings = true; }
        if (_cameraBindings) { CameraBindingsPage(); return; }
        var cfg = Runtime.Settings;
        Toggle(250, 150, 390, "Bonnet in camera cycle", ref cfg.Bonnet);
        Toggle(250, 190, 390, "Bumper in camera cycle", ref cfg.Bumper);
        if (Button(250, 239, 160, "Bonnet", !_editBumper)) _editBumper = false;
        if (Button(425, 239, 160, "Bumper", _editBumper)) _editBumper = true;
        if (!_editBumper && Button(630, 239, 282, "Fit to car: " + (cfg.CameraAutoFit ? "On" : "Off"), cfg.CameraAutoFit))
        { if (cfg.CameraAutoFit) cfg.SetCameraPose(false, MountedCamera.Pose(false)); cfg.CameraAutoFit = !cfg.CameraAutoFit; Dirty(); }
        var pose = MountedCamera.Pose(_editBumper);
        float side = pose.Side, height = pose.Height, forward = pose.Forward, pitch = pose.Pitch, fov = pose.Fov;
        Slider(295, "Side position (m)", ref side, -2, 2);
        Slider(343, "Height (m)", ref height, .1f, 3);
        Slider(391, "Forward position (m)", ref forward, -2, 4);
        Slider(439, "Pitch down (degrees)", ref pitch, -30, 30, "F1");
        Slider(487, "Field of view", ref fov, 30, 110, "F0");
        var edited = new CameraPose(side, height, forward, pitch, fov);
        if (edited != pose) { cfg.SetCameraPose(_editBumper, edited); if (!_editBumper) cfg.CameraAutoFit = false; Dirty(); }
        if (Button(250, 544, 260, "Reset this view")) { cfg.ResetCamera(_editBumper); Dirty(); }
        Label(535, 546, 377, "Adjustments save automatically.", true);
        Label(250, 590, 662, "Camera: " + MountedCamera.Status, true, 30);
        Label(250, 629, 662, "Cycle to Bonnet or Bumper, then tune with the numpad. Open Bindings to change those keys or use wheel buttons. Fit to car needs a loaded vehicle.", true, 46);
    }
    private static void CameraBindingsPage()
    {
        var input = Runtime.Wheel!;
        Label(250, 151, 662, "Bind a keyboard key or wheel button. Tuning affects the active Bonnet / Bumper view. F6 / F8 remain reserved; Escape cancels.", true, 43);
        for (int i = 0; i < 7; i++)
        {
            int index = _cameraBindingPage * 7 + i; if (index >= CameraTuning.Actions.Length) break;
            string action = CameraTuning.Actions[index]; float y = 212 + i * 51;
            Label(250, y, 164, CameraTuning.Labels[index]);
            Label(416, y, 318, BindingLabel(action), true, 42);
            if (Button(742, y - 4, 90, "Bind")) input.BeginButton(action);
            if (Button(842, y - 4, 70, "Clear")) ClearBinding(action);
        }
        if (Button(250, 584, 130, "Previous")) { input.Cancel(); _cameraBindingPage = 0; }
        if (Button(393, 584, 130, "Next")) { input.Cancel(); _cameraBindingPage = 1; }
        Label(550, 588, 130, $"Page {_cameraBindingPage + 1} / 2", true);
        if (input.CaptureButton != null)
        { Label(250, 638, 545, "Press a key or button for " + ActionLabel(input.CaptureButton) + "…", true, 38); if (Button(820, 632, 92, "Cancel")) input.Cancel(); }
        else Label(250, 631, 662, "Numpad: 8/2 height, 9/7 forward/back, 4/6 side, 1/3 pitch, +/− field of view, 0 reset. The game's own camera keys still work.", true, 46);
    }
    private static void TelemetryPage()
    {
        var cfg = Runtime.Settings;
        Label(250, 160, 230, "Forza UDP port (0 = off)"); _forza = GUI.TextArea(R(620, 152, 150, 38), _forza, 5);
        Label(250, 216, 230, "Detailed JSON UDP port"); _detail = GUI.TextArea(R(620, 208, 150, 38), _detail, 5);
        float rate = cfg.DetailHz; Slider(274, "Detailed stream Hz", ref rate, 1, 60, "F0"); cfg.DetailHz = (int)Math.Round(rate);
        if (Button(250, 325, 300, "Apply connection"))
        {
            if (int.TryParse(_forza, out int f) && f is >= 0 and <= 65535 && int.TryParse(_detail, out int d) && d is >= 0 and <= 65535 && (f == 0 || f != d))
            { cfg.ForzaPort = f; cfg.DetailPort = d; Runtime.ApplyOutputs(); Dirty(); }
            else Message = "Use ports 0..65535; active ports must differ.";
        }
        Label(250, 389, 660, $"Forza packets: {Runtime.Output?.ForzaPackets}   Lost network ticks: {Runtime.Output?.OverwrittenTicks}   Errors: {Runtime.Output?.SendErrors}", false, 54);
        Label(250, 451, 660, $"Recording: {Runtime.Output?.RecordingStatus}   Dropped samples: {Runtime.Output?.RecordingDrops}", false, 42);
        Label(250, 503, 660, Runtime.Output?.LastError ?? "Outputs stay on this computer (127.0.0.1).", true, 40);
        Label(250, 561, 660, "Diagnostic recordings are managed outside the game. Any requested capture finishes automatically on normal exit (20 minutes / 64 MiB maximum).", true, 64);
    }
    private static void HelpPage()
    {
        if (Button(620, 104, 292, "Advanced device details", _deviceDetails)) _deviceDetails = !_deviceDetails;
        if (_deviceDetails) { DevicesPage(); return; }
        Label(250, 162, 662, "Woden Rally Edge Wheel " + Plugin.Version + " / development build", false, 42);
        Label(250, 218, 662, "No controls? Enable Wheel controls in Setup, then check the bars. Use Controls to bind and calibrate each axis.", true, 70);
        Label(250, 310, 662, "No feedback? Check FFB is On and your wheel is selected. The FFB page shows why output is inactive. Close settings and drive. F8 saves Off.", true, 76);
        Label(250, 403, 662, "Camera: use the game's normal Change camera action. Bonnet and Bumper are added to that cycle. Scripted game cameras release our view and FFB.", true, 80);
        Label(250, 504, 662, "Report what happened and roughly when. Diagnostic recording can be prepared before launch; you do not need to manage it in the menu. Close the game normally to finish a capture.", true, 80);
        Label(250, 602, 662, "Setup currently requires a mouse. Menu navigation, H-pattern and a combined support-file action are still being built.", true, 60);
    }
    private static void DevicesPage()
    {
        if (Button(250, 154, 280, "Refresh / reconnect devices")) { Runtime.Force?.Reconnect("Device refresh"); Runtime.Devices?.Refresh(); }
        Label(250, 205, 665, Runtime.Devices?.Status ?? "No device service", true, 40);
        var devices = Runtime.Devices!.Devices;
        if (_devicePage >= Math.Max(1, devices.Count)) _devicePage = 0;
        if (devices.Count == 0) return;
        var d = devices[_devicePage];
        Label(250, 267, 660, d.Info.Name + (d.Ok ? " — reading" : " — unavailable"));
        Label(250, 305, 660, $"{d.Info.Axes} axes / {d.Info.Buttons} buttons / FFB: {d.Info.ForceFeedback}", true);
        Label(250, 344, 660, d.Info.InstanceGuid?.ToString() ?? "No GUID", true);
        for (int i = 0; i < 8; i++) Label(250 + (i % 2) * 330, 394 + (i / 2) * 38, 300, $"{Axes[i]}: {d.Axes[i]}", true);
        Label(250, 565, 660, "Pressed: " + string.Join(", ", Enumerable.Range(0, 128).Where(i => d.Buttons[i] != 0).Select(i => i + 1)), true, 38);
        if (Button(250, 614, 220, "Next device")) _devicePage = (_devicePage + 1) % devices.Count;
        Label(500, 619, 410, $"Device {_devicePage + 1} of {devices.Count}. Reading applies no force.", true);
    }
}
