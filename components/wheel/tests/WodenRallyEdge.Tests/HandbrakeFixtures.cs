namespace UnityEngine
{
    internal sealed class WheelCollider { internal float rpm, brakeTorque; }
}
namespace WodenRallyEdge
{
    // BrakeSys getters return boxed copies in the real interop. Emulate this
    // specifically so a missing assign-back or stale full-struct restore fails.
    internal sealed partial class MainCar
    {
        internal sealed record Brakes(float HandBrakeStiffnessLoss = .8f, float HbStiffNessLossTemp = 0)
        {
            public float HandBrakeStiffnessLoss { get; set; } = HandBrakeStiffnessLoss;
            public float HbStiffNessLossTemp { get; set; } = HbStiffNessLossTemp;
        }
        private Brakes _brakes = new();
        internal Brakes BrakeSystem { get => _brakes with { }; set => _brakes = value with { }; }
        internal sealed class Wheels { internal UnityEngine.WheelCollider[] RearAxle = { new() { rpm = 100, brakeTorque = 80 }, new() { rpm = 20, brakeTorque = 120 } }; }
        internal Wheels WheelData_ = new();
    }
    internal static class HandbrakeChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            var car = new MainCar(); var lease = new HandbrakeLease(car, .25f);
            lease.Apply();
            check(Math.Abs(car.BrakeSystem.HandBrakeStiffnessLoss - .2f) < .0001f, "partial pressure reaches the boxed native tuning slot");
            var native = car.BrakeSystem; native.HbStiffNessLossTemp = native.HandBrakeStiffnessLoss; car.BrakeSystem = native;
            car.WheelData_.RearAxle[0].brakeTorque = 2000; // Emulate verified native pressed branch.
            lease.Restore(true); lease.Restore(false);
            check(car.WheelData_.RearAxle[0].brakeTorque == 500, "native torque scaled exactly once");
            check(car.WheelData_.RearAxle[1].brakeTorque == 120, "wheel below native rpm threshold left alone");
            check(Math.Abs(car.BrakeSystem.HandBrakeStiffnessLoss - .8f) < .0001f && Math.Abs(car.BrakeSystem.HbStiffNessLossTemp - .2f) < .0001f, "tuning restored while retaining the game's updated transient state");
            lease = new HandbrakeLease(car, .5f); lease.Apply(); lease.Restore(false);
            check(Math.Abs(car.BrakeSystem.HandBrakeStiffnessLoss - .8f) < .0001f && car.WheelData_.RearAxle[0].brakeTorque == 500, "exception cleanup restores tuning without another torque write");
        }
    }
}
