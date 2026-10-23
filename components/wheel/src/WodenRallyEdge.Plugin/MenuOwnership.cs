using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Neutralize the producer's existing objects/arrays, including consumers that
// retain their references. Never replace bindings, input devices or action maps.
internal static class MenuOwnership
{
    private static readonly InputReleaseGate CloseRelease = new(), CaptureRelease = new();
    private static readonly Dictionary<EventSystem, bool> Events = new();
    private static readonly string[] MenuControls = MenuNavigation.Actions.Concat(new[] { "Settings panel", "Pause" }).ToArray();
    private static GamePadSystem? _system;
    private static bool _stockHeld, _stockKnown, _cycleHeld, _cycleFailed;
    private static int _cycleReads;
    private static string? _cycleBlocker, _stockBlocker;
    private static double _stockAt = double.NegativeInfinity, _nextError;
    internal static bool Closing { get; private set; }
    internal static long StockReads { get; private set; }
    internal static string Status { get; private set; } = "Waiting for stock input producer";
    internal static string ReleaseBlocker { get; private set; } = "Waiting for stock input producer";
    internal static bool Blocking => Panel.Open;

    internal static void Begin()
    {
        Closing = false; CloseRelease.Reset(); CaptureRelease.Reset();
        OwnEvents();
        if (_system != null) MaskAll(_system);
    }
    internal static void RequestClose() { if (Closing) return; Closing = true; CloseRelease.Reset(); }
    internal static void CancelClose() { Closing = false; CloseRelease.Reset(); }
    internal static void End()
    {
        Closing = false; CloseRelease.Reset(); CaptureRelease.Reset();
        foreach (var entry in Events) if (entry.Key != null) entry.Key.enabled = entry.Value;
        Events.Clear();
    }
    private static void OwnEvents()
    {
        var current = EventSystem.current;
        if (current == null) return;
        if (!Events.ContainsKey(current)) Events.Add(current, current.enabled);
        current.enabled = false;
    }
    internal static void BeginCapture() => CaptureRelease.Reset();
    internal static bool CaptureReady()
    {
        var input = Aggregate(Runtime.Clock.Elapsed.TotalSeconds);
        if (!input.Known) { CaptureRelease.Reset(); return false; }
        if (CaptureRelease.Ready) return true;
        return CaptureRelease.Observe(input.Known, input.Held, Runtime.Clock.Elapsed.TotalSeconds);
    }
    internal static (bool Known, bool Held) Aggregate(double now)
    {
        try
        {
            var keyboard = Keyboard.current;
            // Direct startup shortcuts read legacy Input even when InputSystem
            // exists. Both routes must be released before returning ownership.
            bool legacyHeld = Input.anyKey;
            bool keyboardHeld = legacyHeld || keyboard != null && keyboard.anyKey.isPressed;
            bool known = Runtime.Focused && _stockKnown && now >= _stockAt && now - _stockAt < .15;
            bool held = _stockHeld || keyboardHeld;
            string? blocker = _stockHeld ? _stockBlocker ?? "Stock input held" : null;
            var mouse = Mouse.current;
            bool left = mouse != null ? mouse.leftButton.isPressed : Input.GetMouseButton(0);
            bool right = mouse != null ? mouse.rightButton.isPressed : Input.GetMouseButton(1);
            bool middle = mouse != null ? mouse.middleButton.isPressed : Input.GetMouseButton(2);
            held |= left || right || middle;
            if (left || right || middle) blocker ??= left ? "Mouse left button" : right ? "Mouse right button" : "Mouse middle button";
            if (keyboardHeld) blocker ??= legacyHeld ? LegacyHeld() : "InputSystem keyboard key";
            foreach (string action in MenuControls)
                if (Runtime.Wheel?.Button(action, false) == true) { held = true; blocker ??= "Bound " + action + " button"; }
            ReleaseBlocker = !Runtime.Focused ? "Game is unfocused" : !_stockKnown ? "Stock input read unavailable" :
                now < _stockAt || now - _stockAt >= .15 ? "Stock input read is stale" : blocker ?? "Waiting for 100 ms of released input";
            return (known, held);
        }
        catch (Exception ex) { ReleaseBlocker = "Input release read failed"; Failed("Input release read", ex); return (false, true); }
    }
    // Legacy AnyKey also covers joystick buttons (a wheel, shifter or pad), not only keys and the mouse. Name a held one
    // so a latched switch or a shifter left in gear is not reported as a key. GetJoystickNames is stripped in this build.
    private static string LegacyHeld()
    {
        for (int stick = 0; stick < 8; stick++)
            for (int button = 0; button < 20; button++)
                if (Input.GetKey((KeyCode)((int)KeyCode.Joystick1Button0 + 20 * stick + button)))
                    return "Legacy joystick " + (stick + 1) + " button " + button + " (a wheel, shifter or pad button)";
        return "Legacy input AnyKey (key or mouse button)";
    }
    internal static void Tick()
    {
        if (!Blocking) return;
        OwnEvents();
        if (!Closing) return;
        double now = Runtime.Clock.Elapsed.TotalSeconds;
        var input = Aggregate(now);
        Panel.Message = "Waiting to close: " + ReleaseBlocker + ". Release the input, or choose Keep settings open.";
        if (CloseRelease.Observe(input.Known, input.Held, now)) Panel.CompleteClose(true);
    }
    internal static void BeforeStock(GamePadSystem system)
    { _system = system; _cycleHeld = false; _cycleFailed = false; _cycleReads = 0; _cycleBlocker = null; }
    internal static void AfterRead(GamePadSystem.Game_Pad pad)
    {
        if (!Blocking) return;
        try
        {
            string? blocker = HeldReason(pad);
            _cycleHeld |= blocker != null;
            if (blocker != null) _cycleBlocker ??= "Stock pad read " + _cycleReads + ": " + blocker;
            StockReads++; _cycleReads++; if (Blocking) Mask(pad);
        }
        catch (Exception ex) { _cycleFailed = true; Failed("Stock input read", ex); }
    }
    internal static void AfterStock(GamePadSystem system)
    {
        if (!Blocking) { _stockKnown = false; return; } // No input scan in normal driving.
        try
        {
            // Also cover producer paths other than ReadInputs, and repeat the
            // mask after native aggregation so later readers see neutral state.
            int index = 0;
            foreach (var pad in system.Game_Pads)
            {
                string? blocker = HeldReason(pad);
                _cycleHeld |= blocker != null;
                if (blocker != null) _cycleBlocker ??= "Stock pad " + index + ": " + blocker;
                if (Blocking) Mask(pad);
                index++;
            }
            _stockHeld = _cycleHeld; _stockBlocker = _cycleBlocker;
            _stockKnown = !_cycleFailed && _cycleReads >= system.Game_Pads.Count; _stockAt = Runtime.Clock.Elapsed.TotalSeconds;
            Status = _stockKnown ? "Stock input observed; reads=" + StockReads + (_stockHeld ? "; held=" + _stockBlocker : "; neutral") : "Stock input unavailable";
        }
        catch (Exception ex) { _stockKnown = false; Failed("Stock input aggregation", ex); }
    }
    internal static void Failed(string route, Exception error)
    {
        Status = route + ": " + error.Message;
        if (Runtime.Clock.Elapsed.TotalSeconds >= _nextError)
        { Runtime.Log.LogWarning(Status); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 10; }
    }
    internal static bool Held(GamePadSystem.Game_Pad pad) => HeldReason(pad) != null;
    private static string? HeldReason(GamePadSystem.Game_Pad pad)
    {
        if (pad.A) return "A button"; if (pad.B) return "B button"; if (pad.X) return "X button"; if (pad.Y) return "Y button";
        if (pad.Back) return "Back button"; if (pad.Start) return "Start button";
        if (pad.LB) return "LB button"; if (pad.RB) return "RB button"; if (pad.L3) return "L3 button"; if (pad.R3) return "R3 button";
        if (pad.Dpad_Up) return "D-pad up"; if (pad.Dpad_Down) return "D-pad down";
        if (pad.Dpad_Left) return "D-pad left"; if (pad.Dpad_Right) return "D-pad right";
        if (!float.IsFinite(pad.LS.x) || Math.Abs(pad.LS.x) > .25f) return AxisReason("LS.x", pad.LS.x);
        if (!float.IsFinite(pad.LS.y) || Math.Abs(pad.LS.y) > .25f) return AxisReason("LS.y", pad.LS.y);
        if (!float.IsFinite(pad.RS.x) || Math.Abs(pad.RS.x) > .25f) return AxisReason("RS.x", pad.RS.x);
        if (!float.IsFinite(pad.RS.y) || Math.Abs(pad.RS.y) > .25f) return AxisReason("RS.y", pad.RS.y);
        var actions = pad.PadActions;
        if (actions != null) for (int i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            if (action.Pressed || !float.IsFinite(action.value) || Math.Abs(action.value) > .25f)
                return AxisReason("PadActions[" + i + "]", action.value) + (action.Pressed ? " (Pressed)" : "");
        }
        return pad.AnyKey ? "AnyKey (stock aggregate)" : null;
    }
    private static string AxisReason(string name, float value) => name + "=" + value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static void MaskAll(GamePadSystem system)
    { try { foreach (var pad in system.Game_Pads) Mask(pad); } catch (Exception ex) { _stockKnown = false; Failed("Stock input suppression", ex); } }
    internal static void Mask(GamePadSystem.Game_Pad pad)
    {
        pad.AnyKey = pad.A = pad.B = pad.X = pad.Y = pad.Back = pad.Start = pad.LB = pad.RB = pad.L3 = pad.R3 = pad.Dpad_Up = pad.Dpad_Down = pad.Dpad_Left = pad.Dpad_Right = false;
        pad.LS = pad.RS = new Vector2(0, 0); pad.LT = pad.RT = 0;
        var values = pad.InputFloats;
        if (values != null) for (int i = 0; i < values.Length; i++) values[i] = 0;
        var actions = pad.PadActions;
        if (actions != null) for (int i = 0; i < actions.Length; i++)
        { var action = actions[i]; action.value = 0; action.Pressed = false; actions[i] = action; }
    }
}
