using WodenRallyEdge.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    // Emulate the generated action-array indexer returning boxed copies.
    internal sealed class Il2CppReferenceArray<T>
    {
        private readonly T[] _values;
        internal int ThrowOnceOnSet = -1;
        internal Il2CppReferenceArray(T[] values) => _values = values;
        private static T Copy(T value) => value is WodenRallyEdge.GamePadSystem.Actions a ? (T)(object)(a with { }) : value;
        internal T this[int i]
        {
            get => Copy(_values[i]);
            set { if (ThrowOnceOnSet == i) { ThrowOnceOnSet = -1; throw new IOException("Fixture interrupted action write"); } _values[i] = Copy(value); }
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
        internal readonly HashSet<string> Pressed = new();
        internal MainCar? HandbrakeCar;
        internal float HandbrakeAmount;
        internal bool Button(string action, bool edge = true) => Pressed.Contains(action);
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
        }
    }
}
