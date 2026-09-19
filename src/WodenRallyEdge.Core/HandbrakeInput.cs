namespace WodenRallyEdge.Core;

public static class HandbrakeInput
{
    public static float Amount(bool axisMode, bool available, float axis, bool button, bool stockButton)
        // axisMode is retained for compatibility with older callers/settings. Both bindings now contribute.
        => stockButton || button ? 1 : available && float.IsFinite(axis) ? Math.Clamp(axis, 0, 1) : 0;
    public static float Scale(float fullValue, float amount)
        => float.IsFinite(fullValue) && float.IsFinite(amount) ? fullValue * Math.Clamp(amount, 0, 1) : 0;
}
