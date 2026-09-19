using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal sealed class HandbrakeLease
{
    private readonly MainCar _car;
    private readonly float _amount, _loss;
    private WheelCollider? _left, _right;
    private bool _applied, _restored;
    internal HandbrakeLease(MainCar car, float amount)
    { _car = car; _amount = amount; _loss = car.BrakeSystem.HandBrakeStiffnessLoss; }
    internal void Apply()
    {
        var rear = _car.WheelData_.RearAxle;
        if (rear == null || rear.Length != 2) throw new InvalidOperationException("Analog handbrake requires two rear wheels");
        if (rear[0] != null && rear[0].rpm > 30) _left = rear[0];
        if (rear[1] != null && rear[1].rpm > 30) _right = rear[1];
        var brakes = _car.BrakeSystem;
        brakes.HandBrakeStiffnessLoss = HandbrakeInput.Scale(_loss, _amount);
        _applied = true; _car.BrakeSystem = brakes;
    }
    internal void Restore(bool completed)
    {
        if (_restored) return; _restored = true;
        try
        {
            if (completed)
            {
                if (_left != null) _left.brakeTorque = HandbrakeInput.Scale(_left.brakeTorque, _amount);
                if (_right != null) _right.brakeTorque = HandbrakeInput.Scale(_right.brakeTorque, _amount);
            }
        }
        finally
        {
            // BrakeSys is boxed: read current state, restore only the tuning
            // value, and assign it back. Retain the game's updated active/temp state.
            if (_applied && _car != null) { var brakes = _car.BrakeSystem; brakes.HandBrakeStiffnessLoss = _loss; _car.BrakeSystem = brakes; }
        }
    }
}
