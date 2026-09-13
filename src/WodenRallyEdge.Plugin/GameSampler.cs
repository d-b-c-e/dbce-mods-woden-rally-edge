using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using WodenRallyEdge.Core;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace WodenRallyEdge;

internal static class GameSampler
{
    private static NVector V(Vector3 v) => new(v.x, v.y, v.z);
    internal static TelemetrySample Read(MainCar car, AppliedInput? input)
    {
        var s = new TelemetrySample {
            SessionId = Runtime.SessionId, Sequence = Runtime.Sequence++, ElapsedSeconds = Runtime.Clock.Elapsed.TotalSeconds,
            SimulationSeconds = Time.timeAsDouble, CarInstanceId = car.GetInstanceID(),
            State = Runtime.Driving(car) ? "driving" : Pause.Paused ? "paused" : car.Replay ? "replay" : car.Respawning ? "respawning" : "inactive"
        };
        void Group(string name, Action read) { try { read(); } catch (Exception ex) { s.Unavailable.Add(name + ":" + ex.GetType().Name); } }
        Group("game", () => {
            s.Add("game.status", (int)car.Status); s.Add("game.playerIndex", car.PlayerIndex); s.Add("game.carId", car.CarId);
            s.Add("game.gear", car.MyGear); s.Add("game.gears", car.Gears); s.Add("game.rpm", car.Rpm);
            s.Add("game.trueSpeed", car.TrueSpeed); s.Add("game.speedMomentum", car.SpeedMomentum);
            s.Add("game.steer", car.Steering_Wheel_Pos); s.Add("game.throttle", car.Pedal_Acc_Value); s.Add("game.brake", car.Pedal_Brake_Value);
            s.Add("game.grounded", car.Grounded ? 1 : 0); s.Add("game.respawning", car.Respawning ? 1 : 0);
            s.Add("game.replay", car.Replay ? 1 : 0); s.Add("game.locked", car.locked ? 1 : 0); s.Add("game.paused", Pause.Paused ? 1 : 0);
            s.Add("game.rank", car.Rank); s.Add("game.laps", car.Laps); s.Add("game.checkpoint", car.CheckPoint);
        });
        Group("controls", () => {
            s.Add("wheelInput.appliedTicks", Runtime.Wheel?.AppliedTicks ?? 0);
            foreach (var name in new[] { "Steer", "Throttle", "Brake" })
            {
                var b = Runtime.Wheel?.Bindings.Axis(name);
                var d = b == null ? null : Runtime.Devices?.Devices.FirstOrDefault(x => x.Info.InstanceGuid == b.DeviceGuid && x.Ok);
                if (d != null) s.Add("wheelRaw." + name.ToLowerInvariant(), d.Axes[b!.Axis]);
            }
            var c = car.MyControls; if (c == null) return;
            s.Add("controls.steer", c.Steering_float); s.Add("controls.throttle", c.Pedal_Acc); s.Add("controls.brake", c.Pedal_Bra);
            s.Add("game.handbrake", c.B_HandBrake ? 1 : 0);
            if (c.PauseScript != null) s.Add("game.photoMode", c.PauseScript.PhotomodeActive ? 1 : 0);
            if (input != null) { s.Add("wheelInput.steer", input.Steer); s.Add("wheelInput.throttle", input.Throttle); s.Add("wheelInput.brake", input.Brake); }
        });
        Group("camera", () => {
            var cam = car.MyCamera; if (cam == null) return;
            s.Add("camera.mode", (int)cam.Mode); s.Add("camera.mountedView", (int)MountedCamera.Cycle.View);
            s.Add("camera.stockPreset", cam.PresetIndex); s.Add("camera.changing", cam.Changing ? 1 : 0); s.Add("camera.playerOwned", MountedCamera.PlayerOwned ? 1 : 0);
        });
        Group("engine", () => {
            var e = car.EngineSystem; if (e == null) return;
            s.Add("game.enginePower", e.CarPower); s.Add("game.idleSpeed", e.Idle_Speed);
            s.Add("game.actualPower", e.ActualPower); s.Add("game.motorTorqueScale", e.MotorTorqueScale);
            var t = car.TransmissionSystem; if (t != null) { s.Add("game.gearMomentum", t.ActualGearMomentum); s.Add("game.shifting", t.Changing ? 1 : 0); }
        });
        Group("stats", () => {
            var a = car.Stats;
            s.Add("game.life", a.Life); s.Add("game.fuel", a.Fuel); s.Add("game.oil", a.Oil); s.Add("game.temperature", a.Temperature);
            s.Add("game.gForce", a.G_Force); s.Add("game.finalGrip", a.FinalGrip);
            if (car.Aids != null) { s.Add("game.automatic", car.Aids.Automatic ? 1 : 0); s.Add("game.abs", car.Aids.Abs ? 1 : 0); }
            if (car.SteerSystem != null) { s.Add("game.steerAngleLimit", car.SteerSystem.MaxSteerAngle); s.Add("game.steerValue", car.SteerSystem.SteerValue); }
            if (car.Clock != null) { s.Add("game.lastLap", car.Clock.LastLapValue); s.Add("game.bestLap", car.Clock.BestLapValue); s.Add("game.penalty", car.Clock.Penalty ? 1 : 0); }
        });
        var orientation = new NQuaternion();
        Group("motion", () => {
            var rb = car.Rb; if (rb == null) return;
            var q = rb.transform.rotation; orientation = new(q.x, q.y, q.z, q.w);
            s.Vector("motion.position.world", V(rb.position)); s.Vector("motion.velocity.world", V(rb.linearVelocity));
            s.Vector("motion.angularVelocity.local", NVector.Transform(V(rb.angularVelocity), NQuaternion.Inverse(orientation)));
            s.Add("motion.orientation.x", q.x); s.Add("motion.orientation.y", q.y); s.Add("motion.orientation.z", q.z); s.Add("motion.orientation.w", q.w);
        });
        Group("wheels", () => {
            var data = car.WheelData_; if (data == null) return;
            var used = new HashSet<int>();
            ReadAxle(s, car, data.FrontAxle, "f", used);
            ReadAxle(s, car, data.RearAxle, "r", used);
        });
        Runtime.Motion.Process(s, orientation);
        return s;
    }

