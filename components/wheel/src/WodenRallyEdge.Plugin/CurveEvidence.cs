namespace WodenRallyEdge;

/// <summary>
/// Presentation evidence for the curved output, counted in source frames (one per TripleView.Apply), never wall time,
/// so a paused game cannot time out. Allocated textures are not evidence: a build is presented only while the presenter
/// has drawn all three panels within the last two source frames, and a presenter that never draws, or stops drawing,
/// for <see cref="MissingFrames"/> source frames is reported so TripleView can latch flat instead of staying black.
/// </summary>
internal sealed class CurveEvidence
{
    internal const int MissingFrames = 120;
    private int _source, _drawnAt = -1;

    /// <summary>Presenter frames drawn since the last build.</summary>
    internal long Drawn { get; private set; }
    internal bool Presented => _drawnAt >= 0 && _source - _drawnAt <= 2;

    internal void Reset() { _source = 0; _drawnAt = -1; Drawn = 0; }

    /// <summary>One source frame passed. Returns the failure to latch when the presenter has not drawn for MissingFrames frames.</summary>
    internal string? SourceFrame()
    {
        _source++;
        int since = _source - Math.Max(_drawnAt, 0);
        return since > MissingFrames ? (_drawnAt < 0 ? "the presenter never drew a frame" : "the presenter stopped drawing") : null;
    }

    internal void Drew() { Drawn++; _drawnAt = _source; }
}
