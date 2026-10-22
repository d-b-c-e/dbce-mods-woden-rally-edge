using WodenRallyEdge.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    // Emulate the generated action-array indexer returning boxed copies.
    internal sealed class Il2CppReferenceArray<T>
    {
        private readonly T[] _values;
        internal int ThrowOnceOnSet = -1;
        internal int ThrowAfterMatchingWrites = 1;
        internal Il2CppReferenceArray(T[] values) => _values = values;
        private static T Copy(T value) => value is WodenRallyEdge.GamePadSystem.Actions a ? (T)(object)(a with { }) : value;
        internal T this[int i]
        {
            get => Copy(_values[i]);
            set { if (ThrowOnceOnSet == i && --ThrowAfterMatchingWrites == 0) { ThrowOnceOnSet = -1; throw new IOException("Fixture interrupted action write"); } _values[i] = Copy(value); }
        }
    }
}
namespace WodenRallyEdge
{
    internal static class GamePadSystem
    {
        internal sealed record Actions
        {
            public string name = "";
            public float value;
            public bool Pressed;
        }
    }
    internal sealed class WheelInput
    {
        internal readonly Bindings Bindings = new();
        // Mirrors production WheelInput (the UI fixture compiles the real file and checks each state).
        internal Guid? SteeringGuid => ShimInput.Owns ? ShimInput.SteeringGuid : ShimInput.PendingShared ? null : Bindings.Steer?.DeviceGuid;
        internal string? SteeringReason =>
            ShimInput.Owns ? (ShimInput.SteeringGuid.HasValue ? null : "The shared controls profile has no usable steering binding. Check it and restart.")
            : ShimInput.StoppedBeforeOpen ? "Shared input stopped before opening; the force wheel follows its steering after a restart."
            : ShimInput.PendingShared ? "Waiting for the shared controls profile to open; the force wheel follows its steering binding."
            : null;
        internal readonly HashSet<string> Pressed = new();
        internal MainCar? HandbrakeCar;
        internal float HandbrakeAmount;
        internal bool Button(string action, bool edge = true, MainCar? car = null) => Pressed.Contains(action);
    }
    internal static class ShimInput
    {
        internal static bool Owns, Ready = true, PendingShared, StoppedBeforeOpen;
        internal static Guid? SteeringGuid;
        internal static string Status => "Fixture shim stopped";
        internal static float Axis(string id) => 0;
    }
    internal static class TimingDiagnostics { internal static double ControlsMs; }
    internal static class InputLeaseChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            var original = Enumerable.Range(0, 18).Select(i => new GamePadSystem.Actions { name = "action-" + i, value = .1f, Pressed = false }).ToArray();
            var actions = new Il2CppReferenceArray<GamePadSystem.Actions>(original.Select(a => a with { }).ToArray());
            Runtime.Devices = new(); var input = new WheelInput(); var car = new MainCar();
            input.Bindings.Buttons["Camera"] = new(Guid.NewGuid(), 1); input.Pressed.Add("Camera");
            var lease = new InputLease(actions, -.4f, .7f, .2f, input, car);
            check(actions[7].value == .7f && actions[6].value == .2f, "calibrated throttle and brake reach native actions");
            check(actions[17].value == .4f && actions[16].value == 0, "left steering reaches native action pair");
            check(actions[5].Pressed && actions[5].value == 1, "bound camera reaches native countdown dispatch");
            check(actions[2] == original[2] && actions[3] == original[3], "unbound reset/shift actions preserved");
            lease.Restore(); lease.Restore();
            for (int i = 0; i < 18; i++) check(actions[i] == original[i], "native action restored once: " + i);
            actions.ThrowOnceOnSet = 16;
            bool threw = false; try { _ = new InputLease(actions, .4f, .8f, .3f, input, car); } catch (IOException) { threw = true; }
            check(threw, "fixture exercises interrupted write");
            for (int i = 0; i < 18; i++) check(actions[i] == original[i], "partial override restored after exception: " + i);

            input.Bindings.Buttons["Gear up"] = new(Guid.NewGuid(), 2);
            input.Bindings.Buttons["Gear down"] = new(Guid.NewGuid(), 3);
            foreach (bool shim in new[] { false, true })
            foreach (bool held in new[] { false, true })
            {
                input.Pressed.Clear();
                if (held) { input.Pressed.Add("Gear up"); input.Pressed.Add("Gear down"); }
                lease = new InputLease(actions, 0, .6f, 0, input, car, shiftUp: true, shiftDown: true, shim: shim);
                check(actions[1].Pressed && actions[3].Pressed && actions[1].value == 1 && actions[3].value == 1,
                    "synthetic shifts merge with bound buttons in either backend");
                lease.Restore(); lease.Restore();
                for (int i = 0; i < 18; i++) check(actions[i] == original[i],
                    "repeated action writes restore pre-lease value: shim=" + shim + ", held=" + held + ", index=" + i);
            }

            // Fail after the bound button write, on the later synthetic pulse.
            actions.ThrowOnceOnSet = 3; actions.ThrowAfterMatchingWrites = 2;
            threw = false;
            try { _ = new InputLease(actions, 0, .6f, 0, input, car, shiftUp: true, shiftDown: true); }
            catch (IOException) { threw = true; }
            check(threw, "fixture interrupts a repeated action write");
            for (int i = 0; i < 18; i++) check(actions[i] == original[i], "failed repeated write restores original: " + i);
        }
    }
}
