using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Only a validated, accepted, supervised replay may replace the focus predicate.
// This policy is never used by ordinary wheel input or physical output gates.
internal static class StageReplayPolicy
{
    internal static bool BackgroundAllowed(bool replayActive, bool supervised, bool validated, bool muted, bool configured)
        => replayActive && supervised && validated && muted && configured;

    internal static PlayerControlState Eligibility(PlayerControlState state, bool backgroundAllowed)
        => backgroundAllowed ? state with { Focused = true } : state;
}
