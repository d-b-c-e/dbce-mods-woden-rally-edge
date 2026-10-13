using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Deliberately restricted pilot. Unity cannot currently join a Gamepad to the
// shim's exact DirectInput/HID identity. Refuse coexistence instead of disabling
// a guessed device. ReadInputs keeps its separate native keyboard overlay.
internal static partial class ShimInput
{
    private static readonly ShimAction[] Actions = new ShimAction[ShimActionMap.Count];
    private static readonly byte[] Frame = new byte[ShimNative.FrameSize];
    private static readonly string[] DrivingAxes = { "steer", "throttle", "brake" };
    private static readonly ShimButtonGate _buttons = new();
    private static string _directory = "", _config = "", _reports = "";
    private static string _transmission = "Unavailable";
    private static bool _requested, _refused, _terminal;
    private static GamePadSystem? _producer;
    private static IntPtr _handle;
    private static int _frame = -1, _context = -1, _window;
    internal static bool Owns { get; private set; }
    internal static bool Ready => Owns && !_terminal;
    internal static Guid? SteeringGuid { get; private set; }
    internal static string Status { get; private set; } = "Native backend (standalone bindings)";
    // The shared native profile reports Wheelkit's persisted H-pattern label;
    // earlier fixtures and profiles can also report the compact legacy token.
    internal static bool HPattern => _transmission is "H-pattern" or "HPattern";
    internal static bool HPatternGatesAvailable => Ready && HPattern && ShimActionMap.Names
        .Where(ShimActionMap.IsGate).Any(id => Action(id).Status == ShimStatus.Live);
    internal static bool HPatternReady(MainCar? car) => Allowed && HPattern && car?.Aids?.Automatic == false;
    internal static string TransmissionStatus => HPattern ?
        (Runtime.Local?.Aids?.Automatic == true ? "H-pattern profile; native gearbox is Automatic. Select Manual in the game." :
            !HPatternGatesAvailable ? "Shifter gates unavailable; wheel drive stays neutral in Manual. Check the shared profile and device." :
            "H-pattern profile; native Manual gearbox required. Out of gear cuts wheel drive; R swaps wheel pedals.") :
        _transmission != "Sequential" ? "Gear actions unsupported: shared profile is " + _transmission + ". Select Sequential or HPattern." :
        Runtime.Local?.Aids?.Automatic == true ? "Sequential profile; native gearbox is Automatic. Shift buttons are disabled. Select Manual in the game." :
        "Sequential profile; shift buttons require the native Manual gearbox and a completed gear change.";
    private static bool ShiftReady(MainCar? car) => _transmission == "Sequential" && car?.Aids?.Automatic == false && car.TransmissionSystem?.Changing == false;
    internal static void Start(string directory, string config, string reports)
    {
        _directory = directory; _config = config; _reports = reports;
        _requested = Runtime.Settings.InputBackend == "Shim";
        if (_requested) Status = "Shim pilot: waiting for native input producer and game window; Native retained";
    }

