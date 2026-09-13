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
    private static string _saveStatus = "Saved";
    private static string ActionLabel(string action) => action switch
    {
        "Steer" => "Steering", "Gear up" => "Shift up", "Gear down" => "Shift down", "Camera" => "Change camera",
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
            if (_dirty && Runtime.Clock.Elapsed.TotalSeconds - _changedAt > .7) Save();
        }
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
        var cfg = Runtime.Settings;
        bool previous = cfg.WheelEnabled; Toggle(250, 150, 320, "Wheel controls", ref cfg.WheelEnabled);
        if (previous != cfg.WheelEnabled) Runtime.Force?.Disarm("Wheel controls changed");
        if (Button(640, 150, 272, "Refresh devices")) { Runtime.Force?.Disarm("Device refresh"); Runtime.Devices?.Refresh(); }
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
    private static void ControlsPage()
    {
        if (Button(535, 104, 180, "Wheel and pedals", !_controlsButtons)) { Runtime.Wheel?.Cancel(); _controlsButtons = false; }
        if (Button(730, 104, 182, "Buttons", _controlsButtons)) { Runtime.Wheel?.Cancel(); _controlsButtons = true; }
        if (_controlsButtons) ButtonsPage(); else WheelPage();
    }
    private static void WheelPage()
    {
        var cfg = Runtime.Settings; var input = Runtime.Wheel!;
        bool was = cfg.WheelEnabled; Toggle(250, 150, 320, "Wheel input", ref cfg.WheelEnabled);
        if (was != cfg.WheelEnabled) Runtime.Force?.Disarm("Wheel route changed");
        Label(605, 153, 180, "Local player: " + cfg.Player);
        if (Button(790, 150, 48, "-")) { cfg.Player = Math.Max(0, cfg.Player - 1); Dirty(); }
        if (Button(850, 150, 48, "+")) { cfg.Player = Math.Min(3, cfg.Player + 1); Dirty(); }
        Label(250, 197, 660, "Release pedals and centre the wheel before Calibrate. Turn right first for steering.", true, 40);
        int row = 0;
        foreach (string name in new[] { "Steer", "Throttle", "Brake" })
        {
            float y = 248 + row++ * 120; var binding = input.Bindings.Axis(name);
            Label(250, y, 140, ActionLabel(name));
            Label(385, y, 385, binding == null ? "Not bound" : Runtime.Devices!.Describe(binding.DeviceGuid) + " / " + Axes[binding.Axis], true, 30);
            if (Button(772, y - 3, 140, "Calibrate")) input.BeginAxis(name);
            float value = 0; bool available = Runtime.Devices!.TryAxis(binding, out value);
            Bar(250, y + 38, 430, available ? value : 0, name == "Steer");
            Label(698, y + 29, 75, available ? value.ToString("F2") : "--", true);
            if (binding != null)
            {
                if (Button(772, y + 39, 66, "Invert")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Rest = binding.Calibration.End, End = binding.Calibration.Rest } }); input.Save(); }
                if (Button(846, y + 39, 66, "Clear")) { input.Bindings.SetAxis(name, null); input.Save(); }
                Label(250, y + 61, 210, "Deadzone: " + binding.Calibration.Deadzone.ToString("P0"), true);
                if (Button(470, y + 61, 40, "-")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Deadzone = Math.Max(0, binding.Calibration.Deadzone - .01) } }); input.Save(); }
                if (Button(520, y + 61, 40, "+")) { input.Bindings.SetAxis(name, binding with { Calibration = binding.Calibration with { Deadzone = Math.Min(.25, binding.Calibration.Deadzone + .01) } }); input.Save(); }
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
            string text = input.Bindings.Buttons.TryGetValue(action, out var b) ? Runtime.Devices!.Describe(b.DeviceGuid) + " / button " + (b.Button + 1) : "Not bound";
            Label(410, y, 320, text, true, 42);
            if (Button(742, y - 4, 90, "Bind")) input.BeginButton(action);
            if (Button(842, y - 4, 70, "Clear")) { input.Bindings.Buttons.Remove(action); input.Save(); }
        }
        if (Button(250, 591, 130, "Previous")) _buttonPage = 0;
        if (Button(393, 591, 130, "Next")) _buttonPage = 1;
        if (input.CaptureButton != null)
        { Label(250, 637, 540, "Press a button for " + ActionLabel(input.CaptureButton) + "…", true); if (Button(820, 631, 92, "Cancel")) input.Cancel(); }
        else Label(250, 638, 660, "Driving buttons follow the stock actions. H-pattern and menu navigation are not added yet.", true, 38);
    }
    private static void ForcePage()
    {
        var cfg = Runtime.Settings; var force = Runtime.Force!;
        bool prior = cfg.FfbEnabled; Toggle(250, 150, 290, "FFB", ref cfg.FfbEnabled);
        if (prior != cfg.FfbEnabled) force.Disarm();
        if (Button(552, 150, 360, force.Armed ? "Stop FFB" : "Start FFB for this session", force.Armed)) { if (force.Armed) force.Panic(); else force.Arm(); }
        var wheels = Runtime.Devices!.Devices.Where(x => x.Info.ForceFeedback).ToArray();
        string selected = Guid.TryParse(cfg.FfbGuid, out var id) ? Runtime.Devices.Describe(id) : "Choose a wheel";
        Label(250, 202, 490, "Output: " + selected);
        if (Button(752, 194, 160, "Next wheel"))
        {
            force.Disarm("Output device changed");
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
        bool invert = cfg.FfbInvert; Toggle(552, 430, 360, "Invert FFB", ref cfg.FfbInvert); if (invert != cfg.FfbInvert) force.Disarm("Direction changed; start the session output again when ready");
        Label(250, 570, 662, force.Status + $". Accepted output {force.Sent:P1}; failed calls {force.Failures}.", true, 45);
        Label(250, 625, 662, "Experimental FFB starts only for this session. Begin at 10%. Feedback is inactive while settings are open; close the panel to drive.", true, 46);
    }
    private static void CameraPage()
    {
        var cfg = Runtime.Settings;
        Toggle(250, 154, 390, "Bonnet in camera cycle", ref cfg.Bonnet);
        Toggle(250, 200, 390, "Bumper in camera cycle", ref cfg.Bumper);
        if (Button(250, 253, 180, "Bonnet offsets", !_editBumper)) _editBumper = false;
        if (Button(445, 253, 180, "Bumper offsets", _editBumper)) _editBumper = true;
        if (_editBumper)
        { Slider(317, "Height", ref cfg.BumperHeight, .1f, 3); Slider(373, "Forward offset", ref cfg.BumperForward, -2, 4); Slider(429, "Pitch (degrees)", ref cfg.BumperPitch, -30, 30, "F1"); }
        else
        { Slider(317, "Height", ref cfg.CameraHeight, .1f, 3); Slider(373, "Forward offset", ref cfg.CameraForward, -2, 4); Slider(429, "Pitch (degrees)", ref cfg.CameraPitch, -30, 30, "F1"); }
        if (Button(250, 490, 260, "Reset this view"))
        { if (_editBumper) { cfg.BumperHeight = .35f; cfg.BumperForward = 2.2f; cfg.BumperPitch = 0; } else { cfg.CameraHeight = .85f; cfg.CameraForward = .75f; cfg.CameraPitch = 3; } Dirty(); }
        Label(250, 545, 662, "Camera: " + MountedCamera.Status, true, 42);
        Label(250, 597, 662, "Use the game's normal Camera button: stock views, bonnet, bumper, then stock again. Game camera takeover releases the mounted view and stops FFB.", true, 67);
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
        if (Button(565, 325, 347, cfg.Record ? "Stop recording" : "Start recording", cfg.Record)) { cfg.Record = !cfg.Record; Runtime.ApplyOutputs(); Dirty(); }
        Label(250, 389, 660, $"Forza packets: {Runtime.Output?.ForzaPackets}   Lost network ticks: {Runtime.Output?.OverwrittenTicks}   Errors: {Runtime.Output?.SendErrors}", false, 54);
        Label(250, 451, 660, $"Recording: {Runtime.Output?.RecordingStatus}   Dropped samples: {Runtime.Output?.RecordingDrops}", false, 42);
        Label(250, 503, 660, Runtime.Output?.LastError ?? "Outputs stay on this computer (127.0.0.1).", true, 40);
        Label(250, 561, 660, "Capture is bounded to 20 minutes / 64 MiB. Files are in BepInEx/WodenRecordings. Raw engine/gear scales remain separate from validated motion.", true, 64);
    }
    private static void HelpPage()
    {
        if (Button(620, 104, 292, "Advanced device details", _deviceDetails)) _deviceDetails = !_deviceDetails;
        if (_deviceDetails) { DevicesPage(); return; }
        Label(250, 162, 662, "Woden Rally Edge Wheel " + Plugin.Version + " / development build", false, 42);
        Label(250, 218, 662, "No controls? Enable Wheel controls in Setup, then check the bars. Use Controls to bind and calibrate each axis.", true, 70);
        Label(250, 310, 662, "No feedback? Turn FFB on, select your physical wheel and start FFB for this session. Close settings and drive. F8 stops feedback.", true, 76);
        Label(250, 403, 662, "Camera: use the game's normal Change camera action. Bonnet and Bumper are added to that cycle. Scripted game cameras release our view and FFB.", true, 80);
        Label(250, 504, 662, "For a problem report, start a short recording in Telemetry and stop it before exiting. Files: BepInEx/WodenRecordings. Loader log: BepInEx/LogOutput.log.", true, 80);
        Label(250, 602, 662, "Setup currently requires a mouse. Menu navigation, H-pattern and a combined support-file action are still being built.", true, 60);
    }
    private static void DevicesPage()
    {
        if (Button(250, 154, 280, "Refresh / reconnect devices")) { Runtime.Force?.Disarm("Device refresh"); Runtime.Devices?.Refresh(); }
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
