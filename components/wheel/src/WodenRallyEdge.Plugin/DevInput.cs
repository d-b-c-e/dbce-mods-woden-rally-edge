using BepInEx.Configuration;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WodenRallyEdge;

/// <summary>
/// Development-only keyboard injection through the Input System's own event queue,
/// driven by a command file, so an unattended test can drive menus without focus or
/// OS input. Ported from iRacing Arcade's DevInput.
/// </summary>
/// <remarks>
/// Off unless <c>[Dev] InputCommandFile = true</c>. Lines in <c>dev\cmd.txt</c> beside the
/// plugin: <c>press Enter</c>, <c>hold W 3000</c>, <c>release W</c>, <c>shot name</c>
/// (screenshot into <c>dev\</c>). The file is consumed and deleted. SendInput can't reach
/// the Input System (it drops events without a device handle), so events are queued
/// in-process with <see cref="StateEvent.From"/>.
/// </remarks>
internal static class DevInput
{
    // KeyboardState: InputEvent header (20 bytes) + FourCC format (4) precede the key bitfield.
    private const int StateOffset = 24;

    private static ConfigEntry<bool> _enabled = null!;
    private static string? _dir, _cmdPath;
    private static double _nextPoll;
    private static bool _announced, _dirty;
    private static readonly Dictionary<Key, double> Held = new();
    // Game pad overrides applied after GamePadSystem.ReadInputs: button name -> until.
    private static readonly Dictionary<string, double> PadHeld = new();
    private static Vector2 _stick;
    private static double _stickUntil;

    internal static void Bind(ConfigFile cfg, string pluginDirectory)
    {
        _enabled = cfg.Bind("Dev", "InputCommandFile", false, "Development only: read keyboard commands from dev\\cmd.txt beside the plugin.");
        _dir = Path.Combine(pluginDirectory, "dev");
        _cmdPath = Path.Combine(_dir, "cmd.txt");
    }

    internal static void FrameTick(double now)
    {
        if (!_enabled.Value || _cmdPath == null) return;
        if (!_announced)
        {
            _announced = true;
            try { Directory.CreateDirectory(_dir!); } catch { }
            Runtime.Log.LogInfo("Dev input: watching " + _cmdPath);
            // The Input System disables devices in the background, dropping injected events.
            try
            {
                var settings = InputSystem.settings;
                if (settings != null && settings.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus)
                {
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    Runtime.Log.LogInfo("Dev input: backgroundBehavior = IgnoreFocus");
                }
            }
            catch (Exception ex) { Runtime.Log.LogWarning("Dev input: background behaviour: " + ex.Message); }
        }

        if (now >= _nextPoll)
        {
            _nextPoll = now + 0.2;
            if (File.Exists(_cmdPath))
            {
                string[]? lines = null;
                try { lines = File.ReadAllLines(_cmdPath); File.Delete(_cmdPath); }
                catch (Exception ex) { Runtime.Log.LogWarning("Dev input: " + ex.Message); }
                if (lines != null) foreach (var line in lines) Parse(line, now);
            }
        }

        bool changed = _dirty;
        _dirty = false;
        foreach (var key in Held.Where(p => p.Value <= now).Select(p => p.Key).ToList()) { Held.Remove(key); changed = true; }
        if (changed) Push();
    }

