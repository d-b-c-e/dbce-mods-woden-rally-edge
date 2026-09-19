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
    private static double _stockAt = double.NegativeInfinity, _nextError;
    internal static bool Closing { get; private set; }
    internal static long StockReads { get; private set; }
    internal static string Status { get; private set; } = "Waiting for stock input producer";
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
            // Woden's configured keyboard actions also use the legacy input
            // route. It can establish release when Keyboard.current is absent.
            bool keyboardHeld = keyboard != null ? keyboard.anyKey.isPressed : Input.anyKey;
            bool known = Runtime.Focused && _stockKnown && now >= _stockAt && now - _stockAt < .15;
            bool held = _stockHeld || keyboardHeld;
            var mouse = Mouse.current;
            held |= mouse != null ? mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed : Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            foreach (string action in MenuControls) held |= Runtime.Wheel?.Button(action, false) == true;
            return (known, held);
        }
        catch (Exception ex) { Failed("Input release read", ex); return (false, true); }
    }
    internal static void Tick()
    {
        if (!Blocking) return;
        OwnEvents();
        if (!Closing) return;
        double now = Runtime.Clock.Elapsed.TotalSeconds;
        var input = Aggregate(now);
        Panel.Message = input.Known ? "Release keys, menu buttons and controls to close settings." : "Waiting for a fresh input read before closing settings.";
        if (CloseRelease.Observe(input.Known, input.Held, now)) Panel.CompleteClose(true);
    }
    internal static void BeforeStock(GamePadSystem system)
    { _system = system; _cycleHeld = false; _cycleFailed = false; _cycleReads = 0; }
    internal static void AfterRead(GamePadSystem.Game_Pad pad)
    {
        if (!Blocking) return;
        try { _cycleHeld |= Held(pad); StockReads++; _cycleReads++; if (Blocking) Mask(pad); }
        catch (Exception ex) { _cycleFailed = true; Failed("Stock input read", ex); }
    }
    internal static void AfterStock(GamePadSystem system)
    {
        if (!Blocking) { _stockKnown = false; return; } // No input scan in normal driving.
        try
        {
            // Also cover producer paths other than ReadInputs, and repeat the
            // mask after native aggregation so later readers see neutral state.
            foreach (var pad in system.Game_Pads) { _cycleHeld |= Held(pad); if (Blocking) Mask(pad); }
            _stockHeld = _cycleHeld; _stockKnown = !_cycleFailed && _cycleReads >= system.Game_Pads.Count; _stockAt = Runtime.Clock.Elapsed.TotalSeconds;
            Status = _stockKnown ? "Stock input observed; reads=" + StockReads : "Stock input unavailable";
        }
        catch (Exception ex) { _stockKnown = false; Failed("Stock input aggregation", ex); }
    }
    internal static void Failed(string route, Exception error)
    {
        Status = route + ": " + error.Message;
        if (Runtime.Clock.Elapsed.TotalSeconds >= _nextError)
        { Runtime.Log.LogWarning(Status); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 10; }
    }
    internal static bool Held(GamePadSystem.Game_Pad pad)
    {
        bool held = pad.AnyKey || pad.A || pad.B || pad.X || pad.Y || pad.Back || pad.Start || pad.LB || pad.RB || pad.L3 || pad.R3 || pad.Dpad_Up || pad.Dpad_Down || pad.Dpad_Left || pad.Dpad_Right;
        held |= Math.Abs(pad.LS.x) > .25f || Math.Abs(pad.LS.y) > .25f || Math.Abs(pad.RS.x) > .25f || Math.Abs(pad.RS.y) > .25f;
        var actions = pad.PadActions;
        if (actions != null) for (int i = 0; i < actions.Length; i++)
        { var action = actions[i]; held |= action.Pressed || !float.IsFinite(action.value) || Math.Abs(action.value) > .25f; }
        return held;
    }
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
