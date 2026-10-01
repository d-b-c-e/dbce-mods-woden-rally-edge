namespace WodenRallyEdge.Core;

public sealed record AxisCalibration(int Rest, int End, int? Centre = null, double Deadzone = 0)
{
    public bool Valid => Rest != End && Rest is >= 0 and <= 65535 && End is >= 0 and <= 65535 &&
        double.IsFinite(Deadzone) && Deadzone >= 0 && Deadzone <= .25 &&
        (!Centre.HasValue || Centre > Math.Min(Rest, End) && Centre < Math.Max(Rest, End));

    public double Normalize(int raw)
    {
        if (!Valid) throw new InvalidOperationException("Invalid axis calibration; refusing to apply input.");
        double value;
        if (Centre is int centre)
        {
            double sign = Math.Sign(End - Rest);
            value = raw * sign >= centre * sign ? (double)(raw - centre) / (End - centre) : -(double)(raw - centre) / (Rest - centre);
            value = Math.Clamp(value, -1, 1);
            return Math.Abs(value) <= Deadzone ? 0 : Math.Sign(value) * (Math.Abs(value) - Deadzone) / (1 - Deadzone);
        }
        value = Math.Clamp((double)(raw - Rest) / (End - Rest), 0, 1);
        return value <= Deadzone ? 0 : (value - Deadzone) / (1 - Deadzone);
    }
}