    private static void ReadAxle(TelemetrySample s, MainCar car, Il2CppReferenceArray<WheelCollider>? axle, string name, HashSet<int> used)
    {
        if (axle == null || axle.Length != 2 || axle[0] == null || axle[1] == null) { s.Unavailable.Add("wheel." + name + ":axle-not-two-wheels"); return; }
        var a = axle[0]; var b = axle[1];
        int aid = a.GetInstanceID(), bid = b.GetInstanceID();
        if (aid == bid || used.Contains(aid) || used.Contains(bid)) { s.Unavailable.Add("wheel." + name + ":duplicate-wheel"); return; }
        used.Add(aid); used.Add(bid);
        float ax = car.transform.InverseTransformPoint(a.transform.position).x;
        float bx = car.transform.InverseTransformPoint(b.transform.position).x;
        if (!float.IsFinite(ax) || !float.IsFinite(bx) || Math.Abs(ax - bx) < .01) { s.Unavailable.Add("wheel." + name + ":ambiguous-corners"); return; }
        ReadWheel(s, ax < bx ? a : b, name + "l");
        ReadWheel(s, ax < bx ? b : a, name + "r");
    }
    private static void ReadWheel(TelemetrySample s, WheelCollider wheel, string corner)
    {
        string p = "wheel." + corner + ".";
        try
        {
            float rpm = wheel.rpm;
            s.Add(p + "rpm", rpm); s.Add(p + "angularSpeed", rpm * (Math.PI / 30)); s.Add(p + "radius", wheel.radius);
            s.Add(p + "steerAngle", wheel.steerAngle); s.Add(p + "motorTorque", wheel.motorTorque); s.Add(p + "brakeTorque", wheel.brakeTorque);
            wheel.GetWorldPose(out var hub, out _);
            s.Vector(p + "hubOffset.local", V(wheel.transform.InverseTransformPoint(hub)));
            bool grounded = WheelContact.Read(wheel, out var hit);
            s.Add(p + "grounded", grounded ? 1 : 0);
            if (grounded && hit != null)
            {
                s.Add(p + "contactForce", hit.m_Force); s.Add(p + "forwardSlip", hit.m_ForwardSlip); s.Add(p + "sidewaysSlip", hit.m_SidewaysSlip);
                s.Vector(p + "contactPoint.world", V(hit.m_Point)); s.Vector(p + "contactNormal.world", V(hit.m_Normal));
                if (hit.m_Collider != null) s.Add(p + "colliderId", hit.m_Collider.GetInstanceID());
            }
        }
        catch (Exception ex) { s.Unavailable.Add(p + ex.GetType().Name); }
    }
}
