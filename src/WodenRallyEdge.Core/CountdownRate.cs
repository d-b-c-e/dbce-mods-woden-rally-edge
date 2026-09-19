namespace WodenRallyEdge.Core;

// The game subtracts (Time.time - TimerControl) before checking expiry. Move
// that anchor forward before the native update, so expiry sees the scaled step.
// Never refund TimeLeft after the game has already declared a timeout.
public readonly record struct CountdownAdjustment(float OriginalAnchor, float AdjustedAnchor)
{
    public float RestoreIfUnused(float currentAnchor) => currentAnchor == AdjustedAnchor ? OriginalAnchor : currentAnchor;
}

public static class CountdownRate
{
    public static CountdownAdjustment? Prepare(float previous, float now, float remaining, float speedPercent)
    {
        if (!float.IsFinite(previous) || !float.IsFinite(now) || !float.IsFinite(remaining) || !float.IsFinite(speedPercent) ||
            remaining <= 0 || now <= previous || speedPercent >= 100) return null;
        float elapsed = now - previous;
        if (!float.IsFinite(elapsed)) return null;
        float rate = Math.Clamp(speedPercent, 25, 100) / 100;
        float adjusted = now - elapsed * rate;
        return float.IsFinite(adjusted) && adjusted > previous ? new(previous, adjusted) : null;
    }
}