    // Prefix: inspect before settings ownership may mask the native tables.
    internal static void BeforeStock(GamePadSystem system)
    {
        if (!_requested || _refused || _terminal) return;
        _producer = system;
        if (!Owns && (system.Game_Pads == null || system.Game_Pads.Count == 0)) return;
        try { string? reason = IsolationReason(); if (reason != null) RefuseOrStop(reason); }
        catch (Exception e) { RefuseOrStop("Cannot establish native input isolation: " + e.Message); }
    }
    private static string? IsolationReason()
    {
        if (Gamepad.all.Count != 0) return "Unity gamepads are connected; coexistence is unsupported";
        if (_producer == null || _producer.Game_Pads == null || _producer.Game_Pads.Count == 0) return "Native input producer is not ready";
        foreach (var pad in _producer.Game_Pads)
        {
            string? reason = PadReason(pad); if (reason != null) return reason;
        }
        return null;
    }
    private static string? PadReason(GamePadSystem.Game_Pad? pad)
    {
        if (pad == null || pad.Controller != null) return "A native gamepad controller remains cached";
        // ReadInputs can project cached raw pad state even without Controller.
        // Keyboard keys modify Actions independently, not these raw fields.
        if (pad.LS.x != 0 || pad.LS.y != 0 || pad.RS.x != 0 || pad.RS.y != 0 || pad.LT != 0 || pad.RT != 0 ||
            pad.A || pad.B || pad.X || pad.Y || pad.Back || pad.Start || pad.LB || pad.RB || pad.L3 || pad.R3 ||
            pad.Dpad_Up || pad.Dpad_Down || pad.Dpad_Left || pad.Dpad_Right)
            return "Native gamepad state remains cached; restart with gamepads disconnected";
        return null;
    }
    internal static bool ValidatePad(GamePadSystem.Game_Pad? pad)
    {
        if (!Ready) return false;
        // Controls can retain its own fallback pad across a scene transition.
        // Check that exact consumer source too, not only the last producer table.
        string? reason = PadReason(pad);
        if (reason == null) return true;
        RefuseOrStop(reason); return false;
    }
    internal static void Poll()
    {
        if (!_requested || _refused || _terminal) return;
        try
        {
            if (Owns && !GameWindow.IsOwned(_window)) { RefuseOrStop("Owned Unity window is no longer live, visible or valid"); return; }
            if (_producer == null) return;
            if (!Owns && (_producer.Game_Pads == null || _producer.Game_Pads.Count == 0)) return;
            string? reason = IsolationReason();
            if (reason != null) { RefuseOrStop(reason); return; }
            if (!Owns)
            {
                int window = GameWindow.CaptureForShim();
                if (window == 0) { Status = "Shim pilot: waiting for a live, visible Woden Unity window; Native retained"; return; }
                var bytes = ShimProfile.Section(File.ReadAllBytes(Path.Combine(_config, "dbce-controls.ini")));
                reason = ShimNative.Load(_directory); if (reason != null) { RefuseOrStop(reason); return; }
                string nonce = _trialNonce ?? Guid.NewGuid().ToString("N");
                string folder = Path.Combine(_reports, "run-" + nonce); Directory.CreateDirectory(folder);
                if (!GameWindow.IsOwned(window)) { Status = "Shim pilot: Unity window changed before open; Native retained"; return; }
                if (_trialNonce != null && (!TrialFences || !Runtime.Focused || !GameWindow.IsFocused(window)))
                { Status="Shim menu trial: waiting for actual Unity focus and muted menu fences; Native retained"; return; }
                string why;
                int result = _trialNonce == null ? ShimNative.Open(folder, nonce, bytes, ShimActionMap.Unsupported, new IntPtr(window), out _handle, out why) :
                    ShimNative.OpenLatched(folder, nonce, bytes, TrialUnsupported, new IntPtr(window), _trialSession!, _trialBatch!, out _handle, out why, _carTrial ? "woden-car-trial@1" : "woden-menu-trial@1");
                if (result != 0) { RefuseOrStop("Open " + result + ": " + why); return; }
                Owns = true; // Sticky: no legacy binding fallback after native ownership.
                _window = window;
                if (!GameWindow.IsOwned(_window)) { RefuseOrStop("Owned Unity window changed during open"); return; }
                SteeringGuid = ShimProfile.SteeringDevice(bytes, ShimNative.ProfileInfo(_handle), out _transmission);
                Runtime.Wheel?.Cancel();
                Runtime.Log.LogInfo("Shim pilot owns wheel input; native keyboard retained; " + ShimNative.StartReport(_handle));
            }
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                int result = ShimNative.Tick(_handle, Frame);
                if (result != 0) { RefuseOrStop("Tick " + result); return; }
                ShimNative.Decode(Frame, Actions, out ulong sequence, out _, out _, out byte run);
                if (!AcceptFrame(sequence,run)) return;
                _context = Context;
                _buttons.Observe(Actions, _context, Allowed, KnownNeutralMask);
                Status = InputFocused ? "Shim pilot active · wheel-only · native keyboard retained" :
                    "Shim pilot active · Unity window unfocused; wheel actions neutral";
            }
        }
        catch (Exception e) { RefuseOrStop(e.Message); }
    }
    private static bool InputFocused => Runtime.Focused && GameWindow.IsFocused(_window);
    private static bool Allowed => Ready && InputFocused && Runtime.Wheel?.Capturing != true && (_trialNonce == null || TrialFences);
    private static int Context => !InputFocused ? -1 : Panel.Open ? 3 : Pause.Paused ? 2 : Runtime.Local == null ? 0 : 1;
    internal static bool Button(string action, bool edge, MainCar? car = null)
    {
        Poll();
        if (_context != Context) { _context = Context; _buttons.Observe(Actions, _context, Allowed, KnownNeutralMask); }
        bool shift = action is "Gear up" or "Gear down";
        bool gate = ShimActionMap.Buttons.TryGetValue(action, out var id) && ShimActionMap.IsGate(id);
        return _buttons.Read(action, edge, Allowed && TrialDelivers(action) && (!shift || ShiftReady(car ?? Runtime.Local)) &&
            (!gate || HPatternReady(car ?? Runtime.Local)));
    }
    internal static ShimAction Action(string id) => !Ready ? default :
        (id is "shiftUp" or "shiftDown" && _transmission != "Sequential") || (ShimActionMap.IsGate(id) && !HPattern) ?
        new(0, ShimStatus.Unsupported) : Actions[ShimActionMap.Index(id)];
    internal static float Axis(string id) => Allowed && (!_carTrial || _carTrialDelivery) ? Action(id).Applied : 0;
    internal static bool AxesLive => Allowed && (!_carTrial || _carTrialDelivery) && DrivingAxes.All(id => Action(id) is { Status: ShimStatus.Live } a && float.IsFinite(a.Value));
    private static void RefuseOrStop(string reason)
    {
        if (Owns) Stop(reason);
        else { _refused = true; Status = "Shim refused; Native retained: " + reason; Runtime.Log.LogWarning(Status); }
    }
    internal static void Stop(string reason)
    {
        if (!_requested && !Owns) return;
        _terminal = true; Array.Clear(Actions, 0, Actions.Length);
        _buttons.Observe(Actions, -1, false);
        Status = "Shim stopped; wheel actions neutral until restart: " + reason;
        if (_handle != IntPtr.Zero)
        {
            var handle = _handle; _handle = IntPtr.Zero;
            try { int result = ShimNative.Stop(handle, out string report); Runtime.Log.LogInfo("Shim stop " + result + ": " + report); }
            catch (Exception e) { Runtime.Log.LogError("Shim stop failed: " + e.Message); }
        }
        Runtime.Force?.Suspend(Status); Runtime.Log.LogWarning(Status);
    }
}