    private static void Parse(string line, double now)
    {
        var p = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 1) return;
        string verb = p[0].ToLowerInvariant();
        if (verb == "shot")
        {
            string name = p.Length > 1 ? p[1] : DateTime.Now.ToString("HHmmss");
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir!, name + ".png"), 1);
            Runtime.Log.LogInfo($"Dev input: screenshot {name}.png; triple {TripleView.Status}; screen {Screen.width}x{Screen.height}");
            return;
        }
        if (verb == "window" && p.Length >= 3 && int.TryParse(p[1], out int w) && int.TryParse(p[2], out int h))
        {
            // For testing a span without Surround: a plain window the size of three screens.
            var mode = p.Length > 3 && Enum.TryParse<FullScreenMode>(p[3], true, out var m2) ? m2 : FullScreenMode.Windowed;
            Screen.SetResolution(w, h, mode);
            Runtime.Log.LogInfo($"Dev input: {w}x{h} {mode} requested");
            return;
        }
        if (verb == "status")
        {
            Runtime.Log.LogInfo($"Dev input: triple {TripleView.Status}; camera {MountedCamera.Status}; screen {Screen.width}x{Screen.height} fullscreen {Screen.fullScreenMode}");
            return;
        }
        if (verb == "pad" && p.Length >= 2)
        {
            // pad A|B|X|Y|Start|Back|Dpad_Up|Dpad_Down|Dpad_Left|Dpad_Right [ms]
            PadHeld[p[1]] = now + (p.Length > 2 && double.TryParse(p[2], out var pm) ? pm : 150) / 1000;
            return;
        }
        if (verb == "stick" && p.Length >= 3 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sx)
            && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sy))
        {
            _stick = new Vector2(sx, sy);
            _stickUntil = now + (p.Length > 3 && double.TryParse(p[3], out var sm) ? sm : 300) / 1000;
            return;
        }
        if (p.Length < 2) return;
        if (!Enum.TryParse(p[1], true, out Key key)) { Runtime.Log.LogWarning("Dev input: unknown key " + p[1]); return; }
        double ms = p.Length > 2 && double.TryParse(p[2], out var m) ? m : 120;
        switch (verb)
        {
            case "press":
            case "hold": Held[key] = now + ms / 1000; _dirty = true; break;
            case "release": Held.Remove(key); _dirty = true; break;
        }
    }

    /// <summary>After the game reads a pad: apply held dev buttons and stick.</summary>
    internal static void AfterPadRead(GamePadSystem.Game_Pad pad)
    {
        if (!_enabled.Value) return;
        double now = Runtime.Clock.Elapsed.TotalSeconds;
        bool any = false;
        foreach (var (name, until) in PadHeld.ToList())
        {
            if (until <= now) { PadHeld.Remove(name); continue; }
            any = true;
            switch (name.ToLowerInvariant())
            {
                case "a": pad.A = true; break;
                case "b": pad.B = true; break;
                case "x": pad.X = true; break;
                case "y": pad.Y = true; break;
                case "start": pad.Start = true; break;
                case "back": pad.Back = true; break;
                case "dpad_up": pad.Dpad_Up = true; break;
                case "dpad_down": pad.Dpad_Down = true; break;
                case "dpad_left": pad.Dpad_Left = true; break;
                case "dpad_right": pad.Dpad_Right = true; break;
            }
        }
        if (_stickUntil > now) { pad.LS = _stick; any = true; }
        if (any) pad.AnyKey = true;
    }

    // Queue one full keyboard state: everything held set, everything else clear.
    private static unsafe void Push()
    {
        var kb = Keyboard.current;
        if (kb == null) { Runtime.Log.LogWarning("Dev input: no keyboard device"); return; }
        NativeArray<byte> buffer = StateEvent.From(kb, out InputEventPtr evt, Allocator.Temp);
        try
        {
            byte* data = (byte*)evt.data;
            int size = (int)evt.sizeInBytes;
            int stateBytes = size - StateOffset;
            if (data == null || stateBytes < 15) { Runtime.Log.LogWarning($"Dev input: unexpected keyboard event size {size}"); return; }
            for (int i = 0; i < stateBytes; i++) data[StateOffset + i] = 0;
            foreach (var k in Held.Keys)
            {
                int bit = (int)k;
                if (bit / 8 < stateBytes) data[StateOffset + bit / 8] |= (byte)(1 << (bit % 8));
            }
            InputSystem.QueueEvent(evt);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Dev input: queue failed: " + ex.Message); }
        finally { try { buffer.Dispose(); } catch { } }
    }
}
