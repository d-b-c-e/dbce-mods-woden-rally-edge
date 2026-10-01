namespace WodenRallyEdge.Core;

public enum PlayerPhase { Countdown, Racing, Finished, Destroyed, Unavailable }

public readonly record struct PlayerControlState(PlayerPhase Phase, bool Selected, bool Focused, bool PanelOpen,
    bool Paused, bool Replay, bool Respawning, bool PhotoMode, bool Locked)
{
    private bool Interactive => Selected && Focused && !PanelOpen && !Paused && !Replay && !Respawning && !PhotoMode;
    public bool Driving => Interactive && Phase == PlayerPhase.Racing && !Locked;
    // The game's start-line lock continues to govern the car's physics.
    public bool PreRace => Interactive && Phase == PlayerPhase.Countdown;
    public bool CameraAvailable => Driving || PreRace;
    public bool WheelAvailable => Driving || PreRace;
}
