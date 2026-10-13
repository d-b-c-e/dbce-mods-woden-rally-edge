namespace WodenRallyEdge;

// One launch has one stage-writer owner: supervised stage control (request.txt and its FileSessionControl) or the
// ordinary full-session capture. Once the ordinary capture owns the launch, a later or stale stage request neither
// mutes outputs nor reaches the supervised commands (which would also reset force state). Engine-free for fixtures.
internal sealed class StageLaunchOwner
{
    internal bool OrdinaryOwns { get; private set; }
    /// <summary>Claims the launch for the ordinary capture only when no supervised control is present.</summary>
    internal bool TryClaimOrdinary(bool supervisedPresent)
    {
        if (supervisedPresent) return false;
        OrdinaryOwns = true;
        return true;
    }
    /// <summary>A stage request file mutes outputs only in a launch the ordinary capture does not own.</summary>
    internal bool MuteFor(bool requestPresent) => requestPresent && !OrdinaryOwns;
    internal bool PollSupervised => !OrdinaryOwns;
}
